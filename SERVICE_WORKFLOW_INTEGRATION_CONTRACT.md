# SERVICE WORKFLOW INTEGRATION CONTRACT

**Mã tài liệu:** `CONTRACT-SWP391-SERVICES-CLINICAL-001`  
**Dự án:** DentalCare Clinic Management System (SWP391 Group 6)  
**Worktree:** `D:\SWP391\DentalCare-Services`  
**Branch tác giả:** `feature/fix-admin-services-workflow`  
**Bên tích hợp:** Task B (Clinical & Billing — Anti 2, branch `feature/fix-clinical-billing-demo`)  
**Ngày phát hành:** 17/11/2026  
**Trạng thái tích hợp:** `INTEGRATION REQUIRED (Pending Anti 2 Task B)`

---

## 1. MỤC TIÊU & PHẠM VI HỢP ĐỒNG

Hợp đồng này quy định chi tiết giao diện kỹ thuật (API, DTO, ID liên kết, quy tắc nghiệp vụ) giữa:
- **Module Quản lý Dịch vụ (Admin Services Management):** Nơi Quản trị viên (SystemAdministrator) thiết lập đơn giá, chuyên khoa, thời lượng và **quy trình xác nhận 1 bước** cho từng dịch vụ nha khoa.
- **Module Lâm sàng Bác sĩ (Dentist Clinical Execution):** Nơi Bác sĩ điều trị hoặc Kỹ thuật viên hình ảnh tiếp nhận lượt khám, kiểm tra điều kiện thực hiện và xác nhận hoàn tất dịch vụ.

> [!IMPORTANT]
> **Nguyên tắc phân định trách nhiệm:**
> - Branch `feature/fix-admin-services-workflow` sở hữu cấu hình dịch vụ, lưu trữ phiên bản quy trình xác nhận 1 bước, và các API kiểm tra bảo vệ xóa/ngừng cung cấp.
> - Branch `feature/fix-clinical-billing-demo` sở hữu màn hình lâm sàng bác sĩ (`/Dentist/MedicalRecords`), ghi nhận `PerformedServices`, chỉ định và chụp `Diagnostics` (X-quang), kê đơn thuốc và thanh toán hóa đơn.
> - Hai bên giao tiếp thông qua Database Contract và REST API Contract dưới đây. Tuyệt đối không tạo module song song hay hardcode dữ liệu xác nhận.

---

## 2. API ENDPOINTS HIỆN CÓ

### 2.1. Lấy thông tin cấu hình quy trình dịch vụ
- **URL:** `GET /api/admin/services/{id}/workflow`
- **Quyền truy cập:** `SystemAdministrator` (và Bác sĩ thông qua service injection hoặc authorized gateway).
- **Query Parameters:**
  - `version` (int, optional): Số phiên bản quy trình cần truy xuất. Nếu để trống (`null`), hệ thống tự động trả về phiên bản có hiệu lực mới nhất (`active latest version`).
- **Mã phản hồi:**
  - `200 OK`: Trả về `ApiResponse<ServiceWorkflowDto>`.
  - `404 Not Found`: Khi không tìm thấy dịch vụ hoặc phiên bản chỉ định.

### 2.2. Thiết lập quy trình xác nhận dịch vụ (Admin)
- **URL:** `POST /api/admin/services/{id}/workflow`
- **Quyền truy cập:** `SystemAdministrator`
- **Request Body:** `ConfigureWorkflowRequest`
- **Mã phản hồi:**
  - `200 OK`: Trả về `ApiResponse<ServiceWorkflowDto>` với phiên bản mới tăng đơn điệu.
  - `400 Bad Request`: Khi dữ liệu cấu hình không hợp lệ.

### 2.3. Kiểm tra khả năng xóa an toàn
- **URL:** `GET /api/admin/services/{id}/can-delete`
- **Quyền truy cập:** `SystemAdministrator`
- **Response:** `ApiResponse<ServiceDeleteCheckDto>` (báo `CanDelete: false` và `ActiveReferenceCount > 0` nếu đã phát sinh bất kỳ bản ghi lâm sàng nào).

