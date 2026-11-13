# DentalCare SWP391 – Database Architecture & Standalone Schema Delivery

> **Dự án:** Hệ thống Quản lý Phòng khám Nha khoa DentalCare (SWP391 - Nhóm 6)  
> **Tài liệu bàn giao:** Kiến trúc Cơ sở Dữ liệu & Hướng dẫn Cài đặt Độc lập (Standalone Schema)  
> **Trạng thái:** `BASELINE SCHEMA VERIFIED – FINAL WORKFLOW SCHEMA UPDATE PENDING`  
> **Hệ quản trị CSDL hỗ trợ:** Microsoft SQL Server 2017 / 2019 / 2022 / Azure SQL Edge / LocalDB / SQLEXPRESS  

---

## 1. Tổng Quan & Yêu Cầu Bàn Giao

Bộ môn SWP391 và Hội đồng nghiệm thu yêu cầu bàn giao **MỘT FILE SQL DUY NHẤT CHỨA TOÀN BỘ CẤU TRÚC DATABASE (STANDALONE SCHEMA)** có khả năng khởi tạo toàn bộ đối tượng database từ một cơ sở dữ liệu trống, không phụ thuộc vào chuỗi migration EF Core nối tiếp và tách biệt hoàn toàn giữa cấu trúc schema và dữ liệu mẫu (demo seed data).

Thư mục `DB/` cung cấp đầy đủ bộ ba deliverables chuẩn hóa:

| Tên Tệp | Định Dạng | Mô Tả Chức Năng |
| :--- | :--- | :--- |
| **`DB/DentalClinic_Schema_Full_Standalone.sql`** | SQL Script (198 KB) | **File cấu trúc CSDL độc lập duy nhất (Schema Only)**: Khởi tạo đầy đủ 69 bảng, 69 Khóa chính, 164 Khóa ngoại, 87 Check Constraints, 104 Unique Constraints/Indexes, 110 Indexes hiệu năng, 1 View và 2 Stored Procedures. Không chứa dữ liệu demo. Tích hợp Safety Guard 4 lớp bảo vệ. |
| **`DB/DentalClinic_Demo_Data.sql`** | SQL Script (6.0 MB) | **Dữ liệu mẫu chuẩn hóa (57 bảng, 13.957 dòng dữ liệu)**: Dùng để nạp dữ liệu demo sau khi đã chạy file schema. Đảm bảo đúng 4 vai trò chính thức (`Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`), mật khẩu băm chuẩn Identity (`Password123@`), loại bỏ hoàn toàn vai trò lỗi thời `DepartmentManager` và không chứa PII nhạy cảm thực tế. |
| **`DB/README_Database.md`** | Markdown | Tài liệu hướng dẫn cài đặt, ma trận thống kê đối tượng (Database Metrics), kết quả kiểm toán toàn vẹn và phân tích sai khác schema của các nhánh tính năng đang hoàn thiện. |

---

## 2. Cơ Chế An Toàn (Target Database Safety Guard)

Cả hai tệp SQL `DentalClinic_Schema_Full_Standalone.sql` và `DentalClinic_Demo_Data.sql` đều được tích hợp **Target Database Safety Guard** ở phần đầu tệp để ngăn ngừa mọi rủi ro ghi đè hoặc phá hủy dữ liệu:

1. **Chặn database hệ thống:** Từ chối thực thi và ngắt kết nối ngay lập tức nếu mục tiêu là `master`, `tempdb`, `model`, hoặc `msdb`.
2. **Chặn database đang hoạt động / được bảo vệ:** Từ chối thực thi trên các database bảo vệ: `DentalClinicManagementDB`, `DentalCare_Integration_Dev`, `DentalCare_Release_Clean_QA`, `DentalCare_Restore_Verification_QA`.
3. **Chặn database không rỗng (Non-Empty Guard):** File schema từ chối thực thi trên database đã có sẵn bảng người dùng (`sys.tables WHERE is_ms_shipped = 0`), tuyệt đối không tự động DROP bảng hay xóa dữ liệu.
4. **Kiểm tra tiên quyết dữ liệu (Prerequisite Guard):** File dữ liệu demo từ chối thực thi nếu database chưa có sẵn cấu trúc bảng schema của DentalCare.
5. **Fail-Fast Severity 20:** Khi vi phạm điều kiện an toàn, script kích hoạt lỗi mức độ nghiêm trọng 20 (`RAISERROR(..., 20, 1) WITH LOG`), ngắt session SQL Server ngay lập tức.

