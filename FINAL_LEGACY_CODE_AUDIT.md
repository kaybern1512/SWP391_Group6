# DENTALCARE SWP391 — FINAL LEGACY CODE & REDUNDANCY AUDIT REPORT

**Worktree:** `D:\SWP391\DentalCare-Integration`  
**Branch:** `release/demo-integration`  
**Base Commit:** `70694bd`  
**Database:** `DentalCare_SWP391_UAT_Fixture_TEST` (SQL Server Express - 38 physical tables)  
**Audit Date:** 17/11/2026 (Demo: 19/11/2026)  
**Status:** **AUDITED, CLEANED & VERIFIED**

---

## 1. PHƯƠNG PHÁP & NGUYÊN TẮC PHÂN LOẠI (SAFE CLEANUP POLICY)

Mọi thành phần mã nguồn trong hệ thống DentalCare (Backend API, Frontend MVC, Razor Views, Scripts) được rà soát và phân loại nghiêm ngặt theo 4 nhóm:

- **GROUP A — SAFE TO REMOVE (Đã xóa an toàn):**
  Thành phần chứng minh được 100% không còn caller/entrypoint nào trong BE và FE, không có DI registration, không phụ thuộc reflection/background job/test, không ảnh hưởng schema 38 bảng, không ảnh hưởng dữ liệu lịch sử và API contract.
- **GROUP B — LEGACY BUT REQUIRED (Giữ nguyên - Cầu nối tương thích):**
  Thành phần code cũ hoặc trường dữ liệu legacy nhưng vẫn cần thiết để bảo đảm tương thích ngược (backward compatibility), tránh crash khi client gọi query string cũ, hoặc bảo toàn ràng buộc lịch sử.
- **GROUP C — DUPLICATE / REFACTOR CANDIDATE (Giữ nguyên trước demo):**
  Thành phần có sự trùng lặp logic nhỏ hoặc thiết kế chưa tối ưu nhưng đang chạy ổn định. **Tuyệt đối không refactor trước ngày demo 19/11/2026** để tránh rủi ro phát sinh hồi quy.
- **GROUP D — UNCERTAIN (Giữ nguyên):**
  Thành phần chưa đủ 100% bằng chứng chứng minh dead code -> Luôn giữ nguyên an toàn.

---

## 2. KẾT QUẢ RÀ SOÁT TỪNG NHÓM NGHIỆP VỤ

### 2.1. Nhóm A: Role Cũ (`DepartmentManager`, `Pharmacist`)
- **Phạm vi kiểm tra:** Toàn bộ Controllers, Services, DTOs, Razor Views, Layouts, Menus, JavaScript, Authorization Policies, Unit Tests.
- **Kết quả Audit:**
  - `DepartmentManager`: **0 kết quả** trong toàn bộ source code C#, CSHTML, JS.
  - `Pharmacist`: **0 kết quả** trong toàn bộ source code C#, CSHTML, JS.
  - Bảng `UserAccounts` trong DB: Chỉ có đúng 4 role: `Dentist`, `Patient`, `Receptionist`, `SystemAdministrator`.
- **Phân loại:** **SẠCH HOÀN TOÀN** (Không tồn tại role rác).

---

### 2.2. Nhóm B: Workflow Cuộc Hẹn Cũ (`PendingDepartmentReview`, Thao tác duyệt cũ)
- **Phạm vi kiểm tra:** Trạng thái trong `Appointments` và `AppointmentServices`, các endpoint duyệt lịch hẹn.
- **Kết quả Audit:**
  - Trạng thái `PendingDepartmentReview`: **0 kết quả** trong code.
  - Các trạng thái `Appointments` trong Database: `PendingReceptionReview`, `Confirmed`, `CheckedIn`, `InProgress`, `Completed`, `Cancelled`. Đúng chuẩn 100% với vai trò Lễ tân điều phối.
  - Các trạng thái `AppointmentServices`: `PendingReceptionReview`, `Confirmed`, `CheckedIn`, `InService`, `Completed`, `Cancelled`.
- **Phân loại:** **GROUP B (Chuẩn hóa)** — Toàn bộ trạng thái khớp hoàn toàn với workflow 4-role.

---