---

## 3. DATA CONTRACT (REQUEST / RESPONSE DTOs)

### 3.1. `ServiceWorkflowDto` (Response trả về cấu hình quy trình)

```csharp
public class ServiceWorkflowDto
{
    public long WorkflowId { get; set; }              // Id dịch vụ (DepartmentServiceId)
    public long DepartmentServiceId { get; set; }      // Id dịch vụ
    public string WorkflowName { get; set; }           // Ví dụ: "Quy trình: Nhổ Răng Khôn (v1)"
    public int Version { get; set; }                   // Phiên bản quy trình (1, 2, 3...)
    public bool IsActive { get; set; }                 // Trạng thái hiệu lực (true/false)
    public string ApprovalStatus { get; set; }         // "Approved"
    public long? ApprovedByUserId { get; set; }        // UserId của Admin phê duyệt
    public DateTime? ApprovedAt { get; set; }          // Thời điểm phê duyệt (UTC)
    public string? Description { get; set; }           // Mô tả dịch vụ / quy trình
    public List<ServiceWorkflowStepDto> Steps { get; set; } = new();
}
```

### 3.2. `ServiceWorkflowStepDto` (Chi tiết bước xác nhận)

```csharp
public class ServiceWorkflowStepDto
{
    public long StepId { get; set; }                  // Thứ tự bước (1)
    public long WorkflowId { get; set; }              // DepartmentServiceId
    public string StepName { get; set; }              // Tên bước xác nhận
    public string? Description { get; set; }          // Hướng dẫn thực hiện
    public int StepOrder { get; set; }                // Luôn là 1 (quy trình 1 bước)
    public bool IsRequired { get; set; }              // true
    public string? CompletionRequirement { get; set; }// Tiêu chí hoàn tất
    public string ExpectedDataType { get; set; }      // "None" (Dịch vụ thường) hoặc "Image" (X-quang)
}
```

---

## 4. QUY TẮC NGHIỆP VỤ & LIÊN KẾT DỮ LIỆU LÂM SÀNG

### 4.1. Phân loại dịch vụ & Yêu cầu kết quả

| Phân loại | `DiagnosticType` | Tên bước xác nhận mặc định | `ExpectedDataType` | Bắt buộc đính kèm tệp |
|---|---|---|:---:|:---:|
| **Dịch vụ điều trị** | `Treatment` / `Consultation` / `Surgery`... | *"Xác nhận đã thực hiện dịch vụ"* | `None` | **Không** |
| **Dịch vụ chẩn đoán hình ảnh** | `XRay` / `Diagnostic` / `Imaging` | *"Xác nhận đã chụp X-quang"* | `Image` | **Bắt buộc** (Ảnh chụp X-quang hợp lệ) |

### 4.2. Khóa liên kết dữ liệu giữa Admin Services và Bác sĩ khám

```
[DepartmentServices] (DepartmentServiceId)
         │
         ├── (1:N) ──> [AppointmentServices] (AppointmentServiceId)
         │                    │
         │                    ├── (1:1) ──> [PerformedServices] (PerformedServiceId, Notes, ToothNumber)
         │                    │
         │                    └── (1:N) ──> [Diagnostics] (DiagnosticId, ImagingType, FileUrl, Findings)
         │                                         │
         │                                         └── (1:N) ──> [DiagnosticImages] (ImageId, ImageUrl)
         │
         └── (Audit) ─> [AuditLogs] (EntityName='ServiceWorkflow', EntityId=DepartmentServiceId, Action='CONFIGURE')
```

