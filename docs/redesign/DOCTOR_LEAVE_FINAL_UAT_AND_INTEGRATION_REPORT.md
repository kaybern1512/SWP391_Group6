# DENTALCARE – BÁO CÁO NGHIỆM THU CUỐI CÙNG (FINAL UAT) VÀ TÍCH HỢP AN TOÀN VÀO DEVELOP
## PHÂN HỆ DOCTOR LEAVE REQUEST & APPOINTMENT DISRUPTION RECOVERY
### DENTALCARE 4-ROLE CLINIC SYSTEM

---

- **Dự án**: DentalCare - Hệ Thống Quản Lý Phòng Khám Nha Khoa Đa Phân Hệ (4 Roles)
- **Tính năng**: Doctor Leave Request & Appointment Disruption Recovery System
- **Feature Branch**: `feature/dentist-leave-appointment-recovery`
- **Feature Commit SHA**: `01dc5cd` (bổ sung resource invariants, DI registrations, index filtering)
- **Develop SHA trước khi merge**: `5d78135` (đã bao gồm phân hệ `Receptionist Workforce Management` tại commit `167a810`)
- **Integration Branch**: `integration/doctor-leave-to-develop`
- **Merge SHA vào Develop**: `6f1b32a`
- **Database Kiểm thử QA**: `DentalCare_Leave_Dev` trên SQL Server `.\SQLEXPRESS`
- **Ngày hoàn thành**: 10/11/2026
- **Trạng thái**: **COMPLETED & SAFELY MERGED TO DEVELOP**

---

## 1. TỔNG HỢP KẾT QUẢ TÍCH HỢP & COMMIT TRACELOG

| Thông tin | Commit SHA / Giá trị | Mô tả |
| :--- | :--- | :--- |
| **Baseline Develop ban đầu** | `d4e6cfc` | Nhánh cơ sở khi bắt đầu UAT |
| **Feature Head SHA** | `01dc5cd` | Hoàn thiện UAT, Resource Allocation & DI |
| **Develop Head trước merge** | `5d78135` | Đã tích hợp nhánh Receptionist Workforce |
| **Develop Head sau merge** | `6f1b32a` | Merge thành công Doctor Leave vào `origin/develop` |
| **Working Tree** | `D:\SWP391\DentalCare-Leave-Integration` | Sạch hoàn toàn, 0 conflict |
| **Database chính & Main branch** | `DentalClinicManagementDB` / `main` | **Tuyệt đối an toàn, không bị can thiệp** |
| **Các nhánh độc lập khác** | Patient Redesign, Admin Attendance | **Không bị can thiệp hay merge đè** |

---

## 2. CÁC LỖI VÀ RỦI RO ĐÃ PHÁT HIỆN VÀ SỬA CHỮA (EXHAUSTIVE AUDIT)

Trong quá trình UAT chuyên sâu và Pre-Merge Audit, các vấn đề kỹ thuật sau đã được phát hiện và khắc phục triệt để:

### 2.1. Lỗi Thiếu Dependency Injection Trong Frontend (`FE/DentalClinic.Web/Program.cs`)
- **Triệu chứng**: Khi điều hướng tới `/Receptionist/Appointments/Disruptions`, ứng dụng trả về mã lỗi HTTP 500 (`InvalidOperationException: Unable to resolve service for type 'IAppointmentDisruptionApiService'`).
- **Nguyên nhân**: Hai service `IDentistLeaveApiService` và `IAppointmentDisruptionApiService` đã được viết và gọi trong MVC Controller nhưng chưa được đăng ký trong pipeline `AddHttpClient` của `Program.cs`.
- **Khắc phục**: Đã bổ sung cấu hình `AddHttpClient<IDentistLeaveApiService, DentistLeaveApiService>` và `AddHttpClient<IAppointmentDisruptionApiService, AppointmentDisruptionApiService>` kèm `ApiAuthorizationHandler` và timeout an toàn 30s.

### 2.2. Index Filtered Cho ActionToken Đã Vô Hiệu Hóa
- **Triệu chứng**: Ràng buộc Unique trên `ActionToken` nếu không có bộ lọc `WHERE [ActionToken] IS NOT NULL` sẽ khiến thao tác vô hiệu hóa token (`ActionToken = null`) chỉ cho phép tối đa 1 bản ghi mang giá trị `NULL`.
- **Khắc phục**: 
  * Cập nhật migration SQL: thay thế `IX_AppointmentDisruptions_ActionToken` bằng `UQ_AppointmentDisruptions_ActionToken` (`CREATE UNIQUE NONCLUSTERED INDEX ... WHERE [ActionToken] IS NOT NULL`).
  * Cập nhật EF Core Fluent API trong `DentalClinicDbContext.cs`: `.HasIndex(e => e.ActionToken, "UQ_AppointmentDisruptions_ActionToken").IsUnique().HasFilter("[ActionToken] IS NOT NULL")`.
  * Đã thực thi script nâng cấp schema trên database `DentalCare_Leave_Dev`.

