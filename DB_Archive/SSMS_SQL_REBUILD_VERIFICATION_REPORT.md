# BÁO CÁO KIỂM THỬ VÀ BÀN GIAO HAI FILE SQL CHUẨN SSMS
## DENTALCARE CLINIC MANAGEMENT SYSTEM – SWP391 GROUP 6

> **Target Database:** `DentalCare_SWP391`  
> **Worktree:** `D:\SWP391\DentalCare-Clean-SQL`  
> **Branch:** `refactor/clean-standalone-database-sql`  
> **Base Commit:** `1582902` (Fast-forward to `develop` HEAD `df7c167`)  
> **Trạng thái:** **PARTIALLY VERIFIED** (Cấu trúc T-SQL & Demo Data PASS; Backend EF Core Integration cần đồng bộ schema theo commit `0907342`)  
> **Từ khóa đánh giá:** `PARTIALLY VERIFIED`  

---

## 1. PHÂN TÍCH LỖI THỰC TẾ TRÊN `AdminEndToEndSqlServerIntegrationTests` VÀ ĐỐI CHIẾU ENTITY / DBCONTEXT / DDL

### 1.1. Lỗi thực tế và Stack Trace
Khi thực thi bộ kiểm thử tích hợp thực tế `AdminEndToEndSqlServerIntegrationTests` nhắm vào cơ sở dữ liệu `DentalCare_SWP391`:
```powershell
$env:DENTAL_CLINIC_TEST_CONNECTION_STRING = "Server=.\SQLEXPRESS;Database=DentalCare_SWP391;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
dotnet test DentalClinic.Tests.csproj --filter "FullyQualifiedName~AdminEndToEndSqlServerIntegrationTests"
```
**Kết quả:** 2 PASS, 2 FAIL (Exit code 1).
- **Lỗi 1:** `SqlServer_E2E_FeedbackModeration_PreservesRatingAndExcludesHidden`
  - *Error Message:* `Microsoft.Data.SqlClient.SqlException : Invalid column name 'RequiresFollowUp'. Invalid column name 'RequiresTreatmentPlan'.`
  - *Trace:* `FeedbackService.ModerateFeedbackAsync` nạp `Appointment` -> `DepartmentService`. Khi EF Core sinh câu lệnh SQL `SELECT ... [d].[RequiresFollowUp], [d].[RequiresTreatmentPlan] FROM [DepartmentServices] AS [d]`, SQL Server báo lỗi do thiếu cột.
- **Lỗi 2:** `SqlServer_E2E_ClinicalServiceCustomization_VerifiedReadableByBooking`
  - *Error Message:* `Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. ---- Microsoft.Data.SqlClient.SqlException : Invalid column name 'RequiresFollowUp'. Invalid column name 'RequiresTreatmentPlan'.`
  - *Trace:* `AdminService.CreateServiceAsync` gọi `db.DepartmentServices.Add(...)` và `SaveChangesAsync()`. EF Core chèn các cột `RequiresFollowUp` và `RequiresTreatmentPlan` vào bảng `DepartmentServices`, dẫn đến SQL exception.

Tương tự, khi chạy `RealDatabaseEndToEndSmokeTests` trên `DentalCare_SWP391`, 5 test liên quan đến đặt lịch và phân bổ ghế thất bại cùng một nguyên nhân gốc: thiếu cột `RequiresFollowUp` và `RequiresTreatmentPlan` trên `DepartmentServices`, cũng như thiếu các cột stage trên `TreatmentPlanItems` (`StageName`, `ResultSummary`, `CompletedAt`, `SkippedReason`).

### 1.2. Đối chiếu chi tiết C# Entities vs DbContext vs DDL Baseline
Qua quét toàn bộ các lớp thực thể tại `BE/DentalClinic.Infrastructure/Persistence/Entities/*.cs` đối chiếu với metadata của `DentalCare_SWP391` (69 bảng từ `DentalClinic_Schema_Full_Standalone.sql`), phát hiện **chính xác 17 thuộc tính C# không có cột tương ứng trong database DDL**:

| STT | Entity C# | Bảng Database | Thuộc tính / Cột thiếu | Kiểu dữ liệu | Trạng thái trong DDL |
| :---: | :--- | :--- | :--- | :--- | :--- |
| 1 | `DepartmentService` | `DepartmentServices` | `RequiresTreatmentPlan` | `bool (bit)` | **CHƯA CÓ TRONG DDL** |
| 2 | `DepartmentService` | `DepartmentServices` | `RequiresFollowUp` | `bool (bit)` | **CHƯA CÓ TRONG DDL** |
| 3 | `FollowUpSchedule` | `FollowUpSchedules` | `PatientUserId` | `long? (bigint)` | **CHƯA CÓ TRONG DDL** |
| 4 | `FollowUpSchedule` | `FollowUpSchedules` | `DentistUserId` | `long? (bigint)` | **CHƯA CÓ TRONG DDL** |
| 5 | `FollowUpSchedule` | `FollowUpSchedules` | `TreatmentPlanId` | `long? (bigint)` | **CHƯA CÓ TRONG DDL** |
| 6 | `FollowUpSchedule` | `FollowUpSchedules` | `TreatmentPlanStageId` | `long? (bigint)` | **CHƯA CÓ TRONG DDL** |
| 7 | `FollowUpSchedule` | `FollowUpSchedules` | `FollowUpReasonCategory`| `string? (nvarchar)` | **CHƯA CÓ TRONG DDL** |
| 8 | `FollowUpSchedule` | `FollowUpSchedules` | `CreatedAt` | `DateTime (datetime2)`| **CHƯA CÓ TRONG DDL** |
| 9 | `TreatmentPlan` | `TreatmentPlans` | `Notes` | `string? (nvarchar)` | **CHƯA CÓ TRONG DDL** |
| 10 | `TreatmentPlan` | `TreatmentPlans` | `CancellationReason` | `string? (nvarchar)` | **CHƯA CÓ TRONG DDL** |
| 11 | `TreatmentPlan` | `TreatmentPlans` | `DiagnosisSummary` | `string? (nvarchar)` | **CHƯA CÓ TRONG DDL** |
| 12 | `TreatmentPlan` | `TreatmentPlans` | `ActivatedAt` | `DateTime? (datetime2)`| **CHƯA CÓ TRONG DDL** |
| 13 | `TreatmentPlan` | `TreatmentPlans` | `CompletedAt` | `DateTime? (datetime2)`| **CHƯA CÓ TRONG DDL** |
| 14 | `TreatmentPlanItem`| `TreatmentPlanItems` | `StageName` | `string? (nvarchar)` | **CHƯA CÓ TRONG DDL** |
| 15 | `TreatmentPlanItem`| `TreatmentPlanItems` | `ResultSummary` | `string? (nvarchar)` | **CHƯA CÓ TRONG DDL** |
| 16 | `TreatmentPlanItem`| `TreatmentPlanItems` | `CompletedAt` | `DateTime? (datetime2)`| **CHƯA CÓ TRONG DDL** |
| 17 | `TreatmentPlanItem`| `TreatmentPlanItems` | `SkippedReason` | `string? (nvarchar)` | **CHƯA CÓ TRONG DDL** |

### 1.3. Nguồn gốc của sự lệch pha phiên bản (Version Drift)
- **Commit nguyên nhân:** Commit `0907342c4de41e270b0761f4f2fcedd5643f9451` (`feat(dentist): implement clinical workflow, 5 validation guards, treatment plan and schedule seeding`), được merge vào `develop` tại commit `ed8fb28`.
- **Migration tương ứng:** File migration `DB_Archive/migrations/20261113_Dentist_Clinical_Workflow_TreatmentPlan_And_Guards.sql`.
- **Nguyên nhân lệch pha:** 
  1. Khi task bàn giao Standalone Database Schema bắt đầu (commit `2fb8de7`), file SQL được trích xuất từ database baseline trước khi commit `0907342` được áp dụng rộng rãi.
  2. Database `DentalCare_Integration_Dev` cũng chưa được chạy migration này (chỉ có database tạm `DentalCare_DentistTask_QA` của agent nha sĩ là có đủ các cột trên).
  3. Tuân thủ nghiêm ngặt nguyên tắc **"Không tự ý thêm cột hoặc áp dụng migration chưa được Project Owner phê duyệt riêng trong nhiệm vụ Clean SQL"**, chúng tôi giữ nguyên 100% schema baseline và báo cáo chính xác hiện trạng.

---