- **`DepartmentServiceId`:** Khóa định danh dịch vụ niêm yết do Admin quản lý.
- **`AppointmentServiceId`:** Khóa liên kết lượt đặt lịch khám của bệnh nhân với dịch vụ.
- **`PerformedServices`:** Ghi nhận thực tế bác sĩ đã thực hiện dịch vụ nào trong ca điều trị (`TreatmentSessionId`).
- **`Diagnostics` & `DiagnosticImages`:** Nơi lưu trữ thông tin chỉ định chẩn đoán hình ảnh và các tệp ảnh chụp X-quang (URL tệp, thời điểm tải lên, bác sĩ đọc kết quả).

---

## 5. ĐIỀU KIỆN TÍCH HỢP CHO TASK B (ANTI 2)

### 5.1. Những điều kiện ĐÃ ĐƯỢC BACKEND HỖ TRỢ (Branch Services)
- [x] Lưu trữ phiên bản hóa quy trình 1 bước vào `AuditLogs` với `EntityName = "ServiceWorkflow"` và `Action = "CONFIGURE"`.
- [x] Tự động khởi tạo quy trình mặc định chuẩn theo loại dịch vụ (`Image` cho X-quang, `None` cho điều trị).
- [x] Endpoint `GET /api/admin/services/{id}/workflow` hỗ trợ đọc cấu hình mới nhất hoặc theo `version` lịch sử.
- [x] Ngăn chặn đổi `DepartmentId` nếu dịch vụ đã có dữ liệu lâm sàng liên kết (`AppointmentServices`, `PerformedServices`).
- [x] Endpoint `GET /api/admin/services/{id}/can-delete` kiểm tra 7 bảng liên kết lâm sàng và từ chối xóa nếu có lịch sử.
- [x] API `DeleteServiceAsync` kiểm tra ràng buộc FK trực tiếp trong database transaction để bảo vệ toàn vẹn dữ liệu.

### 5.2. Những điều kiện CẦN ANTI 2 TÍCH HỢP (Dentist Execution)
> [!WARNING]
> Các mục dưới đây thuộc phạm vi module Clinical của Anti 2 và được đánh dấu là:  
> **`INTEGRATION REQUIRED (Pending Anti 2 Task B)`**

1. **Giao diện Bác sĩ xác nhận dịch vụ (`/Dentist/MedicalRecords`):**
   - Khi bác sĩ bấm *"Hoàn tất dịch vụ"* hoặc *"Xác nhận thực hiện"*: Cần gọi `GetWorkflowAsync(serviceId)` để hiển thị đúng tên bước và kiểm tra yêu cầu kết quả.
2. **Kiểm tra ảnh X-quang trước khi đóng lượt khám:**
   - Đối với dịch vụ có `ExpectedDataType == "Image"`: Backend Clinical phải kiểm tra bảng `Diagnostics` / `DiagnosticImages` có ít nhất 1 ảnh hợp lệ (`FileUrl IS NOT NULL`). Nếu chưa có ảnh, từ chối xác nhận với thông báo: *"Dịch vụ X-quang yêu cầu phải tải lên hình ảnh chụp phim trước khi hoàn tất."*
3. **Chống trùng lặp `PerformedServices`:**
   - Đảm bảo mỗi `AppointmentServiceId` chỉ tạo duy nhất 1 bản ghi `PerformedService` khi bác sĩ xác nhận hoàn tất.
4. **Bảo toàn trạng thái Appointment:**
   - Không tự động đánh dấu toàn bộ `Appointment` sang `Completed` nếu còn dịch vụ khác trong cùng lịch hẹn chưa được thực hiện.

---

## 6. KẾT LUẬN & CAM KẾT

- **Admin Services Management:** Hoàn thành 100% các tiêu chí quản trị, persistence, versioning, data patch và FK audit.
- **Dentist Clinical Integration:** Hợp đồng API và DTO đã được ấn định chính xác. Không có blocker kỹ thuật phía Admin Services.
- **Trạng thái bàn giao:** `READY FOR CLINICAL INTEGRATION`.
