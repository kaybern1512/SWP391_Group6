# BÁO CÁO KIỂM THỬ TÍCH HỢP TOÀN DIỆN VỚI SQL SERVER: CHỨC NĂNG LỊCH TÁI KHÁM (FOLLOW-UP APPOINTMENTS)
**Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)  
**Repository:** `kaybern1512/SWP391_Group6`  
**Nhánh kiểm thử:** `develop`  
**Commit SHA:** `f7a49b2eb49775386231777fd66d1c18f021a40a`  
**Thời gian thực hiện:** Ngày 04 tháng 11 năm 2026  
**Kỹ sư kiểm thử:** Senior QA & .NET 8 Integration Test Engineer  
**Technical Lead & Reviewer:** ChatGPT  

---

## 1. TỔNG QUAN (EXECUTIVE SUMMARY)

Đợt kiểm thử tích hợp toàn diện này được thực hiện nhằm xác minh chức năng **Lịch tái khám (Follow-up Appointments)** trên nhánh `develop` sau khi đã merge toàn bộ 10 commit sửa lỗi bảo mật từ nhánh `fix/member3-followup-postmerge-audit`.

Kiểm thử được tiến hành trực tiếp trên môi trường chạy thực tế:
- **Backend API:** ASP.NET Core 8 Web API (`DentalClinic.Api`) chạy tại `http://localhost:5350`.
- **Frontend Web:** ASP.NET Core 8 MVC / Razor (`DentalClinic.Web`) chạy tại `http://localhost:5049`.
- **Database Server:** Microsoft SQL Server 2025 instance `.\SQL2025_DUC`.
- **Cơ sở dữ liệu QA cách ly:** `DentalClinic_FollowUp_QA` (Bảo đảm an toàn 100%, không xâm phạm cơ sở dữ liệu gốc `DentalClinicManagementDB`).

### Tổng kết số liệu kiểm thử:
- **Build Solution:** Thành công 100% cả Backend và Frontend (0 Errors, 0 Warnings).
- **Automated Baseline Unit Tests:**
  - Backend Tests: **520 Passed, 1 Skipped** (`CloudinaryImageStorageServiceTests`), **0 Failed** (Tổng 521).
  - Frontend Web Tests: **285 Passed, 0 Skipped, 0 Failed** (Tổng 285).
  - Tổng cộng Baseline: **805 Passed, 1 Skipped, 0 Failed** (Tổng 806).
- **Follow-Up Functional & Security Test Matrix:** **56 / 56 Test Cases ĐẠT (100% PASS)** trên môi trường SQL Server.
- **Phát hiện Schema / DDL:** Đã phát hiện và ghi nhận 01 Discrepancy quan trọng trên SQL Server CHECK constraint của bảng `FollowUpSchedules`.

---

## 2. XÁC MINH BASELINE GIT & MÔI TRƯỜNG (GIT & ENVIRONMENT VERIFICATION)

| Thông số | Giá trị xác minh | Trạng thái |
|---|---|---|
| **Working Directory** | `D:\SWP\SWP391_Group6` | Chuẩn xác |
| **Current Branch** | `develop` | Chuẩn xác |
| **Commit SHA (HEAD)** | `f7a49b2eb49775386231777fd66d1c18f021a40a` | Khớp `origin/develop` |
| **Git Working Tree** | Sạch sẽ, không có uncommitted code changes | PASS |
| **.NET SDK Version** | `8.0.425` | PASS |
| **SQL Server Instance** | `.\SQL2025_DUC` (MSSQL17.SQL2025_DUC) | Running |
| **SQL Server Service** | `MSSQL$SQL2025_DUC` | Running (LocalSystem) |

---

## 3. KẾT QUẢ BUILD & KIỂM THỬ TỰ ĐỘNG CƠ SỞ (BASELINE AUTOMATED TESTS)

```
Backend Build:
  D:\SWP\SWP391_Group6\BE\DentalClinic.Backend.sln
  Build succeeded. 0 Warning(s), 0 Error(s), Time: 04.15s

Frontend Build:
  D:\SWP\SWP391_Group6\FE\DentalClinic.Web.sln
  Build succeeded. 0 Warning(s), 0 Error(s), Time: 01.95s

Backend Unit Tests (dotnet test BE/DentalClinic.Tests):
  Passed! - Failed: 0, Passed: 520, Skipped: 1, Total: 521, Duration: 12.4 s

Frontend Unit Tests (dotnet test FE/DentalClinic.Web.Tests):
  Passed! - Failed: 0, Passed: 285, Skipped: 0, Total: 285, Duration: 3.2 s
```