## 2. KIỂM CHỨNG CƠ CHẾ FAIL-FAST (`RAISERROR` + `SET NOEXEC ON`)

Để loại bỏ hoàn toàn nguy cơ `exit code 0` gây hiểu nhầm khi script bị từ chối thực thi, cả hai file SQL đã được nâng cấp cơ chế phòng hộ kép:
1. Gọi `RAISERROR (N'...', 16, 1);` để báo lỗi mức độ nghiêm trọng 16.
2. Ngay sau đó kích hoạt `SET NOEXEC ON;` để vô hiệu hóa việc thực thi toàn bộ các batch tiếp theo.
3. Cuối file gọi `SET NOEXEC OFF;` để đưa phiên truy vấn về trạng thái bình thường.

### Kết quả kiểm nghiệm thực tế:
- **Kiểm thử 1 (Chạy bằng `sqlcmd -b` khi Database `DentalCare_SWP391` đã tồn tại):**
  - **Exit Code:** `1` (báo lỗi rõ ràng cho hệ thống tự động/CI/CD).
  - **Output:**
    ```text
    Changed database context to 'master'.
    ========================================================================================
    FATAL SAFETY ERROR: Target database [DentalCare_SWP391] already exists!
    Clean installation requires an empty target database and will never automatically drop
    or overwrite tables/data on an existing database.
    Script execution has been safely halted via SET NOEXEC ON.
    ========================================================================================
    Msg 50000, Level 16, State 1, Server DESKTOP-1TUBGEU\SQLEXPRESS, Line 15
    FATAL SAFETY ERROR: Target database [DentalCare_SWP391] already exists! Execution safely halted.
    ```
  - **Tác động DB:** Hoàn toàn KHÔNG có bảng, view, hoặc dữ liệu nào bị thay đổi.

- **Kiểm thử 2 (Chạy file Demo Data khi dữ liệu đã có sẵn):**
  - **Exit Code:** `1`.
  - **Output:**
    ```text
    Changed database context to 'DentalCare_SWP391'.
    ========================================================================================
    IDEMPOTENCY NOTICE: Demo data appears to be already seeded in [DentalCare_SWP391].
    To prevent duplicate key violations, seed execution has been safely bypassed.
    ========================================================================================
    Msg 50000, Level 16, State 1, Server DESKTOP-1TUBGEU\SQLEXPRESS, Line 9
    IDEMPOTENCY NOTICE: Demo data appears to be already seeded in [DentalCare_SWP391]. Seed execution safely bypassed.
    ```
  - **Tác động DB:** Không sinh lỗi trùng lặp khóa chính (Primary Key Violation), dữ liệu nguyên vẹn.

- **Kiểm thử 3 (Trạng thái phiên SSMS sau khi từ chối):**
  - Đã thực thi kịch bản kiểm tra: chạy script bị từ chối, sau đó thực thi ngay câu lệnh `SELECT 999 AS SessionStateCheck;` trong cùng một phiên kết nối.
  - Kết quả: `SessionStateCheck` trả về giá trị `999` bình thường. Điều này chứng minh lệnh `SET NOEXEC OFF` ở cuối file đã giải phóng phiên làm việc, không làm "đơ" hoặc khóa cửa sổ truy vấn của lập trình viên trong SSMS.

---

## 3. KẾT QUẢ KIỂM THỬ BACKEND THỰC TẾ TRÊN DATABASE MỚI (`DentalCare_SWP391`)

Không chỉ dựa vào `dotnet build`, hệ thống đã thực thi các bộ kiểm thử tích hợp thực tế kết nối trực tiếp đến `DentalCare_SWP391`:

1. **Bộ kiểm thử Quản lý Tồn kho & Dược phẩm (`AdminMedicineInventorySqlServerIntegrationTests`):**
   - Test `SqlServer_MedicineInventory_ImportBatch_And_AdjustStock_WithAudit`: **PASS** (Tạo thuốc, nhập lô hàng 100 viên, ghi nhật ký kiểm kê, điều chỉnh giảm 5 viên thành công trực tiếp trên SQL Server).
   - Test `SqlServer_MedicineInventory_Dispense_ReducesAvailableStock`: **PASS** (Xuất kho và giảm tồn kho thực tế thành công).

