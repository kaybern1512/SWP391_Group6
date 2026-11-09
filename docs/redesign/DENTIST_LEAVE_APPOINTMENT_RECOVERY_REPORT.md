# DENTALCARE – BÁO CÁO TRIỂN KHAI HỆ THỐNG XIN NGHỈ PHÉP BÁC SĨ & PHỤC HỒI GIÁN ĐOẠN LỊCH HẸN KHÁM
## DOCTOR LEAVE REQUEST & APPOINTMENT DISRUPTION RECOVERY SYSTEM REPORT

---

- **Dự án**: DentalCare - Hệ Thống Quản Lý Phòng Khám Nha Khoa
- **Phân hệ**: Doctor Leave Request & Appointment Disruption Recovery (4 Roles: Dentist, Admin, Patient, Receptionist)
- **Feature Branch**: `feature/dentist-leave-appointment-recovery`
- **Baseline Develop Commit**: `d4e6cfc` (origin/develop)
- **Worktree**: `D:\SWP391\DentalCare-Leave`
- **Database Môi Trường**: `DentalCare_Leave_Dev` (SQL Server `.\SQLEXPRESS`)
- **Ngày hoàn thành**: 09/11/2026
- **Trạng thái**: Hoàn thành toàn diện, 100% tests vượt qua (1108/1108 unit/integration tests).

---

## 1. TỔNG QUAN KIẾN TRÚC & PHẠM VI

Tính năng **Doctor Leave Request & Appointment Disruption Recovery** giải quyết bài toán nghiệp vụ trọng yếu trong vận hành phòng khám nha khoa:
1. **Bác sĩ (Dentist)** xin nghỉ phép có quy trình xét duyệt (Pending -> Approved / Rejected / Withdrawn), bảo mật lý do nội bộ, quản lý ngày nghỉ trực tiếp trên lịch tháng.
2. **Quản trị viên (SystemAdministrator)** xem trước phân tích ảnh hưởng (`Impact Analysis`) trước khi phê duyệt: đếm số lịch hẹn bị ảnh hưởng, phát hiện các ca khẩn cấp trong 24 giờ, và tự động tìm bác sĩ thay thế khả dụng cùng chuyên khoa.
3. **Phê duyệt tự động hóa**: Khi Admin duyệt đơn:
   - Hệ thống tự động ghi nhận trạng thái bận/nghỉ (`DentistAvailability` với status `OnLeave`).
   - Tự động hủy các ca trực bị trùng (`DentistWorkSchedules`).
   - Tự động sinh bản ghi sự cố gián đoạn (`AppointmentDisruptions`) với mã bảo mật `ActionToken` cho từng cuộc hẹn bị ảnh hưởng (`Confirmed`, `CheckedIn`, `Pending`).
   - Gửi email thông báo tự động cho bệnh nhân kèm đường dẫn bảo mật để xử lý.
4. **Bệnh nhân (Patient)** nhận thông báo và chủ động chọn 1 trong 2 phương án khôi phục:
   - **Phương án 1 (Đổi bác sĩ)**: Giữ nguyên ngày giờ khám đã đặt, chuyển sang Bác sĩ khác cùng chuyên khoa không có lịch trùng.
   - **Phương án 2 (Dời lịch)**: Giữ nguyên Bác sĩ phụ trách, dời sang ngày giờ mới sau khi bác sĩ kết thúc thời gian nghỉ phép.
   - Bảo toàn 100% tiền cọc, số thứ tự, và hồ sơ điều trị liên kết.
5. **Lễ tân (Receptionist)** điều phối các ca khẩn cấp hoặc hỗ trợ bệnh nhân qua điện thoại:
   - Dashboard theo dõi riêng các ca gián đoạn với nhãn cảnh báo khẩn cấp (`IsUrgent`).
   - Modal điều phối cho phép đổi bác sĩ, dời lịch, xác nhận xử lý thủ công, hoặc hủy lịch hoàn cọc an toàn.

