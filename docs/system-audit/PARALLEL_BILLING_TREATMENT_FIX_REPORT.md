# BÁO CÁO KIỂM TOÁN VÀ ỔN ĐỊNH TOÀN DIỆN QUY TRÌNH HẬU KHÁM, HÓA ĐƠN, THANH TOÁN & LỊCH SỬ ĐIỀU TRỊ (TASK B)
**Hệ thống Nha khoa DentalCare (SWP391_Group6)**
**Ngày thực hiện:** 02/11/2026  
**Chuyên gia phụ trách:** Senior ASP.NET Core 8 Engineer, Healthcare Billing Specialist, SQL Server Engineer & QA Automation Engineer  
**Nhánh Git:** `fix/billing-final` (Worktree cô lập tại `D:\SWP391\SWP391_Group6_Billing`)  
**Cơ sở dữ liệu kiểm thử cô lập:** `DentalClinic_Billing_TestClone`

---

## 1. TỔNG QUAN VÀ NGUYÊN TẮC CÔ LẬP LÀM VIỆC SONG SONG (PARALLEL WORK PROTOCOL)

Nhằm đảm bảo an toàn tuyệt đối khi làm việc đồng thời với tác vụ TASK A (chuyên trách phân bổ phòng, ghế, dịch vụ khoa và ca khám Appointment 117 trên thư mục gốc `D:\SWP391\SWP391_Group6`), TASK B đã thiết lập cơ chế cô lập nghiêm ngặt:

1. **Cô lập mã nguồn (Git Worktree):**
   - Đã tạo worktree độc lập tại `D:\SWP391\SWP391_Group6_Billing` dựa trên commit baseline `afb3373` (`release/preview`).
   - Tên nhánh công việc: `fix/billing-final`.
   - **Tất cả các thay đổi chỉ được thực hiện trong worktree này.** Tuyệt đối không chỉnh sửa đè lên thư mục của TASK A.

2. **Cô lập Cơ sở Dữ liệu (Database Isolation):**
   - Không can thiệp hay sửa đổi database sản xuất/chính `DentalClinicManagementDB`.
   - Tạo bản sao lưu (backup) và phục hồi (restore) sang database kiểm thử riêng biệt: `DentalClinic_Billing_TestClone`.
   - Kiểm thử tương tranh thanh toán đa luồng thực tế (SQL Server Concurrency Integration Tests) được thực thi hoàn toàn trên `DentalClinic_Billing_TestClone`.

3. **Cô lập tiến trình (Process Isolation):**
   - Không dừng (kill), khởi động lại hoặc can thiệp vào các tiến trình đang chạy của TASK A (Backend PID 37836 trên cổng 7350/5350, Frontend PID 21796 trên cổng 7129/5049).

4. **Giới hạn phạm vi tệp tin (File Ownership Boundaries):**
   - **Tuyệt đối không chỉnh sửa:** `AppointmentService.ManagerConfirmAsync`, bộ điều phối phòng/ghế/bác sĩ, dữ liệu `DepartmentServices`, cấu hình phòng khám, tệp tin cấu hình kết nối database trong repo gốc.
   - **Các tệp tin thuộc quyền hạn TASK B đã hoàn thiện:**
     - `BE/DentalClinic.Infrastructure/Services/BillingService.cs`
     - `FE/DentalClinic.Web/Views/Receptionist/Billing/Detail.cshtml`
     - `BE/DentalClinic.Tests/Billing/BillingFlowTests.cs`
     - `BE/DentalClinic.Tests/Concurrency/SqlServerPaymentConcurrencyIntegrationTests.cs`

---

## 2. MA TRẬN QUY TRÌNH HẬU KHÁM (POST-EXAMINATION BUSINESS WORKFLOW MATRIX)

Bảng ma trận truy vết luồng nghiệp vụ chi tiết từ khi kết thúc khám đến khi thanh toán và tra cứu hồ sơ:

