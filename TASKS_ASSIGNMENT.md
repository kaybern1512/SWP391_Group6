# 📋 BẢNG PHÂN CHIA NHIỆM VỤ CÒN LẠI (PROJECT BACKLOG & TASKS ASSIGNMENT)
## Dự án: Hệ Thống Quản Lý Phòng Khám Nha Khoa Đa Khoa (SWP391)
*Cập nhật trạng thái thực tế: Ngày 12/10/2026*

---

## 🧭 I. MA TRẬN TIẾN ĐỘ THỰC TẾ CỦA HỆ THỐNG

| Phân hệ (Module) | Các bảng DB liên quan | Trạng thái hiện tại | Ghi chú |
| :--- | :--- | :--- | :--- |
| **1. Xác thực & Phân quyền (Auth & RBAC)** | `UserAccounts`, `StaffProfiles`, `PatientProfiles` | ✅ **Hoàn thành 100%** | Login thường, Google OAuth, OTP kích hoạt, Profile |
| **2. Quản trị hệ thống (Admin)** | `Departments`, `Rooms`, `DentalChairs`, `DepartmentServices`, `AuditLogs` | ✅ **Hoàn thành 100%** | CRUD Khoa, Phòng, Ghế, Dịch vụ & Bảng giá, Audit log |
| **3. Đặt lịch & Tiếp đón (Booking & Check-in)** | `Appointments`, `AppointmentStatusHistories`, `AppointmentChangeProposals` | ✅ **Hoàn thành 100%** | Đặt lịch 3 bước, Lễ tân check-in phân số thứ tự Q-xxx, Trưởng khoa duyệt, **tự động gửi email xác nhận** |
| **4. Lịch trực Bác sĩ (Doctor Shifts)** | `DentistWorkSchedules`, `DentistAvailabilities`, `DentistDepartments` | ✅ **Hoàn thành 100%** | Trưởng khoa phân ca trực (lọc đúng bác sĩ khoa), Bác sĩ đăng ký lịch rảnh & xem lịch tuần |
| **5. Bệnh án điện tử & Sơ đồ răng** | `DentalMedicalRecords`, `ClinicalEntries`, `OdontogramEntries` | ✅ **Hoàn thành 90%** | Khám lâm sàng, chẩn đoán, sơ đồ tương tác 32 răng FDI. *(Còn thiếu upload phim X-quang)* |
| **6. Kế hoạch điều trị (Treatment Plans)** | `TreatmentPlans`, `TreatmentPlanItems`, `TreatmentSessions` | ✅ **Hoàn thành 100%** | Lập phác đồ đa giai đoạn, ghi nhận buổi thủ thuật, theo dõi tiến độ % |
| **7. Đơn thuốc điện tử (Prescriptions)** | `Prescriptions`, `PrescriptionItems` | ✅ **Hoàn thành 100%** | Kê đơn, quản lý liều dùng, in đơn thuốc chuẩn, bệnh nhân xem đơn |
| **8. Đánh giá & Phản hồi (Feedback)** | `VisitFeedbacks` | ✅ **Hoàn thành 100%** | Bệnh nhân đánh giá 1-5 sao sau khám, Bác sĩ & Manager xem |
| **9. Hóa đơn & Thu ngân (Billing)** | `Invoices`, `InvoiceItems`, `Payments` | 🔴 **CHƯA LÀM (0%)** | **Cần giao cho Thành viên 1** |
| **10. Chuyển khoa & Lịch tái khám** | `DepartmentReferrals`, `FollowUpSchedules` | 🔴 **CHƯA LÀM (0%)** | **Cần giao cho Thành viên 2** |
| **11. Upload Phim X-quang (Imaging)** | `ImagingRecords` (tích hợp Cloudinary) | 🟡 **CHƯA LÀM (0%)** | **Cần giao cho Thành viên 3** |
| **12. Báo cáo Doanh thu & Thống kê** | Tổng hợp từ `Invoices`, `Appointments`, `Feedbacks` | 🟡 **CHƯA LÀM (0%)** | **Cần giao cho Thành viên 4** |

---

