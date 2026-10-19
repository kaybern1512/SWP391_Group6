# PROJECT FILE MAP — DENTAL CLINIC MANAGEMENT SYSTEM
> **Repository:** `SWP391_Group6` (DentalClinic)  
> **Mục đích:** Bản đồ tra cứu file chính xác 100% theo từng phân hệ giúp lập trình viên và AI biết chính xác cần đọc, sửa hoặc kiểm thử file nào khi xử lý một vấn đề cụ thể.

---

## MỤC LỤC CÁC PHÂN HỆ
1. [Module 1: Authentication, Authorization & Identity](#module-1-authentication-authorization--identity)
2. [Module 2: User Profiles & Avatar Uploads](#module-2-user-profiles--avatar-uploads)
3. [Module 3: Clinic Catalog & Master Data (Khoa, Dịch vụ, Phòng, Ghế)](#module-3-clinic-catalog--master-data)
4. [Module 4: Dentist Working Shifts & Availability (Lịch trực & Nghỉ phép)](#module-4-dentist-working-shifts--availability)
5. [Module 5: Appointments & Booking Flow (Đặt lịch & Điều phối 4 vai trò)](#module-5-appointments--booking-flow)
6. [Module 6: Dental Medical Records & Odontogram (Hồ sơ bệnh án & Sơ đồ răng)](#module-6-dental-medical-records--odontogram)
7. [Module 7: Treatment Plans & Clinical Sessions (Phác đồ & Buổi điều trị)](#module-7-treatment-plans--clinical-sessions)
8. [Module 8: Electronic Prescriptions (Đơn thuốc điện tử)](#module-8-electronic-prescriptions)
9. [Module 9: Patient Visit Feedback (Đánh giá ca khám)](#module-9-patient-visit-feedback)
10. [Module 10: System Notifications (Thông báo In-App & Email)](#module-10-system-notifications)
11. [Module 11: Admin Console, Audit Logs & Analytics Reports](#module-11-admin-console-audit-logs--analytics-reports)
12. [Module 12: Billing & Payments (Hóa đơn viện phí - Trạng thái chờ hoàn thiện)](#module-12-billing--payments)
13. [Cross-Cutting Infrastructure & Handlers](#cross-cutting-infrastructure--handlers)
14. [Automated Test Suites Map](#automated-test-suites-map)

---

## MODULE 1: AUTHENTICATION, AUTHORIZATION & IDENTITY

### Mục đích nghiệp vụ
Đăng ký bệnh nhân mới, xác thực OTP qua email, đăng nhập bằng mật khẩu hoặc Google SSO, cấp và làm mới JWT token, chống brute-force khóa tài khoản, quên mật khẩu và đổi mật khẩu.

### Files Backend liên quan
- **Controller:**  
  `BE/DentalClinic.Api/Controllers/AuthController.cs`
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/IAuthService.cs`  
  `BE/DentalClinic.Application/Common/Interfaces/IJwtTokenService.cs`  
  `BE/DentalClinic.Application/Common/Interfaces/IOtpService.cs`  
  `BE/DentalClinic.Application/Common/Interfaces/IPasswordHasherService.cs`  
  `BE/DentalClinic.Application/Common/Interfaces/IGoogleAuthService.cs`  
  `BE/DentalClinic.Application/Features/Auth/DTOs/LoginDtos.cs`  
  `BE/DentalClinic.Application/Features/Auth/DTOs/RegisterDtos.cs`  
  `BE/DentalClinic.Application/Features/Auth/DTOs/PasswordDtos.cs`  
  `BE/DentalClinic.Application/Features/Auth/DTOs/TokenDtos.cs`  
  `BE/DentalClinic.Application/Validators/AuthValidators.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/AuthService.cs`  
  `BE/DentalClinic.Infrastructure/Services/JwtTokenService.cs`  
  `BE/DentalClinic.Infrastructure/Services/OtpService.cs`  
  `BE/DentalClinic.Infrastructure/Services/PasswordHasherService.cs`  
  `BE/DentalClinic.Infrastructure/Services/GoogleAuthService.cs`  
  `BE/DentalClinic.Infrastructure/Services/EmailService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/UserAccount.cs`  
  `BE/DentalClinic.Domain/Enums/UserRole.cs`  
  `BE/DentalClinic.Domain/Enums/AccountStatus.cs`

### Files Frontend liên quan
- **Controller:**  
  `FE/DentalClinic.Web/Controllers/AccountController.cs`
- **Services & Handlers:**  
  `FE/DentalClinic.Web/Services/IAuthApiService.cs`  
  `FE/DentalClinic.Web/Services/AuthApiService.cs`  
  `FE/DentalClinic.Web/Services/ITokenService.cs`  
  `FE/DentalClinic.Web/Services/TokenService.cs`  
  `FE/DentalClinic.Web/Services/IAuthCookieService.cs`  
  `FE/DentalClinic.Web/Services/AuthCookieService.cs`  
  `FE/DentalClinic.Web/Handlers/ApiAuthorizationHandler.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Account/Login.cshtml`  
  `FE/DentalClinic.Web/Views/Account/Register.cshtml`  
  `FE/DentalClinic.Web/Views/Account/VerifyEmail.cshtml`  
  `FE/DentalClinic.Web/Views/Account/ForgotPassword.cshtml`  
  `FE/DentalClinic.Web/Views/Account/ResetPassword.cshtml`  
  `FE/DentalClinic.Web/Views/Account/ChangePassword.cshtml`  
  `FE/DentalClinic.Web/Views/Account/AccessDenied.cshtml`  
  `FE/DentalClinic.Web/Views/Shared/_AuthLayout.cshtml`

### API Endpoints
- `POST /api/auth/register`
- `POST /api/auth/verify-email`
- `POST /api/auth/resend-verification`
- `POST /api/auth/login`
- `POST /api/auth/google`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`
- `POST /api/auth/change-password`

### Database Tables
- `UserAccounts`, `PatientProfiles`, `AuditLogs`

### Tests liên quan
- `BE/DentalClinic.Tests/Auth/AuthServiceTests.cs`
- `BE/DentalClinic.Tests/Auth/JwtTokenServiceTests.cs`
- `BE/DentalClinic.Tests/Auth/OtpServiceTests.cs`
- `BE/DentalClinic.Tests/Auth/PasswordHasherServiceTests.cs`
- `BE/DentalClinic.Tests/Auth/ValidationTests.cs`
- `FE/DentalClinic.Web.Tests/Controllers/AccountControllerTests.cs`
- `FE/DentalClinic.Web.Tests/Services/AuthApiServiceTests.cs`
- `FE/DentalClinic.Web.Tests/Services/TokenServiceTests.cs`

---

## MODULE 2: USER PROFILES & AVATAR UPLOADS

### Mục đích nghiệp vụ
Xem và cập nhật thông tin hồ sơ cá nhân (bệnh nhân hoặc nhân viên), upload ảnh đại diện lên Cloudinary (hoặc fallback lưu local wwwroot).

### Files Backend liên quan
- **Controller:**  
  `BE/DentalClinic.Api/Controllers/ProfileController.cs`
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/IProfileService.cs`  
  `BE/DentalClinic.Application/Common/Interfaces/IImageStorageService.cs`  
  `BE/DentalClinic.Application/Features/Profile/DTOs/ProfileDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/ProfileService.cs`  
  `BE/DentalClinic.Infrastructure/Services/CloudinaryImageStorageService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/PatientProfile.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/StaffProfile.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/DentistProfile.cs`

### Files Frontend liên quan
- **Controller:**  
  `FE/DentalClinic.Web/Controllers/ProfileController.cs`
- **Service:**  
  `FE/DentalClinic.Web/Services/IProfileApiService.cs`  
  `FE/DentalClinic.Web/Services/ProfileApiService.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Profile/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Profile/Edit.cshtml`

### API Endpoints
- `GET /api/profile/me`
- `PUT /api/profile/me`
- `POST /api/profile/avatar`

### Database Tables
- `UserAccounts`, `PatientProfiles`, `StaffProfiles`, `DentistProfiles`

### Tests liên quan
- `BE/DentalClinic.Tests/Profile/ProfileServiceTests.cs`
- `BE/DentalClinic.Tests/Profile/CloudinaryImageStorageServiceTests.cs`
- `FE/DentalClinic.Web.Tests/Controllers/ProfileControllerTests.cs`
- `FE/DentalClinic.Web.Tests/Services/ProfileApiServiceTests.cs`

---

## MODULE 3: CLINIC CATALOG & MASTER DATA

### Mục đích nghiệp vụ
Quản trị danh mục chuyên khoa, dịch vụ bảng giá, phòng khám và ghế nha khoa điều trị.

### Files Backend liên quan
- **Controllers:**  
  `BE/DentalClinic.Api/Controllers/AdminController.cs` (CRUD)  
  `BE/DentalClinic.Api/Controllers/BookingController.cs` (Read public)
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/IAdminService.cs`  
  `BE/DentalClinic.Application/Common/Interfaces/IBookingService.cs`  
  `BE/DentalClinic.Application/Features/Admin/DTOs/AdminDtos.cs`  
  `BE/DentalClinic.Application/Features/Booking/DTOs/BookingDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/AdminService.cs`  
  `BE/DentalClinic.Infrastructure/Services/BookingService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/ClinicProfile.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/Department.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/DepartmentService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/Room.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/DentalChair.cs`

### Files Frontend liên quan
- **Controllers:**  
  `FE/DentalClinic.Web/Controllers/Admin/AdminDepartmentController.cs`  
  `FE/DentalClinic.Web/Controllers/Admin/AdminServiceController.cs`  
  `FE/DentalClinic.Web/Controllers/Admin/AdminRoomChairController.cs`
- **Service:**  
  `FE/DentalClinic.Web/Services/IAdminApiService.cs`  
  `FE/DentalClinic.Web/Services/AdminApiService.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Admin/Departments/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Admin/Services/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Admin/RoomChair/Index.cshtml`

### API Endpoints
- `GET|POST /api/admin/departments`, `PUT /api/admin/departments/{id}`, `POST /api/admin/departments/{id}/toggle-status`
- `GET|POST /api/admin/services`, `PUT /api/admin/services/{id}`, `POST /api/admin/services/{id}/toggle-status`
- `GET|POST /api/admin/rooms`, `PUT /api/admin/rooms/{id}`, `POST /api/admin/rooms/{id}/status`
- `GET|POST /api/admin/chairs`, `PUT /api/admin/chairs/{id}`, `POST /api/admin/chairs/{id}/status`
- `GET /api/booking/departments`, `GET /api/booking/departments/{id}/services`

### Database Tables
- `ClinicProfile`, `Departments`, `DepartmentServices`, `Rooms`, `DentalChairs`

### Tests liên quan
- `BE/DentalClinic.Tests/Admin/AdminServiceTests.cs`
- `FE/DentalClinic.Web.Tests/Controllers/AdminWebControllerTests.cs`

---

## MODULE 4: DENTIST WORKING SHIFTS & AVAILABILITY

### Mục đích nghiệp vụ
Quản lý ca trực của bác sĩ trong 60 ngày tới, đăng ký trạng thái bận đột xuất hoặc nghỉ phép, tính toán khung giờ trống khả dụng (Slot Engine).

### Files Backend liên quan
- **Controllers:**  
  `BE/DentalClinic.Api/Controllers/DentistScheduleController.cs`  
  `BE/DentalClinic.Api/Controllers/DentistAvailabilityController.cs`  
  `BE/DentalClinic.Api/Controllers/BookingController.cs` (Available Slots)
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/IDentistScheduleService.cs`  
  `BE/DentalClinic.Application/Features/DentistSchedule/DTOs/DentistScheduleDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/DentistScheduleService.cs`  
  `BE/DentalClinic.Infrastructure/Services/BookingService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/DentistWorkSchedule.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/DentistAvailability.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/DepartmentWorkSchedule.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/DentistDepartment.cs`

### Files Frontend liên quan
- **Controllers:**  
  `FE/DentalClinic.Web/Controllers/Dentist/DentistScheduleController.cs`  
  `FE/DentalClinic.Web/Controllers/Department/DepartmentScheduleController.cs`
- **Service:**  
  `FE/DentalClinic.Web/Services/IDentistScheduleApiService.cs`  
  `FE/DentalClinic.Web/Services/DentistScheduleApiService.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Dentist/Schedule/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Dentist/Availability/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Department/Schedule/Index.cshtml`

### API Endpoints
- `GET|POST /api/DentistSchedule`, `GET /api/DentistSchedule/my`, `PUT /api/DentistSchedule/{id}`, `PUT /api/DentistSchedule/{id}/cancel`
- `GET|POST /api/DentistAvailability`, `GET /api/DentistAvailability/my`, `PUT|DELETE /api/DentistAvailability/{id}`
- `GET /api/booking/available-slots`

### Database Tables
- `DentistWorkSchedules`, `DentistAvailability`, `DepartmentWorkSchedules`, `DentistDepartments`

### Tests liên quan
- `BE/DentalClinic.Tests/DentistSchedule/DentistScheduleServiceTests.cs`
- `FE/DentalClinic.Web.Tests/Controllers/DentistScheduleWebControllerTests.cs`

---

## MODULE 5: APPOINTMENTS & BOOKING FLOW

### Mục đích nghiệp vụ
Quy trình trung tâm 4 vai trò: Bệnh nhân đặt lịch trực tuyến 6 bước $\rightarrow$ Lễ tân tiếp nhận chuyển Trưởng khoa $\rightarrow$ Trưởng khoa duyệt & phân bổ Bác sĩ, Phòng, Ghế (hoặc đề xuất đổi lịch) $\rightarrow$ Lễ tân Check-in bệnh nhân $\rightarrow$ Bác sĩ tiếp nhận thăm khám.

### Files Backend liên quan
- **Controller:**  
  `BE/DentalClinic.Api/Controllers/AppointmentsController.cs`
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/IAppointmentService.cs`  
  `BE/DentalClinic.Application/Features/Appointments/DTOs/AppointmentDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/Appointment.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/AppointmentChangeProposal.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/AppointmentStatusHistory.cs`  
  `BE/DentalClinic.Domain/Enums/AppointmentStatus.cs`  
  `BE/DentalClinic.Domain/Enums/AppointmentEnums.cs`

### Files Frontend liên quan
- **Controllers:**  
  `FE/DentalClinic.Web/Controllers/Patient/PatientAppointmentController.cs`  
  `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistAppointmentController.cs`  
  `FE/DentalClinic.Web/Controllers/Department/DepartmentAppointmentController.cs`  
  `FE/DentalClinic.Web/Controllers/Dentist/DentistAppointmentController.cs`
- **Service:**  
  `FE/DentalClinic.Web/Services/IAppointmentApiService.cs`  
  `FE/DentalClinic.Web/Services/AppointmentApiService.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Patient/Appointment/Book.cshtml`  
  `FE/DentalClinic.Web/Views/Patient/Appointment/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Patient/Appointment/Detail.cshtml`  
  `FE/DentalClinic.Web/Views/Patient/Appointment/Success.cshtml`  
  `FE/DentalClinic.Web/Views/Receptionist/Appointments/Requests.cshtml`  
  `FE/DentalClinic.Web/Views/Receptionist/Appointments/TodayCheckIn.cshtml`  
  `FE/DentalClinic.Web/Views/Department/Appointments/Requests.cshtml`  
  `FE/DentalClinic.Web/Views/Department/Appointments/Review.cshtml`  
  `FE/DentalClinic.Web/Views/Dentist/Appointments/Today.cshtml`

### API Endpoints
- Bệnh nhân: `POST|GET /api/patient/appointments`, `POST /api/patient/appointments/{id}/withdraw`, `POST /api/patient/appointments/{id}/cancel`, `GET /api/patient/appointments/{id}/proposal`, `POST /api/patient/appointments/{id}/proposal/accept|reject`
- Lễ tân: `GET /api/receptionist/appointment-requests`, `POST /api/receptionist/appointments/{id}/forward|reject`, `GET /api/receptionist/today-appointments`, `POST /api/receptionist/appointments/{id}/check-in`
- Trưởng khoa: `GET /api/department/appointment-requests`, `GET /api/department/appointments/{id}/available-dentists`, `GET /api/department/available-resources`, `POST /api/department/appointments/{id}/confirm`, `POST /api/department/appointments/{id}/propose-change`
- Bác sĩ: `GET /api/dentist/today-appointments`, `GET /api/dentist/appointments`

### Database Tables
- `Appointments`, `AppointmentChangeProposals`, `AppointmentStatusHistories`, `Rooms`, `DentalChairs`, `DentistProfiles`

### Tests liên quan
- `BE/DentalClinic.Tests/Appointments/AppointmentFlowTests.cs` (18 scenarios)
- `FE/DentalClinic.Web.Tests/Controllers/AppointmentWebControllerTests.cs`

---

## MODULE 6: DENTAL MEDICAL RECORDS & ODONTOGRAM

### Mục đích nghiệp vụ
Quản lý hồ sơ bệnh án điện tử, ghi nhận phiếu khám lâm sàng (triệu chứng, chẩn đoán), tương tác sơ đồ 32 răng FDI vĩnh viễn, lưu trữ hình ảnh X-quang.

### Files Backend liên quan
- **Controller:**  
  `BE/DentalClinic.Api/Controllers/MedicalRecordController.cs`
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/IMedicalRecordService.cs`  
  `BE/DentalClinic.Application/Features/MedicalRecords/DTOs/MedicalRecordDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/MedicalRecordService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/DentalMedicalRecord.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/ClinicalEntry.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/OdontogramEntry.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/ImagingRecord.cs`

### Files Frontend liên quan
- **Controllers:**  
  `FE/DentalClinic.Web/Controllers/Dentist/DentistMedicalRecordController.cs`  
  `FE/DentalClinic.Web/Controllers/Patient/PatientMedicalRecordController.cs`
- **Service:**  
  `FE/DentalClinic.Web/Services/IMedicalRecordApiService.cs`  
  `FE/DentalClinic.Web/Services/MedicalRecordApiService.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Dentist/MedicalRecords/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Patient/MedicalRecords/Index.cshtml`

### API Endpoints
- `GET /api/medical-records/patient/{patientUserId}`
- `GET /api/medical-records/my`
- `POST /api/medical-records/entry`
- `GET /api/medical-records/patients`
- `GET /api/medical-records/patient/{patientUserId}/odontogram`

### Database Tables
- `DentalMedicalRecords`, `ClinicalEntries`, `OdontogramEntries`, `ImagingRecords`, `PatientProfiles`

### Tests liên quan
- *Chưa có test tự động cho module này trong backend test suite (Cần bổ sung).*

---

## MODULE 7: TREATMENT PLANS & CLINICAL SESSIONS

### Mục đích nghiệp vụ
Lập phác đồ điều trị đa bước (`TreatmentPlans`), chỉ định dịch vụ can thiệp từng bước (`TreatmentPlanItems`), ghi nhận buổi điều trị thực tế (`TreatmentSessions`) và các dịch vụ hoàn thành (`PerformedServices`).

### Files Backend liên quan
- **Controller:**  
  `BE/DentalClinic.Api/Controllers/TreatmentController.cs`
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/ITreatmentService.cs`  
  `BE/DentalClinic.Application/Features/Treatments/DTOs/TreatmentDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/TreatmentService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/TreatmentPlan.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/TreatmentPlanItem.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/TreatmentSession.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/PerformedService.cs`

### Files Frontend liên quan
- **Controllers:**  
  `FE/DentalClinic.Web/Controllers/Dentist/DentistTreatmentPlanController.cs`  
  `FE/DentalClinic.Web/Controllers/Department/DepartmentTreatmentController.cs`  
  `FE/DentalClinic.Web/Controllers/Patient/PatientTreatmentPlanController.cs`
- **Service:**  
  `FE/DentalClinic.Web/Services/ITreatmentApiService.cs`  
  `FE/DentalClinic.Web/Services/TreatmentApiService.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Dentist/TreatmentPlans/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Dentist/TreatmentPlans/Create.cshtml`  
  `FE/DentalClinic.Web/Views/Dentist/TreatmentPlans/Detail.cshtml`  
  `FE/DentalClinic.Web/Views/Dentist/TreatmentPlans/Sessions.cshtml`  
  `FE/DentalClinic.Web/Views/Department/Treatment/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Department/Treatment/Detail.cshtml`  
  `FE/DentalClinic.Web/Views/Patient/TreatmentPlans/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Patient/TreatmentPlans/Detail.cshtml`

### API Endpoints
- `GET|POST /api/treatments/plans`, `GET /api/treatments/plans/{id}`, `POST /api/treatments/plans/{id}/items`, `PUT /api/treatments/plans/{id}/status`
- `PUT /api/treatments/items/{itemId}/status`, `POST /api/treatments/items/{itemId}/assign-dentist`
- `GET|POST /api/treatments/sessions`, `GET /api/treatments/eligible-phases`
- `GET /api/treatments/department/progress`
- `GET /api/treatments/patient/my-plans`
- `GET /api/treatments/options/patients|services|dentists`

### Database Tables
- `TreatmentPlans`, `TreatmentPlanItems`, `TreatmentSessions`, `PerformedServices`, `DepartmentServices`

### Tests liên quan
- `BE/DentalClinic.Tests/Treatments/TreatmentServiceTests.cs`
- `FE/DentalClinic.Web.Tests/Controllers/TreatmentWebControllerTests.cs`

---

## MODULE 8: ELECTRONIC PRESCRIPTIONS

### Mục đích nghiệp vụ
Bác sĩ kê đơn thuốc điện tử cho bệnh nhân gắn liền với ca khám/buổi điều trị, in đơn thuốc chuẩn phòng khám, bệnh nhân tra cứu lịch sử dùng thuốc.

### Files Backend liên quan
- **Controller:**  
  `BE/DentalClinic.Api/Controllers/PrescriptionController.cs`
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/IPrescriptionService.cs`  
  `BE/DentalClinic.Application/Features/Prescriptions/DTOs/PrescriptionDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/PrescriptionService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/Prescription.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/PrescriptionItem.cs`

### Files Frontend liên quan
- **Controllers:**  
  `FE/DentalClinic.Web/Controllers/Dentist/DentistPrescriptionController.cs`  
  `FE/DentalClinic.Web/Controllers/Patient/PatientPrescriptionController.cs`
- **Service:**  
  `FE/DentalClinic.Web/Services/IPrescriptionApiService.cs`  
  `FE/DentalClinic.Web/Services/PrescriptionApiService.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Dentist/Prescriptions/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Dentist/Prescriptions/Print.cshtml`  
  `FE/DentalClinic.Web/Views/Patient/Prescriptions/Index.cshtml`

### API Endpoints
- `GET /api/prescriptions/dentist`
- `GET /api/prescriptions/dentist/eligible-appointments`
- `GET /api/prescriptions/my`
- `GET /api/prescriptions/{id}`
- `POST /api/prescriptions`

### Database Tables
- `Prescriptions`, `PrescriptionItems`, `TreatmentSessions`, `Appointments`

### Tests liên quan
- *Chưa có test tự động cho module này trong backend test suite (Cần bổ sung).*

---

## MODULE 9: PATIENT VISIT FEEDBACK

### Mục đích nghiệp vụ
Bệnh nhân gửi đánh giá chất lượng ca khám (1-5 sao bác sĩ, 1-5 sao cơ sở vật chất), Bác sĩ và Trưởng khoa xem thống kê điểm hài lòng.

### Files Backend liên quan
- **Controller:**  
  `BE/DentalClinic.Api/Controllers/FeedbackController.cs`
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/IFeedbackService.cs`  
  `BE/DentalClinic.Application/Features/Feedback/DTOs/FeedbackDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/FeedbackService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/VisitFeedback.cs`

### Files Frontend liên quan
- **Views:**  
  `FE/DentalClinic.Web/Views/Patient/Appointment/Feedback.cshtml`  
  `FE/DentalClinic.Web/Views/Dentist/Feedback/Index.cshtml`
- **Service:**  
  `FE/DentalClinic.Web/Services/IFeedbackApiService.cs`  
  `FE/DentalClinic.Web/Services/FeedbackApiService.cs`

### API Endpoints
- `POST /api/Feedback`
- `GET /api/Feedback/appointment/{appointmentId}`
- `GET /api/Feedback/dentist/{dentistUserId}`
- `GET /api/Feedback/my`
- `GET /api/Feedback`

### Database Tables
- `VisitFeedback`, `Appointments`

### Tests liên quan
- `BE/DentalClinic.Tests/Feedback/FeedbackServiceTests.cs`

---

## MODULE 10: SYSTEM NOTIFICATIONS

### Mục đích nghiệp vụ
Tạo thông báo tự động (khi đặt lịch, phê duyệt, check-in, hủy lịch), hiển thị chuông thông báo In-App và số lượng chưa đọc, đánh dấu đã đọc.

### Files Backend liên quan
- **Controller:**  
  `BE/DentalClinic.Api/Controllers/NotificationController.cs`
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/INotificationService.cs`  
  `BE/DentalClinic.Application/Features/Notifications/DTOs/NotificationDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/NotificationService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/Notification.cs`

### Files Frontend liên quan
- **Controller:**  
  `FE/DentalClinic.Web/Controllers/NotificationController.cs`
- **Service:**  
  `FE/DentalClinic.Web/Services/INotificationApiService.cs`  
  `FE/DentalClinic.Web/Services/NotificationApiService.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Notification/Index.cshtml`

### API Endpoints
- `GET /api/notifications`
- `GET /api/notifications/unread-count`
- `POST /api/notifications/{id}/read`
- `POST /api/notifications/read-all`
- `DELETE /api/notifications/{id}`
- `POST /api/notifications`

### Database Tables
- `Notifications`, `UserAccounts`, `Appointments`

### Tests liên quan
- `BE/DentalClinic.Tests/Notifications/NotificationServiceTests.cs`
- `FE/DentalClinic.Web.Tests/Controllers/NotificationWebControllerTests.cs`

---

## MODULE 11: ADMIN CONSOLE, AUDIT LOGS & ANALYTICS REPORTS

### Mục đích nghiệp vụ
Bảng điều khiển Quản trị viên, thống kê doanh thu, tỷ lệ hoàn thành ca khám, quản lý tài khoản người dùng (khóa/mở), tra cứu nhật ký kiểm toán.

### Files Backend liên quan
- **Controller:**  
  `BE/DentalClinic.Api/Controllers/AdminController.cs`
- **Application Interfaces & DTOs:**  
  `BE/DentalClinic.Application/Common/Interfaces/IAdminService.cs`  
  `BE/DentalClinic.Application/Common/Interfaces/IAuditLogService.cs`  
  `BE/DentalClinic.Application/Features/Admin/DTOs/AdminDtos.cs`
- **Infrastructure Services & Entities:**  
  `BE/DentalClinic.Infrastructure/Services/AdminService.cs`  
  `BE/DentalClinic.Infrastructure/Services/AuditLogService.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/AuditLog.cs`

### Files Frontend liên quan
- **Controllers:**  
  `FE/DentalClinic.Web/Controllers/Admin/AdminDashboardController.cs`  
  `FE/DentalClinic.Web/Controllers/Admin/AdminUserController.cs`  
  `FE/DentalClinic.Web/Controllers/Admin/AdminAuditLogController.cs`
- **Service:**  
  `FE/DentalClinic.Web/Services/IAdminApiService.cs`  
  `FE/DentalClinic.Web/Services/AdminApiService.cs`
- **Views:**  
  `FE/DentalClinic.Web/Views/Admin/Dashboard.cshtml`  
  `FE/DentalClinic.Web/Views/Admin/Users/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Admin/Reports/Index.cshtml`  
  `FE/DentalClinic.Web/Views/Admin/AuditLogs/Index.cshtml`

### API Endpoints
- `GET /api/admin/dashboard-stats`
- `GET /api/admin/reports/revenue`
- `GET /api/admin/reports/appointments`
- `GET /api/admin/audit-logs`
- `GET /api/admin/users`
- `POST /api/admin/users/{userId}/toggle-lock`

### Database Tables
- `AuditLogs`, `UserAccounts`, `StaffProfiles`, `PatientProfiles`, `Appointments`, `Rooms`, `DentalChairs`

### Tests liên quan
- `BE/DentalClinic.Tests/Admin/AdminServiceTests.cs`
- `FE/DentalClinic.Web.Tests/Controllers/AdminWebControllerTests.cs`

---

## MODULE 12: BILLING & PAYMENTS (CHỜ HOÀN THIỆN)

### Mục đích nghiệp vụ
Tạo hóa đơn từ các dịch vụ đã thực hiện (`PerformedServices`) và dịch vụ lịch hẹn (`Appointments`), quản lý thanh toán tiền mặt / ngân hàng / online, theo dõi công nợ viện phí.

### Files Backend
- **Entities có sẵn:**  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/Invoice.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/InvoiceItem.cs`  
  `BE/DentalClinic.Infrastructure/Persistence/Entities/Payment.cs`
- **Chưa có (Cần tạo khi thực hiện Task 4):**  
  `BE/DentalClinic.Application/Features/Billing/DTOs/`  
  `BE/DentalClinic.Application/Common/Interfaces/IBillingService.cs`  
  `BE/DentalClinic.Infrastructure/Services/BillingService.cs`  
  `BE/DentalClinic.Api/Controllers/BillingController.cs`

### Files Frontend
- **Chưa có (Cần tạo khi thực hiện Task 4):**  
  `FE/DentalClinic.Web/Services/IBillingApiService.cs`  
  `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistBillingController.cs`  
  `FE/DentalClinic.Web/Views/Receptionist/Billing/`

### Database Tables
- `Invoices`, `InvoiceItems`, `Payments`, `Appointments`, `PerformedServices`

---

## CROSS-CUTTING INFRASTRUCTURE & HANDLERS

### Backend Cross-Cutting
- `BE/DentalClinic.Api/Program.cs`: Cấu hình Dependency Injection, JWT Bearer, CORS, Swagger, Exception Handler.
- `BE/DentalClinic.Api/Middleware/GlobalExceptionHandler.cs`: Bắt ngoại lệ toàn cục, trả về ApiResponse JSON chuẩn.
- `BE/DentalClinic.Infrastructure/DependencyInjection.cs`: Phương thức mở rộng `AddInfrastructureServices`.
- `BE/DentalClinic.Infrastructure/Persistence/DentalClinicDbContext.cs`: DbContext trung tâm với Fluent API cho 34 bảng.
- `BE/DentalClinic.Application/Common/Models/ApiResponse.cs`: Chuẩn hóa định dạng JSON trả về của toàn bộ API (`Success`, `Message`, `Data`, `Errors`).

### Frontend Cross-Cutting
- `FE/DentalClinic.Web/Program.cs`: Cấu hình Cookie Auth, Session, Typed HttpClients, pipeline xử lý HTTP.
- `FE/DentalClinic.Web/Handlers/ApiAuthorizationHandler.cs`: DelegatingHandler tự động đính kèm JWT Bearer và refresh token khi gặp 401.
- `FE/DentalClinic.Web/Services/TokenService.cs`: Quản lý lưu trữ/đọc/xóa Access & Refresh Tokens trong Session.
- `FE/DentalClinic.Web/Services/AuthCookieService.cs`: Quản lý Claims và tạo Cookie đăng nhập cho trình duyệt.
- `FE/DentalClinic.Web/Views/Shared/_Layout.cshtml`: Khung giao diện chính của ứng dụng.
- `FE/DentalClinic.Web/Views/Shared/_Sidebar.cshtml`: Thanh điều hướng bên trái phân chia động theo 5 vai trò.
- `FE/DentalClinic.Web/Views/Shared/_Navbar.cshtml`: Thanh tiêu đề phía trên kèm menu người dùng và chuông thông báo.
- `FE/DentalClinic.Web/Views/Shared/_UserMenu.cshtml`: Menu dropdown tài khoản (Hồ sơ, Đổi mật khẩu, Đăng xuất).

---

## AUTOMATED TEST SUITES MAP

### Backend Unit Tests (`BE/DentalClinic.Tests`) — 90 Tests
```
BE/DentalClinic.Tests/
├── Appointments/AppointmentFlowTests.cs            (18 tests: Trọn vẹn chu trình đặt lịch, xung đột tài nguyên)
├── Auth/AuthServiceTests.cs                       (13 tests: Đăng ký, đăng nhập, khóa tài khoản, OTP, Google)
├── Admin/AdminServiceTests.cs                     (10 tests: Quản trị khoa, dịch vụ, phòng, ghế, logs, báo cáo)
├── Auth/ValidationTests.cs                        (7 tests: FluentValidation rule tests)
├── DentistSchedule/DentistScheduleServiceTests.cs (7 tests: Lịch trực, trạng thái bận, nghỉ phép)
├── Feedback/FeedbackServiceTests.cs               (6 tests: Đánh giá ca khám, tính sao trung bình)
├── Notifications/NotificationServiceTests.cs      (5 tests: Tạo, đọc, xóa thông báo)
├── Profile/ProfileServiceTests.cs                 (5 tests: Xem, sửa hồ sơ cá nhân)
├── Auth/OtpServiceTests.cs                        (4 tests: Sinh OTP, băm HMAC-SHA256)
├── Auth/PasswordHasherServiceTests.cs             (4 tests: Băm & xác thực PBKDF2 V3)
├── Profile/CloudinaryImageStorageServiceTests.cs  (4 tests: Upload & validate ảnh đại diện)
├── Treatments/TreatmentServiceTests.cs            (4 tests: Phác đồ điều trị và các giai đoạn)
└── Auth/JwtTokenServiceTests.cs                   (3 tests: Sinh & giải mã JWT claims)
```

### Frontend Unit & Integration Tests (`FE/DentalClinic.Web.Tests`) — 83 Tests
```
FE/DentalClinic.Web.Tests/
├── Controllers/AccountControllerTests.cs           (14 tests: Đăng nhập, đăng ký, quên mật khẩu MVC)
├── Controllers/DentistScheduleWebControllerTests.cs(12 tests: Giao diện lịch trực và nghỉ phép bác sĩ)
├── Controllers/AdminWebControllerTests.cs          (9 tests: Điều hướng admin console)
├── Validation/ViewModelValidationTests.cs          (7 tests: DataAnnotations validation)
├── Controllers/ProfileControllerTests.cs          (6 tests: Xem & sửa hồ sơ người dùng)
├── Controllers/AppointmentWebControllerTests.cs   (5 tests: Đặt lịch, danh sách khám, check-in)
├── Controllers/DashboardWebControllerTests.cs     (4 tests: Dữ liệu dashboard 5 vai trò)
├── Controllers/NotificationWebControllerTests.cs  (4 tests: Danh sách và thao tác thông báo)
├── Controllers/TreatmentWebControllerTests.cs     (4 tests: Xem phác đồ và tiến độ điều trị)
├── Integration/SmokeTests.cs                      (4 tests: Khởi động ứng dụng MVC)
├── Services/AuthApiServiceTests.cs                (4 tests: Mock HttpClient gọi Auth API)
├── Services/ProfileApiServiceTests.cs             (4 tests: Mock HttpClient gọi Profile API)
├── Helpers/RoleRouteHelperTests.cs                (3 tests: Điều hướng theo role sau đăng nhập)
└── Services/TokenServiceTests.cs                  (3 tests: Lưu trữ token trong session)
```