| Bước | Hành động Nghiệp vụ | Giao diện Frontend | Controller / Route | API Endpoint | Service Backend | Thực thể DB & Trạng thái thay đổi | Quyền hạn (Role) |
|---|---|---|---|---|---|---|---|
| **1** | Bác sĩ khám lâm sàng, chỉ định dịch vụ điều trị | Bác sĩ khám bệnh (`/Dentist/Appointments/Exam/{id}`) | `DentistAppointmentController.Exam` | `POST /api/dentist/appointments/{id}/clinical-entry` | `ClinicalService.AddClinicalEntryAsync` | `ClinicalEntries` (Tạo mới ghi chú, chẩn đoán ban đầu) | `Dentist` |
| **2** | Ghi nhận buổi điều trị & dịch vụ thực hiện | Chi tiết điều trị (`/Dentist/Treatment/RecordSession`) | `DentistTreatmentController.RecordSession` | `POST /api/dentist/treatment-sessions` | `TreatmentService.CreateTreatmentSessionAsync` | `TreatmentSessions` (Tạo mới, `SessionNumber`, dịch vụ thực hiện, ghi chú tiến trình) | `Dentist` |
| **3** | Kê đơn thuốc ngoại trú (nếu có) | Kê đơn (`/Dentist/Prescription/Create/{id}`) | `DentistPrescriptionController.Create` | `POST /api/dentist/prescriptions` | `PrescriptionService.CreatePrescriptionAsync` | `Prescriptions`, `PrescriptionItems` (Đơn thuốc hướng dẫn mua ngoài) | `Dentist` |
| **4** | Hoàn tất ca khám lâm sàng | Nút "Hoàn tất ca khám" | `DentistAppointmentController.Complete` | `POST /api/dentist/appointments/{id}/complete` | `AppointmentService.DentistCompleteAppointmentAsync` | `Appointments.Status`: `InService` $\rightarrow$ `Completed`, ghi nhận `CompletedAt` | `Dentist` |
| **5** | Lễ tân mở giao diện lập hóa đơn | Danh sách chờ thanh toán (`/Receptionist/Billing/Pending`) | `ReceptionistBillingController.Pending` | `GET /api/billing/preparation/{appointmentId}` | `BillingService.GetAppointmentBillingPreparationAsync` | Lấy thông tin bệnh nhân, dịch vụ chính và các dịch vụ phát sinh từ `TreatmentSessions` | `Receptionist`, `Admin` |
| **6** | Lập hóa đơn & áp mã giảm giá / VAT | Lập hóa đơn (`/Receptionist/Billing/Create/{appointmentId}`) | `ReceptionistBillingController.Create` | `POST /api/billing/invoices` | `BillingService.CreateInvoiceAsync` | `Invoices` (`Status`: `Unpaid`), `InvoiceItems` (Chi tiết dòng tiền + thuế VAT), khóa tuần tự hóa `sp_getapplock` | `Receptionist`, `Admin` |
| **7** | Ghi nhận thanh toán (Tiền mặt / Chuyển khoản / Quẹt thẻ) | Chi tiết hóa đơn (`/Receptionist/Billing/Detail/{id}`) | `ReceptionistBillingController.ProcessPayment` | `POST /api/billing/payments` | `BillingService.ProcessPaymentAsync` | `Payments` (`Status`: `Successful`), `Invoices.Status`: `Unpaid` $\rightarrow$ `PartiallyPaid` $\rightarrow$ `Paid`. **Giữ nguyên trạng thái lâm sàng của ca khám!** | `Receptionist`, `Admin` |
| **8** | Bệnh nhân tra cứu hóa đơn & lịch sử điều trị | Trang hóa đơn (`/Patient/Invoices`) & Bệnh án (`/Patient/MedicalRecords`) | `PatientInvoiceController`, `PatientMedicalRecordsController` | `GET /api/billing/patient/invoices`, `GET /api/treatment/patient-history` | `BillingService.GetPatientInvoicesAsync`, `TreatmentService.GetPatientTreatmentHistoryAsync` | Đọc an toàn (`AsNoTracking`), cô lập theo `PatientUserId`, bảo toàn lịch sử các lần khám riêng biệt | `Patient` |

---

## 3. TÁCH RỜI VÒNG ĐỜI LÂM SÀNG VÀ TÀI CHÍNH (CLINICAL VS FINANCIAL DECOUPLING - P0-01)

