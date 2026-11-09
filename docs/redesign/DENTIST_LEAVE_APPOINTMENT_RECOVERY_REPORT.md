# DENTALCARE – BÁO CÁO TOÀN DIỆN TRIỂN KHAI VÀ HARDENING HỆ THỐNG XIN NGHỈ PHÉP BÁC SĨ & PHỤC HỒI GIÁN ĐOẠN LỊCH HẸN KHÁM
## DOCTOR LEAVE REQUEST & APPOINTMENT DISRUPTION RECOVERY SYSTEM REPORT
### (PRODUCTION-READY HARDENING & 4-ROLE WORKFLOWS)

---

- **Dự án**: DentalCare - Hệ Thống Quản Lý Phòng Khám Nha Khoa
- **Phân hệ**: Doctor Leave Request & Appointment Disruption Recovery (4 Roles: Dentist, Admin, Patient, Receptionist)
- **Feature Branch**: `feature/dentist-leave-appointment-recovery`
- **Worktree**: `D:\SWP391\DentalCare-Leave`
- **Database Môi Trường**: `DentalCare_Leave_Dev` (SQL Server `.\SQLEXPRESS`)
- **Ngày hoàn thành hardening**: 10/11/2026
- **Trạng thái**: Hoàn thành toàn diện, 100% tests vượt qua (**801/802 Backend tests pass, 1 skip Cloudinary; 315/315 Frontend tests pass; 19/19 Leave & Recovery tests pass**).

---

## 1. TỔNG QUAN NGHIỆP VỤ & KIẾN TRÚC VẬN HÀNH

Tính năng **Doctor Leave Request & Appointment Disruption Recovery** giải quyết triệt để bài toán gián đoạn lịch khám khi bác sĩ nghỉ phép đột xuất, đảm bảo tính liên tục của dịch vụ và trải nghiệm chăm sóc khách hàng:

1. **Bác sĩ (Dentist)**:
   - Nộp đơn xin nghỉ phép toàn ca hoặc một phần ca (`PartialShift`), bắt buộc ghi lý do nội bộ và ghi chú bàn giao.
   - Quản lý lịch sử đơn nghỉ, rút đơn (`Withdrawn`) an toàn khi đơn chưa được duyệt.
   - Lịch tháng cá nhân hiển thị trực quan các ngày/ca xin nghỉ phép (`.leave-pill`).
2. **Quản trị viên (SystemAdministrator)**:
   - Tab "Duyệt Đơn Nghỉ Phép" với bảng thống kê và danh sách đơn chờ duyệt.
   - Phân tích ảnh hưởng (`Impact Analysis`): đếm số lịch hẹn bị ảnh hưởng, phát hiện các ca khẩn cấp trong vòng 24 giờ, và tự động tìm bác sĩ thay thế cùng chuyên khoa không trùng lịch.
   - Phê duyệt đơn kích hoạt tự động: cập nhật `DentistAvailability(OnLeave)`, hủy ca trực làm việc bị trùng, sinh các bản ghi sự cố gián đoạn `AppointmentDisruptions`, và phát hành email thông báo.
   - Từ chối đơn: bắt buộc nhập lý do từ chối gửi thông báo nội bộ đến bác sĩ.
   - Quyền hạn nghiêm ngặt: Chỉ SystemAdministrator mới có quyền duyệt hoặc từ chối đơn nghỉ; chặn Lễ tân can thiệp vào quy trình xét duyệt của bác sĩ.
3. **Bệnh nhân (Patient)**:
   - Nhận email thông báo và xem cảnh báo trên cổng cá nhân.
   - Chủ động lựa chọn 1 trong 2 phương án tự phục hồi lịch hẹn:
     * **Phương án 1 (Đổi bác sĩ)**: Giữ nguyên ngày giờ khám, chuyển sang bác sĩ khác cùng chuyên khoa đang rảnh lịch.
     * **Phương án 2 (Dời lịch)**: Giữ nguyên bác sĩ điều trị, chọn ngày giờ khám mới thuận tiện sau thời gian bác sĩ kết thúc nghỉ phép.
   - Bảo toàn 100% hóa đơn (`Invoice`), tiền cọc (`Deposit`), phác đồ điều trị (`TreatmentPlan`), và lịch tái khám (`FollowUpSchedule`).
