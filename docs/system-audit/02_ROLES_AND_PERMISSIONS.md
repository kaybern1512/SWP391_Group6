# PHÂN TÍCH VAI TRÒ NGƯỜI DÙNG & MA TRẬN PHÂN QUYỀN (ROLES & PERMISSIONS)
**Tài liệu:** `02_ROLES_AND_PERMISSIONS.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Phương pháp:** Kiểm chứng mã nguồn tĩnh (Static Source Code Inspection) & Kiểm thử tích hợp

---

## 1. CÁC VAI TRÒ THỰC TẾ TRONG HỆ THỐNG

Dựa trên định nghĩa tại `BE/DentalClinic.Domain/Enums/UserRole.cs` và cơ sở dữ liệu `DentalClinicManagementDB`, hệ thống hiện thực đầy đủ **5 vai trò chính thức**:

1. **`Patient` (Bệnh nhân / Khách hàng):** Người dùng đặt lịch khám, xem hồ sơ bệnh án cá nhân, theo dõi kế hoạch điều trị, đơn thuốc và thanh toán viện phí.
2. **`Receptionist` (Nhân viên Tiếp tân / Lễ tân):** Tiếp nhận yêu cầu đặt hẹn online, tiếp đón bệnh nhân tại quầy, cấp số thứ tự, thực hiện check-in, quản lý vắng mặt (No-Show), lập hóa đơn thu phí, nhận thanh toán tiền mặt/chuyển khoản và hỗ trợ trực tuyến (Live Chat).
3. **`Dentist` (Bác sĩ điều trị):** Tiếp nhận ca khám, chẩn đoán lâm sàng, cập nhật sơ đồ răng FDI 32 răng, tải phim X-quang/chẩn đoán hình ảnh, lập phác đồ điều trị, kê đơn thuốc và tạo giấy chuyển khoa (Referral).
4. **`DepartmentManager` (Trưởng khoa chuyên môn):** Duyệt yêu cầu đặt lịch, phân công bác sĩ phụ trách, điều phối phòng khám & ghế nha khoa, giải quyết xung đột lịch làm việc, tạo đề xuất thay đổi lịch hẹn (Proposal), duyệt giấy chuyển khoa và giám sát hiệu suất chuyên khoa.
5. **`SystemAdministrator` (Quản trị viên hệ thống):** Quản lý toàn bộ danh mục khoa phòng, ghế nha khoa, dịch vụ và bảng giá, quản lý tài khoản người dùng (khóa/mở khóa tài khoản), giám sát nhật ký kiểm toán hệ thống (`AuditLogs`), duyệt bài viết chuyên môn (Blog) và xem báo cáo tài chính toàn diện.

---

## 2. PHÂN TÍCH CHI TIẾT TỪNG VAI TRÒ

### 2.1. VAI TRÒ `Patient` (Bệnh nhân)
- **Màn hình truy cập:**
  - Trang chủ (`/Home/Index`), Blog nha khoa (`/Blog/Index`, `/Blog/Detail/{slug}`)
  - Bảng điều khiển cá nhân (`/Patient/Dashboard`)
  - Đặt lịch khám trực tuyến (`/Patient/Appointment/Book`)
  - Danh sách lịch khám cá nhân (`/Patient/Appointment/Index`)
  - Chi tiết lịch khám & Xem đề xuất đổi lịch (`/Patient/Appointment/Detail/{id}`)
  - Dời lịch khám (`/Patient/Appointment/Reschedule/{id}`)
  - Đánh giá chất lượng ca khám (`/Patient/Appointment/Feedback/{id}`)
  - Hồ sơ bệnh án điện tử cá nhân (`/Patient/MedicalRecords/Index`)
  - Kế hoạch điều trị cá nhân (`/Patient/TreatmentPlans/Index`, `/Patient/TreatmentPlans/Detail/{id}`)
  - Đơn thuốc cá nhân (`/Patient/Prescriptions/Index`)
  - Hóa đơn & Thanh toán VietQR (`/Patient/Invoices/Index`, `/Patient/Invoices/Detail/{id}`)
  - Lịch tái khám (`/Patient/FollowUps/Index`)
  - Hồ sơ cá nhân & Đổi mật khẩu (`/Profile/Index`, `/Profile/Edit`, `/Account/ChangePassword`)
- **Thao tác khả dụng:** Đăng ký, đăng nhập, đặt lịch khám, rút yêu cầu đặt lịch, hủy lịch khám, dời lịch, chấp nhận/từ chối đề xuất của Trưởng khoa, gửi phản hồi/đánh giá bác sĩ, tải ảnh đại diện, trò chuyện trực tuyến với lễ tân.
- **Ràng buộc nghiệp vụ & Sở hữu dữ liệu:**
  - **Chính sách phạt hủy sát giờ (Strike Policy):** Nếu bệnh nhân hủy lịch khám trong vòng dưới 12 giờ trước giờ hẹn hoặc vắng mặt (No-Show), hệ thống tự động ghi nhận 1 gậy vi phạm. Tích lũy 3 gậy trong vòng 30 ngày sẽ bị **tạm khóa quyền tự đặt lịch khám online 30 ngày** (`AppointmentService.cs` dòng 776-884).
  - **Quyền sở hữu dữ liệu (Data Ownership):** Bệnh nhân **chỉ được xem dữ liệu của chính mình** (`patientUserId == currentUserId`). Mọi API bệnh án, đơn thuốc, lịch hẹn đều có mệnh đề `Where(a => a.PatientUserId == currentUserId)`.

### 2.2. VAI TRÒ `Receptionist` (Nhân viên Lễ tân)
- **Màn hình truy cập:**
  - Bảng điều khiển tiếp tân (`/Receptionist/Dashboard`)
  - Danh sách yêu cầu đặt hẹn mới cần tiếp nhận (`/Receptionist/Appointments/Requests`)
  - Check-in ca khám hôm nay & Bàn gọi số (`/Receptionist/Appointments/TodayCheckIn`)
  - Tra cứu thông tin bệnh nhân (`/Receptionist/Patients/Index`)
  - Thu ngân & Hóa đơn viện phí (`/Receptionist/Billing/Index`, `/Receptionist/Billing/Create`, `/Receptionist/Billing/Detail/{id}`, `/Receptionist/Billing/Print/{id}`)
  - Quản lý lịch hẹn tái khám (`/Receptionist/FollowUps/Index`)
  - Hỗ trợ trực tuyến / Bàn tiếp đón chat (`/Receptionist/Chat/Index`)
- **Thao tác khả dụng:** Chuyển tiếp lịch hẹn sang Trưởng khoa (`Forward`), từ chối lịch hẹn không hợp lệ (`Reject`), check-in ca khám, ghi nhận vắng mặt (`No-Show`), hủy lịch đột xuất do lý do phòng khám, tạo hóa đơn cho ca khám đã hoàn thành, ghi nhận thanh toán (Tiền mặt / Thẻ / Chuyển khoản VietQR), in biên lai viện phí, nhắn tin hỗ trợ bệnh nhân.
- **Ràng buộc nghiệp vụ & Sở hữu dữ liệu:**
  - Lễ tân không có quyền chẩn đoán hoặc can thiệp vào hồ sơ bệnh án và sơ đồ răng.
  - Lễ tân chỉ được lập hóa đơn cho các ca khám đã ở trạng thái `Completed`.

### 2.3. VAI TRÒ `DepartmentManager` (Trưởng khoa chuyên môn)
- **Màn hình truy cập:**
  - Bảng điều khiển trưởng khoa (`/Department/Dashboard`)
  - Danh sách ca khám chờ duyệt của khoa (`/Department/Appointments/Requests`)
  - Màn hình duyệt ca & Phân bổ tài nguyên (`/Department/Appointments/Review/{id}`)
  - Quản lý danh sách bác sĩ trong khoa (`/Department/Dentists/Index`)
  - Lập lịch làm việc & Phân ca bác sĩ (`/Department/Schedule/Index`)
  - Quản lý dịch vụ thuộc chuyên khoa (`/Department/Services/Index`)
  - Theo dõi tiến độ điều trị bệnh nhân (`/Department/Treatment/Index`, `/Department/Treatment/Detail/{id}`)
  - Duyệt giấy chuyển khoa từ bác sĩ (`/Department/Referrals/Index`)
  - Báo cáo hiệu suất chuyên khoa (`/Department/Reports/Performance`)
- **Thao tác khả dụng:** Duyệt xác nhận ca khám (`ManagerConfirm`), tạo đề xuất thay đổi lịch hẹn/bác sĩ (`ManagerProposeChange`), phân công bác sĩ phụ trách ca điều trị nhiều giai đoạn, duyệt tiếp nhận bệnh nhân chuyển khoa, quản lý ca trực của bác sĩ thuộc khoa quản lý.
- **Ràng buộc nghiệp vụ & Sở hữu dữ liệu:**
  - **Giới hạn phạm vi khoa (Department Scope Isolation):** Trưởng khoa chỉ có quyền duyệt lịch hẹn, xếp ca trực và xem báo cáo của **chuyên khoa do mình quản lý** (`appointment.Department?.ManagerUserId == currentUserId`).
  - **Quy tắc tôn trọng lựa chọn của Bệnh nhân:** Nếu bệnh nhân đã yêu cầu đích danh một bác sĩ cụ thể, Trưởng khoa **không thể âm thầm đổi sang bác sĩ khác** qua nút Confirm mà bắt buộc phải gửi Đề xuất thay đổi (`Proposal`) để bệnh nhân phê duyệt (`AppointmentService.cs` dòng 1481-1485).

### 2.4. VAI TRÒ `Dentist` (Bác sĩ điều trị)
- **Màn hình truy cập:**
  - Bảng điều khiển bác sĩ (`/Dentist/Dashboard`)
  - Ca khám hôm nay & Hàng đợi ghế (`/Dentist/Appointments/Today`)
  - Hồ sơ bệnh án & Sơ đồ răng FDI (`/Dentist/MedicalRecords/Index`)
  - Kế hoạch điều trị (`/Dentist/TreatmentPlans/Index`, `/Dentist/TreatmentPlans/Create`, `/Dentist/TreatmentPlans/Detail/{id}`, `/Dentist/TreatmentPlans/Sessions`)
  - Kê đơn thuốc điện tử (`/Dentist/Prescriptions/Index`, `/Dentist/Prescriptions/Print/{id}`)
  - Đăng ký lịch làm việc & Xem lịch trực (`/Dentist/Schedule/Index`, `/Dentist/Availability/Index`)
  - Chuyển khoa chuyên môn (`/Dentist/Referrals/Index`)
  - Xem đánh giá của bệnh nhân (`/Dentist/Feedback/Index`)
  - Quản lý bài viết kiến thức nha khoa (`/Dentist/Blog/Index`, `/Dentist/Blog/Edit`)
- **Thao tác khả dụng:** Bắt đầu ca khám (`InService`), chọn/đổi ghế nha khoa trong phòng điều trị, ghi nhận tình trạng răng trên sơ đồ FDI 32 răng, tải ảnh chụp phim X-quang/ConeBeam CT, lập kế hoạch điều trị nhiều giai đoạn, ghi nhận buổi điều trị thực tế, kê đơn thuốc, tạo yêu cầu chuyển khoa cho trường hợp phức tạp, hoàn tất ca khám (`Completed`).
- **Ràng buộc nghiệp vụ & Sở hữu dữ liệu:**
  - Bác sĩ chỉ được phép bấm bắt đầu ca khám (`StartAppointment`) nếu ca khám đó được phân công cho chính mình (`AssignedDentistUserId == currentUserId`) và bệnh nhân đã làm thủ tục check-in.
  - Sau khi ca khám hoàn tất (`Completed`), hệ thống tự động khóa vị trí ghế, không cho phép thay đổi ghế nữa nhằm đảm bảo tính toàn vẹn hồ sơ y khoa (`AppointmentService.cs` dòng 2160-2164).

### 2.5. VAI TRÒ `SystemAdministrator` (Quản trị viên hệ thống)
- **Màn hình truy cập:**
  - Bảng điều khiển quản trị (`/Admin/Dashboard`)
  - Quản lý tài khoản người dùng (`/Admin/Users/Index`)
  - Quản lý chuyên khoa (`/Admin/Departments/Index`)
  - Quản lý danh mục dịch vụ & giá niêm yết (`/Admin/Services/Index`)
  - Quản lý phòng khám & ghế nha khoa (`/Admin/RoomChair/Index`)
  - Báo cáo doanh thu & công suất phòng khám (`/Admin/Reports/Index`, `/Admin/Reports/Revenue`)
  - Tra cứu nhật ký kiểm toán hệ thống (`/Admin/AuditLogs/Index`)
  - Kiểm duyệt bài viết Blog nha khoa (`/Admin/Blog/Index`)
- **Thao tác khả dụng:** Khóa/mở khóa tài khoản nhân viên và bệnh nhân, tạo/cập nhật thông tin chuyên khoa, cấu hình phòng bệnh và ghế điều trị, quản lý bảng giá dịch vụ điều trị, xem toàn bộ log hệ thống, phê duyệt bài viết Blog y khoa để xuất bản ra trang chủ.

---

## 3. MA TRẬN PHÂN QUYỀN HỆ THỐNG (ROLE-PERMISSION MATRIX)

| Vai trò | Phân hệ / Màn hình | Thao tác (Action) | Phân quyền Frontend (FE) | Phân quyền Backend (BE) | Bằng chứng mã nguồn (Evidence) | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| **Patient** | Đặt lịch khám | Tạo lịch hẹn mới | `[Authorize(Roles = "Patient")]` | `[Authorize(Roles = UserRole.Patient)]` | `PatientAppointmentController.cs`, `AppointmentsController.cs:17` | `COMPLETE` |
| **Patient** | Quản lý lịch hẹn | Hủy lịch / Rút yêu cầu | `[Authorize(Roles = "Patient")]` | `[Authorize(Roles = UserRole.Patient)]` | `PatientAppointmentController.cs`, `AppointmentsController.cs:33` | `COMPLETE` |
| **Patient** | Quản lý lịch hẹn | Dời ngày khám | `[Authorize(Roles = "Patient")]` | `[Authorize(Roles = UserRole.Patient)]` | `PatientAppointmentController.cs`, `AppointmentsController.cs:41` | `COMPLETE` |
| **Patient** | Đề xuất thay đổi | Chấp nhận / Từ chối đề xuất | `[Authorize(Roles = "Patient")]` | `[Authorize(Roles = UserRole.Patient)]` | `PatientAppointmentController.cs`, `AppointmentsController.cs:51` | `COMPLETE` |
| **Patient** | Bệnh án cá nhân | Xem hồ sơ răng miệng | `[Authorize(Roles = "Patient")]` | `[Authorize(Roles = UserRole.Patient)]` | `PatientMedicalRecordController.cs`, `MedicalRecordController.cs:37` | `COMPLETE` |
| **Patient** | Hóa đơn & QR | Xem & quét mã VietQR | `[Authorize(Roles = "Patient")]` | `[Authorize(Roles = UserRole.Patient)]` | `PatientInvoiceController.cs`, `BillingController.cs:64` | `COMPLETE` |
| **Receptionist**| Tiếp nhận lịch hẹn | Chuyển tiếp Trưởng khoa | `[Authorize(Roles = "Receptionist")]` | `[Authorize(Roles = "Receptionist,SystemAdministrator")]` | `ReceptionistAppointmentController.cs`, `AppointmentsController.cs:66` | `COMPLETE` |
| **Receptionist**| Tiếp nhận lịch hẹn | Từ chối lịch hẹn | `[Authorize(Roles = "Receptionist")]` | `[Authorize(Roles = "Receptionist,SystemAdministrator")]` | `ReceptionistAppointmentController.cs`, `AppointmentsController.cs:73` | `COMPLETE` |
| **Receptionist**| Bàn đón tiếp | Check-in ca hôm nay | `[Authorize(Roles = "Receptionist")]` | `[Authorize(Roles = "Receptionist,SystemAdministrator")]` | `ReceptionistAppointmentController.cs`, `AppointmentsController.cs:88` | `COMPLETE` |
| **Receptionist**| Bàn đón tiếp | Đánh dấu No-Show | `[Authorize(Roles = "Receptionist")]` | `[Authorize(Roles = "Receptionist,SystemAdministrator")]` | `ReceptionistAppointmentController.cs`, `AppointmentsController.cs:95` | `COMPLETE` |
| **Receptionist**| Thu ngân viện phí | Tạo hóa đơn thanh toán | `[Authorize(Roles = "Receptionist")]` | `[Authorize(Roles = "Receptionist,SystemAdministrator")]` | `ReceptionistBillingController.cs`, `BillingController.cs:21` | `COMPLETE` |
| **Receptionist**| Thu ngân viện phí | Xác nhận nhận tiền & In | `[Authorize(Roles = "Receptionist")]` | `[Authorize(Roles = "Receptionist,SystemAdministrator")]` | `ReceptionistBillingController.cs`, `BillingController.cs:28` | `COMPLETE` |
| **DeptManager** | Duyệt lịch hẹn | Xác nhận & gán Bác sĩ | `[Authorize(Roles = "DepartmentManager")]`| `[Authorize(Roles = "DepartmentManager,SystemAdministrator")]`| `DepartmentAppointmentController.cs`, `AppointmentsController.cs:127` | `COMPLETE` |
| **DeptManager** | Duyệt lịch hẹn | Đề xuất đổi lịch/Bác sĩ | `[Authorize(Roles = "DepartmentManager")]`| `[Authorize(Roles = "DepartmentManager,SystemAdministrator")]`| `DepartmentAppointmentController.cs`, `AppointmentsController.cs:134` | `COMPLETE` |
| **DeptManager** | Lịch làm việc | Xếp ca trực Bác sĩ | `[Authorize(Roles = "DepartmentManager")]`| `[Authorize(Roles = "DepartmentManager,SystemAdministrator")]`| `DepartmentScheduleController.cs`, `DentistScheduleController.cs:40` | `COMPLETE` |
| **Dentist** | Ca khám hôm nay | Bấm Vào Khám (`InService`)| `[Authorize(Roles = "Dentist")]` | `[Authorize(Roles = "Dentist,SystemAdministrator")]` | `DentistAppointmentController.cs`, `AppointmentsController.cs:155` | `COMPLETE` |
| **Dentist** | Ca khám hôm nay | Chọn / Đổi ghế điều trị| `[Authorize(Roles = "Dentist")]` | `[Authorize(Roles = "Dentist,SystemAdministrator")]` | `DentistAppointmentController.cs`, `AppointmentsController.cs:175` | `COMPLETE` |
| **Dentist** | Khám lâm sàng | Cập nhật sơ đồ răng FDI | `[Authorize(Roles = "Dentist")]` | `[Authorize(Roles = UserRole.Dentist)]` | `DentistMedicalRecordController.cs`, `MedicalRecordController.cs:44` | `COMPLETE` |
| **Dentist** | Khám lâm sàng | Tải lên phim X-quang | `[Authorize(Roles = "Dentist")]` | `[Authorize(Roles = "Dentist,SystemAdministrator,DepartmentManager")]` | `DentistMedicalRecordController.cs`, `MedicalRecordController.cs:64` | `COMPLETE` |
| **Dentist** | Điều trị | Kê đơn thuốc điện tử | `[Authorize(Roles = "Dentist")]` | `[Authorize(Roles = UserRole.Dentist)]` | `DentistPrescriptionController.cs`, `PrescriptionController.cs:44` | `COMPLETE` |
| **Dentist** | Ca khám hôm nay | Hoàn tất ca khám | `[Authorize(Roles = "Dentist")]` | `[Authorize(Roles = "Dentist,SystemAdministrator")]` | `DentistAppointmentController.cs`, `AppointmentsController.cs:162` | `COMPLETE` |
| **Admin** | Người dùng | Khóa / Mở tài khoản | `[Authorize(Roles = "SystemAdministrator")]` | `[Authorize(Roles = UserRole.SystemAdministrator)]` | `AdminUserController.cs`, `AdminController.cs:142` | `COMPLETE` |
| **Admin** | Danh mục | Quản lý khoa phòng/ghế| `[Authorize(Roles = "SystemAdministrator")]` | `[Authorize(Roles = UserRole.SystemAdministrator)]` | `AdminRoomChairController.cs`, `AdminController.cs:79` | `COMPLETE` |
| **Admin** | Báo cáo | Báo cáo doanh thu | `[Authorize(Roles = "SystemAdministrator")]` | `[Authorize(Roles = UserRole.SystemAdministrator)]` | `AdminReportController.cs`, `ReportController.cs:21` | `COMPLETE` |

---

## 4. ĐÁNH GIÁ NGUY CƠ BẢO MẬT: TRUY CẬP TRÁI PHÉP, IDOR & LEO THANG ĐẶC QUYỀN

### 4.1. Kiểm tra Lỗ hổng Tham chiếu Đối tượng Trực tiếp không An toàn (IDOR - Insecure Direct Object References)
- **Lịch khám (`Appointments`):**
  - Trong phương thức `GetAppointmentDetailAsync`: Backend kiểm tra `if (currentRole == UserRole.Patient && appointment.PatientUserId != currentUserId) return Fail("Không có quyền");`.
  - Trong phương thức `PatientCancelAsync`, `PatientWithdrawAsync`, `PatientRescheduleAsync`: Đều có bước kiểm tra bắt buộc `if (appointment.PatientUserId != patientUserId) return Fail("Bạn không có quyền...");`.
  - **Kết luận:** Đã được phòng chống IDOR ở tầng Service đối với vai trò Bệnh nhân.
- **Hồ sơ bệnh án (`DentalMedicalRecord` & `Odontogram`):**
  - Bệnh nhân chỉ có thể gọi endpoint `/api/medical-records/my` sử dụng claim `UserId` lấy trực tiếp từ JWT Token. Endpoint `/api/medical-records/patient/{id}` chỉ mở cho các vai trò nhân viên y tế (`Dentist`, `DepartmentManager`, `Receptionist`, `SystemAdministrator`).
- **Phân công bác sĩ điều trị:**
  - Bác sĩ chỉ được phép bắt đầu ca khám hoặc chọn ghế nếu `appointment.AssignedDentistUserId == currentUserId`. Nếu Bác sĩ A truyền ID ca khám của Bác sĩ B vào request, hệ thống trả về lỗi `"Bạn không phải là bác sĩ được phân công cho ca khám này."`.

### 4.2. Kiểm tra Leo thang đặc quyền (Privilege Escalation)
- **Tầng Web MVC:** Toàn bộ các controller theo phân quyền đều kiểm tra Cookie Claim `ClaimsIdentity.Role`. Nếu người dùng đăng nhập với vai trò `Patient` cố tình gõ URL `/Admin/Dashboard`, middleware của Cookie Auth tự động bắt và chuyển hướng về `/Account/AccessDenied`.
- **Tầng Web API:** Tất cả các endpoint đều có thuộc tính `[Authorize(Roles = ...)]`. Ngay cả khi kẻ tấn công can thiệp vào request HTTP gửi thẳng tới Web API, middleware JWT của ASP.NET Core sẽ giải mã chữ ký khóa bí mật đối xứng (`SymmetricSecurityKey`) và chặn với mã lỗi `403 Forbidden` nếu Role claim trong payload token không hợp lệ.
- **Cơ chế Token Refresh an toàn:** Endpoint `POST /api/auth/refresh` yêu cầu cả `RefreshToken` hợp lệ và chưa hết hạn, đồng thời kiểm tra tài khoản trong database không ở trạng thái `Locked` hoặc `Inactive` mới cấp phát `AccessToken` mới.