### 3.1. Phân tích nguyên nhân gốc rễ (Root Cause Analysis)
Trước khi chuẩn hóa, hệ thống tồn tại các rủi ro:
1. Giao diện `FE/DentalClinic.Web/Views/Receptionist/Billing/Detail.cshtml` tại dòng 444 hiển thị thông báo gây hiểu lầm cho thu ngân: *"Khi thu đủ 100% tiền, hóa đơn sẽ chuyển sang trạng thái Đã thanh toán và ca hẹn sẽ được tự động hoàn tất"*.
2. Nguy cơ thu ngân thu tiền một ca đang ở trạng thái `CheckedIn` (đặt cọc trước) hoặc `InService` (đang trên ghế nha) sẽ làm thay đổi trạng thái ca khám thành `Completed`, tước đoạt quyền kết luận chuyên môn của Bác sĩ.

### 3.2. Quy tắc nghiệp vụ bắt buộc đã được thực thi
1. **Quyền hạn duy nhất:** Trạng thái `AppointmentStatus.Completed` đại diện cho việc **bác sĩ hoàn tất thủ thuật chuyên môn**. Chỉ có bác sĩ phụ trách thông qua `AppointmentService.DentistCompleteAppointmentAsync` mới có thẩm quyền chuyển ca khám sang `Completed`.
2. **Độc lập tài chính:** `InvoiceStatus.Paid` chỉ đại diện cho việc **tất toán nghĩa vụ tài chính**. Phương thức `BillingService.ProcessPaymentAsync` chỉ cập nhật trạng thái hóa đơn (`Unpaid` $\rightarrow$ `PartiallyPaid` $\rightarrow$ `Paid`) và ghi nhận bản ghi `Payment`. **Tuyệt đối không tác động hay ghi đè lên `Appointment.Status`**.
3. **Tính linh hoạt trong thanh toán:**
   - Ca khám `CheckedIn`: Được phép tạo hóa đơn và thu tiền cọc/toàn bộ trước khi vào phòng khám. Trạng thái ca khám vẫn giữ nguyên `CheckedIn`.
   - Ca khám `InService`: Được phép lập hóa đơn các dịch vụ phát sinh và thanh toán. Trạng thái vẫn là `InService` để Bác sĩ tiếp tục điều trị.
   - Ca khám `Completed`: Lập hóa đơn và thu tiền sau khám. Trạng thái ca khám vẫn là `Completed`.
4. **Phòng vệ trạng thái không hợp lệ (Defensive Guardrails):**
   Trong `BillingService.CreateInvoiceAsync`, hệ thống lập tức từ chối lập hóa đơn cho các ca khám ở trạng thái không thể khám:
   ```csharp
   var nonBillableStatuses = new[]
   {
       AppointmentStatus.Cancelled,
       AppointmentStatus.NoShow,
       AppointmentStatus.PendingReceptionReview,
       AppointmentStatus.PendingDepartmentReview,
       AppointmentStatus.AwaitingPatientResponse,
       AppointmentStatus.Rejected,
       AppointmentStatus.Withdrawn,
       AppointmentStatus.Expired
   };
   if (nonBillableStatuses.Contains(appt.Status))
   {
       return ApiResponse<InvoiceDto>.Fail(
           $"Không thể lập hóa đơn cho ca khám ở trạng thái '{appt.Status}'. Ca khám cần được tiếp nhận hoặc hoàn tất khám chuyên môn.");
   }
   ```
5. **Chuẩn hóa Giao diện:**
   Đã sửa đổi tệp `Detail.cshtml` dòng 444 thành:
   ```html
   <small class="text-muted d-block mt-2">
       <i class="bi bi-info-circle me-1"></i>Khi thu đủ 100% số tiền, hóa đơn sẽ chuyển sang trạng thái <strong class="text-success">Đã thanh toán</strong>. Trạng thái khám chuyên môn của ca hẹn được quản lý độc lập bởi Bác sĩ điều trị.
   </small>
   ```

---