# ========================================================
# 💳 TASK 1: PHÂN HỆ HÓA ĐƠN, THANH TOÁN & THU NGÂN (BILLING & PAYMENTS)
# ========================================================
* **Mức độ ưu tiên:** 🔴 **CAO NHẤT (P1 - Cốt lõi của phòng khám)**
* **Phù hợp phân công:** Thành viên phụ trách phân hệ Lễ tân / Kế toán / Thu ngân.
* **Vai trò người dùng:** `Receptionist`, `Patient`, `SystemAdministrator`

### 1. Mục tiêu & Luồng nghiệp vụ
1. Khi Bác sĩ hoàn thành khám hoặc ca điều trị thủ thuật $\rightarrow$ Lễ tân mở danh sách cuộc hẹn/ca khám.
2. Lễ tân bấm **"Lập hóa đơn"**:
   - Hệ thống tự động load thông tin bệnh nhân và dịch vụ khám (từ cuộc hẹn hoặc ca điều trị).
   - Cho phép chọn/thêm các dịch vụ phát sinh, số lượng, đơn giá.
   - Cho phép nhập giảm giá/chiết khấu (`DiscountAmount`), tính tổng tiền thanh toán (`TotalAmount`).
   - Tự động sinh mã hóa đơn định dạng: `INV-YYYYMMDD-XXXX` (VD: `INV-20261012-0001`).
3. Lễ tân thực hiện **"Thu tiền (Thanh toán)"**:
   - Chọn phương thức thanh toán: `Cash` (Tiền mặt), `BankTransfer` (Chuyển khoản ngân hàng), `CreditCard` (Thẻ).
   - Hỗ trợ thanh toán toàn bộ (Trạng thái Invoice thành `Paid`) hoặc thanh toán một phần / đặt cọc (Trạng thái `PartiallyPaid`).
   - Sau khi thanh toán đủ $\rightarrow$ Hệ thống tự động chuyển trạng thái lịch hẹn `Appointments` thành `Completed`.
4. Chức năng **In hóa đơn / Phiếu thu viện phí**:
   - Giao diện in sạch đẹp, chuẩn phòng khám nha khoa có logo, mã vạch/mã số, chữ ký thu ngân.
5. Phía **Bệnh nhân**:
   - Xem lịch sử hóa đơn thanh toán của mình tại menu: `/Patient/Invoices`.

### 2. Các bảng Database sử dụng
* `Invoices` (`InvoiceId`, `InvoiceCode`, `AppointmentId`, `GeneratedByUserId`, `TotalAmount`, `Status`, `CreatedAt`)
* `InvoiceItems` (`InvoiceItemId`, `InvoiceId`, `PerformedServiceId`, `Description`, `Quantity`, `UnitPrice`, `Amount`)
* `Payments` (`PaymentId`, `InvoiceId`, `PaidByPatientUserId`, `Method`, `Amount`, `TransactionReference`, `Status`, `PaidAt`, `CreatedAt`)

### 3. Các file cần code
#### Backend:
- DTOs: Tạo thư mục `BE/DentalClinic.Application/Features/Billing/DTOs/`:
  - `InvoiceDto.cs`, `InvoiceItemDto.cs`, `PaymentDto.cs`
  - `CreateInvoiceRequest.cs`, `ProcessPaymentRequest.cs`, `InvoiceFilterRequest.cs`
- Interface: `BE/DentalClinic.Application/Common/Interfaces/IBillingService.cs`
- Implementation: `BE/DentalClinic.Infrastructure/Services/BillingService.cs`
- Controller API: `BE/DentalClinic.Api/Controllers/BillingController.cs` (`[Authorize(Roles = "Receptionist,SystemAdministrator,Patient")]`)
- Đăng ký DI trong `BE/DentalClinic.Infrastructure/DependencyInjection.cs`

#### Frontend MVC:
- Service API: `FE/DentalClinic.Web/Services/IBillingApiService.cs` & `BillingApiService.cs`
- Controller: `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistBillingController.cs`
- Controller Bệnh nhân: `FE/DentalClinic.Web/Controllers/Patient/PatientInvoiceController.cs`
- Razor Views:
  - `Views/Receptionist/Billing/Index.cshtml`: Bảng danh sách hóa đơn (lọc theo ngày, trạng thái thanh toán).
  - `Views/Receptionist/Billing/Create.cshtml`: Form tạo hóa đơn theo `AppointmentId`.
  - `Views/Receptionist/Billing/Detail.cshtml`: Chi tiết hóa đơn + Modal bấm nút thanh toán thu tiền.
  - `Views/Receptionist/Billing/Print.cshtml`: Mẫu hóa đơn in giấy chuẩn (CSS `@media print`).
  - `Views/Patient/Invoices/Index.cshtml`: Lịch sử viện phí của bệnh nhân.