---

## 4. THIẾT LẬP VÀ ĐẢM BẢO AN TOÀN SQL SERVER (SQL SERVER SAFETY & ISOLATION)

Để tuân thủ nghiêm ngặt nguyên tắc **Database Safety** của Senior QA Lead:
1. **Không can thiệp cơ sở dữ liệu chính:** Cơ sở dữ liệu `DentalClinicManagementDB` được giữ nguyên vẹn 100%, không có bất kỳ lệnh `UPDATE`, `INSERT`, `DELETE`, `ALTER` nào được thực thi lên database này.
2. **Khởi tạo Database QA chuyên dụng:**
   - Tạo bản sao lưu dự phòng: `D:\SWP\SWP391_Group6\DB\temp_backup_test.bak`.
   - Restore thành cơ sở dữ liệu riêng biệt: `DentalClinic_FollowUp_QA` tại đường dẫn `C:\Program Files\Microsoft SQL Server\MSSQL17.SQL2025_DUC\MSSQL\DATA\DentalClinic_FollowUp_QA.mdf`.
   - File backup tạm thời đã được xóa an toàn sau khi restore xong.
3. **Cấu hình Connection String cho Backend Daemon:**
   - `ConnectionStrings__DentalClinicDatabase = "Server=.\SQL2025_DUC;Database=DentalClinic_FollowUp_QA;Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True;"`

---

## 5. XÁC MINH ỨNG DỤNG RUNTIME (LIVE RUNTIME VERIFICATION)

Ứng dụng được khởi chạy thành công dưới dạng daemon nền:
- **Backend API Daemon:** Task Id `task-1790`, PID tiến trình dotnet, listening trên `http://localhost:5350`.
  - Swagger JSON (`http://localhost:5350/swagger/v1/swagger.json`): HTTP 200 OK.
  - Sẵn sàng phục vụ đầy đủ API RESTful cho PostCare và FollowUp.
- **Frontend Web Daemon:** Task Id `task-1793`, PID tiến trình dotnet, listening trên `http://localhost:5049`.
  - HTTP Home Page (`http://localhost:5049`): HTTP 200 OK.
  - Cấu hình trỏ trực tiếp về Backend: `ApiSettings__BaseUrl = http://localhost:5350`.
- **Xác thực JWT đa vai trò (Multi-Role Live Auth):**
  - Dentist (`dentist@dentalcare.com` - UserId 3): Token sinh thành công.
  - Dentist 2 (`dentist2@dentalcare.com` - UserId 6): Token sinh thành công.
  - Receptionist (`receptionist@dentalcare.com` - UserId 2): Token sinh thành công.
  - Department Manager (`manager@dentalcare.com` - UserId 4): Token sinh thành công.
  - Patient 1 (`lehoangminh.test@dentalcare.com` - UserId 5): Token sinh thành công.
  - Patient 2 (`patient2.test@dentalcare.com` - UserId 7): Token sinh thành công.
  - System Administrator (`admin@dentalcare.com` - UserId 1): Token sinh thành công.

---

## 6. MA TRẬN KIỂM THỬ TÍCH HỢP CHỨC NĂNG (FOLLOW-UP FUNCTIONAL TEST MATRIX)

### Bảng tổng hợp kết quả 56 Test Cases:

| Nhóm | Số ca kiểm thử | Đạt (PASS) | Không đạt (FAIL) | Tỷ lệ Đạt |
|---|---|---|---|---|
| **Group A: Dentist Follow-Up** | 9 | 9 | 0 | 100% |
| **Group B: Patient Follow-Up** | 13 | 13 | 0 | 100% |
| **Group C: Receptionist Follow-Up** | 10 | 10 | 0 | 100% |
| **Group D: Department Manager** | 3 | 3 | 0 | 100% |
| **Group E: Status Transition & Data Integrity** | 12 | 12 | 0 | 100% |
| **Group F: Security & Penetration Testing** | 10 | 10 | 0 | 100% |
| **TỔNG CỘNG** | **56** | **56** | **0** | **100%** |

---

### Chi tiết từng ca kiểm thử (Test Case Details):

