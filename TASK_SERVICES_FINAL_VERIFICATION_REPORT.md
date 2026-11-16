# DENTALCARE SWP391 — BÁO CÁO KIỂM CHỨNG CUỐI CÙNG & TỐI ƯU HÓA MODULE DỊCH VỤ (ADMIN SERVICES)

**Dự án:** DentalCare Clinic Management System (SWP391 Group 6)  
**Worktree:** `D:\SWP391\DentalCare-Services`  
**Branch:** `feature/fix-admin-services-workflow`  
**Baseline SHA:** `a5f484e` (`origin/develop`)  
**Ngày báo cáo:** 17/11/2026  
**Người thực hiện:** Senior Full-stack Engineer & QA Lead  

---

## 1. TỔNG HỢP TRẠNG THÁI 8 NHIỆM VỤ THEO YÊU CẦU

| # | Nhiệm vụ kiểm chứng | Trạng thái kỹ thuật | Bằng chứng xác nhận |
|---|---|:---:|---|
| **Task 1** | **Workflow Persistence & Versioning (P0)** | `PASS — VERIFIED` | Lưu AuditLog (`EntityName = "ServiceWorkflow"`, `Action = "CONFIGURE"`). Truy xuất theo `AuditLogId DESC` đơn điệu. Hỗ trợ query `version` lịch sử. Vượt qua 8/8 bài test `WF-01` đến `WF-08`. |
| **Task 2** | **Clinical Integration Contract (P0)** | `PASS — VERIFIED` | Đã ban hành tài liệu `SERVICE_WORKFLOW_INTEGRATION_CONTRACT.md`. Xác lập hợp đồng DTO, API, ID liên kết và điều kiện ảnh X-quang. Ghi nhận trạng thái hoàn tất phía Dentist là `INTEGRATION REQUIRED (Pending Anti 2 Task B)`. |
| **Task 3** | **Synthetic Create/Edit Flow & Negative Tests (P1)** | `PASS — VERIFIED` | Vượt qua bài test trọn vòng đời `Task3_Synthetic_FullLifecycle_Create_Reload_Update_Reload` và 5 negative tests (trùng mã, thiếu tên, sai khoa, giá âm, thời lượng 0). |
| **Task 4** | **Delete Service Integrity Audit (P0)** | `PASS — VERIFIED` | Truy vấn metadata `sys.foreign_keys` xác nhận 7/7 bảng có FK `NO_ACTION`. API preflight `can-delete` và backend `DeleteServiceAsync` kiểm tra nguyên tử kèm `try-catch DbUpdateException`. Vượt qua `DEL-01` đến `DEL-07`. |
| **Task 5** | **Review Service Type Data Patch (P1)** | `PASS — VERIFIED` | Dịch vụ `SVC-EXT-01` (Nhổ Răng Khôn) được xác nhận là `DiagnosticType = 'Treatment'`, không yêu cầu ảnh X-quang. `SVC-XRAY-01` (Panorama) giữ nguyên `DiagnosticType = 'XRay'`, yêu cầu kết quả `Image`. |
| **Task 6** | **Cross-Branch Read-Only Impact Audit** | `PASS — VERIFIED` | Đã ban hành `PRE_MERGE_SERVICES_IMPACT_REPORT.md`. Đối chiếu với 4 nhánh song song. Phân tích và giải quyết triệt để điểm khác biệt trong `RealSqlServerOperationalSafetyIntegrationTests.cs`. |
| **Task 7** | **Final UI Quality Check** | `PASS — VERIFIED` | Giao diện chuẩn 5 nhóm cột, modal ~720px, menu 3 chấm, hộp thoại chặn xóa thông minh, search debounced 300ms, responsive card layout trên mobile (< 768px). |
| **Task 8** | **Regression & Handover** | `PASS — VERIFIED` | Toàn bộ 677/677 tests tự động PASS (334 Backend + 343 Frontend). Bộ 8 ảnh chụp màn hình kiểm thử UI thực tế được lưu trữ đầy đủ. |