---

# ========================================================
# 🔄 TASK 2: PHÂN HỆ CHUYỂN KHOA & HẸN TÁI KHÁM (REFERRALS & FOLLOW-UPS)
# ========================================================
* **Mức độ ưu tiên:** 🟡 **CAO (P2 - Nghiệp vụ liên chuyên khoa)**
* **Phù hợp phân công:** Thành viên phụ trách phân hệ Bác sĩ / Trưởng khoa.
* **Vai trò người dùng:** `Dentist`, `DepartmentManager`, `Patient`

### 1. Mục tiêu & Luồng nghiệp vụ
1. **Chuyển khoa / Hội chẩn liên chuyên khoa (`DepartmentReferrals`)**:
   - Khi Bác sĩ đang khám/điều trị cho bệnh nhân (ví dụ nhổ răng xong), nhận thấy bệnh nhân cần chỉnh nha hoặc cấy ghép implant:
     - Bác sĩ bấm **"Chuyển chuyên khoa"**: Chọn khoa đích (`ToDepartmentId`), mức độ ưu tiên (`Normal` / `Urgent`), lý do chuyển và dặn dò lâm sàng.
   - Trưởng khoa tiếp nhận:
     - Trưởng khoa của khoa được chuyển đến vào menu `/Department/Referrals` xem danh sách ca chuyển đến.
     - Trưởng khoa duyệt tiếp nhận (`Approved`) hoặc từ chối (`Rejected`), đồng thời phân công Bác sĩ khoa mình (`AssignedDentistUserId`) tiếp quản.
2. **Lịch hẹn tái khám định kỳ (`FollowUpSchedules`)**:
   - Sau khi kết thúc buổi điều trị, Bác sĩ bấm **"Hẹn ngày tái khám"**:
     - Chọn ngày đề xuất (`RecommendedDate`), ghi chú lý do (ví dụ: sau 7 ngày cắt chỉ, sau 30 ngày siết niềng).
   - Hệ thống tự tạo thông báo cho bệnh nhân.
   - Bệnh nhân vào trang `/Patient/FollowUps` xem các mốc hẹn tái khám.
   - Lễ tân có bảng danh sách theo dõi bệnh nhân đến hạn tái khám để gọi điện chăm sóc.

### 2. Các bảng Database sử dụng
* `DepartmentReferrals` (`ReferralId`, `TreatmentSessionId`, `FromDepartmentId`, `ToDepartmentId`, `AssignedDentistUserId`, `ReviewedByUserId`, `Reason`, `Status`, `CreatedAt`, `ReviewedAt`)
* `FollowUpSchedules` (`FollowUpId`, `TreatmentSessionId`, `RecommendedDate`, `Reason`, `Status`, `AppointmentId`)

### 3. Các file cần code
#### Backend:
- DTOs: `BE/DentalClinic.Application/Features/PostCare/DTOs/`:
  - `DepartmentReferralDto.cs`, `CreateReferralRequest.cs`, `ReviewReferralRequest.cs`
  - `FollowUpScheduleDto.cs`, `CreateFollowUpRequest.cs`
- Interface: `BE/DentalClinic.Application/Common/Interfaces/IPostCareService.cs`
- Implementation: `BE/DentalClinic.Infrastructure/Services/PostCareService.cs`
- Controller: `BE/DentalClinic.Api/Controllers/PostCareController.cs`

#### Frontend MVC:
- `FE/DentalClinic.Web/Services/PostCareApiService.cs`
- Controller:
  - Bác sĩ: Thêm chức năng trong `DentistTreatmentPlanController` hoặc tạo `DentistPostCareController.cs`.
  - Trưởng khoa: `FE/DentalClinic.Web/Controllers/Department/DepartmentReferralController.cs`.
  - Bệnh nhân: `FE/DentalClinic.Web/Controllers/Patient/PatientFollowUpController.cs`.
- Razor Views:
  - `Views/Department/Referrals/Index.cshtml`: Danh sách ca chuyển khoa chờ Trưởng khoa duyệt.
  - `Views/Patient/FollowUps/Index.cshtml`: Lịch hẹn tái khám của bệnh nhân.