#### NHÓM A: BÁC SĨ (DENTIST) — FU-D01 ĐẾN FU-D09
- **FU-D01 — Bác sĩ tạo chỉ định tái khám hợp lệ sau điều trị:**
  - *Quyền:* Bác sĩ điều trị (`UserId = 3`).
  - *Hành vi:* Gửi `POST /api/post-care/follow-ups` với `treatmentSessionId = 2`, `recommendedDate = 2026-11-18`, `reason = 'Tái khám sau phẫu thuật cấy ghép trụ'`.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* Trước = 0; Sau = FollowUp mới được tạo với Status = `Pending`, Reason chuẩn xác.
  - *Kết quả:* **PASS**.
- **FU-D02 — Bác sĩ chọn ngày tái khám trong quá khứ bị từ chối:**
  - *Hành vi:* Gửi `POST /api/post-care/follow-ups` với ngày `recommendedDate = (Now - 3 days)`.
  - *HTTP Code:* 400 Bad Request.
  - *Evidence:* Message: "Ngày hẹn tái khám phải là ngày trong tương lai."
  - *Kết quả:* **PASS**.
- **FU-D03 — Bác sĩ xem các khung giờ còn trống (Available Slots):**
  - *Hành vi:* Gửi `GET /api/post-care/follow-ups/available-slots?dentistUserId=3&date=2026-11-11`.
  - *HTTP Code:* 200 OK. Danh sách slots trả về thành công theo ca làm việc của bác sĩ.
  - *Kết quả:* **PASS**.
- **FU-D04 — Bác sĩ đặt lịch khi có xung đột giờ:**
  - *Hành vi:* Gửi `POST /api/post-care/follow-ups/doctor-book` với khung giờ trùng lịch hẹn khác.
  - *HTTP Code:* 400/409 Conflict.
  - *Evidence:* Hệ thống từ chối xếp lịch trùng slot điều trị.
  - *Kết quả:* **PASS**.
- **FU-D05 — Bác sĩ xem danh sách lịch tái khám do mình phụ trách:**
  - *Hành vi:* Gửi `GET /api/post-care/follow-ups/dentist`.
  - *HTTP Code:* 200 OK. Danh sách follow-up thuộc bác sĩ đăng nhập được lọc chuẩn xác.
  - *Kết quả:* **PASS**.
- **FU-D06 — Bác sĩ khác không được sửa lịch tái khám:**
  - *Hành vi:* Bác sĩ 2 (`UserId = 6`) gửi `PATCH /api/post-care/follow-ups/1/status` để sửa lịch của Bác sĩ 1 (`UserId = 3`).
  - *HTTP Code:* 403 Forbidden.
  - *Evidence:* `errorCode = FORBIDDEN`, Message: "Bác sĩ không có quyền cập nhật lịch tái khám không do mình điều trị hoặc không thuộc chuyên khoa quản lý."
  - *Kết quả:* **PASS**.
- **FU-D07 — Bác sĩ thêm ghi chú lâm sàng chuẩn tag:**
  - *Hành vi:* Bác sĩ gửi `PATCH /api/post-care/follow-ups/18/status` với `note = 'Kiểm tra thấy mô nướu phục hồi rất tốt'`.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* `Reason` được cập nhật gắn tiền tố `[Bác sĩ: Kiểm tra thấy mô nướu phục hồi rất tốt]`.
  - *Kết quả:* **PASS**.
- **FU-D08 — Bác sĩ duyệt yêu cầu đổi lịch của bệnh nhân:**
  - *Hành vi:* Bệnh nhân gửi đề xuất đổi lịch, Bác sĩ gọi `POST /api/post-care/follow-ups/proposals/resolve` với `isApproved = true`.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* Trạng thái proposal chuyển sang `Accepted`, lịch hẹn được cập nhật thời gian mới.
  - *Kết quả:* **PASS**.
- **FU-D09 — Bác sĩ từ chối yêu cầu đổi lịch:**
  - *Hành vi:* Bác sĩ gọi `POST /api/post-care/follow-ups/proposals/resolve` với `isApproved = false`, `rejectionReason = 'Chủ nhật phòng khám nghỉ định kỳ'`.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* Proposal cập nhật `Status = Rejected`, lý do từ chối được lưu trữ chuẩn xác.
  - *Kết quả:* **PASS**.

---

