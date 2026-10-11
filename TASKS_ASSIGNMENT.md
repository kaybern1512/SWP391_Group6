# 📋 BẢNG PHÂN CHIA NHIỆM VỤ CÒN LẠI (TASKS ASSIGNMENT)
## Dự án: Multi-Specialty Dental Clinic Management System (SWP391)

Tài liệu này tổng hợp toàn bộ các phân hệ **CHƯA ĐƯỢC THỰC HIỆN** trong cơ sở dữ liệu `Dental_Clinic_Final_Database_Clean.sql`, được chia thành 4 Task độc lập, chi tiết từng bước (Step-by-step) để bạn bàn giao cho các thành viên trong nhóm code.

---

## 📌 BẢNG TỔNG QUAN CÁC MODULE CẦN LÀM

| Task | Tên Phân Hệ | Thực Thể (Database Tables) | Vai Trò Chính | Mức Độ |
| :--- | :--- | :--- | :--- | :--- |
| **TASK 1** | **Hồ Sơ Bệnh Án, Khám Lâm Sàng & Sơ Đồ Răng (Odontogram)** | `DentalMedicalRecords`, `ClinicalEntries`, `OdontogramEntries`, `ImagingRecords` | Dentist (Bác sĩ) | 🔴 Cao (P1) |
| **TASK 2** | **Kế Hoạch Điều Trị & Ca Thủ Thuật (Treatment)** | `TreatmentPlans`, `TreatmentPlanItems`, `TreatmentSessions`, `PerformedServices` | Dentist (Bác sĩ) | 🔴 Cao (P1) |
| **TASK 3** | **Đơn Thuốc, Chuyển Khoa & Hẹn Tái Khám** | `Prescriptions`, `PrescriptionItems`, `DepartmentReferrals`, `FollowUpSchedules` | Dentist, Dept Manager | 🟡 Trung bình (P2) |
| **TASK 4** | **Hóa Đơn, Thanh Toán & Thu Ngân (Billing & Payment)** | `Invoices`, `InvoiceItems`, `Payments` | Receptionist, Cashier | 🔴 Cao (P1) |

---

# ========================================================
# 🦷 CHI TIẾT TASK 1: HỒ SƠ BỆNH ÁN, KHÁM LÂM SÀNG & SƠ ĐỒ RĂNG
# ========================================================

### 1. Mục tiêu nghiệp vụ
Khi bệnh nhân đã được Lễ tân Check-in và vào phòng khám, Bác sĩ tiếp nhận bệnh nhân:
- Mở hoặc tạo mới **Hồ sơ bệnh án điện tử** (`DentalMedicalRecords`).
- Nhập kết quả **Khám lâm sàng** (`ClinicalEntries`): Lý do khám, tiền sử bệnh (tim mạch, dị ứng thuốc), khám ngoài mặt/trong miệng, chẩn đoán ban đầu, lời khuyên của bác sĩ.
- Xem và cập nhật **Sơ đồ răng tương tác (Odontogram)** (`OdontogramEntries`): 32 răng vĩnh viễn (đánh số FDI 11-48), chọn mặt răng (M, D, O, B, L), đánh dấu trạng thái răng (Răng sâu, Đã trám, Mất răng, Răng khôn mọc lệch, Răng sứ).
- Upload và lưu trữ **Phim chụp X-quang / Hình ảnh miệng** (`ImagingRecords`): Tích hợp Cloudinary qua service có sẵn `IImageStorageService`.

### 2. Các bảng Database liên quan
- `DentalMedicalRecords` (`RecordId`, `RecordNumber`, `PatientUserId`, `CreatedDate`, `Status`)
- `ClinicalEntries` (`EntryId`, `RecordId`, `AppointmentId`, `DentistUserId`, `ChiefComplaint`, `MedicalHistory`, `ClinicalExamination`, `Diagnosis`, `DoctorAdvice`, `RecordedAt`)
- `OdontogramEntries` (`OdontogramId`, `RecordId`, `ToothNumber`, `ToothSurface`, `Condition`, `TreatmentStatus`, `Note`, `RecordedAt`)
- `ImagingRecords` (`ImagingId`, `RecordId`, `ImagingType`, `ImageUrl`, `TakenAt`, `Note`)

### 3. Hướng dẫn các bước code cho thành viên

