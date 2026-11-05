# BÁO CÁO KHẮC PHỤC LỖI BIÊN DỊCH FRONTEND TESTS MODULE BILLING

**Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)  
**Workspace:** `D:\SWP\SWP391_Group6`  
**Nhánh công việc (Fix Branch):** `fix/billing-frontend-test-compilation`  
**Nhánh cơ sở (Base Branch):** `develop`  
**Kỹ sư thực hiện:** Senior .NET 8 QA Engineer  
**Technical Lead & Reviewer:** ChatGPT  
**Thời điểm thực hiện:** 2026-11-05 15:06 UTC+7  

---

## 1. NGUYÊN NHÂN GỐC (ROOT CAUSE ANALYSIS)

### A. Triệu chứng & Mã lỗi Compiler
Khi chạy lệnh `dotnet test FE/DentalClinic.Web.Tests/DentalClinic.Web.Tests.csproj`, trình biên dịch C# báo 2 lỗi:
```text
FE/DentalClinic.Web.Tests/Controllers/ReceptionistBillingControllerTests.cs(131,46): error CS1503: Argument 1: cannot convert from 'DentalClinic.Web.Models.ApiDtos.CreateInvoiceRequest' to 'long' [D:\SWP\SWP391_Group6\FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj]
FE/DentalClinic.Web.Tests/Controllers/ReceptionistBillingControllerTests.cs(158,46): error CS1503: Argument 1: cannot convert from 'DentalClinic.Web.Models.ApiDtos.CreateInvoiceRequest' to 'long' [D:\SWP\SWP391_Group6\FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj]
```

### B. Phân tích Chi tiết Cơ chế Lỗi
1. **Thay đổi trong commit `68e3221`:**  
   Commit `68e322116ad93b6a7053f580d3af1f20f65399be` ("fix(billing): resolve 405 error on invoice creation and update DB constraints") đã thay đổi phương thức action trong `ReceptionistBillingController.cs`:
   ```csharp
   // Trước commit 68e3221:
   [HttpPost("Create")]
   [ValidateAntiForgeryToken]
   public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request)

   // Sau commit 68e3221:
   [HttpPost("Create")]
   [HttpPost("Create/{appointmentId:long}")]
   [ValidateAntiForgeryToken]
   public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, [FromRoute] long? appointmentId)
   ```
2. **Xung đột Quá tải Hàm (Overload Resolution):**  
   Controller đồng thời sở hữu phương thức action GET:
   ```csharp
   [HttpGet("Create/{appointmentId:long}")]
   public async Task<IActionResult> Create(long appointmentId)
   ```
   Do tham số thứ hai `[FromRoute] long? appointmentId` không có giá trị mặc định `= null`, khi mã unit test gọi `controller.Create(request)` với 1 đối số:
   - Trình biên dịch C# tìm các phương thức nhận đúng 1 tham số.
   - Phương thức POST nhận 2 tham số bắt buộc nên bị loại bỏ.
   - Phương thức GET nhận 1 tham số `long appointmentId` được chọn, dẫn đến lỗi ép kiểu `CS1503` (không thể ép `CreateInvoiceRequest` sang `long`).
3. **Lỗi Kiểm tra Reflection về Thuộc tính CSRF:**  
   Tại dòng 64-66 của `ReceptionistBillingControllerTests.cs`:
   ```csharp
   var method = typeof(ReceptionistBillingController).GetMethod(
       nameof(ReceptionistBillingController.Create),
       new[] { typeof(CreateInvoiceRequest) });
   ```
   Reflection tìm phương thức `Create` với 1 tham số kiểu `CreateInvoiceRequest`. Khi method trong controller có 2 tham số `(CreateInvoiceRequest, long?)`, hàm `GetMethod` trả về `null`, dẫn đến assertion failure `Expected method not to be <null>`.

---

## 2. DANH SÁCH FILE THAY ĐỔI & NỘI DUNG SỬA ĐỔI