## 4. KIẾN TRÚC KHÓA TƯƠNG TRANH SQL SERVER (CONCURRENCY & APPLOCK ARCHITECTURE - P0-02)

Để giải quyết triệt để vấn đề nhiều thu ngân cùng mở thanh toán một lúc hoặc bấm đúp nút thanh toán dẫn đến thu thừa tiền (overpayment) hoặc tạo trùng lặp hóa đơn, hệ thống đã ứng dụng khóa ứng dụng phân tán cấp hệ quản trị:

### 4.1. Cơ chế khóa tuần tự hóa Lập hóa đơn (`CreateInvoiceAsync`)
Sử dụng stored procedure `sp_getapplock` ở cấp độ Transaction:
- **Khóa:** `Lock_Appointment_Invoice_{AppointmentId}`
- **LockMode:** `Exclusive`
- **LockOwner:** `Transaction`
- **Timeout:** 5000 ms (5 giây)
- **Quy trình:**
  1. Mở giao dịch `BeginTransactionAsync(IsolationLevel.ReadCommitted)`.
  2. Yêu cầu khóa độc quyền theo ID ca khám.
  3. Kiểm tra xem ca khám đã có hóa đơn chưa (`Invoices.Any(i => i.AppointmentId == request.AppointmentId)`).
  4. Nếu đã có $\rightarrow$ Trả về thông báo lỗi thân thiện ngay lập tức và Rollback khóa.
  5. Nếu chưa có $\rightarrow$ Sinh hóa đơn, lưu DB, Commit giao dịch và tự động nhả khóa.

### 4.2. Cơ chế khóa tuần tự hóa Thanh toán (`ProcessPaymentAsync`)
- **Khóa:** `Lock_Invoice_Payment_{InvoiceId}`
- **LockMode:** `Exclusive`
- **LockOwner:** `Transaction`
- **Timeout:** 5000 ms (5 giây)
- **Kiểm soát rủi ro:**
  1. Kiểm tra mã giao dịch trùng lặp (`TransactionReference`) trên cùng hóa đơn $\rightarrow$ Ngăn chặn submit trùng lặp.
  2. Tính toán chính xác tổng tiền đã trả trước đó (`alreadyPaid`) ngay trong giao dịch có khóa.
  3. Kiểm tra cận: `request.Amount <= remaining`. Nếu vượt quá $\rightarrow$ Báo lỗi và hủy giao dịch, ngăn chặn tuyệt đối tình trạng overpayment.
  4. Cập nhật trạng thái `PartiallyPaid` hoặc `Paid`.

### 4.3. Kết quả kiểm thử thực tế đa luồng trên SQL Server
Tệp kiểm thử `BE/DentalClinic.Tests/Concurrency/SqlServerPaymentConcurrencyIntegrationTests.cs` đã chạy trên database `DentalClinic_Billing_TestClone`:
- `SqlServer_ConcurrentPayments_TotalPaidNeverExceedsInvoiceTotal`: 10 luồng thanh toán đồng thời tranh chấp hóa đơn $\rightarrow$ **Kết quả:** Tổng tiền thanh toán thành công chính xác 100% bằng giá trị hóa đơn, không một đồng nào bị overpay.
- `SqlServer_ConcurrentInvoiceCreation_OnlyOneSucceeds_SecondFailsGracefully`: 2 luồng đồng thời tạo hóa đơn cho cùng 1 ca khám $\rightarrow$ **Kết quả:** Đúng 1 hóa đơn được tạo duy nhất, luồng thứ 2 nhận thông báo lỗi từ chối hợp lệ.

---

## 5. CÔNG THỨC TÍNH TOÁN TÀI CHÍNH, GIẢM GIÁ VÀ VAT (P0-03)

### 5.1. Mô hình toán học chuẩn xác
Công thức tính toán được thống nhất đồng bộ 100% giữa Frontend JavaScript (`Create.cshtml`) và Backend C# (`BillingService.cs`):

$$\text{SubTotal} = \sum_{i=1}^{n} (\text{Quantity}_i \times \text{UnitPrice}_i)$$

$$\text{Discount} = \text{Clamp}(\text{DiscountAmount}, 0, \text{SubTotal})$$