#### Bước 1: Tạo DTOs & Interfaces (Backend - Application Layer)
- Tạo folder `BE/DentalClinic.Application/Features/MedicalRecord/DTOs/`:
  - `MedicalRecordDto`, `CreateMedicalRecordRequest`
  - `ClinicalEntryDto`, `CreateClinicalEntryRequest`
  - `OdontogramEntryDto`, `UpdateToothConditionRequest`
  - `ImagingRecordDto`, `CreateImagingRecordRequest`
- Tạo interface `IMedicalRecordService.cs` trong `BE/DentalClinic.Application/Common/Interfaces/`:
  - `Task<MedicalRecordDto> GetOrCreateRecordAsync(long patientUserId);`
  - `Task<ClinicalEntryDto> AddClinicalEntryAsync(long dentistUserId, CreateClinicalEntryRequest req);`
  - `Task<List<OdontogramEntryDto>> GetOdontogramAsync(long recordId);`
  - `Task<OdontogramEntryDto> UpdateToothConditionAsync(long recordId, UpdateToothConditionRequest req);`
  - `Task<ImagingRecordDto> AddImagingRecordAsync(long recordId, CreateImagingRecordRequest req);`

#### Bước 2: Viết Service & Web API (Backend)
- Tạo `BE/DentalClinic.Infrastructure/Services/MedicalRecordService.cs` implement interface trên.
  - Tự động sinh `RecordNumber` định dạng: `MED-YYYYMMDD-XXXX`.
  - Tích hợp `IImageStorageService` để upload ảnh X-quang.
- Đăng ký DI trong `DependencyInjection.cs`.
- Tạo Controller `BE/DentalClinic.Api/Controllers/MedicalRecordController.cs` (`[Authorize(Roles = "Dentist,DepartmentManager")]`).

#### Bước 3: Viết Frontend MVC
- Tạo Service API Client `IMedicalRecordApiService` và `MedicalRecordApiService` trong `FE/DentalClinic.Web/Services/`.
- Tạo Controller `FE/DentalClinic.Web/Controllers/Dentist/DentistMedicalRecordController.cs`:
  - `GET /Dentist/MedicalRecord/Patient/{patientUserId}`: Xem hồ sơ bệnh án tổng thể của bệnh nhân.
  - `POST /Dentist/MedicalRecord/Clinical`: Lưu phiếu khám lâm sàng.
  - `POST /Dentist/MedicalRecord/Odontogram`: Cập nhật trạng thái từng răng.
  - `POST /Dentist/MedicalRecord/Imaging`: Upload ảnh X-quang.
- Tạo Razor Views:
  - `Views/Dentist/MedicalRecord/Detail.cshtml`: Giao diện trực quan chia tab (Tab 1: Lịch sử khám lâm sàng, Tab 2: Sơ đồ 32 răng có màu sắc theo tình trạng, Tab 3: Thư viện phim X-quang).

---

# ========================================================
# 🛠️ CHI TIẾT TASK 2: KẾ HOẠCH ĐIỀU TRỊ & CA ĐIỀU TRỊ THỦ THUẬT
# ========================================================

### 1. Mục tiêu nghiệp vụ
Sau khi khám lâm sàng, Bác sĩ lập phác đồ điều trị cho bệnh nhân:
- **Kế hoạch điều trị** (`TreatmentPlans`): Ví dụ "Phác đồ Cấy ghép Implant răng 36", gồm chẩn đoán, tổng chi phí dự kiến, trạng thái (`Draft`, `InProgress`, `Completed`, `Cancelled`).
- **Các bước trong phác đồ** (`TreatmentPlanItems`): Thứ tự các bước điều trị, dịch vụ thuộc chuyên khoa (`DepartmentServiceId`), số răng cần can thiệp, chi phí từng bước.
- **Ghi nhận buổi điều trị** (`TreatmentSessions`): Mỗi lần bệnh nhân đến theo hẹn, Bác sĩ ghi nhận buổi thực hiện (Buổi 1: Vệ sinh răng & cắm trụ, Buổi 2: Cắt chỉ, v.v.), gắn với phòng khám và ghế nha khoa.
- **Dịch vụ thực tế đã thực hiện** (`PerformedServices`): Bác sĩ tick chọn dịch vụ thực tế đã hoàn thành trong buổi đó kèm đơn giá, giảm giá. **(Dữ liệu này là căn cứ để Task 4 xuất hóa đơn thu tiền)**.