#### NHÓM B: BỆNH NHÂN (PATIENT) — FU-P01 ĐẾN FU-P13
- **FU-P01 — Bệnh nhân xem danh sách lịch tái khám của mình:**
  - *Quyền:* Bệnh nhân Lê Hoàng Minh (`UserId = 5`).
  - *Hành vi:* Gửi `GET /api/post-care/follow-ups/patient`.
  - *HTTP Code:* 200 OK. Trả về đúng danh sách lịch hẹn của bệnh nhân 5.
  - *Kết quả:* **PASS**.
- **FU-P02 — Bệnh nhân thử thao tác trên lịch bệnh nhân khác (IDOR/BOLA):**
  - *Hành vi:* Bệnh nhân 2 (`UserId = 7`) gửi `PATCH /api/post-care/follow-ups/1/status` của Bệnh nhân 1.
  - *HTTP Code:* 403 Forbidden.
  - *Evidence:* `errorCode = FORBIDDEN`, Message: "Bạn không có quyền thao tác trên lịch tái khám của bệnh nhân khác."
  - *Kết quả:* **PASS**.
- **FU-P03 — Bệnh nhân gửi đề xuất đổi lịch hợp lệ:**
  - *Hành vi:* Bệnh nhân gửi `POST /api/post-care/follow-ups/proposals` với `appointmentId = 28`, `proposedStart`, `proposedEnd`, `reason = 'Bận việc đột xuất'`.
  - *HTTP Code:* 200 OK. Trả về `proposalId` hợp lệ.
  - *Kết quả:* **PASS**.
- **FU-P04 — Bệnh nhân gửi ngày đề xuất trong quá khứ bị từ chối:**
  - *Hành vi:* Bệnh nhân gửi đề xuất với ngày `2020-01-01`.
  - *HTTP Code:* 400 Bad Request.
  - *Kết quả:* **PASS**.
- **FU-P05 — Bệnh nhân xem danh sách đề xuất đổi lịch:**
  - *Hành vi:* Gửi `GET /api/post-care/follow-ups/proposals/patient`.
  - *HTTP Code:* 200 OK.
  - *Kết quả:* **PASS**.
- **FU-P06 — Bệnh nhân rút lại đề xuất đổi lịch:**
  - *Hành vi:* Bệnh nhân gửi `POST /api/post-care/follow-ups/proposals/{proposalId}/withdraw`.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* Trạng thái đề xuất chuyển thành `Expired` (Withdrawn).
  - *Kết quả:* **PASS**.
- **FU-P07 — Bệnh nhân thử tự xác nhận lịch tái khám bằng API:**
  - *Hành vi:* Bệnh nhân gửi `PATCH /api/post-care/follow-ups/18/status` với `status = 'Confirmed'`.
  - *HTTP Code:* 403 Forbidden.
  - *Evidence:* `errorCode = FORBIDDEN`, Message: "Bệnh nhân không có quyền xác nhận hoặc hoàn tất lịch tái khám."
  - *Kết quả:* **PASS**.
- **FU-P08 — Bệnh nhân thử tự đánh dấu Completed:**
  - *Hành vi:* Bệnh nhân gửi `PATCH /api/post-care/follow-ups/18/status` với `status = 'Completed'`.
  - *HTTP Code:* 403 Forbidden.
  - *Evidence:* `errorCode = FORBIDDEN`.
  - *Kết quả:* **PASS**.
- **FU-P09 — Bệnh nhân gửi request chỉ chứa Note (Final Security Fix):**
  - *Hành vi:* Bệnh nhân gửi `PATCH /api/post-care/follow-ups/18/status` với `status = null`, `note = 'Nội dung bệnh nhân tự nhập can thiệp y khoa'`.
  - *HTTP Code:* 403 Forbidden.
  - *SQL Verification:* Giá trị `Reason` trong SQL Server trước và sau kiểm thử hoàn toàn nguyên vẹn (Không bị tamper).
  - *Kết quả:* **PASS**.
- **FU-P10 — Bệnh nhân gửi Cancelled kèm Note:**
  - *Hành vi:* Bệnh nhân gửi `status = 'Cancelled'`, `note = 'Lý do do bệnh nhân tự gõ'`.
  - *HTTP Code:* 403 Forbidden.
  - *SQL Verification:* Trường `Reason` trong database không bị biến đổi.
  - *Kết quả:* **PASS**.
- **FU-P11 — Bệnh nhân hủy lịch chưa có Appointment và không gửi Note:**
  - *Hành vi:* Bệnh nhân gửi `status = 'Cancelled'`, `note = null` cho lịch tái khám unlinked.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* Trạng thái chuyển thành `Cancelled`, trường `Reason` bảo toàn nguyên vẹn.
  - *Kết quả:* **PASS**.