4. **Lễ tân (Receptionist)**:
   - Dashboard điều phối gián đoạn tập trung: ưu tiên các ca khẩn cấp trong vòng 24 giờ (`IsUrgent`).
   - Modal điều phối cho phép can thiệp hỗ trợ qua điện thoại: đổi bác sĩ, dời lịch, xác nhận xử lý thỏa thuận tại quầy, hoặc hủy lịch hẹn hoàn cọc an toàn.

---

## 2. KẾT QUẢ HARDENING & AN TOÀN NGHIỆP VỤ CHUYÊN SÂU

Trong đợt hardening cuối cùng trước khi đề xuất merge, hệ thống đã giải quyết triệt để toàn bộ 14 rủi ro tiềm ẩn:

### 2.1. Quản lý Số Thứ Tự (QueueNumber) & Tài Nguyên Phòng Ghế Khám
- **Quy tắc cấp số**: `QueueNumber` được thiết kế chỉ cấp phát trong ngày khám khi bệnh nhân có mặt tại phòng khám và làm thủ tục **Check-in** hợp lệ tại quầy tiếp đón.
- Khi bệnh nhân hoặc lễ tân dời lịch sang ngày khác, hoặc đổi sang bác sĩ khác:
  * Hệ thống tự động đặt `QueueNumber = null`.
  * Hệ thống tự động giải phóng tài nguyên phòng khám và ghế nha khoa: `RoomId = null`, `DentalChairId = null`.
  * Tránh tình trạng giữ số thứ tự ảo của ngày cũ hoặc chiếm dụng phòng ghế khám của ca trực khác.

### 2.2. Phân Biệt Nghiêm Ngặt Trạng Thái Cuộc Hẹn (State Machine Guardrails)
- Phân biệt rõ các trạng thái: `Pending`, `Confirmed`, `CheckedIn`, `Waiting`, `Called`, `InService`, `Completed`, `Cancelled`.
- **Bảo vệ ca đã có mặt tại phòng khám**: Chặn tuyệt đối không cho Bệnh nhân tự xử lý trên giao diện web nếu lịch hẹn đã `CheckedIn`, `Waiting`, `Called` hoặc `InService`.
- Khi Admin duyệt đơn nghỉ phép mà có cuộc hẹn đã `CheckedIn`, hệ thống tự động gán `disruption.Status = NeedsStaffAction` và trả thông báo yêu cầu bệnh nhân liên hệ trực tiếp lễ tân tại quầy tiếp đón.

### 2.3. Hỗ Trợ Nghỉ Một Phần Ca (PartialShift Recovery)
- Khi bác sĩ xin nghỉ phép một phần ca trực (ví dụ chỉ nghỉ buổi chiều từ 13:00 - 17:00, hoặc nghỉ 2 tiếng buổi sáng):
  * Hệ thống phân tích chính xác theo `StartDateTime` và `EndDateTime`.
  * Chỉ hủy và sinh disruption cho các cuộc hẹn giao nhau với khoảng thời gian nghỉ thực tế.
  * Các cuộc hẹn buổi sáng hoặc phần ca trực còn lại của bác sĩ được bảo toàn tuyệt đối, không bị hủy nhầm.

### 2.4. Tính Bất Biến & Chống Trùng Lặp (Idempotency)
- Bổ sung Unique Filtered Index trên SQL Server:
  ```sql
  CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentDisruptions_Appt_Leave]
  ON [AppointmentDisruptions] ([AppointmentId], [DentistLeaveRequestId])
  WHERE [AppointmentId] IS NOT NULL AND [DentistLeaveRequestId] IS NOT NULL;
  ```
- Kiểm tra trùng lặp trước khi insert bản ghi disruption.
- Chặn không cho phép đánh dấu Resolved khi cuộc hẹn thực tế chưa được xử lý hoặc đã ở trạng thái hủy.