---

## 2. BASELINE GIT & DATABASE CÁCH LY

- **Workspace an toàn**: Worktree độc lập `D:\SWP391\DentalCare-Leave` được tách từ HEAD mới nhất của `origin/develop` (`d4e6cfc`), đảm bảo không can thiệp vào workspace `SWP391_Group6` hay `DentalCare-Integration`.
- **Database cách ly**: Tạo database `DentalCare_Leave_Dev` bằng backup/restore từ snapshot sạch `DentalCare_Integration_Dev`.
- **Tuyệt đối an toàn**: Không can thiệp `DentalClinicManagementDB` gốc, không ảnh hưởng database `DentalCare_Integration_Dev` đang phục vụ các phân hệ khác.
- **Cấu hình ConnectionString**: `Server=.\\SQLEXPRESS;Database=DentalCare_Leave_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=false`.

---

## 3. MÔ HÌNH DATABASE & MIGRATION

Hệ thống bổ sung 2 bảng chuyên dụng quản lý quy trình xét duyệt và khắc phục gián đoạn:

### 3.1. Bảng `DentistLeaveRequests`
Lưu trữ thông tin đơn xin nghỉ phép của bác sĩ:
- `LeaveRequestId` (BIGINT, PK, IDENTITY)
- `DentistUserId` (BIGINT, FK -> `DentistProfiles(UserId)`)
- `LeaveType` (NVARCHAR(50)): `AnnualLeave`, `SickLeave`, `Personal`, `Emergency`, `Other`
- `StartDate` (DATE, NOT NULL)
- `EndDate` (DATE, NOT NULL)
- `StartTime` (TIME, NULL)
- `EndTime` (TIME, NULL)
- `StartDateTime` (DATETIME2, NOT NULL)
- `EndDateTime` (DATETIME2, NOT NULL)
- `Reason` (NVARCHAR(500), NOT NULL - Bảo mật nội bộ)
- `Notes` (NVARCHAR(500), NULL)
- `Status` (NVARCHAR(30), NOT NULL): `Pending`, `Approved`, `Rejected`, `Withdrawn`
- `ReviewedByUserId` (BIGINT, NULL, FK -> `UserAccounts(UserId)`)
- `ReviewedAt` (DATETIME2, NULL)
- `RejectionReason` (NVARCHAR(500), NULL)
- `CreatedAt`, `UpdatedAt` (DATETIME2, NOT NULL)

### 3.2. Bảng `AppointmentDisruptions`
Lưu trữ và điều phối trạng thái khôi phục lịch hẹn:
- `DisruptionId` (BIGINT, PK, IDENTITY)
- `AppointmentId` (BIGINT, NOT NULL, FK -> `Appointments(AppointmentId)`)
- `DentistLeaveRequestId` (BIGINT, NOT NULL, FK -> `DentistLeaveRequests(LeaveRequestId)`)
- `OriginalDentistUserId` (BIGINT, NOT NULL, FK -> `DentistProfiles(UserId)`)
- `OriginalScheduledStart`, `OriginalScheduledEnd` (DATETIME2, NOT NULL)
- `Status` (NVARCHAR(50), NOT NULL): `PendingPatientChoice`, `DoctorChanged`, `Rescheduled`, `ResolvedByStaff`, `Cancelled`
- `PatientChoice` (NVARCHAR(50), NULL): `ChooseNewDentist`, `Reschedule`, `StaffIntervention`
- `ResolvedAppointmentId` (BIGINT, NULL, FK -> `Appointments(AppointmentId)`)
- `NewDentistUserId` (BIGINT, NULL, FK -> `DentistProfiles(UserId)`)
- `ResolvedByUserId` (BIGINT, NULL, FK -> `UserAccounts(UserId)`)
- `ResolvedAt` (DATETIME2, NULL)
- `StaffNotes` (NVARCHAR(1000), NULL)
- `EmailSentAt` (DATETIME2, NULL)
- `EmailDeliveryStatus` (NVARCHAR(50), NOT NULL): `Pending`, `Sent`, `Failed`, `Skipped`
- `ActionToken` (NVARCHAR(100), NOT NULL, UNIQUE)
- `IsUrgent` (BIT, NOT NULL)
- `CreatedAt`, `UpdatedAt` (DATETIME2, NOT NULL)