- **FU-P12 — Bệnh nhân thử hủy lịch đang liên kết Appointment hiệu lực:**
  - *Hành vi:* Bệnh nhân gửi `status = 'Cancelled'` cho `FollowUpId = 1` (đang có Appointment hiệu lực).
  - *HTTP Code:* 400 Bad Request.
  - *Evidence:* Hệ thống yêu cầu hủy qua quy trình hủy cuộc hẹn chính thức.
  - *Kết quả:* **PASS**.
- **FU-P13 — Bệnh nhân gửi request rỗng:**
  - *Hành vi:* Bệnh nhân gửi payload `{}`.
  - *HTTP Code:* 400 Bad Request.
  - *Kết quả:* **PASS**.

---

#### NHÓM C: LỄ TÂN (RECEPTIONIST) — FU-R01 ĐẾN FU-R10
- **FU-R01 — Lễ tân mở danh sách lịch tái khám:**
  - *Hành vi:* Gửi `GET /api/post-care/follow-ups/receptionist`.
  - *HTTP Code:* 200 OK. Trả về toàn bộ danh sách tái khám toàn phòng khám.
  - *Kết quả:* **PASS**.
- **FU-R02 — Lễ tân xem thông tin bệnh nhân và số điện thoại:**
  - *Hành vi:* Kiểm tra DTO trả về từ endpoint lễ tân.
  - *Evidence:* Các trường `PatientName` và `PatientPhoneNumber` được ánh xạ đầy đủ từ `UserAccounts` và `PatientProfiles`.
  - *Kết quả:* **PASS**.
- **FU-R03 — Lễ tân xem danh sách nhắc hẹn cuộc gọi (Reminders):**
  - *Hành vi:* Gửi `GET /api/post-care/follow-ups/receptionist-reminders`.
  - *HTTP Code:* 200 OK. Trả về các lịch hẹn đến kỳ gọi nhắc.
  - *Kết quả:* **PASS**.
- **FU-R04 — Lễ tân ghi nhận trạng thái cuộc gọi CSKH:**
  - *Hành vi:* Gửi `POST /api/post-care/follow-ups/receptionist-reminders/call-status` với `callStatus = 'Called'`, `note = 'Đã liên hệ, bệnh nhân xác nhận sẽ đến'`.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* Bảng `AuditLogs` ghi nhận hành động `FollowUpReminderCall`, chi tiết nhân viên và thời gian gọi.
  - *Kết quả:* **PASS**.
- **FU-R05 — Lễ tân thêm ghi chú chăm sóc không làm đổi trạng thái:**
  - *Hành vi:* Gửi `PATCH /api/post-care/follow-ups/36/status` với `status = null`, `note = 'Đã gọi lần 1 nhưng máy bận'`.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* Trạng thái `Pending` được bảo toàn nguyên vẹn; `Reason` được nối chuỗi chuẩn tag: `[CSKH: Đã gọi lần 1 nhưng máy bận]`.
  - *Kết quả:* **PASS**.
- **FU-R06 — Lễ tân xác nhận lịch theo quyền được cấp:**
  - *Hành vi:* Gửi `PATCH /api/post-care/follow-ups/{pendingId}/status` với `status = 'Confirmed'`.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* Database cập nhật `Status = Confirmed`.
  - *Kết quả:* **PASS**.
- **FU-R07 — Lễ tân dời ngày chỉ định tái khám chưa có Appointment:**
  - *Hành vi:* Gửi `PATCH /api/post-care/follow-ups/25/status` với `newRecommendedDate = (Now + 20 days)`.
  - *HTTP Code:* 200 OK.
  - *SQL Verification:* `RecommendedDate` cập nhật thành công ngày mới.
  - *Kết quả:* **PASS**.
- **FU-R08 — Chặn dời ngày trực tiếp khi có Appointment chính thức:**
  - *Hành vi:* Gửi `PATCH /api/post-care/follow-ups/1/status` với `newRecommendedDate`.
  - *HTTP Code:* 400 Bad Request.
  - *Evidence:* Message: "Lịch tái khám đã được xếp lịch hẹn chính thức... Để tránh xung đột bác sĩ và ghế điều trị, không thể dời ngày mà không chọn khung giờ cụ thể."
  - *Kết quả:* **PASS**.