### 2.5. Bảo Mật Cấp Cao & Phòng Chống IDOR (Insecure Direct Object Reference)
- **Cơ chế ActionToken Cryptographically Strong**:
  * Mã token sinh bằng bộ sinh số ngẫu nhiên an toàn mật mã `System.Security.Cryptography.RandomNumberGenerator` (32 bytes hex - 256 bits entropy).
  * Gắn thời hạn hiệu lực `TokenExpiresAt` (tối đa 7 ngày hoặc trước giờ khám bắt đầu).
  * **Cơ chế Anti-Reuse (Chống sử dụng lại token)**: Ngay sau khi cuộc hẹn được giải quyết thành công qua `PatientChooseNewDentistAsync` hoặc `PatientRescheduleDisruptionAsync`, hệ thống lập tức vô hiệu hóa token bằng cách set `ActionToken = null` và `TokenExpiresAt = null`. Mọi nỗ lực gửi lại request với token cũ sẽ bị từ chối ngay lập tức.
- **Phòng chống lộ thông tin riêng tư & IDOR**:
  * Endpoint `GetLeaveRequestDetailAsync`: Chỉ cho phép chính Bác sĩ sở hữu đơn nghỉ hoặc SystemAdministrator xem chi tiết đơn. Chặn Bác sĩ khác và Bệnh nhân xem lý do cá nhân riêng tư của bác sĩ.
  * Endpoint `GetDisruptionByAppointmentIdAsync` & `GetDisruptionDetailAsync`: Kiểm tra IDOR nghiêm ngặt. Bệnh nhân chỉ được xem disruption của lịch hẹn thuộc về chính mình.

### 2.6. Email Delivery Worker & Concurrency Isolation
- **Thêm cột lưu vết retry**: `[EmailRetryCount] INT NOT NULL DEFAULT (0)` vào bảng `[AppointmentDisruptions]`.
- **Khóa phân tán chống gửi trùng (Worker Concurrency)**:
  * Phương thức `ProcessPendingDisruptionEmailsAsync` sử dụng `SqlAppLockHelper.AcquireLocksAsync` với key `DISRUPTION_EMAIL_WORKER_LOCK`.
  * Đảm bảo khi có nhiều tiến trình worker hoặc cụm máy chủ chạy nền song song, chỉ một tiến trình được quét và gửi email tại một thời điểm.
- **Cơ chế Retry tối đa 3 lần**:
  * Mỗi lần gửi thất bại tăng `EmailRetryCount += 1`.
  * Nếu sau 3 lần gửi vẫn thất bại, hệ thống đánh dấu `EmailDeliveryStatus = Failed` và tự động phát thông báo khẩn cấp (Emergency Alert Notification) đến toàn bộ Quản trị viên và Lễ tân trực ca để liên hệ trực tiếp qua điện thoại.
- **Ngôn ngữ thông báo trung tính**: Nội dung email gửi cho bệnh nhân tuyệt đối không chứa lý do riêng tư của bác sĩ, sử dụng thông điệp chuyên nghiệp: *"do thay đổi về lịch công tác"*.
- **Chính sách hoàn tiền cọc**: Khi hủy lịch hẹn (`Cancelled`), hệ thống không tự tiện đánh dấu tiền cọc đã hoàn trả (`DepositRefunded`). Mọi khoản tiền cọc được giữ nguyên liên kết hóa đơn và ghi chú lưu vết audit để bộ phận kế toán/thu ngân thực hiện quy trình đối soát hoàn tiền theo quy định tài chính.

---

## 3. MÔ HÌNH DATABASE & MIGRATIONS

### 3.1. Cập Nhật Bảng `DentistLeaveRequests`
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc / Mô Tả |
| :--- | :--- | :--- |
| `LeaveRequestId` | BIGINT IDENTITY(1,1) | Khóa chính PK |
| `DentistUserId` | BIGINT NOT NULL | FK -> `DentistProfiles(UserId)` |
| `LeaveType` | NVARCHAR(50) NOT NULL | `AnnualLeave`, `SickLeave`, `Personal`, `Emergency`, `Other` |
| `StartDate`, `EndDate` | DATE NOT NULL | Ngày bắt đầu và kết thúc nghỉ |
| `StartTime`, `EndTime` | TIME(7) NULL | Giờ bắt đầu và kết thúc (cho ca nghỉ một phần) |
| `StartDateTime`, `EndDateTime` | DATETIME2 NOT NULL | Thời điểm chính xác phục vụ truy vấn overlap |
| `Reason` | NVARCHAR(500) NOT NULL | Lý do nghỉ phép (bảo mật nội bộ) |
| `Notes` | NVARCHAR(500) NULL | Ghi chú bàn giao công việc |
| `Status` | NVARCHAR(30) NOT NULL | `Pending`, `Approved`, `Rejected`, `Withdrawn` |
| `ReviewedByUserId` | BIGINT NULL | FK -> `UserAccounts(UserId)` (Admin duyệt) |
| `ReviewedAt` | DATETIME2 NULL | Thời điểm xét duyệt |
| `RejectionReason` | NVARCHAR(500) NULL | Lý do Admin từ chối đơn |
| `CreatedAt`, `UpdatedAt` | DATETIME2 NOT NULL | Thời gian tạo và cập nhật |