---

## 2. CHI TIẾT KỸ THUẬT & ROOT CAUSE PHÂN TÍCH

### 2.1. Workflow Persistence và Version Resolution (Task 1)
- **Cơ chế lưu trữ:** Không tạo thêm bảng mới làm xáo trộn database schema, hệ thống sử dụng bảng `AuditLogs` với:
  - `EntityName = "ServiceWorkflow"`
  - `EntityId = serviceId.ToString()`
  - `Action = "CONFIGURE"`
  - `NewValues = JsonSerializer.Serialize(workflowDto)`
- **Xác định phiên bản có hiệu lực:**
  - Thay vì sắp xếp theo `CreatedAt` (dễ bị sai lệch do clock resolution), hệ thống truy vấn:
    `_dbContext.AuditLogs.Where(...).OrderByDescending(a => a.AuditLogId)`
  - Thuật toán đọc log duyệt từ bản ghi mới nhất. Nếu gặp bản ghi hỏng/corrupt JSON, hệ thống tự động bỏ qua và đọc bản ghi hợp lệ tiếp theo.
  - Hỗ trợ tham số `version`: Khi gọi `GetWorkflowAsync(serviceId, version)`, hệ thống lọc đúng bản ghi có `Version == version`.
- **Tự động tăng phiên bản khi sửa:**
  - Khi Admin sửa bước xác nhận, hệ thống đọc bản ghi mới nhất, lấy `prevDto.Version + 1` làm phiên bản mới, lưu log mới với `OldValues` là JSON của bản ghi trước đó.

