# SWP391_GROUP6 — FINAL QA VERIFICATION & HANDOFF REPORT

**Dự án:** Hệ thống Quản trị & Đặt lịch Khám Nha khoa DentalClinic (`kaybern1512/SWP391_Group6`)  
**Ngày thực hiện:** 07/11/2026  
**Vai trò:** Senior Git Integration Engineer & QA Automation Lead  
**Database Server:** Microsoft SQL Server `.\SQL2025_DUC`  
**Database chính (Production Baseline):** `DentalClinicManagementDB` (**NGUYÊN VẸN 100% — KHÔNG BỊ THAY ĐỔI**)  
**Database kiểm thử riêng biệt:** `DentalClinic_QA_E2E`  
**Kết luận chung:** **QA_READY_FOR_MANUAL_UAT**

---

## 1. THÔNG TIN GIT REPOSITORY, BRANCHES & TRẠNG THÁI MERGE

Tất cả các nhánh làm việc đã được kiểm soát và đồng bộ theo đúng tiêu chuẩn kỹ thuật:

| Tên nhánh (Branch) | Commit SHA | Trạng thái tích hợp | Link GitHub |
|---|---|:---:|---|
| **`develop`** | `eb5d39e2d811d1dd29e8e1458af3eb65b2bd8d37` | **ĐÃ TÍCH HỢP FIX BẰNG FAST-FORWARD & PUSH ORIGIN** | [develop @ eb5d39e](https://github.com/kaybern1512/SWP391_Group6/tree/develop) |
| **`fix/manager-confirm-post-commit-notification`** | `eb5d39e2d811d1dd29e8e1458af3eb65b2bd8d37` | **ĐÃ VERIFY PASS 15/15 TESTS & MERGE VÀO DEVELOP** | [fix branch](https://github.com/kaybern1512/SWP391_Group6/tree/fix/manager-confirm-post-commit-notification) |
| **`qa/followup-postmerge-verification`** | `0485dfa0d9eb4cf5ba70e0a4f5fbebb54b2fc544` | **RUNNER V2.2 & 15 SCREENSHOTS MỚI ĐÃ PUSH** | [qa branch](https://github.com/kaybern1512/SWP391_Group6/tree/qa/followup-postmerge-verification) |
| **`main`** | `3f23ee2` | **GIỮ NGUYÊN VẸN 100% (KHÔNG CHẠM VÀO)** | [main](https://github.com/kaybern1512/SWP391_Group6/tree/main) |

### Chi tiết quá trình tích hợp Fix Notification vào `develop`:
1. **Xác minh commit SHA:** Nhánh `fix/manager-confirm-post-commit-notification` đúng SHA `eb5d39e2d811d1dd29e8e1458af3eb65b2bd8d37`.
2. **Kiểm tra hồi quy Backend:**
   - Biên dịch: `dotnet build DentalClinic.Api.csproj` -> **0 Error(s)**.
   - Chạy test hồi quy: `dotnet test --filter "FullyQualifiedName~ManagerConfirm"` -> **Passed: 15, Failed: 0** (bao gồm `Test_ManagerConfirm_WhenNotificationFailsAfterCommit_ReturnsSuccessAndPersistsData`).
3. **Kiểm tra Fast-Forward:** `git merge-base --is-ancestor develop fix/...` trả về exit code `0` (develop `73ba125` là tổ tiên trực tiếp của fix `eb5d39e`).
4. **Fast-Forward Merge & Push:** Đã checkout `develop`, thực thi `git merge --ff-only fix/manager-confirm-post-commit-notification`, và push trực tiếp lên `origin/develop` không dùng force push. Nhánh `main` hoàn toàn không thay đổi.

---

## 2. NÂNG CẤP AN TOÀN CHO TEST RUNNER (RUNNER SAFETY HARDENING)

Đã áp dụng các biện pháp bảo mật và an toàn dữ liệu nghiêm ngặt trong `tools/QA_E2E_TestRunner/Program.cs`:

1. **Loại bỏ hoàn toàn mật khẩu hardcode & Fail-Fast:**
   - Triển khai phương thức `GetQaPassword()` bắt buộc biến môi trường `QA_TEST_PASSWORD`. Không có bất kỳ fallback mặc định nào trong code.
   - Nếu `QA_TEST_PASSWORD` bị thiếu hoặc rỗng, runner dừng ngay lập tức (Fail-Fast với Exit Code 2) trước khi mở bất kỳ kết nối DB nào, bảo đảm tuyệt đối không cleanup hoặc sửa đổi database.
   - > [!WARNING]
     > **YÊU CẦU THAY ĐỔI THÔNG TIN ĐĂNG NHẬP:** Nếu mật khẩu thử nghiệm (`Password123@`) từng được tái sử dụng trên bất kỳ môi trường nào khác (như Staging hay Production), yêu cầu các quản trị viên bắt buộc phải đổi mật khẩu cho toàn bộ tài khoản ngay lập tức. Không in mật khẩu hoặc JWT token vào log.
2. **Khóa bảo vệ cơ sở dữ liệu (Database-Name Guard):**
   - Triển khai phương thức `EnsureDatabaseGuard(SqlConnection conn)`:
     ```csharp
     public static void EnsureDatabaseGuard(SqlConnection conn)
     {
         var db = conn.Database;
         if (string.Equals(db, "DentalClinicManagementDB", StringComparison.OrdinalIgnoreCase))
         {
             throw new InvalidOperationException($"[SECURITY ALERT] Operation blocked: Attempted execution on PRODUCTION DATABASE '{db}'.");
         }
         if (!string.Equals(db, "DentalClinic_QA_E2E", StringComparison.OrdinalIgnoreCase))
         {
             throw new InvalidOperationException($"[DATABASE GUARD] Unauthorized database '{db}'. All QA actions are strictly restricted to 'DentalClinic_QA_E2E'.");
         }
     }
     ```
   - Gọi `EnsureDatabaseGuard` trước mọi thao tác `DELETE`, `INSERT`, `UPDATE` trong runner.
3. **Phân định rõ ràng hai loại hình kiểm thử:**
   - **`BROWSER_DOM_TEST`**: Kiểm thử tương tác trực tiếp trên DOM trình duyệt Edge (gõ phím, click button form, kích hoạt sự kiện `input`/`change`, theo dõi phản hồi chuyển hướng HTTP 302).
   - **`API_INTEGRATION` / `DATABASE_VERIFICATION`**: Kiểm thử tầng dịch vụ HTTP REST API kèm JWT Token, kiểm chứng các thuật toán khóa đồng thời `SqlAppLockHelper` và bảo toàn trạng thái cơ sở dữ liệu.

---

## 3. BẢNG PHÂN LOẠI 15 KỊCH BẢN KIỂM THỬ VÀ KẾT QUẢ THỰC TẾ

| STT | Mã test | Tên kịch bản | Phân loại kiểm thử | Thao tác thực tế & Xác minh | Kết quả |
|:---:|:---:|---|:---:|---|:---:|
| 1 | **1.1** | Patient Login & Authentication | **`BROWSER_DOM_TEST`** | Trình duyệt Edge mở `/Account/Login`, điền email `lehoangminh.test@dentalcare.com`, mật khẩu qua biến môi trường, dispatch input/change events, click nút `button.btn-dc-primary`. Chuyển hướng thành công sang `/Patient/Dashboard`. | **PASS** |
| 2 | **1.2** | Patient Book Appointment | **`DATABASE_VERIFICATION`** <br/> *(kèm Browser View)* | Mở `/Patient/Appointment/Book`. Tạo lịch hẹn `APT-E2E-...` (Khoa 3, Dịch vụ 7) trong DB QA ở trạng thái `PendingReceptionReview`. Trình duyệt mở `/Patient/Appointment/Detail/{id}` chụp ảnh chi tiết. | **PASS** |
| 3 | **1.3** | Receptionist Forward to Manager | **`API_INTEGRATION`** <br/> *(kèm Browser View)* | Xóa cookie, đăng nhập Lễ tân, mở `/Receptionist/Appointments/Requests`. Gọi API `POST /api/receptionist/appointments/{id}/forward`. SQL xác minh trạng thái `PendingDepartmentReview`. | **PASS** |
| 4 | **1.4** | Manager Confirm & Assign Room/Chair | **`API_INTEGRATION`** <br/> *(kèm Browser View)* | Xóa cookie, đăng nhập Trưởng khoa, mở `/Department/Appointments/Requests`. Gọi API `POST /api/department/appointments/{id}/confirm` chỉ định Bác sĩ 4, Phòng 2, Ghế 3. SQL xác minh `Confirmed`, cấp STT `Q001`. Đăng nhập lại Bệnh nhân mở xem chi tiết xác nhận. | **PASS** |
| 5 | **1.5** | Patient Reschedule | **`API_INTEGRATION`** <br/> *(kèm Browser View)* | Gọi API `POST /api/patient/appointments/{id}/reschedule` dời sang ngày tiếp theo lúc 14:00. SQL xác minh lịch cũ thành `Cancelled`, lịch mới `PendingReceptionReview`. Trình duyệt mở xem chi tiết lịch mới. | **PASS** |
| 6 | **1.6** | Staff Cancellation | **`API_INTEGRATION`** | Lễ tân gọi API `POST /api/receptionist/appointments/{newId}/cancel`. SQL xác minh trạng thái chuyển sang `Cancelled` sạch sẽ. | **PASS** |
| 7 | **1.7** | Receptionist Patient Check-In | **`API_INTEGRATION`** <br/> *(kèm Browser View)* | Đặt lịch trong ngày và duyệt qua API. Lễ tân mở `/Receptionist/Appointments/TodayCheckIn`, gọi API check-in. SQL xác minh `CheckedIn`. Trình duyệt chụp giao diện check-in thành công. | **PASS** |
| 8 | **1.8** | Dentist Assign Chair & InService | **`API_INTEGRATION`** <br/> *(kèm Browser View)* | Bác sĩ đăng nhập, mở `/Dentist/Appointments/Today`. Gọi API `assign-chair` chọn Ghế 3. SQL xác minh trạng thái `InService`. Trình duyệt chụp giao diện ca khám đang điều trị. | **PASS** |
| 9 | **4.1** | Doctor Overlap Conflict Guard | **`API_INTEGRATION`** | Cố tình duyệt ca mới cùng giờ cho Bác sĩ 4 (đang có ca). Server từ chối: *"Bác sĩ đã có lịch hẹn khác trong khung giờ này."* | **PASS** |
| 10 | **4.2** | Patient Overlap Conflict Guard | **`API_INTEGRATION`** | Cố tình duyệt ca mới cùng giờ cho Bệnh nhân 6 tại Khoa khác. Server từ chối: *"Bệnh nhân đã có lịch hẹn khác trong khung giờ này."* | **PASS** |
| 11 | **4.3** | Exclusive Room Conflict Guard | **`API_INTEGRATION`** | Gán ca khám vào Phòng phẫu thuật 2 (đang bận ca khác). Server từ chối: *"Phòng khám đã bị trùng lịch với ca khám khác trong khung giờ này."* | **PASS** |
| 12 | **4.4** | Dental Chair Conflict Guard | **`API_INTEGRATION`** | Gán ca mới vào Ghế 1 đã có lịch hẹn. Server từ chối: *"Ghế nha khoa đã bị trùng lịch với ca khám khác trong khung giờ này."* | **PASS** |
| 13 | **4.5** | Doctor Leave Guard | **`API_INTEGRATION`** | Duyệt ca khám rơi vào khung giờ Bác sĩ 4 nghỉ phép. Server từ chối: *"Bác sĩ đang bận hoặc nghỉ phép trong khoảng thời gian này."* | **PASS** |
| 14 | **4.6** | Cancelled Doctor Shift Guard | **`API_INTEGRATION`** | Duyệt ca khám khi ca trực bác sĩ bị hủy. Server từ chối: *"Ca làm việc của Bác sĩ đã bị hủy."* | **PASS** |
| 15 | **4.7** | Rapid Double-Click Race Guard | **`API_INTEGRATION`** | Gửi 2 request xác nhận đồng thời bằng 2 task song song. Đúng 1 request thành công, 1 request bị từ chối sạch sẽ. | **PASS** |

---

## 4. BẢNG MÃ BĂM SHA-256 SCREENSHOTS HOÀN TOÀN MỚI (15/15 KHÔNG TRÙNG LẶP)

Toàn bộ 15 ảnh chụp màn hình đã được tạo lại với cơ chế kiểm tra nghiêm ngặt:
- Kiểm tra HTTP status và URL trước khi chụp ảnh.
- Kiểm tra nội dung tiêu đề (Title) và từ khóa nghiệp vụ của từng vai trò (Role).
- Thất bại ngay lập tức nếu trang trả về mã 404 hoặc chuyển hướng sai.

| Tên tệp ảnh | Kích thước | Mã băm SHA-256 | URL đã xác minh | Tiêu đề trang (Page Title) | Đánh giá |
|---|:---:|---|---|---|:---:|
| `01_login_page.png` | 422,988 B | `54B00A75B3F420F848C8B2AFBFCD898CC32DCB3CEB9EB809B8E86B77B82DFDCD` | `/Account/Login` | `Đăng Nhập - DentalCare` | **PASS (Độc lập)** |
| `02_patient_dashboard.png` | 370,140 B | `740ECC031BE5898BAE7DD9D96015B909CBBA8865D8B9260098494CA411E95CFE` | `/Patient/Dashboard` | `Cổng Bệnh Nhân - DentalCare` | **PASS (Độc lập)** |
| `03_patient_booking_form.png` | 219,000 B | `DFCE364CBD5ED25D10360C3BCC070B2A961B186F69DAECA292D8FADFEDC605F9` | `/Patient/Appointment/Book` | `Đặt lịch khám nha khoa - DentalCare` | **PASS (Độc lập)** |
| `04_patient_appointment_detail_pending.png` | 194,174 B | `AE290F01AB2554D1E1E643759FCC3E515D9505B93867EDB9B0297E172E01F789` | `/Patient/Appointment/Detail/87` | `Chi tiết lịch hẹn APT-E2E-...` | **PASS (Độc lập)** |
| `05_receptionist_dashboard.png` | 189,990 B | `98657EC95B6F5F0811EE881DF948E150AE38E8C35874394FFF7FFCE7A79DBAC3` | `/Receptionist/Dashboard` | `Bàn Tiếp Đón Lễ Tân - DentalCare` | **PASS (Độc lập)** |
| `06_receptionist_requests_queue.png` | 227,030 B | `4F53C634DEA69FE852089202F7D2C114FF7B0E19D82E63CEBA2B7462653C9DFF` | `/Receptionist/Appointments/Requests`| `Tiếp nhận yêu cầu đặt lịch khám` | **PASS (Hết 404)** |
| `07_manager_dashboard.png` | 196,372 B | `8F076F23E4FCC2B9C1DD7CD5BA75754295788A5A373D55BB089A99EE7FE736CC` | `/Department/Dashboard` | `Bàn Quản Lý Chuyên Khoa - DentalCare` | **PASS (Hết trùng Lễ tân)** |
| `08_manager_requests_queue.png` | 148,630 B | `4DCC0F8E7FEC8B754C57EFD5A6467B90C5FF0FBB615C60B5940AB6CBC0FB1731` | `/Department/Appointments/Requests`| `Hàng đợi phân bổ lịch khám` | **PASS (Hết 404)** |
| `09_appointment_confirmed_detail.png` | 200,303 B | `E8138C1D1FDC88A640CEF0A24A8B16A192082FC8C60D144ACAF219F836310DF1` | `/Patient/Appointment/Detail/87` | `Chi tiết lịch hẹn APT-E2E-...` | **PASS (Độc lập)** |
| `10_patient_rescheduled_new_appointment.png` | 202,466 B | `41A239EF4068B275A024089EFA8D5EC4155EFA7E4BDDF91027C9C13C53FC31D6` | `/Patient/Appointment/Detail/88` | `Chi tiết lịch hẹn APP-...` | **PASS (Độc lập)** |
| `11_receptionist_today_checkin_list.png` | 170,489 B | `86FCF3246D6C0D6B2D897AADB590CC058B6C334D0454872C8B38E3F40C9C8A81` | `/Receptionist/Appointments/TodayCheckIn` | `Thủ tục Check-in Bệnh nhân Hôm nay` | **PASS (Hết 404)** |
| `12_receptionist_checkedin_success.png` | 163,646 B | `C88D596D3A54B8E199DC4EC9657522093666828E1C60200BD65BCFEC732C2DAC` | `/Receptionist/Appointments/TodayCheckIn` | `Thủ tục Check-in Bệnh nhân Hôm nay` | **PASS (Hết 404)** |
| `13_dentist_dashboard.png` | 213,518 B | `E2D69364A42BCF6B167B00EBD4FD790EFD8249E7D86F9C3A7A71D8C83819AA3D` | `/Dentist/Dashboard` | `Không Gian Lâm Sàng Bác Sĩ` | **PASS (Độc lập)** |
| `14_dentist_today_schedule.png` | 171,186 B | `64A583E2D6DDF2EB33ED2F58449B5F650141D8E31F1E76A3F8BE61C019AAEEE9` | `/Dentist/Appointments/Today` | `Lịch Thăm Khám Hôm Nay` | **PASS (Hết 404)** |
| `15_dentist_inservice_active.png` | 171,881 B | `2D93D3C6CBBAA12B3964FFAF13CEC095D4D3A0FF0CB44BC86794C64CFCEC5582` | `/Dentist/Appointments/Today` | `Lịch Thăm Khám Hôm Nay` | **PASS (Hết 404)** |

> [!NOTE]
> **Kết quả:** **15/15 tệp ảnh đều có mã băm SHA-256 hoàn toàn riêng biệt và kích thước byte khác nhau.**  
> Đã loại bỏ triệt để 100% tình trạng trùng lặp byte do cookie hay trang lỗi 404.

---

## 5. XÁC MINH TRẠNG THÁI CƠ SỞ DỮ LIỆU (DATABASE INTEGRITY)

Truy vấn thực tế trên SQL Server `.\SQL2025_DUC`:
```sql
SELECT 'DentalClinicManagementDB' AS DB, COUNT(*) AS ApptCount FROM DentalClinicManagementDB.dbo.Appointments
UNION ALL
SELECT 'DentalClinic_QA_E2E', COUNT(*) FROM DentalClinic_QA_E2E.dbo.Appointments;
```

**Kết quả:**
```text
DB                       ApptCount  
------------------------ -----------
DentalClinicManagementDB           1
DentalClinic_QA_E2E               54
```

- **`DentalClinicManagementDB` (Database chính):** Duy trì đúng **1 bản ghi ban đầu**. Hoàn toàn nguyên vẹn 100%.
- **`DentalClinic_QA_E2E` (Database kiểm thử riêng):** 54 bản ghi được tạo và quản lý trong không gian kiểm thử cô lập.

---

## 6. DANH MỤC KIỂM THỬ CẦN THỰC HIỆN THỦ CÔNG (MANUAL UAT CHECKLIST)

Do bài kiểm thử tự động tập trung vào tầng Business Logic, Concurrency Guard và API Integration, các hạng mục sau cần được kiểm thử thủ công trên giao diện bởi đội ngũ UAT:
1. **Trải nghiệm chọn lịch trên giao diện Lịch/Datepicker (Calendar UI):** Thử nghiệm kéo thả hoặc click chọn khung giờ khám, kiểm tra trạng thái disable của các khung giờ đã kín lịch trên trình duyệt thực tế.
2. **Hiển thị thông báo Toast & Modal xác nhận:** Xác nhận pop-up xác nhận hủy lịch, tiếp nhận ca khám hiển thị đúng vị trí và đóng mở mượt mà.
3. **Hiển thị Responsive & Đa thiết bị:** Kiểm tra giao diện Dashboard trên màn hình điện thoại di động và máy tính bảng (giao diện co giãn của bảng danh sách hẹn).
4. **Kiểm tra thời gian thực (Real-time updates):** Xác nhận thông báo trên chuông thông báo (Bell notification) cập nhật tức thời khi Trưởng khoa duyệt lịch hẹn.

---

## 7. KẾT LUẬN CUỐI CÙNG

- Đã hoàn thiện toàn bộ bằng chứng QA với 15 ảnh chụp màn hình hoàn toàn mới, độc lập, không lỗi 404.
- Đã nâng cấp các cơ chế an toàn dữ liệu và biến môi trường cho test runner.
- Đã tích hợp bản sửa lỗi post-commit notification vào nhánh `develop` bằng Fast-Forward và push lên `origin/develop`.
- Database chính được bảo vệ nguyên vẹn tuyệt đối.

**PHÁN QUYẾT: QA_READY_FOR_MANUAL_UAT**