### 3.2. Cập Nhật Bảng `AppointmentDisruptions`
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc / Mô Tả |
| :--- | :--- | :--- |
| `DisruptionId` | BIGINT IDENTITY(1,1) | Khóa chính PK |
| `AppointmentId` | BIGINT NOT NULL | FK -> `Appointments(AppointmentId)` |
| `DentistLeaveRequestId` | BIGINT NOT NULL | FK -> `DentistLeaveRequests(LeaveRequestId)` |
| `OriginalDentistUserId` | BIGINT NOT NULL | FK -> `DentistProfiles(UserId)` |
| `OriginalScheduledStart`, `End` | DATETIME2 NOT NULL | Khung giờ gốc của lịch hẹn |
| `Status` | NVARCHAR(50) NOT NULL | `PendingPatientChoice`, `DoctorChanged`, `Rescheduled`, `ResolvedByStaff`, `NeedsStaffAction`, `Cancelled` |
| `PatientChoice` | NVARCHAR(50) NULL | `ChooseNewDentist`, `Reschedule`, `StaffIntervention` |
| `ResolvedAppointmentId` | BIGINT NULL | FK -> `Appointments(AppointmentId)` |
| `NewDentistUserId` | BIGINT NULL | FK -> `DentistProfiles(UserId)` |
| `ResolvedByUserId` | BIGINT NULL | FK -> `UserAccounts(UserId)` |
| `ResolvedAt` | DATETIME2 NULL | Thời điểm xử lý xong |
| `StaffNotes` | NVARCHAR(1000) NULL | Ghi chú của lễ tân |
| `EmailSentAt` | DATETIME2 NULL | Thời điểm gửi email thông báo |
| `EmailDeliveryStatus` | NVARCHAR(50) NOT NULL | `Pending`, `Sent`, `Failed`, `Skipped` |
| `EmailRetryCount` | INT NOT NULL DEFAULT(0) | Số lần đã retry gửi email (tối đa 3 lần) |
| `ActionToken` | NVARCHAR(100) NULL | Token ngẫu nhiên (32 bytes hex), xóa sau khi resolved |
| `TokenExpiresAt` | DATETIME2 NULL | Hạn sử dụng của ActionToken |
| `IsUrgent` | BIT NOT NULL DEFAULT(0) | Ca khẩn cấp (trong vòng 24h) |
| `CreatedAt`, `UpdatedAt` | DATETIME2 NOT NULL | Thời gian tạo và cập nhật |

### 3.3. Tệp Quản Lý Migrations
- Migration script cập nhật: `DB/redesign_migrations/20261109_Add_DoctorLeave_And_AppointmentDisruption.sql` (đã bao gồm cột `EmailRetryCount`, `TokenExpiresAt` và index `UQ_AppointmentDisruptions_Appt_Leave`).
- Rollback script an toàn: `DB/redesign_migrations/20261109_Rollback_DoctorLeave_And_AppointmentDisruption.sql`.

---

## 4. CHI TIẾT CÁC GIAO DIỆN 4 ROLES

### 4.1. Role Dentist (`/Dentist/Schedule`)
- Header tích hợp nút **Xin Nghỉ Phép** (`#createLeaveModal`) và **Đơn Nghỉ Của Tôi** (`#myLeavesModal` kèm badge số lượng đơn chờ duyệt).
- Calendar Grid hiển thị trực quan các ngày có đơn nghỉ đã duyệt hoặc đang chờ duyệt (`.leave-pill`).
- Modal nộp đơn: Hỗ trợ chọn loại nghỉ, ngày/giờ bắt đầu và kết thúc, lý do bắt buộc và ghi chú bàn giao.
- Modal đơn cá nhân: Cho phép theo dõi trạng thái, xem lý do từ chối và rút đơn an toàn khi đơn đang ở trạng thái `Pending`.