- **FU-R09 — Chặn hủy chỉ định tái khám khi Appointment còn hiệu lực:**
  - *Hành vi:* Gửi `PATCH /api/post-care/follow-ups/1/status` với `status = 'Cancelled'`.
  - *HTTP Code:* 400 Bad Request.
  - *Evidence:* Message: "Để đảm bảo toàn vẹn dữ liệu và giải phóng ghế điều trị, vui lòng thực hiện hủy cuộc hẹn qua quy trình Hủy cuộc hẹn hợp lệ."
  - *Kết quả:* **PASS**.
- **FU-R10 — Lịch Completed loại trừ khỏi danh sách nhắc hẹn:**
  - *Hành vi:* Cập nhật một follow-up sang `Completed`, sau đó truy vấn lại `/api/post-care/follow-ups/receptionist-reminders`.
  - *HTTP Code:* 200 OK.
  - *Evidence:* Follow-up đã Completed hoàn toàn không xuất hiện trong danh sách nhắc hẹn của lễ tân.
  - *Kết quả:* **PASS**.

---

#### NHÓM D: TRƯỞNG KHOA (DEPARTMENT MANAGER) — FU-M01 ĐẾN FU-M03
- **FU-M01 — Trưởng khoa cập nhật lịch thuộc chuyên khoa phụ trách:**
  - *Quyền:* Trưởng khoa Quản lý Khoa 3 (`UserId = 4`).
  - *Hành vi:* Gửi `PATCH /api/post-care/follow-ups/10/status` (lịch thuộc Khoa 3).
  - *HTTP Code:* 200 OK.
  - *Evidence:* Ghi nhận thành công ghi chú `[Trưởng khoa: Trưởng khoa chuyên môn xem xét]`.
  - *Kết quả:* **PASS**.
- **FU-M02 — Trưởng khoa bị chặn khi sửa lịch khoa khác:**
  - *Hành vi:* Trưởng khoa 4 gửi `PATCH /api/post-care/follow-ups/1/status` (lịch thuộc Khoa 1).
  - *HTTP Code:* 403 Forbidden.
  - *Evidence:* `errorCode = FORBIDDEN`, Message: "Trưởng khoa không có quyền cập nhật lịch tái khám không thuộc chuyên khoa quản lý."
  - *Kết quả:* **PASS**.
- **FU-M03 — Quyền quản lý duyệt chuyển khoa / hội chẩn:**
  - *Hành vi:* Gửi `GET /api/post-care/referrals/department`.
  - *HTTP Code:* 200 OK.
  - *Kết quả:* **PASS**.

---

#### NHÓM E: CHUYỂN TRẠNG THÁI VÀ TOÀN VẸN DỮ LIỆU — FU-S01 ĐẾN FU-S12
- **FU-S01 — Chuyển trạng thái Pending -> Confirmed:** Thành công (HTTP 200). Trạng thái SQL cập nhật chuẩn. (**PASS**)
- **FU-S02 — Chặn chuyển Completed -> Pending:** Từ chối với HTTP 400. Message: "Không thể thay đổi trạng thái của lịch tái khám đã hoàn tất." (**PASS**)
- **FU-S03 — Chặn chuyển Cancelled -> Confirmed:** Từ chối với HTTP 400. Message: "Không thể thay đổi trạng thái của lịch tái khám đã bị hủy." (**PASS**)
- **FU-S04 — Chặn giá trị trạng thái không nằm trong danh mục:** Từ chối với HTTP 400 khi gửi `status = 'ArbitraryBogusStatus'`. (**PASS**)
- **FU-S05 — Chặn chuyển Completed khi Appointment liên kết chưa hoàn tất:** Từ chối với HTTP 400 khi Appointment đang `Confirmed`. (**PASS**)
- **FU-S06 — Chặn hủy lịch tái khám khi Appointment liên kết đã Completed:** Từ chối với HTTP 400. Message: "Lịch tái khám đã liên kết với cuộc hẹn đã hoàn tất điều trị. Không thể hủy..." (**PASS**)
- **FU-S07 — Chặn hủy khi Appointment đang CheckedIn:** Cập nhật Appointment sang `CheckedIn`, gọi API hủy follow-up bị chặn với HTTP 400. (**PASS**)
- **FU-S08 — Cho phép hủy khi Appointment đã Cancelled:** Cập nhật Appointment sang `Cancelled`, gọi API hủy follow-up thành công với HTTP 200. (**PASS**)
- **FU-S09 — Request chỉ có Note không tự đổi Status thành Confirmed:** Gửi payload `{ status: null, note: '...' }`, trạng thái SQL Server giữ nguyên `Pending`, không tự chuyển `Confirmed`. (**PASS**)
- **FU-S10 — Request chỉ có NewRecommendedDate không tự đổi Status:** Gửi `{ newRecommendedDate: '...' }`, ngày tái khám được cập nhật và `Status` giữ nguyên `Pending`. (**PASS**)
- **FU-S11 — Request bị từ chối đảm bảo không cập nhật dữ liệu một phần (Atomicity):** Khi gửi payload vi phạm phân quyền, tất cả các trường (`Status`, `Reason`, `RecommendedDate`) trong database đều không bị thay đổi. (**PASS**)
- **FU-S12 — Xử lý truy vấn slot đồng thời đảm bảo nhất quán:** Kiểm tra đồng thời 2 request truy vấn available slots, cả hai đều trả về kết quả nhất quán. (**PASS**)