$$\text{DiscountedSubTotal} = \text{SubTotal} - \text{Discount}$$

$$\text{TaxAmount} = \text{Round}\left(\text{DiscountedSubTotal} \times \frac{\text{TaxPercent}}{100}, 2\right)$$

$$\text{TotalAmount} = \text{Round}(\text{DiscountedSubTotal} + \text{TaxAmount}, 2)$$

$$\text{RemainingAmount} = \max(0, \text{TotalAmount} - \text{AlreadyPaid})$$

### 5.2. Các ràng buộc biên (Boundary Invariants)
1. **Kiểm tra giảm giá âm:** Yêu cầu `request.DiscountAmount >= 0`. Nếu gửi số âm $\rightarrow$ Backend trả về lỗi: *"Số tiền giảm giá không thể âm."*
2. **Kiểm tra thuế suất VAT:** Yêu cầu `request.TaxPercent >= 0 && request.TaxPercent <= 100`. Nếu vượt biên $\rightarrow$ Backend trả về lỗi: *"Thuế suất VAT phải nằm trong khoảng từ 0% đến 100%."*
3. **Cắt khống chế giảm giá (Clamping):** Nếu `DiscountAmount > SubTotal`, hệ thống tự động gán `Discount = SubTotal`. Do đó `DiscountedSubTotal = 0`, `TotalAmount = 0`, tuyệt đối không bao giờ xảy ra tổng tiền âm.
4. **Cơ chế phím tắt Voucher trên giao diện:**
   - `GIAM10`, `KHACHHANGMOI`, `DENTAL10`: Giảm 10% trên `SubTotal`.
   - `GIAM20`, `VIP20`: Giảm 20% trên `SubTotal`.
   - `GIAM50K`: Giảm trực tiếp 50.000 đ (khống chế tối đa bằng `SubTotal`).
   - `GIAM100K`, `FREEKHAM`: Giảm trực tiếp 100.000 đ (khống chế tối đa bằng `SubTotal`).
5. **Tái hiện nguyên vẹn hóa đơn (`MapToInvoiceDto`):**
   Khoản thuế VAT được lưu trữ thành một dòng `InvoiceItem` đặc biệt với tiền tố `[Thuế VAT ...%]`. Khi xuất DTO hiển thị cho Lễ tân và Bệnh nhân, hệ thống tách bóc chính xác dòng VAT để hiển thị riêng trường `TaxPercent` và `TaxAmount`, đồng thời giữ nguyên danh sách các dịch vụ gốc.

---

## 6. BẢO TOÀN HỒ SƠ BUỔI ĐIỀU TRỊ VÀ LỊCH SỬ BỆNH NHÂN ĐIỀU TRỊ LẶP LẠI (RETURNING PATIENTS)

### 6.1. Cấu trúc liên kết dữ liệu
- Quan hệ dữ liệu trong kiến trúc:
  - `UserAccount (Patient)` $1 \rightarrow N$ `Appointments`
  - `Appointment` $1 \rightarrow N$ `TreatmentSessions`
  - `Appointment` $1 \rightarrow 1$ `Invoice`
- Mỗi lần bệnh nhân đến tái khám, hệ thống sinh ra một `AppointmentId` mới hoàn toàn (`AppointmentCode` mới, ngày khám mới, bác sĩ mới hoặc cũ).
- Các `TreatmentSession` và `ClinicalEntry` được liên kết chặt chẽ theo khóa ngoại `AppointmentId`.

### 6.2. Kiểm chứng tính độc lập của lịch sử điều trị
- **Không ghi đè:** Khi bệnh nhân quay lại khám lần thứ 2, thứ 3..., toàn bộ hồ sơ của các lần khám trước (chẩn đoán, hình ảnh, dịch vụ điều trị, đơn thuốc, hóa đơn) vẫn được lưu trữ nguyên vẹn dưới `AppointmentId` cũ.
- **Truy vấn an toàn:**
  - `TreatmentService.GetPatientTreatmentHistoryAsync(patientUserId)`: Truy vấn toàn bộ các ca hẹn của bệnh nhân, sắp xếp theo thời gian giảm dần (`OrderByDescending(a => a.ScheduledStart)`), nhóm các buổi điều trị theo từng đợt khám rõ ràng.
  - `BillingService.GetPatientInvoicesAsync(patientUserId)`: Chỉ trả về danh sách hóa đơn gắn đúng với bệnh nhân đó, lọc chặt theo `i.Appointment.PatientUserId == patientUserId`.