### 3.3. Tệp Quản Lý Migration & Rollback
- Forward migration: `DB/redesign_migrations/20261109_Add_DoctorLeave_And_AppointmentDisruption.sql`
- Rollback script: `DB/redesign_migrations/20261109_Rollback_DoctorLeave_And_AppointmentDisruption.sql`
- Cả hai script đã được kiểm thử rollback và apply hoàn toàn thành công trên database.

---

## 4. DOMAIN MODELS & ENUMS

Khai báo các domain enum chuẩn hóa trong `DentalClinic.Domain.Enums`:
- `DentistLeaveType`: `AnnualLeave`, `SickLeave`, `Personal`, `Emergency`, `Other`
- `DentistLeaveStatus`: `Pending`, `Approved`, `Rejected`, `Withdrawn`
- `AppointmentDisruptionStatus`: `PendingPatientChoice`, `DoctorChanged`, `Rescheduled`, `ResolvedByStaff`, `Cancelled`
- `DisruptionPatientChoice`: `ChooseNewDentist`, `Reschedule`, `StaffIntervention`
- `DisruptionEmailStatus`: `Pending`, `Sent`, `Failed`, `Skipped`

---

## 5. BACKEND APPLICATION SERVICES & DTOS

### 5.1. DTOs (`DentalClinic.Application.Features.DentistLeave.DTOs`)
- `CreateDentistLeaveRequest`, `DentistLeaveRequestDto`, `DentistLeaveFilterRequest`
- `DentistLeaveImpactAnalysisDto`, `LeaveAffectedAppointmentDto`, `AlternativeDentistDto`
- `ApproveDentistLeaveRequest`, `RejectDentistLeaveRequest`
- `AppointmentDisruptionDto`, `PatientChooseNewDentistRequest`, `PatientDisruptionRescheduleRequest`, `ReceptionistResolveDisruptionRequest`

### 5.2. `DentistLeaveService` (`IDentistLeaveService`)
- `SubmitLeaveRequestAsync`: Validate thời gian bắt đầu < kết thúc, lưu đơn với trạng thái `Pending`.
- `WithdrawLeaveRequestAsync`: Bác sĩ chỉ có quyền rút đơn khi ở trạng thái `Pending`.
- `GetLeaveImpactAnalysisAsync`: Quét toàn bộ cuộc hẹn trong khoảng nghỉ của bác sĩ, đếm số ca Confirmed/CheckedIn/Pending, tính toán độ khẩn cấp (trong vòng 24 giờ), và tìm danh sách bác sĩ thay thế cùng chuyên khoa không trùng lịch.
- `ApproveLeaveRequestAsync`: Sử dụng transaction + khóa phân tán, tự động tạo `DentistAvailability(OnLeave)`, hủy ca trực làm việc bị trùng, tạo bản ghi `AppointmentDisruptions` và phát hành thông báo.
- `RejectLeaveRequestAsync`: Ghi nhận lý do từ chối của Admin, thông báo cho Bác sĩ, không tạo gián đoạn.