---

## 3. Hướng Dẫn Cài Đặt Độc Lập Từ Database Trống

### Bước 1: Tạo Database Trống

Mở terminal (PowerShell hoặc Command Prompt) và tạo database mới với Collation chuẩn UTF-8/Case-Insensitive:

```powershell
sqlcmd -S .\SQLEXPRESS -E -b -Q "CREATE DATABASE DentalCare_Delivery_DB COLLATE SQL_Latin1_General_CP1_CI_AS;"
```

### Bước 2: Khởi Tạo Cấu Trúc Database (Schema Only)

Thực thi file schema độc lập duy nhất vào database vừa tạo. Tham số `-b` (bắt buộc) đảm bảo ngắt tiến trình ngay nếu gặp lỗi:

```powershell
sqlcmd -S .\SQLEXPRESS -E -d DentalCare_Delivery_DB -b -f 65001 -i "DB\DentalClinic_Schema_Full_Standalone.sql"
```

*Kết quả mong đợi:*
```text
>>> DENTAL CLINIC STANDALONE SCHEMA INITIALIZED SUCCESSFULLY (69 TABLES, VIEWS & PROCEDURES)!
```

### Bước 3: Nạp Dữ Liệu Mẫu Chuẩn Hóa (Tùy Chọn)

Nếu cần kiểm thử nghiệp vụ, demo hội đồng chấm thi hoặc chạy hệ thống hoàn chỉnh:

```powershell
sqlcmd -S .\SQLEXPRESS -E -d DentalCare_Delivery_DB -b -f 65001 -i "DB\DentalClinic_Demo_Data.sql"
```

*Kết quả mong đợi:*
```text
>>> DENTAL CLINIC DATABASE & DATA SUCCESSFULLY INITIALIZED 100% (69 TABLES)!
```

---

## 4. Ma Trận Thống Kê Đối Tượng Database (Authoritative Metrics)

Bảng đối chiếu kiểm toán giữa cơ sở dữ liệu đối chứng phát triển (`DentalCare_Integration_Dev`) và cơ sở dữ liệu khởi tạo từ Standalone Schema (`DentalCare_StandaloneSchema_QA`):

| Loại Đối Tượng Database | Cơ Sở Dữ Liệu Gốc (`Integration_Dev`) | Cơ Sở Dữ Liệu Khởi Tạo Độc Lập (`StandaloneSchema_QA`) | Trạng Thái Đối Soát |
| :--- | :---: | :---: | :---: |
| **Tổng số Bảng (Tables)** | **69** | **69** | Khớp 100% |
| - Bảng Nghiệp vụ Domain | 68 | 68 | Khớp 100% |
| - Bảng Lịch sử Migration (`__EFMigrationsHistory`) | 1 | 1 | Khớp 100% |
| **Tổng số Cột (Columns)** | **749** | **749** | Khớp 100% |
| **Khóa chính (Primary Keys)** | **69** | **69** | Khớp 100% |
| **Khóa ngoại (Foreign Keys)** | **164** | **164** | Khớp 100% (164/164 Trusted & Enabled) |
| **Ràng buộc kiểm tra (Check Constraints)** | **87** | **87** | Khớp 100% (87/87 Trusted & Enabled) |
| **Chỉ mục phụ (Indexes Non-PK)** | **110** | **110** | Khớp 100% |
| **Chỉ mục / Ràng buộc Duy nhất (Unique)** | **104** | **104** | Khớp 100% |
| **Khung nhìn (Views)** | **1** (`dbo.vw_PublishedBlogPosts`) | **1** (`dbo.vw_PublishedBlogPosts`) | Khớp 100% |
| **Thủ tục lưu trữ (Stored Procedures)** | **2** | **2** | Khớp 100% |
| - `dbo.sp_CleanupOrphanBlogPostMedia` | Có | Có | Khớp 100% |
| - `dbo.sp_IncrementBlogPostView` | Có | Có | Khớp 100% |
| **Triggers** | **0** | **0** | Khớp 100% |
| **User-Defined Functions (UDF)** | **0** | **0** | Khớp 100% |