---

# ========================================================
# 📷 TASK 3: UPLOAD PHIM X-QUANG & THƯ VIỆN HÌNH ẢNH NHA KHOA (IMAGING RECORDS)
# ========================================================
* **Mức độ ưu tiên:** 🟡 **TRUNG BÌNH (P2 - Trải nghiệm hình ảnh & chẩn đoán)**
* **Phù hợp phân công:** Thành viên phụ trách UI/UX, Bệnh án hoặc Tích hợp Cloud.
* **Vai trò người dùng:** `Dentist`, `Patient`

### 1. Mục tiêu & Luồng nghiệp vụ
1. **Upload phim chụp X-quang / Ảnh miệng**:
   - Khi Bác sĩ khám lâm sàng tại trang `/Dentist/MedicalRecords`:
     - Có nút **"Thêm phim chụp / Hình ảnh X-quang"**:
     - Cho phép chọn file ảnh từ máy tính (hỗ trợ JPG, PNG, DICOM/JPEG).
     - Chọn phân loại phim: `Panorama` (Toàn cảnh), `Periapical` (Cận chóp), `Cephalometric` (Sọ nghiêng), `Intraoral` (Trong miệng).
     - Nhập tóm tắt chẩn đoán hình ảnh (`ResultSummary`).
   - File ảnh tự động được tải lên Cloudinary thông qua service có sẵn `IImageStorageService` (`CloudinaryImageStorageService`).
   - Lưu URL trả về vào bảng `ImagingRecords`.
2. **Thư viện xem ảnh X-quang (Gallery & Lightbox)**:
   - Hiển thị danh sách ảnh dạng lưới (Thumbnail).
   - Nhấp vào ảnh để mở popup phóng to (Lightbox) cho Bác sĩ quan sát chi tiết cấu trúc răng/xương hàm.
3. Phía **Bệnh nhân**:
   - Trong trang Hồ sơ bệnh án của Bệnh nhân (`/Patient/MedicalRecords`), hiển thị tab "Hình ảnh & Phim X-quang" để bệnh nhân xem trực tiếp kết quả chụp của mình.

### 2. Các bảng Database sử dụng
* `ImagingRecords` (`ImagingRecordId`, `ClinicalEntryId`, `ImagingType`, `FileUrl`, `ResultSummary`, `CreatedAt`)
* `ClinicalEntries`

### 3. Các file cần code
#### Backend:
- Mở rộng `MedicalRecordService.cs` và `IMedicalRecordService.cs`:
  - Thêm phương thức: `Task<ApiResponse<ImagingRecordDto>> UploadImagingRecordAsync(long clinicalEntryId, string imagingType, string? summary, IFormFile file);`
  - Thêm phương thức: `Task<ApiResponse<bool>> DeleteImagingRecordAsync(long imagingRecordId);`
- Mở rộng `MedicalRecordController.cs`:
  - `POST /api/medical-records/clinical-entries/{clinicalEntryId}/imaging` (nhận `[FromForm]`).
  - `DELETE /api/medical-records/imaging/{imagingRecordId}`.

#### Frontend MVC:
- Cập nhật `IMedicalRecordApiService.cs` & `MedicalRecordApiService.cs` hỗ trợ gọi API upload multipart/form-data.
- Cập nhật `FE/DentalClinic.Web/Controllers/Dentist/DentistMedicalRecordController.cs`: Action `UploadImaging`.
- Giao diện:
  - Cập nhật `Views/Dentist/MedicalRecords/Index.cshtml`: Thêm Modal Upload và khu vực hiển thị Photo Grid + Lightbox.
  - Cập nhật `Views/Patient/MedicalRecords/Index.cshtml`: Thêm phần xem ảnh X-quang cho bệnh nhân.

---

# ========================================================
# 📊 TASK 4: BÁO CÁO DOANH THU, HIỆU SUẤT & XUẤT DỮ LIỆU EXCEL (ANALYTICS & REPORTS)
# ========================================================
* **Mức độ ưu tiên:** 🟢 **TRUNG BÌNH (P3 - Dành cho Quản trị & Trưởng khoa)**
* **Phù hợp phân công:** Thành viên phụ trách Báo cáo / Thống kê / Admin Dashboard.
* **Vai trò người dùng:** `SystemAdministrator`, `DepartmentManager`