### 2. Các bảng Database liên quan
- `TreatmentPlans` (`PlanId`, `PlanCode`, `RecordId`, `DentistUserId`, `Title`, `Diagnosis`, `EstimatedCost`, `Status`, `CreatedAt`)
- `TreatmentPlanItems` (`ItemId`, `PlanId`, `StepOrder`, `DepartmentServiceId`, `ToothNumber`, `EstimatedCost`, `Status`)
- `TreatmentSessions` (`SessionId`, `PlanId`, `DentistUserId`, `RoomId`, `DentalChairId`, `TreatmentDate`, `SessionNotes`, `NextAction`)
- `PerformedServices` (`PerformedServiceId`, `SessionId`, `DepartmentServiceId`, `Quantity`, `UnitPrice`, `DiscountAmount`, `FinalPrice`, `ToothNumber`, `PerformedAt`)

### 3. Hướng dẫn các bước code cho thành viên

#### Bước 1: Tạo DTOs & Interfaces (Backend)
- Thư mục `BE/DentalClinic.Application/Features/Treatment/DTOs/`:
  - `TreatmentPlanDto`, `CreateTreatmentPlanRequest`, `UpdateTreatmentPlanStatusRequest`
  - `TreatmentSessionDto`, `CreateTreatmentSessionRequest`
  - `PerformedServiceDto`, `AddPerformedServiceRequest`
- Interface `ITreatmentService.cs`:
  - `Task<TreatmentPlanDto> CreatePlanAsync(long dentistUserId, CreateTreatmentPlanRequest req);`
  - `Task<List<TreatmentPlanDto>> GetPlansByRecordIdAsync(long recordId);`
  - `Task<TreatmentSessionDto> CreateSessionAsync(long dentistUserId, CreateTreatmentSessionRequest req);`
  - `Task<PerformedServiceDto> AddPerformedServiceAsync(AddPerformedServiceRequest req);`
  - `Task<bool> CompletePlanAsync(long planId);`

#### Bước 2: Viết Service & Controller (Backend)
- Tạo `BE/DentalClinic.Infrastructure/Services/TreatmentService.cs`.
- Tạo Controller `BE/DentalClinic.Api/Controllers/TreatmentController.cs`.

#### Bước 3: Viết Frontend MVC
- Tạo `ITreatmentApiService` và `TreatmentApiService`.
- Tạo Controller `FE/DentalClinic.Web/Controllers/Dentist/DentistTreatmentController.cs`.
- Tạo Views:
  - `Views/Dentist/Treatment/PlanDetail.cshtml`: Xem tiến độ phác đồ (thanh % hoàn thành các bước).
  - `Views/Dentist/Treatment/SessionEntry.cshtml`: Form ghi nhận buổi điều trị & tick chọn dịch vụ thực hiện.

---

# ========================================================
# 💊 CHI TIẾT TASK 3: ĐƠN THUỐC, CHUYỂN KHOA & HẸN TÁI KHÁM
# ========================================================

### 1. Mục tiêu nghiệp vụ
- **Kê đơn thuốc** (`Prescriptions` & `PrescriptionItems`):
  - Bác sĩ kê thuốc giảm đau, kháng viêm, kháng sinh sau ca thủ thuật.
  - Nhập tên thuốc, liều lượng, số lượng, hướng dẫn uống (Sáng 1 viên sau ăn, Tối 1 viên).
  - Hỗ trợ in đơn thuốc hoặc xem chi tiết.
- **Chuyển khoa / Hội chẩn liên chuyên khoa** (`DepartmentReferrals`):
  - Nếu bệnh nhân cần can thiệp chuyên sâu (ví dụ nhổ răng xong cần chuyển qua Khoa Chỉnh nha hoặc Khoa Phục hình).
  - Bác sĩ gửi phiếu chuyển khoa: Khoa chuyển, Khoa nhận, Lý do chuyển, Mức độ ưu tiên (`Normal`, `Urgent`).
  - Trưởng khoa nhận có chức năng tiếp nhận/chỉ định bác sĩ tiếp theo.