---

## 5. Danh Sách 69 Bảng Phân Theo Cụm Nghiệp Vụ

Dưới đây là 69 bảng theo đúng kiến trúc Clean Architecture của DentalCare:

1. **Hạ tầng & Hệ thống (3 bảng):**
   - `__EFMigrationsHistory`, `AuditLogs`, `ClinicProfile`
2. **Quản lý Tài khoản & Phân quyền RBAC (5 bảng):**
   - `UserAccounts`, `StaffProfiles`, `PatientProfiles`, `DentistProfiles`, `Notifications`
3. **Cơ cấu Tổ chức, Phòng & Thiết bị (6 bảng):**
   - `Departments`, `DentistDepartments`, `Rooms`, `DentalChairs`, `ImagingEquipments`, `SupportChatMessages`
4. **Lịch làm việc, Ca trực & Chấm công (11 bảng):**
   - `DepartmentWorkSchedules`, `DentistWorkSchedules`, `DentistAvailability`, `ReceptionistShiftTemplates`, `ReceptionistWorkSchedules`, `ReceptionistLeaveRequests`, `ReceptionistLeaveRequestPeriods`, `ReceptionistShiftSwapRequests`, `DentistLeaveRequests`, `AttendancePolicies`, `XrayDentistRosters`
5. **Dịch vụ & Gói Điều trị (3 bảng):**
   - `DepartmentServices`, `ServiceCombos`, `ImagingServiceConfigs`
6. **Đặt hẹn, Điều phối Hàng đợi & Tiếp đón (4 bảng):**
   - `Appointments`, `AppointmentChangeProposals`, `AppointmentStatusHistories`, `AppointmentReminderJobs`
7. **Đợt Điều trị & Tác vụ Dịch vụ Đa khoa (2 bảng):**
   - `CareEpisodes`, `EpisodeServiceTasks`
8. **Hồ sơ Bệnh án & Khám Chẩn đoán (7 bảng):**
   - `DentalMedicalRecords`, `ClinicalEntries`, `OdontogramEntries`, `ImagingRecords`, `DiagnosticOrders`, `DiagnosticResults`, `ImagingSessions`
9. **Kế hoạch Điều trị & Thực hiện Dịch vụ (5 bảng):**
   - `TreatmentPlans`, `TreatmentPlanItems`, `TreatmentSessions`, `PerformedServices`, `ClinicalConsultations`
10. **Đơn thuốc & Dược phẩm (7 bảng):**
    - `Prescriptions`, `PrescriptionItems`, `Medicines`, `MedicineBatches`, `MedicineStockTransactions`, `MedicineDispensations`, `MedicineDispensationItems`
11. **Tài chính, Hóa đơn & Thanh toán (3 bảng):**
    - `Invoices`, `InvoiceItems`, `Payments`
12. **Chăm sóc Khách hàng & Phản hồi (3 bảng):**
    - `DepartmentReferrals`, `FollowUpSchedules`, `VisitFeedback`
13. **Tin tức & Truyền thông Nha khoa (3 bảng):**
    - `BlogCategories`, `BlogPosts`, `BlogPostMedia`

---

## 6. Tuân Thủ Quy Tắc Nghiệp Vụ Cốt Lõi (Business Guardrails)

Dữ liệu demo và cấu trúc bảng tuân thủ tuyệt đối các quy định nghiệp vụ của DentalCare SWP391:

1. **Mô hình Phân quyền 4 Vai trò Chính thức:**
   - Chỉ gồm: `Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`.
   - Vai trò cũ `DepartmentManager` đã được loại bỏ 100% khỏi toàn bộ bảng `UserAccounts` và các phân hệ nghiệp vụ. Toàn bộ tài khoản điều hành phòng ban được chuyển đổi sang `Dentist` hoặc `StaffProfiles`.
2. **Chính sách Cấp Số Thứ Tự (Queue Number Policy):**
   - Số thứ tự (`QueueNumber`) được cấp ngay khi lịch hẹn chuyển sang trạng thái `Confirmed`.
   - Đảm bảo tính duy nhất theo từng Chuyên khoa (`DepartmentId`) và Ngày khám (`ScheduledDate`).
   - Khi bệnh nhân đến phòng khám và Lễ tân thực hiện `CheckIn`, số thứ tự được giữ nguyên không đổi để đưa bệnh nhân vào hàng đợi thực tế.
3. **Cơ chế Gộp Hóa đơn Đợt Điều trị (CareEpisode Single Invoice Aggregation):**
   - Một `CareEpisode` chứa nhiều `Appointments` và `EpisodeServiceTasks`.
   - Tất cả các tác vụ bổ sung hoặc chỉ định trong đợt điều trị được gom vào một `Invoice` tổng duy nhất thông qua các dòng chi tiết `InvoiceItems` (với `SourceType = 'EpisodeServiceTask'`, `SourceId = ServiceTaskId`).
4. **Bác sĩ Chuyên trách X-quang theo Ca (`XrayDentistRosters`):**
   - Quản lý phân công bác sĩ có chứng chỉ hình ảnh nha khoa trực theo ca (Morning / Afternoon / FullDay) gắn với Phòng chụp (`Rooms`) và Máy chụp chuyên dụng (`ImagingEquipments`).

---

## 7. Tài Liệu Hóa Schema Drift & Các Nhánh Đang Hoàn Thiện (Pending Branches)

Hiện tại trên repository có hai nhánh tính năng đang được các nhóm kỹ sư hoàn thiện liên quan đến quy trình dịch vụ và task workflows. Cả hai nhánh này **chưa merge vào nhánh `develop`**, do đó **chưa được đưa vào Schema Baseline hiện hành**:

### Nhánh 1: `feature/admin-service-workflow-configuration` (HEAD `2ca7764`)
- **Phạm vi thay đổi:**
  - Bổ sung cột `ServiceCode` (`NVARCHAR(50)`, cho phép NULL) vào bảng `DepartmentServices`.
  - Tạo mới bảng `ServiceWorkflows` (quản lý phiên bản quy trình điều trị cho từng dịch vụ).
  - Tạo mới bảng `ServiceWorkflowSteps` (các bước thực hiện chi tiết trong quy trình).
  - Bổ sung cột phiên bản quy trình vào bảng `EpisodeServiceTasks`.
- **Trạng thái:** Mã nguồn phân hệ Admin đã hoàn thành, đang chờ nghiệm thu UAT trước khi merge vào `develop`.

### Nhánh 2: `feature/service-specific-task-workflows` (Worktree `DentalCare-Dentist-Task-Workflows`)
- **Phạm vi thay đổi:**
  - Tạo mới bảng `EpisodeServiceTaskStepProgresses` (ghi nhận tiến độ thực hiện từng bước trong task điều trị của bác sĩ).
- **Trạng thái:** Đang trong quá trình phát triển logic thực hiện của Bác sĩ.

### Tuyên Bố Trạng Thái Bàn Giao:
> **`STATUS: BASELINE SCHEMA VERIFIED – FINAL WORKFLOW SCHEMA UPDATE PENDING`**  
> Bản bàn giao schema hiện tại (`DentalClinic_Schema_Full_Standalone.sql`) là **bản kiến trúc chuẩn xác 100% của nhánh develop chính thức**, hỗ trợ toàn bộ các luồng nghiệp vụ hiện hữu. Khi hai nhánh workflow trên hoàn tất nghiệm thu và merge vào `develop`, một bản cập nhật schema mở rộng cho 3 bảng workflow sẽ được tích hợp bổ sung.
