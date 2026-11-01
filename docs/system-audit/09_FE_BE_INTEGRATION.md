# KIỂM TOÁN TÍCH HỢP FRONTEND ↔ BACKEND (FE-BE INTEGRATION AUDIT)
**Tài liệu:** `09_FE_BE_INTEGRATION.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Mục tiêu:** Kiểm tra độ khớp 100% giữa giao diện người dùng và API Backend

---

## 1. MÔ HÌNH KẾT NỐI & QUY TẮC ĐỒNG BỘ GIAO THỨC

Frontend ASP.NET Core MVC kết nối với Backend ASP.NET Core Web API theo mô hình **Typed HttpClient kết hợp DelegatingHandler**:

1. **Chuẩn hóa Đường dẫn API (API Routing Conventions):**
   - Đặt lịch & Khám bệnh: `/api/patient/appointments`, `/api/receptionist/appointments`, `/api/department/appointments`, `/api/dentist/appointments`
   - Bệnh án & Sơ đồ răng: `/api/medical-records/...`
   - Kế hoạch điều trị: `/api/treatment/...`
   - Đơn thuốc: `/api/prescriptions/...`
   - Hóa đơn & Thu phí: `/api/billing/...`
   - Quản trị: `/api/admin/...`
2. **Bọc phản hồi chuẩn (Standardized ApiResponse Envelope):**
   Toàn bộ kết quả trả về từ Backend đều tuân thủ cấu trúc đồng nhất:
   ```json
   {
     "success": true,
     "message": "Thông điệp phản hồi bằng tiếng Việt",
     "data": { ... },
     "errors": [ ... ]
   }
   ```
   Frontend sử dụng lớp Generic `ApiResponse<T>` (`FE/DentalClinic.Web/Models/Api/ApiResponse.cs`) để giải tuần tự hóa (deserialize) an toàn.
3. **Cơ chế truyền Bearer Token:**
   Lớp `ApiAuthorizationHandler` tự động đọc token từ Session và gán vào header `Authorization: Bearer <token>`. Khi Backend trả về mã lỗi 401, handler tự động kích hoạt mutex lock để refresh token và thực thi lại request mà người dùng không bị gián đoạn.

---

## 2. MA TRẬN KIỂM TOÁN TÍCH HỢP TỪNG THAO TÁC NGHIỆP VỤ (INTEGRATION AUDIT TABLE)

| Thao tác người dùng (User Action) | Button / Form trên UI | Controller & Action Frontend (FE) | Typed API Service (FE) | HTTP Route & Method (BE) | Controller Backend (BE) | Service & Phương thức xử lý (BE) | Kết quả kiểm toán |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| **Bệnh nhân đặt lịch** | Form Submit tại `/Patient/Appointment/Book` | `PatientAppointmentController.Book [POST]` | `AppointmentApiService.CreatePatientAppointmentAsync` | `POST /api/patient/appointments` | `AppointmentsController.CreateAppointment` | `AppointmentService.CreatePatientAppointmentAsync` | **MATCH (Khớp 100%)** |
| **Bệnh nhân hủy lịch** | Nút bấm "Hủy lịch" tại `/Patient/Appointment/Detail` | `PatientAppointmentController.Cancel [POST]` | `AppointmentApiService.PatientCancelAsync` | `POST /api/patient/appointments/{id}/cancel` | `AppointmentsController.PatientCancel` | `AppointmentService.PatientCancelAsync` | **MATCH (Khớp 100%)** |
| **Bệnh nhân dời lịch** | Form Submit tại `/Patient/Appointment/Reschedule` | `PatientAppointmentController.Reschedule [POST]` | `AppointmentApiService.PatientRescheduleAsync` | `POST /api/patient/appointments/{id}/reschedule` | `AppointmentsController.PatientReschedule` | `AppointmentService.PatientRescheduleAsync` | **MATCH (Khớp 100%)** |
| **Bệnh nhân phản hồi đề xuất** | Nút "Đồng ý" / "Từ chối" trên Detail | `PatientAppointmentController.RespondProposal [POST]` | `AppointmentApiService.PatientRespondProposalAsync` | `POST /api/patient/appointments/{id}/proposal/accept`<br>`POST .../proposal/reject` | `AppointmentsController.AcceptProposal`<br>`AppointmentsController.RejectProposal` | `AppointmentService.PatientRespondProposalAsync` | **MATCH (Khớp 100%)** |
| **Lễ tân chuyển tiếp lịch** | Nút "Chuyển tiếp" tại `/Receptionist/Appointments/Requests` | `ReceptionistAppointmentController.Forward [POST]` | `AppointmentApiService.ReceptionistForwardAsync` | `POST /api/receptionist/appointments/{id}/forward` | `AppointmentsController.ReceptionistForward` | `AppointmentService.ReceptionistForwardAsync` | **MATCH (Khớp 100%)** |
| **Lễ tân từ chối lịch** | Nút "Từ chối" tại Requests | `ReceptionistAppointmentController.Reject [POST]` | `AppointmentApiService.ReceptionistRejectAsync` | `POST /api/receptionist/appointments/{id}/reject` | `AppointmentsController.ReceptionistReject` | `AppointmentService.ReceptionistRejectAsync` | **MATCH (Khớp 100%)** |
| **Lễ tân Check-in bệnh nhân** | Nút "Check-in" tại `/Receptionist/Appointments/TodayCheckIn` | `ReceptionistAppointmentController.CheckIn [POST]` | `AppointmentApiService.ReceptionistCheckInAsync` | `POST /api/receptionist/appointments/{id}/check-in` | `AppointmentsController.ReceptionistCheckIn` | `AppointmentService.ReceptionistCheckInAsync` | **MATCH (Khớp 100%)** |
| **Lễ tân đánh dấu No-Show** | Nút "No-Show" tại TodayCheckIn | `ReceptionistAppointmentController.NoShow [POST]` | `AppointmentApiService.ReceptionistMarkNoShowAsync` | `POST /api/receptionist/appointments/{id}/no-show` | `AppointmentsController.ReceptionistNoShow` | `AppointmentService.ReceptionistMarkNoShowAsync` | **MATCH (Khớp 100%)** |
| **Trưởng khoa xác nhận ca** | Nút "Xác nhận lịch hẹn" tại `/Department/Appointments/Review` | `DepartmentAppointmentController.Confirm [POST]` | `AppointmentApiService.ManagerConfirmAsync` | `POST /api/department/appointments/{id}/confirm` | `AppointmentsController.ManagerConfirm` | `AppointmentService.ManagerConfirmAsync` | **MATCH (Khớp 100%)** |
| **Trưởng khoa đề xuất đổi lịch**| Nút "Gửi đề xuất" tại Review | `DepartmentAppointmentController.ProposeChange [POST]` | `AppointmentApiService.ManagerProposeChangeAsync` | `POST /api/department/appointments/{id}/propose-change` | `AppointmentsController.ManagerProposeChange` | `AppointmentService.ManagerProposeChangeAsync` | **MATCH (Khớp 100%)** |
| **Bác sĩ bấm Vào Khám** | Nút "Vào Khám" tại `/Dentist/Appointments/Today` | `DentistAppointmentController.StartAppointment [POST]` | `AppointmentApiService.DentistStartAppointmentAsync` | `POST /api/dentist/appointments/{id}/start` | `AppointmentsController.DentistStartAppointment` | `AppointmentService.DentistStartAppointmentAsync` | **MATCH (Khớp 100%)** |
| **Bác sĩ chọn/đổi ghế** | Nút "Chọn Ghế" tại Today | `DentistAppointmentController.AssignChair [POST]` | `AppointmentApiService.DentistAssignChairAsync` | `POST /api/dentist/appointments/{id}/assign-chair` | `AppointmentsController.DentistAssignChair` | `AppointmentService.DentistAssignChairAsync` | **MATCH (Khớp 100%)** |
| **Bác sĩ lưu sơ đồ răng** | Nút "Lưu hồ sơ" tại `/Dentist/MedicalRecords/Index` | `DentistMedicalRecordController.CreateEntry [POST]` | `MedicalRecordApiService.CreateClinicalEntryAsync` | `POST /api/medical-records/entry` | `MedicalRecordController.CreateClinicalEntry` | `MedicalRecordService.CreateClinicalEntryAsync` | **MATCH (Khớp 100%)** |
| **Bác sĩ tải phim X-quang** | Nút "Tải ảnh" tại MedicalRecords Modal | `DentistMedicalRecordController.UploadImaging [POST]` | `MedicalRecordApiService.UploadImagingAsync` | `POST /api/medical-records/clinical-entries/{id}/imaging` | `MedicalRecordController.UploadClinicalEntryImaging` | `MedicalRecordService.UploadImagingAsync` | **MATCH (Khớp 100%)** |
| **Bác sĩ hoàn tất ca khám**| Nút "Hoàn Thành Ca Khám" tại Today | `DentistAppointmentController.CompleteAppointment [POST]`| `AppointmentApiService.DentistCompleteAppointmentAsync`| `POST /api/dentist/appointments/{id}/complete` | `AppointmentsController.DentistCompleteAppointment`| `AppointmentService.DentistCompleteAppointmentAsync`| **MATCH (Khớp 100%)** |
| **Lễ tân tạo hóa đơn** | Nút "Phát hành hóa đơn" tại `/Receptionist/Billing/Create` | `ReceptionistBillingController.Create [POST]` | `BillingApiService.CreateInvoiceAsync` | `POST /api/billing/invoices` | `BillingController.CreateInvoice` | `BillingService.CreateInvoiceAsync` | **MATCH (Khớp 100%)** |
| **Lễ tân thu tiền viện phí** | Nút "Xác nhận đã thu" tại `/Receptionist/Billing/Detail` | `ReceptionistBillingController.ProcessPayment [POST]` | `BillingApiService.ProcessPaymentAsync` | `POST /api/billing/payments` | `BillingController.ProcessPayment` | `BillingService.ProcessPaymentAsync` | **MATCH (Khớp 100%)** |
| **Admin khóa tài khoản** | Nút "Khóa tài khoản" tại `/Admin/Users/Index` | `AdminUserController.ToggleLock [POST]` | `AdminApiService.ToggleUserLockAsync` | `POST /api/admin/users/{id}/toggle-lock` | `AdminController.ToggleUserLock` | `AdminService.ToggleUserLockAsync` | **MATCH (Khớp 100%)** |

---

## 3. ĐÁNH GIÁ TÍNH NHẤT QUÁN CỦA CƠ CHẾ VALIDATION

- **Frontend Validation (Client-Side & MVC Model):**
  - Sử dụng thuộc tính kiểm thực `System.ComponentModel.DataAnnotations` trên toàn bộ ViewModel (ví dụ: `[Required]`, `[StringLength]`, `[EmailAddress]`, `[Phone]`, `[Range]`).
  - Hỗ trợ thư viện kiểm thực JavaScript không cần reload trang: `jquery.validate.min.js` và `jquery.validate.unobtrusive.min.js`.
- **Backend Validation (API Level):**
  - Sử dụng thư viện **FluentValidation** chạy tự động trước khi request chạm vào thân Controller:
    `builder.Services.AddFluentValidationAutoValidation();`
  - Nếu dữ liệu không hợp lệ, `InvalidModelStateResponseFactory` được cấu hình để tự động gói toàn bộ danh sách lỗi vào định dạng chuẩn:
    ```json
    {
      "success": false,
      "message": "Dữ liệu đầu vào không hợp lệ.",
      "errors": [ "Email không đúng định dạng.", "Mật khẩu tối thiểu 8 ký tự." ]
    }
    ```
- **Kết luận:** Hoàn toàn đồng nhất giữa quy tắc kiểm tra tại giao diện và quy tắc nghiệp vụ tại máy chủ.