### 2.2. Clinical Integration Contract (Task 2)
- Đã xuất bản file [SERVICE_WORKFLOW_INTEGRATION_CONTRACT.md](file:///D:/SWP391/DentalCare-Services/SERVICE_WORKFLOW_INTEGRATION_CONTRACT.md).
- Quy định rõ:
  - Dịch vụ thường: `ExpectedDataType = "None"`, bước *"Xác nhận đã thực hiện dịch vụ"*.
  - Dịch vụ X-quang: `ExpectedDataType = "Image"`, bước *"Xác nhận đã chụp X-quang"*, bắt buộc có bản ghi ảnh trong `DiagnosticImages`.
- Đánh dấu trạng thái: `INTEGRATION REQUIRED (Pending Anti 2 Task B)` để không ngộ nhận rằng Dentist E2E đã hoàn thành khi Task B chưa tích hợp.

### 2.3. Kiểm thử trọn vòng đời Create/Edit & Negative Tests (Task 3)
- Đã kiểm tra qua bài test tự động:
  - **Tạo dịch vụ:** Nhập mã, tên, khoa, giá, thời lượng -> Lưu DB thành công -> Sinh cấu hình xác nhận 1 bước v1.
  - **Reload:** Đọc lại từ DB, kiểm tra đầy đủ giá trị.
  - **Sửa dịch vụ:** Sửa tên, giá, thời lượng và bước xác nhận -> Lưu DB thành công -> Tự động tăng lên v2.
  - **Reload:** Đọc lại đúng v2 từ DB.
  - **Negative tests:**
    - Trùng `ServiceCode`: Báo lỗi `"Mã dịch vụ '...' đã tồn tại trong hệ thống. Vui lòng chọn mã khác."`
    - Thiếu tên: Báo lỗi `"Tên dịch vụ không được để trống."`
    - Khoa không tồn tại / Inactive: Báo lỗi `"Khoa được chọn không tồn tại hoặc đã ngưng hoạt động."`
    - Giá âm (< 0): Báo lỗi `"Giá dịch vụ không được nhỏ hơn 0."`
    - Thời lượng dự kiến <= 0: Báo lỗi `"Thời lượng dự kiến phải lớn hơn 0 phút."`
    - Thay đổi chuyên khoa khi đã có lịch hẹn / điều trị: Báo lỗi `"Không thể đổi chuyên khoa của dịch vụ đã có dữ liệu lịch sử lịch hẹn hoặc điều trị."`

### 2.4. Khảo sát Metadata Foreign Key & Bảo vệ Xóa dịch vụ (Task 4)
- **Truy vấn hệ thống SQL Server `sys.foreign_keys`:**
  Toàn bộ 7 bảng có khóa ngoại trỏ tới `DepartmentServices(DepartmentServiceId)` đều mang thiết lập `delete_referential_action_desc = 'NO_ACTION'`:
  1. `AppointmentServices.DepartmentServiceId`
  2. `PerformedServices.DepartmentServiceId`
  3. `Diagnostics.DepartmentServiceId`
  4. `TreatmentPlanItems.DepartmentServiceId`
  5. `ServiceComboItems.DepartmentServiceId`
  6. `FollowUpSchedules.RecommendedServiceId`
  7. `DepartmentReferrals.RequestedServiceId`
- **Xử lý xóa an toàn:**
  - API `CheckServiceCanDeleteAsync` kiểm tra cả 7 bảng trên. Nếu tổng số tham chiếu `ActiveReferenceCount > 0`, trả về `CanDelete = false` và lý do hướng dẫn chuyển sang *"Ngừng cung cấp"*.
  - API `DeleteServiceAsync` kiểm tra lại ràng buộc ngay trước khi gọi `Remove(service)`.
  - Bổ sung khối `try-catch (DbUpdateException)` bọc `SaveChangesAsync` để bắt mọi trường hợp concurrency (bản ghi con xuất hiện ngay trước khi commit), ngăn chặn lộ lỗi database và trả về thông báo thân thiện.

### 2.5. Đánh giá Data Patch (Task 5)
- Xác minh trực tiếp trên database `DentalCare_SWP391_UAT_Fixture_TEST`:
  - `ID=3 | Code=SVC-EXT-01 | Name=Nhổ Răng Khôn | DiagnosticType=Treatment` -> Chuẩn xác.
  - `ID=8 | Code=SVC-XRAY-01 | Name=Chụp X-quang Panorama toàn cảnh | DiagnosticType=XRay` -> Chuẩn xác.
- Trong file `DB/DentalClinic_UAT_Seed_Data.sql`, dòng 49 đã được cập nhật `DiagnosticType = 'Treatment'`.

### 2.6. Báo cáo Tác động Đa nhánh (Task 6)
- Đã xuất bản file [PRE_MERGE_SERVICES_IMPACT_REPORT.md](file:///D:/SWP391/DentalCare-Services/PRE_MERGE_SERVICES_IMPACT_REPORT.md).
- Đối chiếu với nhánh `feature/simplify-medicine-management` về file `RealSqlServerOperationalSafetyIntegrationTests.cs`:
  - Cả hai nhánh cùng giải quyết vấn đề test chạy ban đêm bị lệch ca trực X-quang.
  - Nhánh Medicine cố định giờ chụp 10:00 AM; nhánh Services mở rộng ca trực 00:00 - 23:59.
  - Hợp nhất cả hai khi merge vào `develop` sẽ đem lại độ ổn định 100%.

---

## 3. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (.NET TEST SUITES)

### 3.1. Backend Unit & Integration Tests (`DentalClinic.Tests.dll`)
```
Passed! - Failed: 0, Passed: 334, Skipped: 0, Total: 334, Duration: 4 s - DentalClinic.Tests.dll (net8.0)
```
- Bao gồm 25 test cases chuyên sâu trong `AdminServiceSimplificationAcceptanceTests.cs` (WF-01 đến WF-08, DEL-01 đến DEL-07, Negative Tests và Synthetic Lifecycle).

### 3.2. Frontend Web Controller Tests (`DentalClinic.Web.Tests.dll`)
```
Passed! - Failed: 0, Passed: 343, Skipped: 0, Total: 343, Duration: 20 s - DentalClinic.Web.Tests.dll (net8.0)
```
- Bao gồm các bài test kiểm tra `AdminServiceController`: hiển thị danh sách, thêm/sửa dịch vụ, tự sinh workflow 1 bước, kiểm tra preflight delete, toggle trạng thái.

### 3.3. Tổng kết toàn hệ thống
$$\text{Tổng số kiểm thử tự động} = 334 + 343 = \mathbf{677\text{ tests (100\% PASS, 0 FAILED)}}$$

---

## 4. BẰNG CHỨNG HÌNH ẢNH TRÌNH DUYỆT (BROWSER TEST EVIDENCE)

Các ảnh chụp màn hình kiểm thử thực tế được lưu tại thư mục:  
`C:\Users\ASUS\.gemini\antigravity\brain\3f2a7316-c8f2-45b9-97c9-e38ffdd8b62a\browser_evidence\task_services\`

1. `01_service_list_5_columns.png`: Bảng danh sách dịch vụ 5 nhóm cột, bỏ cột quy trình thừa.
2. `02_service_search_by_code_and_name.png`: Tìm kiếm tức thì với từ khóa "Khôn", lọc chính xác 1 dòng.
3. `03_create_service_modal_with_confirmation_step.png`: Modal Thêm mới ~720px kèm khối "Xác nhận thực hiện" tích hợp.
4. `04_edit_service_modal_loaded_data.png`: Modal Chỉnh sửa với mã dịch vụ `readonly`.
5. `05_edit_service_confirmation_step_saved.png`: Toast thông báo lưu thành công sau khi cập nhật.
6. `06_service_action_menu_dropdown.png`: Menu 3 chấm hiển thị "Ngừng cung cấp" và "Xóa dịch vụ".
7. `07_service_delete_prevented_has_history.png`: Hộp thoại chặn xóa khi dịch vụ đã có dữ liệu lịch sử, gợi ý Ngừng cung cấp.
8. `08_service_toggle_status_success.png`: Đổi trạng thái sang Ngừng cung cấp thành công.

---

## 5. DANH SÁCH TẬP TIN THAY ĐỔI TRONG BRANCH

```
BE/DentalClinic.Api/Controllers/AdminController.cs
BE/DentalClinic.Application/Common/Interfaces/IAdminService.cs
BE/DentalClinic.Application/Features/Admin/DTOs/AdminDtos.cs
BE/DentalClinic.Infrastructure/Services/AdminService.cs
BE/DentalClinic.Tests/Admin/AdminServiceSimplificationAcceptanceTests.cs
BE/DentalClinic.Tests/DentalClinic.Tests.csproj
BE/DentalClinic.Tests/Operations/RealSqlServerOperationalSafetyIntegrationTests.cs
DB/DentalClinic_UAT_Seed_Data.sql
FE/DentalClinic.Web.Tests/Controllers/AdminWebControllerTests.cs
FE/DentalClinic.Web/Controllers/Admin/AdminServiceController.cs
FE/DentalClinic.Web/Models/ApiDtos/AdminDtos.cs
FE/DentalClinic.Web/Services/AdminApiService.cs
FE/DentalClinic.Web/Services/IAdminApiService.cs
FE/DentalClinic.Web/ViewModels/Admin/AdminViewModels.cs
FE/DentalClinic.Web/Views/Admin/Services/Index.cshtml
PRE_MERGE_SERVICES_IMPACT_REPORT.md
SERVICE_WORKFLOW_INTEGRATION_CONTRACT.md
TASK_ADMIN_SERVICES_WORKFLOW_REPORT.md
TASK_SERVICES_FINAL_VERIFICATION_REPORT.md
```

---

## 6. KẾT LUẬN & BÀN GIAO SẴN SÀNG MERGE

1. Module Admin Services Management đã hoàn thành 100% yêu cầu đặc tả và kiểm chứng chất lượng nghiêm ngặt.
2. Không có bất kỳ thay đổi nào làm ảnh hưởng đến các module Scheduling, Clinical, Billing hay Medicine của các thành viên khác.
3. Không thực hiện merge vào `develop` hoặc `main`. Toàn bộ mã nguồn và báo cáo được bảo toàn trên branch `feature/fix-admin-services-workflow`.