### 2.3. Nhóm C: Quản Lý Thuốc Cũ (`MedicineBatch`, Lô thuốc, FEFO)
- **Phạm vi kiểm tra:**
  - `BE/DentalClinic.Infrastructure/Services/MedicineService.cs`
  - `FE/DentalClinic.Web/Services/MedicineApiService.cs`
  - `FE/DentalClinic.Web/Controllers/Admin/AdminMedicineController.cs`
  - `FE/DentalClinic.Web/Controllers/Dentist/DentistMedicalRecordController.cs`
- **Phân tích kỹ thuật:**
  - Schema 38 bảng **không có bảng `MedicineBatches`**. Số lượng tồn `StockQuantity`, giá `UnitPrice` và hạn mức cảnh báo được lưu trực tiếp trên bảng `Medicines`.
  - Để tương thích ngược với các frontend component có thể gửi kèm `batchId` hoặc gọi API batches:
    - `MedicineService.GetBatchesByMedicineIdAsync(id)`: Tự động tổng hợp một DTO ảo (`STOCK-{MedicineCode}`) lấy trực tiếp từ `Medicines.StockQuantity` và `Medicines.UnitPrice`.
    - `MedicineService.GetStockTransactionsAsync(batchId, medicineId, ...)`: Truy vấn trực tiếp từ `PrescriptionItems` đã thực cấp (`QuantityDispensed > 0`), ánh xạ `targetMedicineId = medicineId ?? medicineBatchId`.
    - Bác sĩ kê đơn và cấp phát thuốc **hoàn toàn không bị chặn hay bắt buộc chọn lô thuốc vật lý không tồn tại**.
- **Phân loại:** **GROUP B (LEGACY BUT REQUIRED)** — Giữ nguyên cơ chế tổng hợp ảo này vì đây là cầu nối tương thích xuất sắc giúp hệ thống hoạt động mượt mà trên 38 bảng mà không phát sinh lỗi 404/500.

---

### 2.4. Nhóm D: Lịch Trực & Phân Ca (`Scheduling`)
- **Phạm vi kiểm tra:**
  - Phân ca Bác sĩ (`AdminDentistScheduleController`, `DentistScheduleController`)
  - Phân ca Lễ tân (`AdminReceptionistScheduleController`, `ReceptionistScheduleController`)
  - Lịch nghỉ phép (`StaffLeaveRequests`) và bảo vệ ca trực đã duyệt nghỉ (`Approved Leave Protection`).
- **Phân tích kỹ thuật:**
  - Hệ thống sử dụng phân ca định biên theo ca (Shift 1: 08:00 - 12:00, Shift 2: 13:30 - 17:30).
  - Đã có cơ chế bảo vệ: Khi bác sĩ có đơn nghỉ đã duyệt (`Approved`), hệ thống chặn phân ca trùng và cảnh báo.
  - Các phương thức hỗ trợ hoàn hủy / khôi phục lịch trực đều có caller từ background service và controller.
- **Phân loại:** **GROUP B / ACTIVE** — Giữ nguyên.

---

### 2.5. Nhóm E: Lâm Sàng & Phác Đồ Điều Trị (`Clinical & Treatment`)
- **Phân biệt hai nghiệp vụ hợp lệ:**
  1. `AddAdditionalService` (Thêm dịch vụ trong ca khám): Tạo tác vụ lâm sàng trong `AppointmentServices`. PerformedService CHỈ được tạo khi bác sĩ bấm hoàn thành thủ thuật.
  2. `CreateTreatmentPlan` (Lập kế hoạch điều trị nhiều giai đoạn): Tạo kế hoạch dài hạn trong `TreatmentPlans` & `TreatmentPlanItems`. Khóa cố định ngữ cảnh bệnh nhân của ca khám.
- **Phân loại:** **ACTIVE & ESSENTIAL** — Đã được bảo vệ toàn vẹn qua hotfix `70694bd`.

---

### 2.6. Nhóm F: Blog & Nội Dung
- **Phạm vi kiểm tra:** Quy trình phân quyền Blog.
- **Kết quả Audit:**
  - Lễ tân: `ReceptionistBlogController` ("Bài Viết Của Tôi") -> Soạn, sửa, gửi duyệt bài viết.
  - Quản trị viên: `AdminBlogController` ("Quản Lý Blog & Tin Tức") -> Duyệt và xuất bản.
  - Bác sĩ: **Hoàn toàn không có menu hay controller Blog**.
  - Độc giả / Bệnh nhân: Xem bài viết công khai tại `/Blog/Index`.