### 1. Mục tiêu & Luồng nghiệp vụ
1. **Báo cáo Doanh thu phòng khám (Dành cho Admin)**:
   - Biểu đồ đường (Line Chart): Xu hướng doanh thu theo ngày trong tuần / các tháng trong năm.
   - Biểu đồ tròn (Pie/Doughnut Chart): Tỷ trọng doanh thu theo từng chuyên khoa.
   - Bảng tổng hợp: Tổng doanh thu, Số tiền đã thu, Số tiền nợ/chờ thanh toán.
2. **Báo cáo Hoạt động chuyên môn (Dành cho Trưởng khoa)**:
   - Thống kê năng suất của từng Bác sĩ trong khoa: Số ca khám đã thực hiện, Số phác đồ điều trị hoàn tất.
   - Điểm đánh giá trung bình (sao) từ phản hồi bệnh nhân của từng bác sĩ.
3. **Xuất báo cáo (Export Excel / CSV)**:
   - Nút "Xuất file Excel" để Admin/Trưởng khoa tải báo cáo danh sách hóa đơn hoặc thống kê theo khoảng thời gian tùy chọn.

### 2. Các file cần code
#### Backend:
- DTOs: `BE/DentalClinic.Application/Features/Reports/DTOs/`:
  - `RevenueReportDto.cs`, `DentistPerformanceDto.cs`, `DepartmentStatisticDto.cs`
- Interface: `BE/DentalClinic.Application/Common/Interfaces/IReportService.cs`
- Implementation: `BE/DentalClinic.Infrastructure/Services/ReportService.cs`
- Controller: `BE/DentalClinic.Api/Controllers/ReportController.cs`

#### Frontend MVC:
- Service API: `IReportApiService.cs` & `ReportApiService.cs`
- Controller:
  - `FE/DentalClinic.Web/Controllers/Admin/AdminReportController.cs`
  - `FE/DentalClinic.Web/Controllers/Department/DepartmentReportController.cs`
- Razor Views:
  - `Views/Admin/Reports/Revenue.cshtml`: Dashboard biểu đồ Chart.js + bảng thống kê + nút Xuất Excel.
  - `Views/Department/Reports/Performance.cshtml`: Thống kê ca khám và đánh giá bác sĩ của khoa.

---

# ========================================================
# 💎 TASK 5 (BONUS TÙY CHỌN): TÍCH HỢP THANH TOÁN VIETQR / VNPAY SANDBOX
# ========================================================
* **Mục tiêu:**
  - Tạo mã VietQR động theo chuẩn VietQR.io (truyền Số tài khoản, Tên ngân hàng, Số tiền hóa đơn, Mã hóa đơn làm nội dung chuyển khoản).
  - Bệnh nhân hoặc Lễ tân quét mã bằng ứng dụng ngân hàng là chuyển khoản chính xác 100% không sợ sai sót số tiền hay nội dung.
  - (Tùy chọn nâng cao) Tích hợp cổng VNPay Sandbox để thanh toán trực tuyến qua VNPAY-QR / Thẻ ATM nội địa.

---

## 🛠️ NGUYÊN TẮC BẮT BUỘC KHI CẢ NHÓM LÀM VIỆC

1. **KHÔNG SỬA CẤU TRÚC DATABASE:**
   - Database đã chốt cố định trong `Dental_Clinic_Final_Database_Clean.sql`. Tuyệt đối không chạy `add-migration` hay can thiệp phá vỡ schema hiện tại.
2. **QUY TRÌNH PHÂN NHÁNH GIT:**
   - Mỗi thành viên pull code mới nhất từ `origin/develop`:
     ```powershell
     git checkout develop
     git pull origin develop
     ```
   - Tạo nhánh tính năng riêng:
     - Thành viên 1: `git checkout -b feature/billing-invoices`
     - Thành viên 2: `git checkout -b feature/referral-and-followup`
     - Thành viên 3: `git checkout -b feature/imaging-cloudinary`
     - Thành viên 4: `git checkout -b feature/reports-analytics`
   - Sau khi hoàn thành và test kỹ $\rightarrow$ commit và tạo Pull Request (PR) về `develop`.
3. **MẬT KHẨU TÀI KHOẢN TEST:**
   - Dùng chung: `Password123@` (xem chi tiết bảng 22 tài khoản trong file `SETUP.md`).