### 4.2. Role SystemAdministrator (`/Admin/DentistSchedule?tab=leave-requests`)
- Tab thứ 5 "Duyệt Đơn Nghỉ Phép" hiển thị danh sách đơn chờ duyệt và các thẻ thống kê tổng quan.
- Modal **Phân Tích Ảnh Hưởng** (`#impactAnalysisModal`): Tự động nạp qua AJAX danh sách các cuộc hẹn bị trùng, mức độ khẩn cấp (trong vòng 24 giờ), và danh sách bác sĩ thay thế khả dụng cùng chuyên khoa.
- Thao tác phê duyệt đơn tự động cập nhật lịch bận, hủy ca trực trùng và tạo gián đoạn lịch hẹn.
- Thao tác từ chối đơn bắt buộc nhập lý do phản hồi cho bác sĩ.

### 4.3. Role Patient (`/Patient/Appointment` & `/Patient/Appointment/Disruption/{id}`)
- Banner cảnh báo nổi bật trên trang danh sách lịch hẹn và chi tiết lịch hẹn khi lịch bị ảnh hưởng bởi bác sĩ nghỉ phép.
- Giao diện điều phối riêng biệt (`DisruptionDetail.cshtml`):
  * **Phương án 1 (Giữ nguyên giờ - Đổi bác sĩ)**: Lựa chọn danh sách các bác sĩ cùng chuyên khoa đang rảnh trong khung giờ đó.
  * **Phương án 2 (Giữ bác sĩ - Dời lịch)**: Lựa chọn thời gian khám mới sau thời gian bác sĩ nghỉ phép.
  * Hướng dẫn rõ ràng về việc số thứ tự khám (`QueueNumber`) sẽ được cấp tự động khi Check-in vào ngày khám mới.

### 4.4. Role Receptionist (`/Receptionist/Appointments/Disruptions`)
- Dashboard điều phối riêng biệt dành cho lễ tân, phân loại rõ ràng các ca khẩn cấp (`IsUrgent`).
- Hỗ trợ gọi điện trực tiếp cho bệnh nhân qua nút bấm `tel:...`.
- Modal điều phối (`#resolveDisruptionModal`) với 4 phương án giải quyết: Đổi bác sĩ, Dời lịch, Đã thỏa thuận tiếp đón tại quầy, và Hủy lịch hẹn hoàn cọc an toàn.

---

## 5. KẾT QUẢ TEST SUITE & BẢO ĐẢM CHẤT LƯỢNG

### 5.1. Backend Unit & Integration Tests (`BE/DentalClinic.Tests`)
- Bộ kiểm thử chuyên biệt: `BE/DentalClinic.Tests/DentistLeave/DoctorLeaveAndDisruptionRecoveryTests.cs` (19 test cases toàn diện):
  1. `Dentist_SubmitLeaveRequest_SavesAsPending`
  2. `Dentist_WithdrawPendingLeaveRequest_Succeeds`
  3. `Dentist_WithdrawApprovedLeaveRequest_ThrowsInvalidOperationException`
  4. `ImpactAnalysis_DetectsAffectedAppointments_AndFlagsUrgentWithin24Hours`
  5. `Admin_ApproveLeaveRequest_CreatesDisruptions_AndUpdatesAvailability`
  6. `Admin_RejectLeaveRequest_RecordsReason_AndLeavesAppointmentsUntouched`
  7. `Patient_ChooseAlternativeDentist_UpdatesAppointmentAndDisruption`
  8. `Patient_RescheduleDisruption_UpdatesScheduledTime`
  9. `Receptionist_ManualStaffResolved_SetsDisruptionStatus`
  10. `Receptionist_CancelDisruption_CancelsAppointmentSafely`
  11. `NonAdminUser_ApproveLeaveRequest_ThrowsUnauthorizedAccessException`
  12. `PatientReschedule_ResetsQueueNumberAndInvalidatesToken`
  13. `PatientResolution_BlocksCheckedInOrInServiceAppointments`
  14. `ActionToken_ExpiredOrInvalid_IsRejected`
  15. `Security_IDOR_Protection_On_Disruption_And_LeaveDetail`
  16. `PartialShiftLeave_DoesNotCancelOrAffectMorningAppointments`
  17. `ProcessPendingEmails_RetriesUpTo3Times_AndAlertsReceptionistOnMaxFailures`
  18. `Cancellation_PreservesInvoice_AndDoesNotMarkDepositAsRefunded`
  19. `Concurrency_AdminApproveLeave_And_PatientAction_AreSerializedAndConsistent`