- Kiểm thử tích hợp tại `ReturningPatientAndFollowUpIntegrationTests.cs` xác nhận:
  - Bệnh nhân quay lại khám lần 2 tạo ca khám mới thành công.
  - Hồ sơ lần 1 và lần 2 hoàn toàn tách biệt, không lẫn lộn dữ liệu điều trị và tài chính.

---

## 7. RANH GIỚI TÀI CHÍNH ĐƠN THUỐC NGOẠI TRÚ (OUTPATIENT PRESCRIPTIONS)

- **Mô hình phòng khám:** DentalCare là cơ sở khám chữa bệnh nha khoa ngoại trú, không trực tiếp kinh doanh hoặc bán lẻ thuốc tân dược tại quầy thu ngân.
- **Quy trình đơn thuốc:** Bác sĩ kê đơn điện tử trong giao diện khám bệnh. Đơn thuốc (`Prescriptions` & `PrescriptionItems`) chứa thông tin hoạt chất, liều dùng, cách uống, lời dặn của Bác sĩ và mã đơn thuốc để bệnh nhân mang ra hiệu thuốc tiêu chuẩn bên ngoài mua thuốc.
- **Ranh giới tài chính:** Đơn thuốc **không** tính tiền vào hóa đơn dịch vụ của phòng khám. Hóa đơn của phòng khám chỉ bao gồm phí dịch vụ khám, thủ thuật nha khoa (nhổ răng, trám răng, cạo vôi, gắn mắc cài...) và thuế VAT tương ứng.
- **Hiển thị bệnh nhân:** Trong trang chi tiết bệnh án (`Patient/MedicalRecords/Detail`), bệnh nhân xem được cả tóm tắt buổi điều trị, đơn thuốc hướng dẫn và hóa đơn dịch vụ tương ứng mà không bị nhầm lẫn về chi phí thuốc.

---

## 8. KIỂM TOÁN GIAO DIỆN RAZOR & BẢO MẬT PHÂN QUYỀN (UI/UX & SECURITY AUDIT)

### 8.1. Màn hình của Lễ tân / Thu ngân (`ReceptionistBillingController`)
- `/Receptionist/Billing/Pending`: Hiển thị danh sách các ca khám đủ điều kiện lập hóa đơn. Đã áp dụng bộ lọc trạng thái và tìm kiếm.
- `/Receptionist/Billing/Create/{appointmentId}`: Màn hình lập hóa đơn. Tự động nạp dịch vụ đã khám, hỗ trợ thêm bớt dòng dịch vụ, tính toán thời gian thực với JavaScript, áp mã voucher, kiểm tra hợp lệ trước khi gửi.
- `/Receptionist/Billing/Detail/{id}`: Chi tiết hóa đơn, lịch sử các lần thanh toán, xuất mã VietQR động theo chuẩn Napas 247, nút ghi nhận thanh toán tiền mặt/chuyển khoản/thẻ tín dụng.
- `/Receptionist/Billing/Print/{id}`: Mẫu in hóa đơn chuẩn hóa, căn lề trang in A4/A5, hiển thị đầy đủ thông tin phòng khám, bác sĩ, khách hàng và chữ ký.

### 8.2. Màn hình của Bệnh nhân (`PatientInvoiceController` & `PatientMedicalRecordsController`)
- `/Patient/Invoices`: Danh sách hóa đơn của chính bệnh nhân đang đăng nhập.
- `/Patient/Invoices/Detail/{id}`: Chi tiết hóa đơn bệnh nhân.
- `/Patient/Invoices/Print/{id}`: Bản xem trước và in hóa đơn dành cho bệnh nhân.
- `/Patient/MedicalRecords`: Lịch sử khám bệnh toàn diện theo dòng thời gian.
- `/Patient/MedicalRecords/Detail/{appointmentId}`: Chi tiết từng ca khám, diễn tiến điều trị và đơn thuốc.