- **Lịch hẹn tái khám** (`FollowUpSchedules`):
  - Bác sĩ hẹn ngày tái khám (sau 7 ngày cắt chỉ, sau 1 tháng kiểm tra lực kéo niềng).
  - Trạng thái: `Scheduled`, `Completed`, `Missed`.

### 2. Các bảng Database liên quan
- `Prescriptions` (`PrescriptionId`, `PrescriptionCode`, `AppointmentId`, `RecordId`, `DentistUserId`, `Diagnosis`, `Instructions`, `Status`, `CreatedAt`)
- `PrescriptionItems` (`PrescriptionItemId`, `PrescriptionId`, `MedicineName`, `Dosage`, `Unit`, `Quantity`, `UsageInstructions`)
- `DepartmentReferrals` (`ReferralId`, `AppointmentId`, `FromDepartmentId`, `ToDepartmentId`, `ReferringDentistUserId`, `Reason`, `Urgency`, `Status`, `CreatedAt`)
- `FollowUpSchedules` (`FollowUpId`, `AppointmentId`, `PatientUserId`, `DentistUserId`, `DepartmentServiceId`, `FollowUpDate`, `Reason`, `Status`)

### 3. Hướng dẫn các bước code cho thành viên

#### Bước 1: Tạo DTOs & Interfaces (Backend)
- Thư mục `BE/DentalClinic.Application/Features/PostCare/DTOs/`:
  - `PrescriptionDto`, `CreatePrescriptionRequest`, `PrescriptionItemDto`
  - `DepartmentReferralDto`, `CreateReferralRequest`
  - `FollowUpScheduleDto`, `CreateFollowUpRequest`
- Interface `IPostCareService.cs` (hoặc tách riêng `IPrescriptionService` & `IReferralService`).

#### Bước 2: Viết Service & Controller (Backend)
- Tạo `BE/DentalClinic.Infrastructure/Services/PostCareService.cs`.
- Tạo Controller `BE/DentalClinic.Api/Controllers/PrescriptionController.cs` và `ReferralController.cs`.

#### Bước 3: Viết Frontend MVC
- Bác sĩ: Thêm tab "Kê Đơn Thuốc" & "Hẹn Tái Khám" tại trang chi tiết ca khám của Bác sĩ.
- Bệnh nhân: Xem đơn thuốc đã kê và danh sách lịch hẹn tái khám tại `/Patient/Prescriptions` và `/Patient/FollowUps`.
- Trưởng khoa: Trang `/Department/Referrals` quản lý các ca chuyển viện/chuyển khoa đến khoa mình.

---

# ========================================================
# 💳 CHI TIẾT TASK 4: HÓA ĐƠN, THANH TOÁN & THU NGÂN
# ========================================================

### 1. Mục tiêu nghiệp vụ
Khi bệnh nhân hoàn thành buổi khám/điều trị, Lễ tân / Thu ngân tiếp nhận:
- **Tạo Hóa đơn thanh toán** (`Invoices` & `InvoiceItems`):
  - Hệ thống tự động gom các dịch vụ đã thực hiện từ buổi điều trị (`PerformedServices`) hoặc dịch vụ ban đầu của lịch hẹn (`Appointments`).
  - Cho phép áp dụng giảm giá (`DiscountAmount`), thuế VAT (`TaxAmount`), tính toán tổng tiền phải thu (`FinalAmount`).
- **Thanh toán & Thu tiền** (`Payments`):
  - Lễ tân chọn hình thức thanh toán: `Cash` (Tiền mặt), `BankTransfer` (Chuyển khoản QR), `CreditCard` (Thẻ), `VNPay`.
  - Hỗ trợ thanh toán nhiều đợt (Đặt cọc trước $\rightarrow$ Trạng thái hóa đơn `PartiallyPaid`; Thanh toán đủ $\rightarrow$ `Paid`).
  - In biên lai thu tiền cho bệnh nhân.
  - Khi hóa đơn được thanh toán xong $\rightarrow$ Đổi trạng thái lịch hẹn `Appointments` thành `Completed`!

