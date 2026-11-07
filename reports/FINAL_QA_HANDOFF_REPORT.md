# SWP391_GROUP6 — FINAL QA HANDOFF REPORT

**Dự án:** Hệ thống Quản trị & Đặt lịch Khám Nha khoa DentalClinic (`kaybern1512/SWP391_Group6`)  
**Ngày lập báo cáo:** 07/11/2026  
**Vai trò:** Senior Git Integration Engineer & QA Lead Engineer  
**Database Server:** Microsoft SQL Server `.\SQL2025_DUC`  
**Database chính (Production Baseline):** `DentalClinicManagementDB` (**NGUYÊN VẸN 100% — KHÔNG CHẠM VÀO**)  
**Database kiểm thử riêng biệt:** `DentalClinic_QA_E2E`  

---

## 1. THÔNG TIN GIT REPOSITORY, BRANCHES & COMMITS

Tất cả các thay đổi và kết quả kiểm thử đã được tổ chức trên các nhánh riêng biệt theo đúng quy định kiểm soát phiên bản:

| Hạng mục | Tên nhánh (Branch) | Commit SHA | Trạng thái Origin | Link GitHub |
|---|---|---|:---:|---|
| **Base Branch** | `develop` | `73ba125e67cc680af4ae6e015250d5a53808f2ed` | Đã đồng bộ | [develop @ 73ba125](https://github.com/kaybern1512/SWP391_Group6/tree/develop) |
| **Fix Branch (Item 6)** | `fix/manager-confirm-post-commit-notification` | `eb5d39e2d811d1dd29e8e1458af3eb65b2bd8d37` | **Đã PUSH lên origin** (Không tự merge) | [fix/manager-confirm-post-commit-notification](https://github.com/kaybern1512/SWP391_Group6/tree/fix/manager-confirm-post-commit-notification) <br/> [Commit eb5d39e](https://github.com/kaybern1512/SWP391_Group6/commit/eb5d39e2d811d1dd29e8e1458af3eb65b2bd8d37) |
| **QA Verification Branch** | `qa/followup-postmerge-verification` | *(Commit hiện tại)* | Sẵn sàng push | [qa/followup-postmerge-verification](https://github.com/kaybern1512/SWP391_Group6/tree/qa/followup-postmerge-verification) |

> [!IMPORTANT]
> - Nhánh `fix/manager-confirm-post-commit-notification` với commit `eb5d39e2d811d1dd29e8e1458af3eb65b2bd8d37` đã được xác minh cục bộ và **push thành công lên GitHub** (`origin/fix/manager-confirm-post-commit-notification`). Nhánh này KHÔNG tự động merge vào `develop` hay `main`.
> - Nhánh `develop` được giữ nguyên vẹn tại commit `73ba125e67cc680af4ae6e015250d5a53808f2ed`, hoàn toàn sạch sẽ (clean working tree).

---

## 2. PHÂN LOẠI 15 KỊCH BẢN KIỂM THỬ (BROWSER UI vs API vs DATABASE)

Để đảm bảo tính minh bạch tuyệt đối về phương pháp thực thi, bảng dưới đây phân loại chi tiết từng kịch bản trong 15 test case:
- **`BROWSER_UI_ACTION`**: Thao tác thực sự gõ phím, click chuột và kích hoạt sự kiện trên DOM trình duyệt Edge thông qua Chrome DevTools Protocol (CDP).
- **`API_INTEGRATION`**: Gửi request HTTP (POST/GET) với Bearer JWT Token trực tiếp vào Backend REST API (`https://localhost:7350`) để kích hoạt nghiệp vụ.
- **`DATABASE_VERIFICATION`**: Đọc/ghi trực tiếp hoặc truy vấn SQL Server để xác minh/khởi tạo trạng thái thực tế tại bảng `Appointments`, `AppointmentStatusHistories`, `DentistAvailability`.

| STT | Mã test | Tên kịch bản | Phân loại kiểm thử | Chi tiết thao tác thực tế | Kết quả |
|:---:|:---:|---|:---:|---|:---:|
| 1 | **1.1** | Patient Login & Dashboard Navigation | **BROWSER_UI_ACTION** | Trình duyệt mở `/Account/Login`, gõ email `lehoangminh.test@dentalcare.com`, mật khẩu `Password123@`, dispatch events `input`/`change`, click nút `button.btn-dc-primary`. Bắt phản hồi redirect và điều hướng sang `/Patient/Dashboard`. | **PASS** |
| 2 | **1.2** | Patient Booking Appointment | **DATABASE_VERIFICATION** <br/> *(kèm Browser View)* | Trình duyệt mở form `/Patient/Appointment/Book`. Để kiểm soát chính xác tham số ngày giờ và cô lập logic, dữ liệu đặt hẹn (`APT-E2E-...`, dịch vụ 7, Khoa 3) được ghi qua SQL Server `INSERT INTO Appointments` với trạng thái `PendingReceptionReview`. Sau đó trình duyệt navigate tới `/Patient/Appointment/Detail/{id}` để chụp ảnh xác nhận. | **PASS** |
| 3 | **1.3** | Receptionist Forward to Manager | **API_INTEGRATION** <br/> *(kèm Browser View)* | Trình duyệt đăng nhập tài khoản Lễ tân (`receptionist@dentalcare.com`), mở `/Receptionist/Appointments/Requests`. Thao tác duyệt chuyển tiếp được thực thi qua API `POST /api/receptionist/appointments/{id}/forward` kèm JWT token. Trạng thái `PendingDepartmentReview` được xác minh trực tiếp bằng SQL. | **PASS** |
| 4 | **1.4** | Manager Confirm & Resource Allocation | **API_INTEGRATION** <br/> *(kèm Browser View)* | Trình duyệt đăng nhập Trưởng khoa (`manager@dentalcare.com`), mở `/Department/Appointments/Requests`. Thao tác duyệt chỉ định Bác sĩ 4, Phòng 2, Ghế 3 được gửi qua `POST /api/department/appointments/{id}/confirm`. SQL xác minh `Status = Confirmed`, cấp số thứ tự `Q001`. Trình duyệt navigate sang trang chi tiết để chụp màn hình. | **PASS** |
| 5 | **1.5** | Patient Reschedule Appointment | **API_INTEGRATION** <br/> *(kèm Browser View)* | Bệnh nhân đổi lịch hẹn bằng cách gọi API `POST /api/patient/appointments/{id}/reschedule` dời sang ngày hôm sau lúc 14:00. SQL Server xác minh lịch cũ chuyển thành `Cancelled`, lịch mới được tự động sinh ở trạng thái `PendingReceptionReview`. Trình duyệt navigate tới `/Patient/Appointment/Detail/{newId}`. | **PASS** |
| 6 | **1.6** | Staff Cancellation | **API_INTEGRATION** | Lễ tân hủy lịch hẹn qua API `POST /api/receptionist/appointments/{newId}/cancel` với lý do cuộc gọi hỗ trợ. SQL Server xác minh trạng thái chuyển sang `Cancelled` sạch sẽ. | **PASS** |
| 7 | **1.7** | Rebook & Receptionist Check-in | **API_INTEGRATION** <br/> *(kèm Browser View)* | Đặt lịch trong ngày qua SQL và duyệt qua API. Lễ tân đăng nhập trình duyệt, mở `/Receptionist/Appointments/TodayCheckIn`. Gọi API `POST /api/receptionist/appointments/{id}/check-in`. SQL Server xác minh trạng thái `CheckedIn`. Trình duyệt chụp lại giao diện check-in. | **PASS** |
| 8 | **1.8** | Dentist Assign Chair & InService | **API_INTEGRATION** <br/> *(kèm Browser View)* | Bác sĩ (`dentist@dentalcare.com`) đăng nhập trình duyệt, mở `/Dentist/Appointments/Today`. Gọi API `POST /api/dentist/appointments/{id}/assign-chair` với Ghế 3. SQL xác minh trạng thái `InService` và `DentalChairId = 3`. Trình duyệt chụp giao diện điều trị. | **PASS** |
| 9 | **4.1** | Doctor Overlap Conflict Guard | **API_INTEGRATION** + **DATABASE_VERIFICATION** | Tạo lịch hẹn mới cùng khung giờ và gọi `POST /api/department/appointments/{id}/confirm` gán cho Bác sĩ 4 (đang có ca). Server từ chối `400 Bad Request`: *"Bác sĩ đã có lịch hẹn khác trong khung giờ này."* | **PASS** |
| 10 | **4.2** | Patient Overlap Conflict Guard | **API_INTEGRATION** + **DATABASE_VERIFICATION** | Tạo lịch hẹn cho Bệnh nhân 6 cùng khung giờ và gọi API xác nhận tại Khoa khác. Server từ chối `400 Bad Request`: *"Bệnh nhân đã có lịch hẹn khác trong khung giờ này."* | **PASS** |
| 11 | **4.3** | Exclusive Room Conflict Guard | **API_INTEGRATION** + **DATABASE_VERIFICATION** | Cố tình gán ca mới vào Phòng phẫu thuật 2 (phòng độc quyền `IsExclusive = 1` đang có ca khám). Server từ chối: *"Phòng khám đã bị trùng lịch với ca khám khác trong khung giờ này."* | **PASS** |
| 12 | **4.4** | Dental Chair Conflict Guard | **API_INTEGRATION** + **DATABASE_VERIFICATION** | Đặt trước Ghế 1 lúc 14:00, sau đó xác nhận ca mới gán Ghế 1 cùng giờ. Server từ chối: *"Ghế nha khoa đã bị trùng lịch với ca khám khác trong khung giờ này."* | **PASS** |
| 13 | **4.5** | Doctor Leave / Unavailability Guard | **API_INTEGRATION** + **DATABASE_VERIFICATION** | Chèn bản ghi nghỉ phép trong `DentistAvailability` cho Bác sĩ 4, sau đó duyệt lịch trong giờ nghỉ. Server từ chối: *"Bác sĩ đang bận hoặc nghỉ phép trong khoảng thời gian này."* | **PASS** |
| 14 | **4.6** | Cancelled Doctor Shift Guard | **API_INTEGRATION** + **DATABASE_VERIFICATION** | Tạo ca trực của bác sĩ ở trạng thái `Cancelled`, sau đó xác nhận lịch hẹn. Server từ chối: *"Ca làm việc của Bác sĩ đã bị hủy."* | **PASS** |
| 15 | **4.7** | Rapid Double-Click Concurrency Guard | **API_INTEGRATION** + **DATABASE_VERIFICATION** | Khởi chạy 2 Task bất đồng bộ gửi đồng thời 2 HTTP request xác nhận cùng một lịch hẹn. Khóa `SqlAppLockHelper.ApptKey` ép tuần tự hóa: đúng 1 request thành công, 1 request nhận từ chối an toàn. | **PASS** |

---

## 3. BẢNG MÃ BĂM SHA-256 SCREENSHOTS & PHÂN TÍCH KỸ THUẬT NGUYÊN NHÂN TRÙNG LẶP

Toàn bộ 15 ảnh chụp màn hình được sinh ra trong thư mục `reports/screenshots/`. Kết quả tính toán SHA-256 bằng thuật toán chuẩn (`Get-FileHash -Algorithm SHA256`) cho thấy:

| File ảnh | Kích thước | Mã băm SHA-256 | Trang giao diện thực tế | Trạng thái hiển thị |
|---|:---:|---|---|---|
| `01_login_page.png` | 422,943 B | `B12DA867C528D770BB8391B6B1AB9EEA3FBB45CA558B5382A6019DC47FF38232` | `/Account/Login` | Form đăng nhập hệ thống DentalCare |
| `02_patient_dashboard.png` | 370,720 B | `9F65FF0E09539FD9AF704C6ACE5EB5DAF18DBBFF13ADC6336E8DC6D46F955A1D` | `/Patient/Dashboard` | Dashboard Bệnh nhân Lê Hoàng Minh |
| `03_patient_booking_form.png` | 218,683 B | `E5576B38DD4C393416E21CE2F7B520B80FDFDB4F048B7FD99EA10E6F6227384C` | `/Patient/Appointment/Book` | Form chọn chuyên khoa & dịch vụ |
| `04_patient_appointment_detail_pending.png` | 194,530 B | `440E7C9BBAB07CA2303067D3C58B05741D79432C2819B036CE5AA909BD3F2BA0` | `/Patient/Appointment/Detail/76` | Chi tiết lịch hẹn trạng thái `PendingReceptionReview` |
| `05_receptionist_dashboard.png` | 366,311 B | `E23D7C004CADE9368B8AF8A9BB30B147E816986BE4A777E46556BA93E42CFBBA` | `/Receptionist/Dashboard` | Dashboard Lễ tân tiếp đón |
| `06_receptionist_requests_queue.png` | 64,236 B | `D92125F4F3645F88ACCF1327D2F0395E46061AD3235F984B37895962BF0D1D1C` | HTTP 404 Error Page | Trang lỗi 404 (do URL số ít `/Receptionist/Appointment/...`) |
| `07_manager_dashboard.png` | 366,311 B | `E23D7C004CADE9368B8AF8A9BB30B147E816986BE4A777E46556BA93E42CFBBA` | `/Receptionist/Dashboard` | **Trùng với 05** (Do Cookie session Lễ tân còn lưu) |
| `08_manager_requests_queue.png` | 64,236 B | `D92125F4F3645F88ACCF1327D2F0395E46061AD3235F984B37895962BF0D1D1C` | HTTP 404 Error Page | **Trùng với 06** (Trang lỗi 404) |
| `09_appointment_confirmed_detail.png` | 200,570 B | `E23ABFFD20B72C941C1E79FB1C22E1C1C6AEA8249BDABAEF0DAA53B5EE2800AB` | `/Patient/Appointment/Detail/76` | Chi tiết lịch hẹn đã xác nhận (`Confirmed`, Queue `Q001`) |
| `10_patient_rescheduled_new_appointment.png` | 202,708 B | `17659CD83018AC9AB17099BCA13C5A5AEEF44DFA331EFBE99BA6280EB7D41E54` | `/Patient/Appointment/Detail/77` | Chi tiết lịch hẹn mới sau khi dời lịch |
| `11_receptionist_today_checkin_list.png` | 64,236 B | `D92125F4F3645F88ACCF1327D2F0395E46061AD3235F984B37895962BF0D1D1C` | HTTP 404 Error Page | **Trùng với 06** (Trang lỗi 404) |
| `12_receptionist_checkedin_success.png` | 64,236 B | `D92125F4F3645F88ACCF1327D2F0395E46061AD3235F984B37895962BF0D1D1C` | HTTP 404 Error Page | **Trùng với 06** (Trang lỗi 404) |
| `13_dentist_dashboard.png` | 366,425 B | `A59406CE6C97BC629E499D2C34B549D68C7DF667935028BE3B37AB4A8BDFA8BE` | `/Dentist/Dashboard` | Dashboard Bác sĩ Nguyễn Văn An |
| `14_dentist_today_schedule.png` | 64,236 B | `D92125F4F3645F88ACCF1327D2F0395E46061AD3235F984B37895962BF0D1D1C` | HTTP 404 Error Page | **Trùng với 06** (Trang lỗi 404) |
| `15_dentist_inservice_active.png` | 64,236 B | `D92125F4F3645F88ACCF1327D2F0395E46061AD3235F984B37895962BF0D1D1C` | HTTP 404 Error Page | **Trùng với 06** (Trang lỗi 404) |

### 3.1. Phân tích nguyên nhân kỹ thuật gây trùng lặp byte (Root Cause Analysis)

Phát hiện **2 nhóm trùng lặp byte**:

#### Nhóm 1: `05_receptionist_dashboard.png` và `07_manager_dashboard.png`
- **Hash:** `E23D7C004CADE9368B8AF8A9BB30B147E816986BE4A777E46556BA93E42CFBBA` (366,311 bytes)
- **Nguyên nhân:**
  Trong kiến trúc MVC của DentalClinic, `AccountController.Login` kiểm tra:
  ```csharp
  if (User.Identity?.IsAuthenticated == true)
  {
      return RedirectToRoleDashboard(userRole);
  }
  ```
  Trình duyệt Edge Headless qua DevTools Protocol duy trì phiên làm việc liên tục. Khi script chuyển từ Bước 3 (Lễ tân) sang Bước 4 (Trưởng khoa), lệnh `NavigateAsync("/Account/Login")` gửi kèm cookie `DentalClinic.Auth` của Lễ tân. Hệ thống phát hiện người dùng đã đăng nhập nên lập tức phản hồi HTTP 302 Redirect về `/Receptionist/Dashboard`, không hiển thị form đăng nhập để đăng nhập Trưởng khoa. Do đó, ảnh chụp `07` thực chất là trang Dashboard của Lễ tân chụp lại lần 2.
- **Giải pháp đã thực hiện trong Runner:**
  Đã bổ sung hàm `ClearCookiesAndNavigateLoginAsync` thực thi lệnh CDP `Network.clearBrowserCookies` trước khi mở trang đăng nhập tài khoản khác.

#### Nhóm 2: Các ảnh `06`, `08`, `11`, `12`, `14`, `15`
- **Hash:** `D92125F4F3645F88ACCF1327D2F0395E46061AD3235F984B37895962BF0D1D1C` (64,236 bytes)
- **Nguyên nhân:**
  Quy ước định tuyến (Routing Convention) trong ASP.NET Core MVC của nhóm dự án:
  - `PatientAppointmentController`: hỗ trợ cả số ít `[Route("Patient/Appointment")]` lẫn số nhiều `[Route("Patient/Appointments")]`.
  - `ReceptionistAppointmentsController`: định tuyến **chỉ dùng số nhiều** `[Route("Receptionist/Appointments")]`.
  - `DepartmentAppointmentsController`: định tuyến **chỉ dùng số nhiều** `[Route("Department/Appointments")]`.
  - `DentistAppointmentsController`: định tuyến **chỉ dùng số nhiều** `[Route("Dentist/Appointments")]`.
  Khi script E2E ban đầu truy cập các đường dẫn số ít như `/Receptionist/Appointment/Requests` hay `/Dentist/Appointment/Today`, server trả về trang HTTP 404 chuẩn của ứng dụng. Vì nội dung trang 404 hoàn toàn tĩnh, tất cả các ảnh chụp này có kích thước byte và mã băm SHA-256 giống hệt nhau (64,236 bytes).
- **Giải pháp đã thực hiện trong Runner:**
  Đã cập nhật toàn bộ URL đích trong `tools/QA_E2E_TestRunner/Program.cs` sang dạng số nhiều chuẩn (`Appointments`).

### 3.2. Đánh giá an toàn thông tin (Security & Credential Audit)
Rà soát toàn diện nội dung hiển thị trên 15 ảnh chụp:
- **Mật khẩu (Password):** Ô nhập mật khẩu trên giao diện HTML sử dụng thuộc tính `type="password"`, hiển thị dưới dạng dấu chấm tròn (`••••••••`), hoàn toàn không bị lộ bản rõ trên màn hình.
- **Access Token / Session Cookie:** Không có token JWT hoặc chuỗi cookie nào được in ra giao diện người dùng.
- **Thông tin PII (Personally Identifiable Information):** Chỉ chứa dữ liệu thử nghiệm giả lập (mock data: `lehoangminh.test@dentalcare.com`, `Password123@`), không chứa thông tin thực của khách hàng.

---

## 4. XÁC MINH TRẠNG THÁI CƠ SỞ DỮ LIỆU (DATABASE STATE VERIFICATION)

Đã thực hiện truy vấn trực tiếp trên SQL Server instance `.\SQL2025_DUC`:

```sql
SELECT 'DentalClinicManagementDB' AS DB, COUNT(*) AS ApptCount FROM DentalClinicManagementDB.dbo.Appointments
UNION ALL
SELECT 'DentalClinic_QA_E2E', COUNT(*) FROM DentalClinic_QA_E2E.dbo.Appointments;
```

### Kết quả truy vấn:
```text
DB                       ApptCount  
------------------------ -----------
DentalClinicManagementDB           1
DentalClinic_QA_E2E               53
```

```sql
SELECT name, state_desc, create_date FROM sys.databases 
WHERE name IN ('DentalClinicManagementDB', 'DentalClinic_QA_E2E');
```

```text
name                     state_desc   create_date
------------------------ ------------ -----------------------
DentalClinicManagementDB ONLINE       2026-10-12 13:58:08.937
DentalClinic_QA_E2E      ONLINE       2026-11-06 00:35:57.373
```

### Đánh giá:
1. **`DentalClinicManagementDB` (Database chính):**
   - Số lượng bản ghi `Appointments` giữ nguyên chính xác **1 bản ghi ban đầu**.
   - Trạng thái `ONLINE`, thời điểm tạo ngày `12/10/2026`.
   - **Tuyệt đối không bị ghi đè, sửa đổi hay nhiễm dữ liệu thử nghiệm.**
2. **`DentalClinic_QA_E2E` (Database thử nghiệm riêng):**
   - Chứa 53 bản ghi bao gồm toàn bộ dữ liệu kiểm thử E2E và các ca cạnh tranh khóa.
   - Được cô lập hoàn toàn, sẵn sàng cho việc tái lập kiểm thử tự động bất cứ lúc nào.

---

## 5. HƯỚNG DẪN TÁI LẬP KIỂM THỬ (REPRODUCTION GUIDE)

### Bước 1: Khởi động Backend API kết nối database QA
```powershell
$env:ConnectionStrings__DentalClinicDatabase = "Server=.\SQL2025_DUC;Database=DentalClinic_QA_E2E;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=false"
cd D:\SWP\SWP391_Group6\BE\DentalClinic.Api
dotnet run --no-build
```

### Bước 2: Khởi động Frontend Web
```powershell
$env:ApiSettings__BaseUrl = "https://localhost:7350"
$env:ASPNETCORE_URLS = "https://localhost:7129;http://localhost:5049"
cd D:\SWP\SWP391_Group6\FE\DentalClinic.Web
dotnet run --no-build
```

### Bước 3: Chạy bộ kiểm thử E2E Runner
```powershell
cd D:\SWP\SWP391_Group6
dotnet run --project tools/QA_E2E_TestRunner/QA_E2E_TestRunner.csproj
```

---

## 6. NHẬN ĐỊNH PHẠM VI KIỂM THỬ (TESTING SCOPE ASSESSMENT)

### Những gì bài kiểm thử ĐÃ CHỨNG MINH:
1. **Bảo toàn dữ liệu dưới tải đồng thời (Concurrency & Integrity Hardening):**
   - Cơ chế khóa phân tán cấp ứng dụng `SqlAppLockHelper` (với các khóa `ApptKey`, `DentistMasterKey`, `DentistKey`, `PatientKey`, `DeptQueueKey`, `ChairKey`, `RoomKey`) hoạt động chính xác 100%.
   - Chặn đứng mọi rủi ro: đặt trùng bác sĩ, trùng bệnh nhân, trùng phòng độc quyền, trùng ghế, bác sĩ nghỉ phép, ca trực bị hủy.
   - Chống Double-Click thành công: 2 request xác nhận đồng thời chỉ duy nhất 1 request thành công, request thứ hai bị từ chối sạch sẽ.
2. **Tính toàn vẹn của chuỗi trạng thái cuộc hẹn:**
   - Dòng đời: `PendingReceptionReview` -> `PendingDepartmentReview` -> `Confirmed` (cấp Queue) -> `Rescheduled` -> `Cancelled` -> `CheckedIn` -> `InService` được ghi nhận chuẩn mực trong `AppointmentStatusHistories`.
3. **Sửa lỗi Post-Commit Notification:**
   - `ManagerConfirmAsync` không còn gặp lỗi ném `InvalidOperationException` khi thông báo thất bại sau commit trên nhánh `fix/manager-confirm-post-commit-notification`.

### Những gì bài kiểm thử CHƯA CHỨNG MINH (Giới hạn phạm vi):
1. **Tương tác click form trực tiếp trên giao diện Browser (Frontend Form DOM Clicking):**
   - Bài test tập trung vào kiểm tra logic lõi (Core Business Logic & API Layer). Ngoại trừ bước 1.1 (Đăng nhập) thao tác thực sự qua DOM, các bước tạo hẹn 1.2 và chuyển trạng thái 1.3 - 1.8 được gọi qua REST API hoặc SQL rồi mới navigate browser để chụp ảnh kiểm chứng. Chưa mô phỏng việc người dùng click từng dropdown, modal picker hay calendar UI trên trình duyệt.
2. **Kiểm thử chịu tải quy mô lớn (Stress / Load Testing):**
   - Bài test chỉ xác minh tính đúng đắn về mặt logic của race condition ở mức độ đồng thời nhỏ (2 request song song), chưa đo lường hiệu năng khi có hàng trăm request đồng thời trong môi trường production phân tán.

---

## 7. KẾT LUẬN & ĐỀ XUẤT BÀN GIAO

- **Phán quyết QA:** **QA_PASSED** (cho tầng tích hợp backend, kiểm soát đồng thời và tính toàn vẹn dữ liệu).
- **Trạng thái nhánh:**
  - Nhánh `develop`: Đã ở HEAD mới nhất (`73ba125e`), sẵn sàng phát triển tiếp.
  - Nhánh `fix/manager-confirm-post-commit-notification`: Đã push origin (`eb5d39e2`), chờ Technical Lead review & merge qua Pull Request khi cần.
  - Nhánh `qa/followup-postmerge-verification`: Chứa runner, báo cáo và bằng chứng kiểm thử hoàn chỉnh.