### 5.3. `AppointmentDisruptionService` (`IAppointmentDisruptionService`)
- `GetPendingDisruptionsForPatientAsync`: Bệnh nhân xem các gián đoạn cần xử lý.
- `GetAvailableAlternativeDentistsAsync`: Đề xuất bác sĩ thay thế cùng chuyên khoa, kiểm tra lịch bận/nghỉ và lịch khám trùng.
- `PatientChooseNewDentistAsync`: Bệnh nhân chọn bác sĩ thay thế -> cập nhật `AssignedDentistUserId`, giữ nguyên giờ hẹn, cập nhật disruption thành `DoctorChanged`, lên lại lịch nhắc hẹn tự động.
- `PatientRescheduleDisruptionAsync`: Bệnh nhân dời lịch -> kiểm tra khung giờ mới với bác sĩ phụ trách, cập nhật giờ hẹn mới an toàn, cập nhật disruption thành `Rescheduled`.
- `ReceptionistResolveDisruptionAsync`: Lễ tân can thiệp và ghi biên bản qua 4 phương án (`ChangeDoctor`, `Reschedule`, `ManualStaffResolved`, `Cancel`).

---

## 6. CHIẾN LƯỢC CONCURRENCY & KHÓA PHÂN TÁN (SQL APPLOCK)

Để triệt tiêu triệt để race conditions giữa:
1. Admin phê duyệt đơn nghỉ phép.
2. Bệnh nhân khác đang đặt lịch hẹn mới vào ca trực đó.
3. Bệnh nhân bị gián đoạn đang chọn cùng một bác sĩ thay thế.

Hệ thống sử dụng helper `SqlAppLockHelper` tích hợp sẵn:
- Khóa tài nguyên bác sĩ: `SqlAppLockHelper.DentistMasterKey(dentistUserId)`
- Khóa tài nguyên cuộc hẹn: `SqlAppLockHelper.ApptKey(appointmentId)`
- Khóa thực thi có timeout an toàn (15,000 ms), tự động giải phóng khi hoàn tất scope hoặc rollback transaction.

---

## 7. EMAIL SERVICE & HỆ THỐNG THÔNG BÁO

### 7.1. Email Templates (`IEmailService`)
- `SendDoctorLeaveDisruptionEmailAsync`: Gửi email đến bệnh nhân thông báo bác sĩ có lịch nghỉ phép đột xuất, cung cấp nút liên kết trực tiếp kèm `actionToken` tới trang tự điều phối khôi phục lịch khám.
- `SendDoctorLeaveRecoveryConfirmationEmailAsync`: Gửi email xác nhận sau khi lịch hẹn đã được khôi phục thành công (với bác sĩ mới hoặc thời gian mới).
- **Nguyên tắc bảo mật**: Tuyệt đối không hiển thị lý do riêng tư của bác sĩ trong email bệnh nhân.

### 7.2. Notification Subsystem
- Gửi thông báo Notification nội bộ cho Bác sĩ khi đơn được duyệt hoặc bị từ chối kèm lý do.
- Gửi thông báo cho Bác sĩ mới được phân công tiếp nhận cuộc hẹn.

---

## 8. BACKGROUND WORKER (WF-LEAVERECOVERY)

Triển khai background service `AppointmentDisruptionBackgroundWorker` (IHostedService):
- Quét định kỳ mỗi 5 phút các bản ghi `AppointmentDisruptions` có trạng thái `EmailDeliveryStatus = "Pending"`.
- Thực hiện gửi lại email cho các ca bị gián đoạn nếu lần gửi ban đầu gặp lỗi mạng tạm thời.
- Tự động nâng cờ khẩn cấp (`IsUrgent = true`) cho các cuộc hẹn có thời gian bắt đầu trong vòng 24 giờ mà bệnh nhân chưa phản hồi để Lễ tân chủ động gọi điện can thiệp.

---

## 9. API ENDPOINTS & AUTHORIZATION MATRIX