### 2. Các bảng Database liên quan
- `Invoices` (`InvoiceId`, `InvoiceNumber`, `AppointmentId`, `PatientUserId`, `TotalAmount`, `DiscountAmount`, `TaxAmount`, `FinalAmount`, `PaidAmount`, `RemainingAmount`, `PaymentStatus`, `CreatedByStaffUserId`, `CreatedAt`)
- `InvoiceItems` (`InvoiceItemId`, `InvoiceId`, `DepartmentServiceId`, `ItemDescription`, `Quantity`, `UnitPrice`, `DiscountAmount`, `FinalPrice`)
- `Payments` (`PaymentId`, `PaymentCode`, `InvoiceId`, `AmountPaid`, `PaymentMethod`, `TransactionReference`, `PaymentStatus`, `ProcessedByStaffUserId`, `PaymentDate`)

### 3. Hướng dẫn các bước code cho thành viên

#### Bước 1: Tạo DTOs & Interfaces (Backend)
- Thư mục `BE/DentalClinic.Application/Features/Billing/DTOs/`:
  - `InvoiceDto`, `CreateInvoiceRequest`, `InvoiceItemDto`
  - `PaymentDto`, `ProcessPaymentRequest`
  - `InvoiceFilterRequest`
- Interface `IBillingService.cs`:
  - `Task<InvoiceDto> CreateInvoiceFromAppointmentAsync(long staffUserId, CreateInvoiceRequest req);`
  - `Task<InvoiceDto> GetInvoiceByIdAsync(long invoiceId);`
  - `Task<PaymentDto> ProcessPaymentAsync(long staffUserId, ProcessPaymentRequest req);`
  - `Task<PagedResult<InvoiceDto>> GetInvoicesAsync(InvoiceFilterRequest req);`

#### Bước 2: Viết Service & Controller (Backend)
- Tạo `BE/DentalClinic.Infrastructure/Services/BillingService.cs`.
- Tự động sinh `InvoiceNumber`: `INV-YYYYMMDD-XXXX`.
- Cập nhật số dư: `RemainingAmount = FinalAmount - PaidAmount`.
- Nếu `RemainingAmount <= 0` $\rightarrow$ set `PaymentStatus = "Paid"`.
- Tạo Controller `BE/DentalClinic.Api/Controllers/BillingController.cs` (`[Authorize(Roles = "Receptionist,SystemAdministrator")]`).

#### Bước 3: Viết Frontend MVC
- Tạo `IBillingApiService` và `BillingApiService`.
- Tạo Controller `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistBillingController.cs`:
  - `GET /Receptionist/Billing`: Bảng danh sách hóa đơn cần thu tiền và đã thu tiền.
  - `GET /Receptionist/Billing/Create/{appointmentId}`: Màn hình lập hóa đơn (tự động load dịch vụ).
  - `POST /Receptionist/Billing/Pay`: Modal xác nhận thu tiền (chọn Tiền mặt/Chuyển khoản).
  - `GET /Receptionist/Billing/Print/{invoiceId}`: Màn hình in hóa đơn chuẩn phòng khám.
- Bệnh nhân: Xem lịch sử hóa đơn thanh toán của mình tại `/Patient/Invoices`.

---

# 🚀 NGUYÊN TẮC BẮT BUỘC KHI CÁC THÀNH VIÊN CODE

1. **Tuân thủ Database First**: Tuyệt đối **KHÔNG SỬA DATABASE**, không tạo migration, sử dụng nguyên vẹn các bảng và cột trong `DentalClinicDbContext`.
2. **UserId thống nhất**:
   - Bệnh nhân: `PatientUserId`
   - Bác sĩ: `DentistUserId`
   - Nhân viên / Lễ tân: `StaffUserId`
3. **Phân tầng chuẩn Clean Architecture**:
   - DTOs $\rightarrow$ `Application Layer`
   - Business Logic & EF Core $\rightarrow$ `Infrastructure Layer`
   - API $\rightarrow$ `Api Layer`
   - Web MVC $\rightarrow$ `Web Layer` (chỉ gọi API qua `HttpClient`, không chạm DbContext trực tiếp).
4. **Quy trình Git**:
   - Mỗi thành viên tạo 1 nhánh riêng từ `develop`, ví dụ:
     - `feature/medical-records-and-odontogram`
     - `feature/treatment-plans-and-sessions`
     - `feature/prescriptions-and-referrals`
     - `feature/billing-and-payments`
   - Sau khi code xong $\rightarrow$ viết Unit Tests $\rightarrow$ build 0 error $\rightarrow$ tạo Pull Request vào `develop`.