### 2.3. Bổ Sung Ràng Buộc Phân Bổ Tài Nguyên Bắt Buộc (Room & DentalChair Invariants)
- **Yêu cầu**: Cuộc hẹn trạng thái `Confirmed` khi đổi bác sĩ hoặc dời lịch không được để trống tài nguyên nếu phòng khám có cấu hình phòng và ghế; nếu không đủ tài nguyên phải chuyển sang quy trình lễ tân hỗ trợ thay vì báo đã hoàn tất.
- **Khắc phục**:
  * Khi bệnh nhân chọn đổi bác sĩ (`PatientChooseNewDentistAsync`): hệ thống kiểm tra các ghế nha khoa hoạt động (`Active`) trong chuyên khoa chưa bị chiếm dụng trong khung giờ `[OriginalScheduledStart, OriginalScheduledEnd]`. Nếu tìm thấy ghế trống, tự động gán `appt.DentalChairId` và `appt.RoomId`. Nếu toàn bộ ghế đều bận, chuyển trạng thái sang `NeedsStaffAction`, ghi nhận `StaffNotes` và thông báo bệnh nhân chờ lễ tân sắp xếp.
  * Khi bệnh nhân dời lịch (`PatientRescheduleDisruptionAsync`): kiểm tra tương tự cho khung giờ mới. Nếu không có phòng/ghế trống, chuyển `NeedsStaffAction` để lễ tân điều phối.
  * Reset `QueueNumber = null`: số thứ tự chỉ được cấp vào ngày khám khi bệnh nhân Check-in thực tế.

### 2.4. Trình Kích Hoạt Giao Diện & Điều Hướng
- **Nút "Xin Nghỉ Phép" & "Đơn Nghỉ Của Tôi" trên Lịch Bác sĩ (`/Dentist/Schedule`)**: Modal đã tồn tại nhưng thanh công cụ đầu trang thiếu 2 nút bấm mở modal. Đã bổ sung 2 nút semantic buttons trên header view.
- **Tab thứ 5 "Duyệt Đơn Nghỉ Phép" trên Lịch Admin (`/Admin/DentistSchedule?tab=leave-requests`)**: Thêm tab thứ 5 vào thanh điều hướng và nhúng partial view `_LeaveRequestsTab.cshtml` để Admin thao tác duyệt/từ chối và xem Impact Analysis.

### 2.5. Tương Thích Dữ Liệu Kiểm Thử Playwright
- Chuẩn hóa email tài khoản trong kịch bản E2E sang dữ liệu thực tế của hệ thống: `dentist@dentalcare.com`, `receptionist@dentalcare.com`, `admin@dentalcare.com`, `patient1@dentalcare.com`.

---

## 3. KẾT QUẢ KIỂM THỬ TRÌNH DUYỆT THẬT (PLAYWRIGHT REAL BROWSER E2E)

Hệ thống Backend (HTTPS 7350) và Frontend (HTTPS 7129) đã được khởi chạy thực tế trên cơ sở dữ liệu `DentalCare_Leave_Dev`. Bộ kiểm thử E2E Playwright thực thi hoàn chỉnh trên trình duyệt Chromium thực tế:

```text
Running 6 tests using 1 worker

  ok 1 [chromium] › doctor-leave-recovery.spec.js:19:3 › Doctor Leave Request & Appointment Disruption Recovery Flow › E2E-LEAVE-01: Dentist submits leave request and views status on personal schedule (3.0s)
  ok 2 [chromium] › doctor-leave-recovery.spec.js:53:3 › Doctor Leave Request & Appointment Disruption Recovery Flow › E2E-LEAVE-02: Admin navigates to leave requests tab and views impact analysis modal (2.4s)
  ok 3 [chromium] › doctor-leave-recovery.spec.js:87:3 › Doctor Leave Request & Appointment Disruption Recovery Flow › E2E-LEAVE-03: Flow A - Patient selects alternative dentist and verifies schedule update (1.3s)
  ok 4 [chromium] › doctor-leave-recovery.spec.js:123:3 › Doctor Leave Request & Appointment Disruption Recovery Flow › E2E-LEAVE-04: Flow B - Patient reschedules disrupted appointment to new date/time slot (1.3s)
  ok 5 [chromium] › doctor-leave-recovery.spec.js:153:3 › Doctor Leave Request & Appointment Disruption Recovery Flow › E2E-LEAVE-05: Concurrency & Security - Action token invalidation & IDOR protection (1.2s)
  ok 6 [chromium] › doctor-leave-recovery.spec.js:167:3 › Doctor Leave Request & Appointment Disruption Recovery Flow › E2E-LEAVE-06: Receptionist navigates to disruption coordination and verifies controls (999ms)

  6 passed (11.3s)
```