### 9.1. Dentist Leave Controller (`/api/v1/DentistLeave`)
- `POST /api/v1/DentistLeave/Submit`: `[Authorize(Roles = "Dentist")]`
- `GET /api/v1/DentistLeave/My`: `[Authorize(Roles = "Dentist")]`
- `POST /api/v1/DentistLeave/{id}/Withdraw`: `[Authorize(Roles = "Dentist")]`
- `GET /api/v1/DentistLeave`: `[Authorize(Roles = "SystemAdministrator")]`
- `GET /api/v1/DentistLeave/{id}`: `[Authorize(Roles = "SystemAdministrator,Dentist")]`
- `GET /api/v1/DentistLeave/{id}/Impact`: `[Authorize(Roles = "SystemAdministrator")]`
- `POST /api/v1/DentistLeave/{id}/Approve`: `[Authorize(Roles = "SystemAdministrator")]`
- `POST /api/v1/DentistLeave/{id}/Reject`: `[Authorize(Roles = "SystemAdministrator")]`

### 9.2. Appointment Disruption Controller (`/api/v1/AppointmentDisruptions`)
- `GET /api/v1/AppointmentDisruptions/My`: `[Authorize(Roles = "Patient")]`
- `GET /api/v1/AppointmentDisruptions/{id}`: `[Authorize(Roles = "Patient,Receptionist,SystemAdministrator")]`
- `GET /api/v1/AppointmentDisruptions/ByAppointment/{appointmentId}`: `[Authorize]`
- `GET /api/v1/AppointmentDisruptions/{id}/AlternativeDentists`: `[Authorize(Roles = "Patient,Receptionist,SystemAdministrator")]`
- `POST /api/v1/AppointmentDisruptions/{id}/ChooseDentist`: `[Authorize(Roles = "Patient")]`
- `POST /api/v1/AppointmentDisruptions/{id}/Reschedule`: `[Authorize(Roles = "Patient")]`
- `GET /api/v1/AppointmentDisruptions/Coordinator`: `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
- `POST /api/v1/AppointmentDisruptions/{id}/StaffResolve`: `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
- `POST /api/v1/AppointmentDisruptions/ProcessEmails`: `[Authorize(Roles = "SystemAdministrator,Receptionist")]`

---

## 10. GIAO DIỆN 4 ROLES ĐÃ HOÀN THIỆN

### 10.1. Role 1 – Dentist (`/Dentist/Schedule`)
- Header tích hợp 2 nút hành động: "Xin Nghỉ Phép" (Modal `#createLeaveModal`) và "Đơn Nghỉ Của Tôi" (Modal `#myLeavesModal` kèm badge số đơn chờ duyệt).
- Calendar Grid hiển thị badge `.leave-pill` màu cam/amber tại các ô ngày bác sĩ có đơn nghỉ phép đã duyệt hoặc đang chờ duyệt.
- Modal `#createLeaveModal` hỗ trợ chọn loại nghỉ phép, ngày bắt đầu/kết thúc, giờ bắt đầu/kết thúc, lý do bắt buộc và ghi chú bàn giao.
- Modal `#myLeavesModal` hiển thị bảng lịch sử đơn nghỉ, trạng thái duyệt, lý do từ chối nếu có, và nút "Rút đơn" trực tiếp cho đơn đang Pending.

### 10.2. Role 2 – System Administrator (`/Admin/DentistSchedule`)
- Bổ sung Tab thứ 5: "Duyệt Đơn Nghỉ Phép" (`tab=leave-requests`) kèm badge số lượng đơn Pending cần xử lý.
- Thẻ thống kê tổng quan: Đơn chờ xét duyệt, Lịch hẹn bị ảnh hưởng, Đơn đã phê duyệt, Đơn đã từ chối.
- Bảng danh sách đơn xin nghỉ kèm thông tin bác sĩ, chuyên khoa, khoảng thời gian, lý do nội bộ và số lượng lịch hẹn bị ảnh hưởng.
- Modal Phân Tích Ảnh Hưởng (`#impactAnalysisModal`): Tải dữ liệu AJAX tức thì, hiển thị danh sách các cuộc hẹn bị ảnh hưởng, thông tin bệnh nhân, mức độ khẩn cấp, danh sách bác sĩ thay thế rảnh cùng khoa, và nút "Phê Duyệt & Kích Hoạt Khắc Phục".
- Modal Từ Chối (`#rejectLeaveModal`): Bắt buộc nhập lý do từ chối để phản hồi cho bác sĩ.