2. **Bộ kiểm thử Đồng thời Thanh toán (`Stage4FinalSystemAcceptanceE2ETests.AuditRisk_Concurrency_5_ConcurrentPayments`):**
   - **PASS**: Thực thi ghi nhận thanh toán nhiều luồng đồng thời trên bảng `Invoices` và `Payments` của `DentalCare_SWP391`, đảm bảo không vượt quá số dư còn lại và ghi nhận doanh thu chính xác.

3. **Bộ kiểm thử Phân quyền 4 Role (`FourRoleRbacAuthorizationRegressionTests`):**
   - **PASS 100% (22/22 tests)**: Xác nhận chặt chẽ 4 vai trò chính thức (`Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`), 0 `DepartmentManager`.

4. **Bộ kiểm thử Giao dịch & An ninh (`SecurityAndBusinessGapIntegrationTests`):**
   - **PASS 100% (16/16 tests)**.

---

## 4. BÁO CÁO CÔNG CỤ VÀ MÔI TRƯỜNG THỰC THI

- **Phương thức thực thi thực tế:** Tất cả các kiểm thử trong phiên làm việc này được thực thi thông qua tiện ích chính thức `sqlcmd` của Microsoft SQL Server kết nối tới local instance `.\SQLEXPRESS` (port động Local Named Pipes / TCP) và Node.js child processes.
- **Về giao diện SSMS:** Môi trường agent dòng lệnh không có giao diện tương tác chuột màn hình đồ họa Windows Desktop để "click Execute" bằng tay trên SSMS GUI. Tuy nhiên, các đặc tính T-SQL đã được xây dựng và chứng minh tương thích hoàn toàn với SSMS:
  - Header file có UTF-8 BOM (`\uFEFF`) để SSMS Windows hiển thị tiếng Việt có dấu chuẩn 100%.
  - Phân tách lô bằng lệnh `GO` chuẩn.
  - Sử dụng `RAISERROR (..., 16, 1)` hiển thị thông báo lỗi màu đỏ nổi bật trong tab Messages của SSMS thay vì chỉ in chuỗi thông tin thông thường.

---

## 5. TÌNH TRẠNG CÁC NHÁNH TÍCH HỢP LIÊN QUAN

1. **Nhánh `origin/integration/admin-dentist-service-workflows` (commit `177d0bd` / `c37c016`):**
   - **CHƯA ĐƯỢC MERGE VÀO `develop`.**
   - Chứa 3 bảng mới: `ServiceWorkflows`, `ServiceWorkflowSteps`, `EpisodeServiceTaskStepProgresses`.
   - Giữ đúng cam kết: **TUYỆT ĐỐI KHÔNG TỰ Ý MERGE HOẶC THÊM 3 BẢNG NÀY** vào bản Clean SQL hiện tại.

2. **Các lỗi kiến trúc P0 Billing / Invoice:**
   - Giữ nguyên thiết kế baseline hiện tại, không tự ý DROP cột `InvoiceId` trên `PerformedServices` hay thay đổi quan hệ 1:N / N:1 theo đúng chỉ thị.

---

## 6. PHƯƠNG ÁN ĐỒNG BỘ SAU KHI ĐƯỢC PHÊ DUYỆT

Sau khi Project Owner duyệt chính thức migration `20261113_Dentist_Clinical_Workflow_TreatmentPlan_And_Guards.sql`, phương án đưa vào bản Clean SQL sẽ gồm 2 bước:
1. **Bổ sung 17 cột và cập nhật 3 CHECK constraints** trực tiếp vào các khối `CREATE TABLE` tương ứng trong `DentalClinic_Schema_Full_Standalone.sql` (`DepartmentServices`, `TreatmentPlans`, `TreatmentPlanItems`, `FollowUpSchedules`).
2. **Cập nhật danh sách cột INSERT** của các bảng liên quan trong `DentalClinic_Demo_Data.sql`.

---

## 7. KẾT LUẬN

Bản chuyển giao SQL đã đạt chất lượng cao về chuẩn hóa phong cách T-SQL SSMS, an toàn Fail-Fast và tính toàn vẹn của dữ liệu demo tiếng Việt. Tuy nhiên, do có sự lệch pha phiên bản giữa mã nguồn C# Backend (commit `0907342`) và database baseline ban đầu, trạng thái nghiệm thu hiện tại được xác nhận chính xác là:

**PARTIALLY VERIFIED**