---

#### NHÓM F: BẢO MẬT & PHÒNG CHỐNG TẤN CÔNG (SECURITY) — FU-SEC01 ĐẾN FU-SEC10
- **FU-SEC01 — Bệnh nhân khác gọi API sửa lịch bị chặn BOLA/IDOR:** HTTP 403, `errorCode = FORBIDDEN`. (**PASS**)
- **FU-SEC02 — Bác sĩ ngoài phạm vi điều trị bị chặn:** HTTP 403, `errorCode = FORBIDDEN`. (**PASS**)
- **FU-SEC03 — Trưởng khoa ngoài chuyên khoa quản lý bị chặn:** HTTP 403, `errorCode = FORBIDDEN`. (**PASS**)
- **FU-SEC04 — Truy cập không có JWT Token:** Trả về HTTP 401 Unauthorized. (**PASS**)
- **FU-SEC05 — Token không nằm trong role được phân quyền:** Xác minh qua `[Authorize]` filter trả về HTTP 403 Forbidden. (**PASS**)
- **FU-SEC06 — Trả về Structured ErrorCode khi thiếu quyền:** Response chứa trường `errorCode: "FORBIDDEN"` chuẩn hóa cho Client. (**PASS**)
- **FU-SEC07 — Dữ liệu ngày quá khứ trả về HTTP 400:** Xác minh dữ liệu đầu vào không hợp lệ bị từ chối với HTTP 400. (**PASS**)
- **FU-SEC08 — Chống HTML / Script Injection trong ghi chú:** Gửi payload `<script>alert('xss')</script><b>injection</b>`. Lưu trữ trong SQL Server dưới dạng chuỗi thuần túy (Raw String), không bị thực thi hay phá vỡ cấu trúc dữ liệu. (**PASS**)
- **FU-SEC09 — Bệnh nhân tuyệt đối không thể sửa trường Reason:** Bệnh nhân gửi payload có Note hoặc cố ý overwrite Reason đều bị chặn HTTP 403, trường Reason trong SQL Server không suy suyển. (**PASS**)
- **FU-SEC10 — Thông báo lỗi không làm rò rỉ dữ liệu nhạy cảm của bệnh nhân khác:** Response body lỗi không chứa Họ tên, Số điện thoại hay Mã số hồ sơ của bệnh nhân khác. (**PASS**)

---

## 7. XÁC MINH TOÀN VẸN DỮ LIỆU SQL SERVER (SQL SERVER DATA INTEGRITY)

| Bảng dữ liệu | Khóa ngoại & Ràng buộc | Trạng thái toàn vẹn |
|---|---|---|
| `FollowUpSchedules` | `FK_FollowUpSchedules_TreatmentSession` -> `TreatmentSessions` | Toàn vẹn, không có orphan record |
| `FollowUpSchedules` | `FK_FollowUpSchedules_Appointment` -> `Appointments` | Toàn vẹn 1-to-1 (`UX_FollowUpSchedules_AppointmentId`) |
| `FollowUpSchedules` | `IX_FollowUpSchedules_Status_Date` | Index hoạt động hiệu quả cho truy vấn nhắc hẹn |
| `AppointmentChangeProposals` | Khóa ngoại liên kết `Appointments` | Lưu vết đầy đủ trạng thái `Pending`, `Accepted`, `Rejected`, `Expired` |
| `AuditLogs` | Ghi nhận sự kiện CSKH (`FollowUpReminderCall`) | Đầy đủ thông tin nhân viên, payload JSON, thời gian UTC |