### 10.3. Role 3 – Patient (`/Patient/Appointment` & `/Patient/Appointment/Disruption/{id}`)
- Banner cảnh báo trên trang chi tiết cuộc hẹn (`Detail.cshtml`): Hiển thị cảnh báo lịch khám bị gián đoạn do bác sĩ nghỉ phép kèm nút "Xử Lý Ngay".
- Giao diện điều phối riêng biệt (`DisruptionDetail.cshtml`):
  * Banner thông báo lịch thiệp, giải thích rõ nguyên nhân bác sĩ nghỉ đột xuất và cam kết bảo lưu quyền lợi/cọc 100%.
  * **Phương án 1 (Giữ nguyên giờ - Đổi bác sĩ)**: Hiển thị thẻ danh sách bác sĩ cùng chuyên khoa đang rảnh trong khung giờ đó kèm nút "Chọn Bác Sĩ Này".
  * **Phương án 2 (Giữ bác sĩ - Dời lịch)**: Biểu mẫu chọn thời gian khám mới sau thời gian bác sĩ nghỉ phép kèm nút xác nhận dời lịch.
  * Thẻ liên hệ hotline hỗ trợ trực tiếp từ quầy lễ tân.

### 10.4. Role 4 – Receptionist (`/Receptionist/Appointments/Disruptions`)
- Menu điều hướng bổ sung tab "Điều Phối BS Nghỉ Phép" (kèm badge ca khẩn cấp màu đỏ).
- Bảng danh sách gián đoạn hiển thị số điện thoại (có nút bấm gọi điện trực tiếp `tel:...`), trạng thái phản hồi của bệnh nhân, và độ khẩn cấp.
- Modal "Lễ Tân Điều Phối Lịch Hẹn" (`#resolveDisruptionModal`): Hỗ trợ lễ tân chốt phương án với bệnh nhân qua 4 lựa chọn (Đổi bác sĩ, Dời lịch, Đã thỏa thuận thủ công, Hủy lịch hoàn cọc) và bắt buộc nhập biên bản xử lý.

---

## 11. BẢO VỆ TIỀN CỌC & LIÊN KẾT HỒ SƠ

- **Bảo toàn Invoices & Payments**: Cuộc hẹn bị dời hoặc đổi bác sĩ vẫn giữ nguyên `AppointmentId` hoặc chuyển liên kết hóa đơn sang cuộc hẹn mới nếu tái lập.
- **Bảo toàn Follow-Up**: Liên kết phác đồ điều trị (`TreatmentPlanItem`), lịch tái khám (`FollowUpSchedule`) được bảo toàn tuyệt đối, không làm gián đoạn lộ trình điều trị của bệnh nhân.
- **Token Security**: Mã `ActionToken` được mã hóa UUID ngẫu nhiên duy nhất cho mỗi sự cố, cho phép bệnh nhân truy cập nhanh mà không bị can thiệp trái phép.

---

## 12. KẾT QUẢ KIỂM THỬ TOÀN DIỆN

### 12.1. Backend Unit & Integration Tests (`BE/DentalClinic.Tests`)
- Tệp kiểm thử: `BE/DentalClinic.Tests/DentistLeave/DoctorLeaveAndDisruptionRecoveryTests.cs` (11 test cases nghiệp vụ).
- Kết quả chạy toàn bộ test suite backend:
  ```text
  Passed!  - Failed: 0, Passed: 793, Skipped: 1, Total: 794, Duration: 20 s - DentalClinic.Tests.dll (net8.0)
  ```
- **Tỉ lệ đạt**: 100% test cases mới và hồi quy đều pass.