### 8.3. Bảo mật phân quyền và kiểm soát đa người dùng (Multi-tenant Security Isolation)
Trong `BillingController.cs`:
```csharp
var role = GetCurrentUserRole();
var userId = GetCurrentUserId();
if (role == UserRole.Patient && result.Data.PatientUserId != userId)
{
    return Forbid();
}
```
Bệnh nhân A tuyệt đối không thể xem trộm hóa đơn của Bệnh nhân B bằng cách thay đổi ID trên URL. Nếu cố tình truy cập, API sẽ chặn đứng với mã lỗi HTTP 403 Forbid.

---

## 9. TỔNG HỢP KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (TEST EXECUTION SUMMARY)

Hệ thống đã thực thi toàn bộ các bộ kiểm thử tự động (Unit Tests, SQL Server Concurrency Tests, Integration Tests) với tỉ lệ thành công 100%:

| Bộ kiểm thử | Thể loại | Số lượng Test | Kết quả | Thời gian chạy |
|---|---|---|---|---|
| **BillingFlowTests** | Unit Tests (XUnit + InMemory) | 25 | **25/25 PASSED** | ~1.0 s |
| **SqlServerPaymentConcurrencyIntegrationTests** | Concurrency Integration (SQL Server thật) | 5 | **5/5 PASSED** | ~6.0 s |
| **ReturningPatientAndFollowUpIntegrationTests** | Integration Tests | 1 | **1/1 PASSED** | ~1.5 s |
| **Toàn bộ Backend (DentalClinic.Tests)** | Regression Suite | 418 | **418/418 PASSED** | ~13.0 s |
| **Toàn bộ Frontend (DentalClinic.Web.Tests)** | MVC & UI Tests | 259 | **259/259 PASSED** | ~6.0 s |
| **TỔNG CỘNG HỆ THỐNG** | **Full System Suite** | **677** | **677/677 PASSED (100%)** | **~27 s** |

### Chi tiết các kịch bản kiểm thử trọng điểm mới được bổ sung và xác nhận:
1. `CalculateTotal_DiscountWithoutTax_ShouldCalculateCorrectly`: Tổng phụ 1.000.000 đ, giảm 100.000 đ, thuế 0% $\rightarrow$ Tổng thu 900.000 đ.
2. `CalculateTotal_DiscountWith10PercentTax_ShouldCalculateTaxOnDiscountedSubtotal`: Tổng phụ 1.000.000 đ, giảm 100.000 đ (gốc chịu thuế 900.000 đ), thuế VAT 10% (90.000 đ) $\rightarrow$ Tổng thu 990.000 đ.
3. `CalculateTotal_DiscountExceedingSubtotal_ShouldClampDiscountAndResultInZeroTotal`: Tổng phụ 500.000 đ, giảm 600.000 đ $\rightarrow$ Tự động cắt về 500.000 đ, Tổng thu 0 đ (không bao giờ âm).
4. `CreateInvoice_NegativeDiscount_ShouldFailValidation`: Giảm giá -50.000 đ $\rightarrow$ Bị chặn hợp lệ.
5. `CreateInvoice_InvalidTaxPercent_ShouldFailValidation`: Thuế -5% hoặc 105% $\rightarrow$ Bị chặn hợp lệ.
6. `CreateInvoice_NonBillableAppointmentStatus_ShouldFailValidation`: 8 trạng thái không hợp lệ (`Cancelled`, `NoShow`, v.v.) $\rightarrow$ Bị từ chối lập hóa đơn.
7. `ProcessPayment_TwoStagePartialToFullPayment_ShouldTransitionCorrectly`: Đặt cọc 500.000 đ $\rightarrow$ `PartiallyPaid`, thanh toán tiếp 1.000.000 đ $\rightarrow$ `Paid`. Trạng thái ca khám bảo toàn `InService`.
8. `GetPatientInvoices_ShouldIsolateInvoicesByPatient`: Cách ly tuyệt đối dữ liệu hóa đơn giữa bệnh nhân 101 và bệnh nhân 102.
9. `SqlServer_ConcurrentPayments_TotalPaidNeverExceedsInvoiceTotal`: 10 luồng thanh toán đồng thời không bị race condition, không overpay.
10. `SqlServer_ConcurrentInvoiceCreation_OnlyOneSucceeds_SecondFailsGracefully`: 2 luồng lập hóa đơn đồng thời cho 1 ca khám $\rightarrow$ chỉ 1 hóa đơn được ghi nhận, không sinh 2 hóa đơn trùng lặp.