Đồng thời, bộ hồi quy 4 vai trò (`login-auth-flow.spec.js` và `four-role-full-flow.spec.js`) cũng vượt qua 100%:
```text
Running 12 tests using 2 workers
  12 passed (21.9s)
```
**Tổng số E2E tests đạt: 18 / 18 tests passed (100%).**

---

## 4. KẾT QUẢ KIỂM THỬ TEST SUITES (BACKEND & FRONTEND)

Sau khi merge `origin/develop` (chứa toàn bộ Receptionist Workforce) vào `integration/doctor-leave-to-develop`:

### 4.1. Backend Test Suite (`BE/DentalClinic.Tests`)
```text
Passed!  - Failed: 0, Passed: 829, Skipped: 1, Total: 830, Duration: 18 s - DentalClinic.Tests.dll (net8.0)
```
- **829 tests PASS, 0 FAIL** (1 test skip Cloudinary do yêu cầu external UserSecrets).
- Đã bao gồm toàn bộ 21 test cases chuyên sâu trong `DoctorLeaveAndDisruptionRecoveryTests.cs`, 6 test cases `RealDatabaseEndToEndSmokeTests`, và toàn bộ test cases của phân hệ `Receptionist Workforce`.

### 4.2. Frontend Test Suite (`FE/DentalClinic.Web.Tests`)
```text
Passed!  - Failed: 0, Passed: 315, Skipped: 0, Total: 315, Duration: 20 s - DentalClinic.Web.Tests.dll (net8.0)
```
- **315 tests PASS, 0 FAIL**.

**Tổng cộng toàn giải pháp sau tích hợp**: **1,144 Unit & Integration Tests PASS (100%)**.

---

## 5. TÌNH TRẠNG TÍCH HỢP VỚI RECEPTIONIST WORKFORCE

1. **Database Schema Harmonization**:
   - Schema của Doctor Leave (`DentistLeaveRequests`, `AppointmentDisruptions`) và Schema của Receptionist Workforce (`ReceptionistWorkSchedules`, `ReceptionistLeaveRequests`, `ReceptionistShiftSwapRequests`, `ReceptionistCoverageIncidents`, `ReceptionistWorkHandovers`) hoạt động độc lập, không xung đột khóa ngoại hay định danh.
2. **Khóa Ứng Dụng Phân Tán (`SqlAppLockHelper`)**:
   - Doctor Leave sử dụng các key: `DENTIST_MASTER_{id}`, `APPT_{id}`, `DISRUPTION_EMAIL_WORKER_LOCK`.
   - Receptionist Workforce sử dụng các key: `REC_WORKFORCE_COVERAGE_LOCK`, `REC_SWAP_{id}`.
   - Không bị chồng lấn tài nguyên khóa, đảm bảo concurrency tối đa.
3. **Giao Diện Lễ Tân**:
   - Menu Lễ tân (`_Sidebar.cshtml`) hiển thị đầy đủ cả mục điều phối gián đoạn lịch hẹn (`/Receptionist/Appointments/Disruptions`) và các chức năng ca trực lễ tân (Lịch trực, Đổi ca, Xin nghỉ, Bàn giao ca).

---

## 6. NHỮNG HẠN CHẾ & LƯU Ý VẬN HÀNH

1. **Cấu Hình Khóa Phân Tán (Production Clusters)**:
   - Khi triển khai trên môi trường Multi-Instance / Cloud, bắt buộc cơ sở dữ liệu SQL Server đích phải hỗ trợ thủ tục lưu trữ `sp_getapplock` (SQL Server Standard / Enterprise / Azure SQL Database).
2. **Quy Trình Hoàn Tiền Cọc Cho Lịch Bị Hủy**:
   - Khi ca khám bị hủy do bác sĩ nghỉ phép (`Cancelled`), hệ thống không tự động đánh dấu đã hoàn tiền (`DepositRefunded`). Nhân viên tài chính/kế toán cần thực hiện đối soát và xuất biên lai hoàn tiền qua cổng thanh toán/VietQR theo quy định tài chính.
3. **Xác Thực Email Bằng ActionToken**:
   - Token liên kết trong email gửi đến bệnh nhân có thời hạn hiệu lực tối đa 7 ngày hoặc trước giờ khám, và tự động vô hiệu hóa sau một lần sử dụng thành công nhằm ngăn chặn replay attacks.

---

## 7. KẾT LUẬN & XÁC NHẬN AN TOÀN

- Phân hệ **Doctor Leave Request & Appointment Disruption Recovery** đã hoàn tất UAT toàn diện và được tích hợp thành công vào nhánh `develop` tại commit **`6f1b32a`**.
- Toàn bộ 4 vai trò (Dentist, Admin, Patient, Receptionist) hoạt động mượt mà, đúng đặc tả nghiệp vụ, bảo toàn tài nguyên phòng ghế, ngăn chặn IDOR và concurrency race condition.
- Nhánh `main`, database chính `DentalClinicManagementDB`, và các nhánh tính năng khác (Patient Redesign, Admin Attendance) được bảo vệ an toàn 100%.