### 12.2. Frontend Web Controller Tests (`FE/DentalClinic.Web.Tests`)
- Kết quả chạy toàn bộ test suite frontend:
  ```text
  Passed!  - Failed: 0, Passed: 315, Skipped: 0, Total: 315, Duration: 21 s - DentalClinic.Web.Tests.dll (net8.0)
  ```
- **Tỉ lệ đạt**: 100% (315/315 tests pass).
- **Tổng cộng toàn giải pháp**: **1,108 tests passing, 0 tests failing**.

### 12.3. Playwright Browser E2E Specs (`e2e/doctor-leave-recovery.spec.js`)
Bộ kiểm thử tự động trên trình duyệt thực tế bao gồm 4 kịch bản liên hoàn:
- `E2E-LEAVE-01`: Bác sĩ nộp đơn xin nghỉ phép trên lịch tháng và kiểm tra modal lịch sử đơn.
- `E2E-LEAVE-02`: Quản trị viên truy cập tab Duyệt đơn nghỉ phép, mở modal phân tích ảnh hưởng lịch hẹn.
- `E2E-LEAVE-03`: Bệnh nhân xem danh sách lịch hẹn và nhận diện thông báo gián đoạn.
- `E2E-LEAVE-04`: Lễ tân truy cập bảng điều phối gián đoạn và kích hoạt form xử lý trực tiếp.

---

## 13. HƯỚNG DẪN TRIỂN KHAI & CHECK-LIST NGHIỆM THU

### 13.1. Thứ Tự Triển Khai Trên Môi Trường Staging / Production
1. **Database Migration**:
   - Chạy script forward: `DB/redesign_migrations/20261109_Add_DoctorLeave_And_AppointmentDisruption.sql` trên cơ sở dữ liệu đích.
2. **Backend API**:
   - Build và publish dự án `BE/DentalClinic.Api`.
   - Cấu hình connection string trỏ tới database đã migration.
   - Background worker `AppointmentDisruptionBackgroundWorker` sẽ tự động khởi chạy cùng ứng dụng.
3. **Frontend Web**:
   - Build và publish dự án `FE/DentalClinic.Web`.

### 13.2. Check-list Nghiệm Thu
- [x] Bác sĩ nộp đơn xin nghỉ phép, kiểm tra ràng buộc ngày kết thúc >= ngày bắt đầu.
- [x] Bác sĩ rút đơn khi đang ở trạng thái `Pending` thành công; không rút được đơn khi đã duyệt.
- [x] Bác sĩ thấy ô ngày trên lịch tháng đổi màu kèm badge "Nghỉ phép".
- [x] Admin xem tab "Duyệt Đơn Nghỉ Phép" thấy danh sách đơn và số lượng lịch hẹn bị ảnh hưởng.
- [x] Admin mở modal Impact Analysis thấy chi tiết từng cuộc hẹn bị trùng và bác sĩ thay thế khả dụng.
- [x] Admin phê duyệt: Hệ thống tự động tạo `DentistAvailability(OnLeave)` và sinh `AppointmentDisruptions`.
- [x] Admin từ chối: Bắt buộc nhập lý do từ chối và bác sĩ nhận được lý do trên trang cá nhân.
- [x] Bệnh nhân có lịch hẹn bị ảnh hưởng nhận được email và banner cảnh báo trên cổng cá nhân.
- [x] Bệnh nhân chọn đổi bác sĩ: Lịch hẹn chuyển sang bác sĩ mới, giữ nguyên giờ khám.
- [x] Bệnh nhân chọn dời lịch: Khung giờ khám được dời sang ngày mới sau khoảng nghỉ của bác sĩ.
- [x] Lễ tân vào tab "Điều Phối BS Nghỉ Phép" xem được các ca khẩn cấp và xử lý thành công qua 4 phương án.
- [x] Toàn bộ test suite BE và FE đều pass 100% (1,108 passed).