### File 1: `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistBillingController.cs`
- **Dòng sửa:** 70
- **Nội dung:** Bổ sung giá trị mặc định `= null` cho tham số `appointmentId`:
  ```csharp
  // Thay đổi:
  public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, [FromRoute] long? appointmentId = null)
  ```
- **Ý nghĩa:**
  - Tương thích hoàn toàn với route `[HttpPost("Create")]` (không truyền route parameter).
  - Tương thích với route `[HttpPost("Create/{appointmentId:long}")]` (có route parameter).
  - Khôi phục tính hợp lệ cho các lời gọi 1 tham số trong C# / Unit Test (`controller.Create(request)`).
  - **Không làm thay đổi** bất kỳ logic nghiệp vụ lập hóa đơn, routing hoặc thanh toán hiện có.

### File 2: `FE/DentalClinic.Web.Tests/Controllers/ReceptionistBillingControllerTests.cs`
- **Dòng sửa:** 66
  - Cập nhật kiểu tham số reflection cho bài test kiểm tra bảo mật CSRF:
    ```csharp
    var method = typeof(ReceptionistBillingController).GetMethod(
        nameof(ReceptionistBillingController.Create),
        new[] { typeof(CreateInvoiceRequest), typeof(long?) });
    ```
- **Bổ sung Unit Test mới:**
  - Thêm phương thức `Create_WithRouteAppointmentId_OverridesRequestBodyWhenMissing`: kiểm thử trực tiếp tính năng mới được đưa vào trong commit `68e3221` (khi `request.AppointmentId <= 0`, controller tự động lấy `appointmentId` từ route parameter).

---

## 3. KẾT QUẢ KIỂM THỬ TRƯỚC VÀ SAU KHI SỬA ĐỔI

### A. So sánh Trạng thái Frontend Tests

| Tiêu chí | Trước khi Sửa (Commit `70c45b6` trên `develop`) | Sau khi Sửa (Nhánh `fix/billing-frontend-test-compilation`) |
| :--- | :---: | :---: |
| **Biên dịch Frontend Test** | **FAILED (2 lỗi CS1503)** | **PASSED (0 errors, 0 warnings)** |
| **Trạng thái Test Suite** | **BLOCKED / NOT_RUN** | **PASSED (100%)** |
| **Số Test PASSED** | `0` | **`286`** |
| **Số Test FAILED** | `0` (không thể chạy) | **`0`** |
| **Số Test SKIPPED** | `0` | **`0`** |
| **Tổng số Tests FE** | — | **`286`** |

---

### B. Kiểm tra Toàn diện Regression trên Backend Tests

Lệnh chạy: `dotnet test BE/DentalClinic.Tests/DentalClinic.Tests.csproj --configuration Release`

| Nhóm Kiểm thử Backend | Trạng thái | Số lượng Passed | Số lượng Skipped | Số lượng Failed |
| :--- | :---: | :---: | :---: | :---: |
| **Follow-Up Module** (Scheduling, Reminder, E2E, SQL Server) | **PASSED** | 103 | 0 | 0 |
| **Các Phân hệ Khác** (Auth, Admin, Prescriptions, MedicalRecords, Appointments, Billing BE) | **PASSED** | 437 | 1 (`CloudinaryKey`) | 0 |
| **TỔNG CỘNG HỆ THỐNG BE** | **PASSED** | **540** | **1** | **0** |

*Kết luận: Không phát sinh bất kỳ hồi quy (regression) nào trên cả Frontend và Backend.*

---

## 4. THÔNG TIN COMMIT VÀ TRẠNG THÁI GIT

- **Nhánh làm việc:** `fix/billing-frontend-test-compilation`
- **Nhánh cơ sở:** `develop` (không tác động trực tiếp lên `develop` hoặc `main`)
- **Commit SHA:** `fa108c3`
- **Tình trạng:** Đã commit và push lên remote `origin/fix/billing-frontend-test-compilation`. Không tự ý merge vào `develop` hoặc `main`.