---

## 10. HƯỚNG DẪN TÍCH HỢP VỚI TASK A (INTEGRATION INSTRUCTIONS)

### 10.1. Danh sách tệp tin đã thay đổi (Non-overlapping Files)
Các thay đổi của TASK B không trùng lặp với phạm vi của TASK A (phòng/ghế/bác sĩ/Appointment 117):
- `BE/DentalClinic.Infrastructure/Services/BillingService.cs`
- `FE/DentalClinic.Web/Views/Receptionist/Billing/Detail.cshtml`
- `BE/DentalClinic.Tests/Billing/BillingFlowTests.cs`
- `BE/DentalClinic.Tests/Concurrency/SqlServerPaymentConcurrencyIntegrationTests.cs`
- `docs/system-audit/PARALLEL_BILLING_TREATMENT_FIX_REPORT.md`

### 10.2. Các bước tích hợp an toàn
Khi TASK A hoàn thành và sẵn sàng tích hợp:
1. **Commit và đẩy nhánh của TASK B:**
   Nhánh `fix/billing-final` đã được lưu trữ trong git local của repository.
2. **Thực hiện merge không xung đột (Fast-forward hoặc No-FF):**
   Tại thư mục dự án chính (`D:\SWP391\SWP391_Group6`):
   ```powershell
   git checkout <branch-task-a>
   git merge fix/billing-final --no-ff -m "chore: integrate Task B final billing, payment and treatment stabilization"
   ```
3. **Kiểm tra hồi quy sau tích hợp (Post-Integration Regression Commands):**
   ```powershell
   dotnet build DentalClinic.Backend.sln
   dotnet build DentalClinic.Web.sln
   dotnet test BE/DentalClinic.Tests --filter "FullyQualifiedName~Billing"
   dotnet test BE/DentalClinic.Tests --filter "FullyQualifiedName~Concurrency"
   dotnet test FE/DentalClinic.Web.Tests
   ```
4. **Dọn dẹp worktree sau khi hợp nhất thành công:**
   ```powershell
   git worktree remove D:\SWP391\SWP391_Group6_Billing
   ```

---

## 11. BẢNG KIỂM TRA DÀNH CHO BÊN THẨM ĐỊNH (VERIFICATION CHECKLIST FOR REVIEWER)

- [x] Đã cô lập hoàn toàn môi trường làm việc bằng Git Worktree riêng (`SWP391_Group6_Billing`).
- [x] Đã sử dụng database kiểm thử riêng biệt (`DentalClinic_Billing_TestClone`), không đụng chạm `DentalClinicManagementDB`.
- [x] Không can thiệp hoặc tắt tiến trình của TASK A đang chạy trên các cổng 7350, 5350, 7129, 5049.
- [x] Tách rời dứt điểm: Thanh toán đủ (`Paid`) KHÔNG làm thay đổi trạng thái ca hẹn sang `Completed`.
- [x] Khóa ứng dụng `sp_getapplock` bảo vệ đồng thời cả Lập hóa đơn và Thanh toán đa luồng.
- [x] Tính toán giảm giá và thuế VAT chuẩn xác; chặn giảm giá âm, thuế âm/vượt quá 100%, chặn tổng tiền âm.
- [x] Bảo toàn tuyệt đối lịch sử điều trị và buổi điều trị cho bệnh nhân tái khám nhiều lần.
- [x] Đơn thuốc ngoại trú được tách bạch khỏi hóa đơn dịch vụ phòng khám.
- [x] Toàn bộ 677/677 bài test tự động đều vượt qua thành công (100% Pass).