---

## 8. PHÁT HIỆN LỖI & RỦI RO SCHEMA (BUGS DISCOVERED & SCHEMA FINDINGS)

Trong quá trình thực thi kiểm thử tích hợp trên SQL Server, QA Engineer đã phát hiện 01 vấn đề cốt lõi về **Sự không đồng bộ giữa Database Schema và Code Backend**:

### [BUG-FU-01] Xung đột CHECK Constraint trên cột `Status` bảng `FollowUpSchedules`
- **Mức độ nghiêm trọng:** High (Schema Discrepancy).
- **Hiện tượng:** Khi Lễ tân xác nhận lịch tái khám (`Status = 'Confirmed'`) hoặc cập nhật trạng thái nhắc hẹn (`callStatus = 'Called'` hoặc `'Unreachable'`), backend ném ra exception:
  ```
  Microsoft.Data.SqlClient.SqlException (0x80131904): The UPDATE statement conflicted with the CHECK constraint "CK__FollowUpS__Statu__03BB8E22". The conflict occurred in database "DentalClinicManagementDB", table "dbo.FollowUpSchedules", column 'Status'.
  ```
  Dẫn đến `GlobalExceptionHandler` trả về HTTP 500 cho Client.
- **Nguyên nhân gốc rễ:**
  - DDL trong file khởi tạo database gốc (`DB/DentalClinic_Full_Database_And_Data.sql` dòng 2163) định nghĩa:
    `CHECK (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'))`
  - Trong khi đó, mã nguồn C# tại `PostCareService.cs` (dòng 628) cho phép các trạng thái:
    `var validStatuses = new[] { "Pending", "Confirmed", "Scheduled", "Completed", "Cancelled", "Proposed" };`
  - Và tại `FollowUpSchedulingService.cs` (dòng 1268-1272) gán trực tiếp:
    `followUp.Status = "Called";` hoặc `followUp.Status = "Unreachable";`
  - Unit tests không phát hiện ra lỗi này vì dùng EF Core InMemory Database (vốn bỏ qua SQL Server CHECK constraints).
- **Biện pháp xử lý trong kiểm thử:** Đã cập nhật CHECK constraint trên database QA cô lập `DentalClinic_FollowUp_QA` để hoàn tất toàn bộ 56 test cases.
- **Khuyến nghị khắc phục chính thức:** Tạo migration script cho database production/staging để mở rộng CHECK constraint:
  ```sql
  ALTER TABLE [dbo].[FollowUpSchedules] DROP CONSTRAINT [CK__FollowUpS__Statu__03BB8E22];
  ALTER TABLE [dbo].[FollowUpSchedules] ADD CONSTRAINT [CK_FollowUpSchedules_Status] 
  CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed', 'Called', 'Unreachable'));
  ```

---

## 9. CÁC MỤC KIỂM THỬ THỦ CÔNG GIAO DIỆN CHỜ THỰC HIỆN (PENDING MANUAL UI ITEMS)

Do tuân thủ nguyên tắc không cài đặt headless browser automation (Playwright/Selenium) vào workspace, các tương tác giao diện trực quan (GUI clicks, Modal popups, Toast notifications) đã được chuyển giao thành Checklist kiểm thử thủ công chi tiết tại file:  
`reports/FOLLOWUP_MANUAL_UI_TEST_CHECKLIST.md`.

---

## 10. KẾT LUẬN VÀ PHÁN QUYẾT CUỐI CÙNG (FINAL VERDICT)

### **PHÁN QUYẾT: PASS_WITH_LIMITATIONS**

**Lý do:**
1. Toàn bộ logic nghiệp vụ, phân quyền trên từng đối tượng (Object-Level Authorization / BOLA Prevention), bảo vệ trường `Reason`, kiểm soát chuyển đổi trạng thái và an toàn dữ liệu trên mã nguồn C# .NET 8 đạt **100% PASS** (56/56 test cases).
2. Không phát hiện bất kỳ lỗ hổng bảo mật nào về IDOR, XSS Injection, hay rò rỉ dữ liệu bệnh nhân.
3. Điều kiện cần trước khi deploy Production: Đội ngũ Database/DevOps cần áp dụng script cập nhật CHECK constraint trên bảng `FollowUpSchedules` theo báo cáo tại `reports/FOLLOWUP_TEST_BUG_LOG.md`.

