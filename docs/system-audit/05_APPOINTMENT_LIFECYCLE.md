# VÒNG ĐỜI LỊCH HẸN & CỖ MÁY TRẠNG THÁI (APPOINTMENT LIFECYCLE & STATE MACHINE)
**Tài liệu:** `05_APPOINTMENT_LIFECYCLE.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Phân hệ trung tâm:** Core Clinical Workflow Engine

---

## 1. CÁC THỰC THỂ & HẰNG SỐ TRẠNG THÁI THỰC TẾ

Trạng thái lịch khám được định nghĩa tập trung tại `BE/DentalClinic.Domain/Enums/AppointmentStatus.cs` và lưu vào cột `Status` (`NVARCHAR(50)`) của bảng `Appointments` trong cơ sở dữ liệu.

### 14 Hằng số trạng thái chính thức:
1. `PendingReceptionReview`: Yêu cầu vừa tạo từ Bệnh nhân, chờ Lễ tân tiếp nhận sơ bộ.
2. `PendingDepartmentReview`: Đã qua Lễ tân, chuyển tiếp về Trưởng khoa để duyệt chuyên môn và gán tài nguyên.
3. `AwaitingPatientResponse`: Trưởng khoa đã gửi Đề xuất thay đổi (đổi giờ/đổi bác sĩ), đang chờ Bệnh nhân phản hồi.
4. `Confirmed`: Lịch khám đã được Trưởng khoa phê duyệt chính thức, đã cấp số thứ tự khám (`QueueNumber`), gán Bác sĩ, Phòng và Ghế.
5. `CheckedIn`: Bệnh nhân đã có mặt tại quầy tiếp đón của phòng khám, Lễ tân đã xác nhận thủ tục vào phòng chờ.
6. `Waiting`: Bệnh nhân đang ngồi tại khu vực chờ ngoài cửa phòng khám chuyên khoa.
7. `Called`: Hệ thống hoặc nhân viên y tế đã gọi loa/hiển thị số thứ tự mời bệnh nhân vào ghế điều trị.
8. `InService`: Bác sĩ đã bấm "Vào Khám", bệnh nhân đang ngồi trên ghế nha khoa được khám và điều trị trực tiếp.
9. `Completed`: Ca khám đã kết thúc, Bác sĩ đã hoàn tất thủ thuật y tế. Ca khám chuyển sang quầy Thu ngân để thanh toán viện phí.
10. `Rejected`: Lễ tân từ chối yêu cầu đặt lịch khám (kèm lý do giải thích).
11. `Withdrawn`: Bệnh nhân chủ động rút yêu cầu đặt lịch trước khi được phê duyệt.
12. `Cancelled`: Ca khám bị hủy bỏ (bởi Bệnh nhân, Nhân viên tiếp tân, hoặc do dời sang lịch khám khác).
13. `NoShow`: Bệnh nhân không đến khám đúng hẹn vào ngày khám đã xác nhận.
14. `Expired`: Yêu cầu đặt lịch hoặc đề xuất thay đổi quá hạn mà không có phản hồi.

---

## 2. MA TRẬN CHUYỂN ĐỔI TRẠNG THÁI (STATE TRANSITION MATRIX)

| Trạng thái hiện tại | Hành động (Action) | Tác nhân (Actor) | Điều kiện kích hoạt (Conditions) | Trạng thái tiếp theo | API / Phương thức Service | Thay đổi trong Database | Bằng chứng mã nguồn |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| *(None)* | Tạo lịch khám mới | Bệnh nhân (`Patient`) | Tài khoản Active, $\le 2$ gậy phạt Strike Policy, thời gian khám không ở quá khứ | `PendingReceptionReview` | `POST /api/patient/appointments`<br>`AppointmentService.CreatePatientAppointmentAsync` | INSERT `Appointments`<br>INSERT `AppointmentStatusHistories` | `AppointmentService.cs:46-150` |
| `PendingReceptionReview` | Chuyển tiếp | Lễ tân (`Receptionist`) | Lịch khám đang chờ tiếp nhận | `PendingDepartmentReview` | `POST /api/receptionist/appointments/{id}/forward`<br>`AppointmentService.ReceptionistForwardAsync` | UPDATE `Appointments.Status`<br>INSERT `AppointmentStatusHistories` | `AppointmentService.cs:905-941` |
| `PendingReceptionReview` | Từ chối | Lễ tân (`Receptionist`) | Bắt buộc nhập lý do từ chối | `Rejected` | `POST /api/receptionist/appointments/{id}/reject`<br>`AppointmentService.ReceptionistRejectAsync` | UPDATE `Appointments.Status`<br>UPDATE `Appointments.CancellationReason`<br>INSERT `Notifications` | `AppointmentService.cs:943-995` |
| `PendingReceptionReview`<br>`PendingDepartmentReview` | Rút yêu cầu | Bệnh nhân (`Patient`) | Bệnh nhân là chủ sở hữu ca khám | `Withdrawn` | `POST /api/patient/appointments/{id}/withdraw`<br>`AppointmentService.PatientWithdrawAsync` | UPDATE `Appointments.Status`<br>UPDATE `Appointments.CancellationReason`<br>INSERT `AppointmentStatusHistories` | `AppointmentService.cs:255-312` |
| `PendingDepartmentReview` | Xác nhận ca khám | Trưởng khoa (`DepartmentManager`) | Bác sĩ, Phòng và Ghế không bị trùng lịch; nếu bệnh nhân chọn bác sĩ đích danh thì không được tự ý đổi bác sĩ | `Confirmed` | `POST /api/department/appointments/{id}/confirm`<br>`AppointmentService.ManagerConfirmAsync` | UPDATE `Appointments` (Status, QueueNumber, AssignedDentistUserId, RoomId, DentalChairId, ConfirmedAt)<br>INSERT `AppointmentStatusHistories`<br>Gửi Email xác nhận | `AppointmentService.cs:1452-1693` |
| `PendingDepartmentReview` | Gửi đề xuất thay đổi | Trưởng khoa (`DepartmentManager`) | Bác sĩ yêu cầu bận hoặc hết phòng; chọn giờ hoặc bác sĩ mới | `AwaitingPatientResponse` | `POST /api/department/appointments/{id}/propose-change`<br>`AppointmentService.ManagerProposeChangeAsync` | UPDATE `Appointments.Status`<br>INSERT `AppointmentChangeProposals`<br>INSERT `Notifications` | `AppointmentService.cs:1695-1788` |
| `AwaitingPatientResponse` | Đồng ý đề xuất | Bệnh nhân (`Patient`) | Bệnh nhân phản hồi `accept = true`; kiểm tra khung giờ mới còn trống | `PendingDepartmentReview` | `POST /api/patient/appointments/{id}/proposal/accept`<br>`AppointmentService.PatientRespondProposalAsync` | UPDATE `AppointmentChangeProposals.Status = 'Accepted'`<br>UPDATE `Appointments` (Status, ScheduledStart, Dentist)<br>INSERT `AppointmentStatusHistories` | `AppointmentService.cs:645-750` |
| `AwaitingPatientResponse` | Từ chối đề xuất | Bệnh nhân (`Patient`) | Bệnh nhân phản hồi `accept = false` | `PendingDepartmentReview` | `POST /api/patient/appointments/{id}/proposal/reject`<br>`AppointmentService.PatientRespondProposalAsync` | UPDATE `AppointmentChangeProposals.Status = 'Rejected'`<br>UPDATE `Appointments.Status`<br>INSERT `AppointmentStatusHistories` | `AppointmentService.cs:751-774` |
| `Confirmed` | Bệnh nhân hủy hẹn | Bệnh nhân (`Patient`) | Phải có lý do hủy; nếu báo trước $< 12$h sẽ bị tính 1 gậy vi phạm Strike Policy | `Cancelled` | `POST /api/patient/appointments/{id}/cancel`<br>`AppointmentService.PatientCancelAsync` | UPDATE `Appointments.Status = 'Cancelled'`<br>UPDATE `Appointments.CancellationReason`<br>INSERT `AppointmentStatusHistories`<br>INSERT `Notifications` | `AppointmentService.cs:314-442` |
| `Confirmed` | Check-in tại quầy | Lễ tân (`Receptionist`) | Bệnh nhân có mặt tại phòng khám vào ngày hẹn | `CheckedIn` | `POST /api/receptionist/appointments/{id}/check-in`<br>`AppointmentService.ReceptionistCheckInAsync` | UPDATE `Appointments.Status = 'CheckedIn'`<br>UPDATE `Appointments.CheckedInAt = UtcNow`<br>INSERT `AppointmentStatusHistories` | `AppointmentService.cs:1037-1074` |
| `Confirmed`<br>`Waiting` | Đánh dấu vắng mặt | Lễ tân (`Receptionist`) | Quá giờ hẹn mà bệnh nhân không đến khám | `NoShow` | `POST /api/receptionist/appointments/{id}/no-show`<br>`AppointmentService.ReceptionistMarkNoShowAsync` | UPDATE `Appointments.Status = 'NoShow'`<br>Tính 1 gậy vi phạm cho bệnh nhân<br>INSERT `Notifications` | `AppointmentService.cs:1076-1145` |
| `CheckedIn` | Vào khám | Bác sĩ (`Dentist`) | Đúng bác sĩ được phân công (`AssignedDentistUserId == currentUserId`) | `InService` | `POST /api/dentist/appointments/{id}/start`<br>`AppointmentService.DentistStartAppointmentAsync` | UPDATE `Appointments.Status = 'InService'`<br>INSERT `AppointmentStatusHistories`<br>INSERT `Notifications` | `AppointmentService.cs:1852-1898` |
| `InService` | Hoàn thành ca khám | Bác sĩ (`Dentist`) | Đúng bác sĩ được phân công; đã hoàn tất thủ thuật y khoa | `Completed` | `POST /api/dentist/appointments/{id}/complete`<br>`AppointmentService.DentistCompleteAppointmentAsync` | UPDATE `Appointments.Status = 'Completed'`<br>UPDATE `Appointments.CompletedAt = UtcNow`<br>Khóa vị trí ghế điều trị | `AppointmentService.cs:1900-1948` |

---

## 3. TRẢ LỜI 13 CÂU HỎI KIỂM ĐỊNH KỸ THUẬT (CRITICAL AUDIT QUESTIONS)

### 1. Ai là người tạo yêu cầu đặt lịch hẹn?
- **Trả lời:** Bệnh nhân (`Patient`) tự đặt online qua giao diện `/Patient/Appointment/Book`, hoặc Nhân viên Lễ tân (`Receptionist`) tạo trực tiếp tại quầy tiếp đón. Trường `BookingSource` ghi nhận rõ `'Patient'` hoặc `'Receptionist'`.

### 2. Ai là người xem xét yêu cầu đặt lịch?
- **Trả lời:** Quy trình qua 2 vòng phê duyệt độc lập:
  - Vòng 1: Lễ tân (`Receptionist`) tiếp nhận sơ bộ hành chính tại màn hình `/Receptionist/Appointments/Requests`.
  - Vòng 2: Trưởng khoa (`DepartmentManager`) duyệt chuyên môn, sắp xếp nhân sự và phân bổ phòng/ghế tại `/Department/Appointments/Review/{id}`.

### 3. Ai là người phân công bác sĩ phụ trách?
- **Trả lời:** **Trưởng khoa (`DepartmentManager`)**. Bệnh nhân ở bước đặt hẹn ban đầu chỉ gửi nguyện vọng (`RequestedDentistUserId`), quyền phân công thực tế (`AssignedDentistUserId`) hoàn toàn thuộc thẩm quyền của Trưởng khoa.

### 4. Khi nào một lịch hẹn chính thức chuyển sang trạng thái `Confirmed`?
- **Trả lời:** Khi và chỉ khi Trưởng khoa bấm nút **[Xác Nhận]** (`ManagerConfirmAsync`) sau khi hệ thống đã kiểm tra tự động và xác nhận không có bất kỳ xung đột nào về lịch trực của Bác sĩ, Phòng khám và Ghế điều trị. Tại thời điểm này, hệ thống cấp số thứ tự khám (`QueueNumber`) và gửi thông báo xác nhận kèm email cho người bệnh.

### 5. Những ai có quyền từ chối, rút lại, hủy hoặc dời lịch hẹn?
- **Từ chối (`Rejected`):** Lễ tân (`Receptionist`).
- **Rút lại (`Withdrawn`):** Bệnh nhân (`Patient`) khi ca khám chưa được duyệt.
- **Hủy (`Cancelled`):** Bệnh nhân (chỉ hủy được khi ca ở trạng thái `Pending` hoặc `Confirmed`), hoặc Lễ tân/Quản trị viên (kèm lý do).
- **Dời lịch (`Rescheduled`):** Bệnh nhân thao tác tại `/Patient/Appointment/Reschedule/{id}`.

### 6. Lịch hẹn có thể cập nhật sau khi đã xác nhận (`Confirmed`) không?
- **Trả lời:** Có thể dời lịch sang ngày khác hoặc hủy lịch. Tuy nhiên, không thể tự ý sửa đổi bác sĩ điều trị hoặc dịch vụ mà không thông qua quy trình dời lịch hoặc hủy để đặt lại.

### 7. Lịch hẹn đã hủy (`Cancelled`) có thể kích hoạt lại (Reactivate) không?
- **Trả lời:** **KHÔNG**. Trong mã nguồn `AppointmentService.cs`, một khi lịch khám đã chuyển sang `Cancelled`, trạng thái này là trạng thái kết thúc (Terminal State). Bệnh nhân muốn khám phải tạo một lịch hẹn mới hoàn toàn.

### 8. Điều gì xảy ra khi bác sĩ không khả dụng (OnLeave / Unavailable / Bận)?
- **Trả lời:** Khi Trưởng khoa mở màn hình xem xét duyệt ca, phương thức `GetAvailableDentistsForAppointmentAsync` tự động quét bảng `DentistAvailabilities` và `DentistWorkSchedules`. Bác sĩ nào có trạng thái khác `Available` hoặc không có ca trực trong khung giờ đó sẽ bị đánh dấu `IsAvailable = false` và đẩy xuống cuối danh sách lựa chọn.

### 9. Điều gì xảy ra nếu 2 bệnh nhân cùng tranh chấp một khung giờ / bác sĩ?
- **Trả lời:** Ở bước đặt hẹn, cả 2 yêu cầu đều được ghi nhận vào hàng đợi `PendingReceptionReview`. Khi đến tay Trưởng khoa, Trưởng khoa xác nhận ca của bệnh nhân nào trước thì ca đó sẽ chuyển sang `Confirmed` và chiếm dụng khung giờ đó (`IsSlotOccupying = true`). Khi Trưởng khoa mở ca của bệnh nhân thứ hai, hệ thống phát hiện xung đột và chặn lại, buộc Trưởng khoa phải phân công bác sĩ khác hoặc gửi Đề xuất đổi giờ (`Proposal`).

### 10. Hệ thống có ngăn chặn xung đột lịch (Conflicting Assignments) không?
- **Trả lời:** **CÓ, ngăn chặn triệt để 3 lớp**:
  1. Kiểm tra xung đột Bác sĩ: `AssignedDentistUserId == request.AssignedDentistUserId && ScheduledStart < finalEnd && ScheduledEnd > finalStart`
  2. Kiểm tra xung đột Phòng khám: `RoomId == request.RoomId && ScheduledStart < finalEnd && ScheduledEnd > finalStart`
  3. Kiểm tra xung đột Ghế nha khoa: `DentalChairId == request.DentalChairId && ScheduledStart < finalEnd && ScheduledEnd > finalStart`

### 11. Giao diện Frontend có đồng bộ trạng thái với Backend không?
- **Trả lời:** **CÓ, đồng bộ 100%**. Frontend hiển thị huy hiệu (Badge) màu sắc chuẩn theo enum của Backend:
  - `PendingReceptionReview`: Màu vàng cam (Chờ tiếp nhận)
  - `PendingDepartmentReview`: Màu xanh lơ (Chờ Trưởng khoa duyệt)
  - `Confirmed`: Màu xanh lục nhạt (Đã xác nhận)
  - `CheckedIn`: Màu xanh dương (Đã Check-in)
  - `InService`: Màu xanh lá đậm nhấp nháy (Đang trong ca khám)
  - `Completed`: Màu xám đậm / xanh ngọc (Hoàn tất)
  - `Cancelled` / `Rejected`: Màu đỏ (Đã hủy / Từ chối)

### 12. Việc chuyển đổi trạng thái có được kiểm thực nhất quán (Validation) không?
- **Trả lời:** Cực kỳ nghiêm ngặt. Ví dụ: Bác sĩ **không thể** bấm "Vào Khám" nếu bệnh nhân chưa được Lễ tân Check-in (`if (appointment.Status != AppointmentStatus.CheckedIn && appointment.Status != AppointmentStatus.Confirmed) return Fail("Bệnh nhân cần check-in trước");`). Lễ tân **không thể** Check-in nếu ca khám chưa được Trưởng khoa Confirm.

### 13. Người dùng có thể vượt mặt quy trình bằng cách gọi thẳng API không?
- **Trả lời:** **KHÔNG THỂ**. Mọi endpoint của Web API đều xác thực JWT Bearer Token, kiểm tra Role Claim (`[Authorize(Roles = ...)]`), đồng thời Service kiểm tra trạng thái hiện tại trong database trước khi thực hiện cập nhật.