- **Kết quả chạy toàn bộ Backend test suite**:
  ```text
  Passed!  - Failed: 0, Passed: 801, Skipped: 1, Total: 802, Duration: 18 s - DentalClinic.Tests.dll (net8.0)
  ```
  *(1 test skip là Cloudinary test yêu cầu UserSecrets cấu hình bên ngoài)*

### 5.2. Frontend Controller & ViewModel Tests (`FE/DentalClinic.Web.Tests`)
- **Kết quả chạy toàn bộ Frontend test suite**:
  ```text
  Passed!  - Failed: 0, Passed: 315, Skipped: 0, Total: 315, Duration: 20 s - DentalClinic.Web.Tests.dll (net8.0)
  ```

### 5.3. Playwright Browser E2E Specs (`e2e/doctor-leave-recovery.spec.js`)
Bộ kịch bản E2E kiểm thử đầy đủ các luồng:
- `E2E-LEAVE-01`: Dentist nộp đơn xin nghỉ phép và kiểm tra lịch cá nhân.
- `E2E-LEAVE-02`: Admin truy cập tab Duyệt đơn nghỉ phép và kiểm tra modal phân tích ảnh hưởng.
- `E2E-LEAVE-03`: Flow A - Bệnh nhân lựa chọn bác sĩ thay thế và xác nhận cập nhật lịch hẹn.
- `E2E-LEAVE-04`: Flow B - Bệnh nhân dời lịch hẹn sang ngày giờ mới.
- `E2E-LEAVE-05`: Concurrency & Security - Kiểm tra token invalidation và bảo vệ IDOR.
- `E2E-LEAVE-06`: Lễ tân truy cập bảng điều phối gián đoạn và mở modal xử lý.

---

## 6. CHECK-LIST BÀN GIAO & SẴN SÀNG MERGE VÀO DEVELOP

- [x] **Database Isolation**: Toàn bộ thao tác chỉ thực hiện trên database `DentalCare_Leave_Dev`. Database chính `DentalClinicManagementDB` và integration DB không bị can thiệp.
- [x] **Database Migration Scripts**: Script forward và rollback đầy đủ tại thư mục `DB/redesign_migrations/`.
- [x] **Backend Build**: Solution `BE/DentalClinic.Backend.sln` build thành công 0 Warning/Error.
- [x] **Frontend Build**: Solution `FE/DentalClinic.Web.sln` build thành công 0 Warning/Error.
- [x] **Full Backend Tests**: 801/802 tests pass (100% active tests pass).
- [x] **Full Frontend Tests**: 315/315 tests pass (100% tests pass).
- [x] **QueueNumber Hardening**: Reset số thứ tự và phòng ghế khi dời lịch hoặc đổi bác sĩ, cấp lại số khi Check-in.
- [x] **Status Guardrails**: Chặn bệnh nhân tự giải quyết ca đã CheckedIn hoặc InService; điều hướng sang quầy tiếp đón.
- [x] **Partial Shift**: Phục hồi chính xác các cuộc hẹn giao với khung giờ nghỉ, bảo toàn các ca khám khác trong ngày.
- [x] **Anti-Token-Reuse**: Sinh token mật mã an toàn, vô hiệu hóa token ngay khi hoàn tất xử lý.
- [x] **IDOR Protection**: Bảo mật lý do nghỉ riêng tư của bác sĩ và bảo vệ dữ liệu gián đoạn của bệnh nhân.
- [x] **Email Retry & AppLock**: Cơ chế khóa phân tán chống race condition và cảnh báo khẩn cấp khi vượt 3 lần retry.
- [x] **Tài liệu bàn giao**: Báo cáo kỹ thuật chi tiết đã cập nhật đầy đủ và sẵn sàng cho việc nghiệm thu merge vào `develop`.