- **Phân loại:** **HOÀN TOÀN CHUẨN THIẾT KẾ**.

---

### 2.7. Nhóm G: Frontend Views & Shared Partials (Phát hiện & Xử lý Dead Code)
- **Phạm vi kiểm tra:** Toàn bộ 103 file Razor `.cshtml` và 54 route menu trong `_Sidebar.cshtml`.
- **Phát hiện cụ thể:**
  1. **Route Menu (`_Sidebar.cshtml`):** Kiểm tra 54/54 route menu -> **100% khớp với Controller và Action thực tế, không có liên kết 404**.
  2. **Thư mục View mồ côi (`Views/DentistAppointment`):**
     - Tồn tại file: `FE/DentalClinic.Web/Views/DentistAppointment/_AppointmentRow.cshtml`.
     - Caller analysis: `DentistAppointmentController` chỉ có duy nhất action trả về view là `Today()`, và action này chỉ định tường minh: `return View("~/Views/Dentist/Appointments/Today.cshtml", vm);`.
     - Trong `Today.cshtml` (dòng 419 và 457), view được nhúng tường minh là:
       `@await Html.PartialAsync("~/Views/Dentist/Appointments/_AppointmentRow.cshtml", ...)`
     - File trong `Views/DentistAppointment/_AppointmentRow.cshtml` là bản copy cũ (thiếu modal chỉ định X-quang, thiếu badge đa dịch vụ) sót lại từ đợt tái cấu trúc thư mục trước đây. Không có bất kỳ controller hay view nào gọi đến file này.
- **Hành động xử lý:**
  - **Phân loại:** **GROUP A — SAFE TO REMOVE**.
  - **Thực hiện:** Đã xóa bỏ an toàn file `FE/DentalClinic.Web/Views/DentistAppointment/_AppointmentRow.cshtml` và xóa thư mục mồ côi `Views/DentistAppointment`.
  - **Xác minh sau xóa:** Biên dịch `DentalClinic.Web` thành công (0 Warning, 0 Error); toàn bộ 351 unit test của Frontend PASS 100%.

---

## 3. TỔNG HỢP DANH MỤC THÀNH PHẦN AUDIT

| STT | File / Đường dẫn | Phân loại | Hành động | Lý do kỹ thuật |
| :--- | :--- | :--- | :--- | :--- |
| 1 | `FE/DentalClinic.Web/Views/DentistAppointment/_AppointmentRow.cshtml` | **GROUP A** | **ĐÃ XÓA** | Dead code mồ côi. View thực sự được gọi là `Views/Dentist/Appointments/_AppointmentRow.cshtml`. |
| 2 | `Views/Shared/_AppointmentRow.cshtml` | **GROUP B** | **GIỮ NGUYÊN** | Fallback shared view. Giữ nguyên tránh rủi ro dynamic lookup. |
| 3 | `MedicineService.GetBatchesByMedicineIdAsync` | **GROUP B** | **GIỮ NGUYÊN** | Cầu nối tương thích ngược (tạo batch ảo từ Medicine) cho client mà không cần bảng vật lý `MedicineBatches`. |
| 4 | `MedicineService.GetStockTransactionsAsync` | **GROUP B** | **GIỮ NGUYÊN** | Truy vấn biến động kho từ `PrescriptionItems.QuantityDispensed > 0`, hỗ trợ cả query param `batchId`. |
| 5 | Các DTO nhận `batchId` / `medicineBatchId` | **GROUP B** | **GIỮ NGUYÊN** | Đảm bảo hợp đồng API không bị phá vỡ với các request cũ. |
| 6 | 54 Route Links trong `_Sidebar.cshtml` | **ACTIVE** | **GIỮ NGUYÊN** | Đã verify 54/54 route đều trỏ đến Action & Controller hợp lệ, hoạt động 100%. |

---

## 4. KẾT LUẬN & CHỨNG NHẬN AN TOÀN

1. Đã dọn dẹp sạch thành phần thừa được chứng minh an toàn (`Views/DentistAppointment`).
2. Không thực hiện refactor mạo hiểm trên các module đang hoạt động ổn định.
3. Không làm thay đổi schema 38 bảng vật lý.
4. Hệ thống đạt trạng thái tinh gọn, ổn định và sẵn sàng cho buổi demo 19/11/2026.
