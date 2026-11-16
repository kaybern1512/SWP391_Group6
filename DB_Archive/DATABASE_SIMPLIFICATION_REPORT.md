# DENTALCARE SWP391 – BÁO CÁO KIỂM TOÁN VÀ THIẾT KẾ CƠ SỞ DỮ LIỆU ĐÃ HIỆU ĐÍNH (CORRECTED AUDIT & SIMPLIFICATION DESIGN REPORT)

* **Ngày lập báo cáo:** 13/11/2026
* **Tác giả:** Senior SQL Server Database Architect & ASP.NET Core 8 Solution Architect
* **Nhánh làm việc:** `refactor/database-domain-simplification` (Worktree độc lập: `D:\SWP391\DentalCare-DB-Simplification`)
* **Cam kết:** **100% số liệu được trích xuất trực tiếp từ file SQL hiện hành và kiểm chứng thực tế trên SQL Server.** Không tái sử dụng bất kỳ danh sách giả định hoặc dữ liệu cũ nào.
* **Tài nguyên:** Tuân thủ triệt để chỉ thị dừng toàn bộ tác vụ nền, không chạy build/test, không chạy Playwright, không ALTER/DROP database.

---

## 1. TỔNG QUAN HỆ THỐNG VÀ BẢNG SỐ LIỆU ĐỐI SOÁT CHÍNH XÁC (EXECUTIVE METRICS)

Toàn bộ các số liệu dưới đây được truy vấn trực tiếp từ bảng hệ thống `sys.tables`, `sys.columns`, `sys.foreign_keys`, `sys.check_constraints`, `sys.key_constraints` trên cơ sở dữ liệu `DentalCare_Simplification_QA` và đối chiếu từng dòng lệnh `CREATE TABLE` trong file [DentalClinic_Schema_Full_Standalone.sql](file:///D:/SWP391/DentalCare-DB-Simplification/DB/DentalClinic_Schema_Full_Standalone.sql).

| Chỉ số Đối soát (Database Metric) | Giá trị Thực tế Xác minh 100% | Bằng chứng từ Hệ thống (System Evidence) |
| :--- | :---: | :--- |
| **Tổng số Bảng Vật lý (Physical Tables)** | **71** | Đúng 71 câu lệnh `CREATE TABLE` từ dòng 70 đến 2822 trong file SQL. |
| **Bảng Nghiệp vụ (Business Tables)** | **70** | Toàn bộ các bảng lưu trữ nghiệp vụ nha khoa. |
| **Bảng Hệ thống (Migration Metadata)** | **1** | Bảng `__EFMigrationsHistory` (dòng 70 trong file SQL). |
| **Tổng số Cột (Physical Columns)** | **789** | Tổng `sys.columns` của 71 bảng vật lý (khớp 100% với bảng kê 71 dòng). |
| **Khóa chính (Primary Keys)** | **71** | Mỗi bảng có đúng 1 PK (`sys.key_constraints` type = `'PK'`). |
| **Khóa ngoại (Foreign Keys)** | **178** | Đúng 178 ràng buộc `sys.foreign_keys` liên kết giữa các bảng. |
| **Ràng buộc CHECK (Check Constraints)** | **92** | Đúng 92 ràng buộc `sys.check_constraints` kiểm soát miền giá trị. |
| **Ràng buộc UNIQUE (Unique Constraints)** | **32** | Đúng 32 ràng buộc `sys.key_constraints` type = `'UQ'`. |
| **Tổng số dòng dữ liệu hiện hành (QA Rows)** | **14,469** | Tổng `sys.partitions` (gồm 13,997 dòng baseline demo + dữ liệu test tích hợp). |
| **Mô hình Phân quyền (RBAC Roles)** | **4 Roles** | `Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`. |

---

## 2. BẢNG GIẢI TRÌNH VÀ ĐỐI CHIẾU 9 BẢNG "ẢO" (PHANTOM TABLES) TỪ BÁO CÁO CŨ

Trong báo cáo chưa được duyệt trước đó, có một bảng danh sách gồm 80 dòng. Qua đối soát thực tế với 71 câu lệnh `CREATE TABLE` của file `DB/DentalClinic_Schema_Full_Standalone.sql`, phát hiện có **9 tên bảng ảo không hề tồn tại trong cơ sở dữ liệu vật lý**. 

Dưới đây là bảng đối soát chi tiết nguyên nhân xuất hiện của 9 bảng này:

| STT | Tên bảng trong báo cáo cũ | Tình trạng trong SQL hiện hành | Nguyên nhân xuất hiện sai lệch & Vị trí dữ liệu thực tế |
| :---: | :--- | :---: | :--- |
| 1 | `ClinicalRecords` | **KHÔNG TỒN TẠI** | Nhầm lẫn với C# Entity cũ. Trong SQL hiện hành, ghi chép lâm sàng được lưu trực tiếp tại bảng `ClinicalEntries` (11 cột, 191 dòng) và `DentalMedicalRecords` (4 cột, 18 dòng). |
| 2 | `ImagingRooms` | **KHÔNG TỒN TẠI** | Nhầm lẫn từ đề xuất thiết kế phòng chụp riêng. Trong SQL hiện hành, phòng chụp X-quang là một bản ghi trong bảng `Rooms` (11 cột, 9 dòng) với loại phòng chuyên dụng. |
| 3 | `InsuranceClaims` | **KHÔNG TỒN TẠI** | Đề xuất tính năng bảo hiểm từ tài liệu phân tích cũ, chưa từng được tạo bảng trong SQL standalone của dự án. |
| 4 | `InsuranceClaimHistories` | **KHÔNG TỒN TẠI** | Bảng lịch sử đi kèm của tính năng bảo hiểm chưa triển khai, không có trong file SQL. |
| 5 | `FollowUpAuditLogs` | **KHÔNG TỒN TẠI** | Lịch sử gọi nhắc tái khám được tích hợp trực tiếp vào bảng `AuditLogs` (8 cột, 886 dòng) và bảng `FollowUpSchedules`. |
| 6 | `FollowUpDoctorProposals` | **KHÔNG TỒN TẠI** | Đề xuất tái khám của Bác sĩ được lưu trực tiếp trong bảng `FollowUpSchedules` (12 cột, 100 dòng), không tách bảng riêng. |
| 7 | `MedicineInventoryAudits` | **KHÔNG TỒN TẠI** | Lịch sử kiểm kê và biến động kho dược được quản lý tập trung tại bảng sổ cái `MedicineStockTransactions` (10 cột, 323 dòng). |
| 8 | `Feedbacks` | **KHÔNG TỒN TẠI** | Tên bảng thực tế trong cơ sở dữ liệu là `VisitFeedback` (13 cột, 123 dòng). Việc ghi `Feedbacks` là do dùng tên class C# cũ. |
| 9 | `FeedbackModerations` | **KHÔNG TỒN TẠI** | Các trường kiểm duyệt đánh giá (`AdminResponse`, `IsPublic`, `Status`) nằm trực tiếp trong bảng `VisitFeedback`, không tách bảng riêng. |

> [!IMPORTANT]
> **Kết luận kiểm chứng:** $80 \text{ dòng (báo cáo cũ)} - 9 \text{ bảng ảo} = \mathbf{71} \text{ bảng vật lý thực tế}$. 
> Danh sách 71 bảng dưới đây là danh sách duy nhất có thật, được trích xuất trực tiếp từ mã nguồn SQL.

---

## 3. DANH SÁCH KIỂM TOÁN CHÍNH XÁC TOÀN BỘ 71 BẢNG VẬT LÝ TRONG SQL STANDALONE

Bảng kê dưới đây được sinh tự động bằng script truy vấn trực tiếp từ `sys.tables`, `sys.columns`, `sys.partitions` của SQL Server và đối soát dòng lệnh bắt đầu trong file [DentalClinic_Schema_Full_Standalone.sql](file:///D:/SWP391/DentalCare-DB-Simplification/DB/DentalClinic_Schema_Full_Standalone.sql).

* Tổng số cột của 71 bảng: **789 cột**.
* Tổng số dòng dữ liệu: **14,469 dòng**.
* Tổng số khóa ngoại (FK): **178 FKs**.
* Tổng số ràng buộc CHECK: **92 Checks**.
* Tổng số ràng buộc UNIQUE: **32 UQs**.

| STT | Tên Bảng (Table Name) | Vị trí trong SQL | Số Cột | Số Dòng | Số FK | Số Check | Số UQ | Khóa Chính (PK) | BE Files | FE Files | Nhóm Nghiệp Vụ Chính |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- | :---: | :---: | :--- |
| 1 | `__EFMigrationsHistory` | Dòng 70 | 2 | 0 | 0 | 0 | 0 | `MigrationId` | 0 | 0 | System & Migration |
| 2 | `ClinicProfile` | Dòng 82 | 9 | 1 | 0 | 2 | 0 | `ClinicId` | 7 | 0 | Clinic Master & Facilities |
| 3 | `UserAccounts` | Dòng 107 | 13 | 35 | 0 | 3 | 0 | `UserId` | 78 | 0 | Users & Identity |
| 4 | `AuditLogs` | Dòng 140 | 8 | 886 | 1 | 0 | 0 | `AuditLogId` | 16 | 8 | System & Audit |
| 5 | `PatientProfiles` | Dòng 168 | 11 | 18 | 1 | 1 | 1 | `UserId` | 78 | 0 | Users & Identity |
| 6 | `StaffProfiles` | Dòng 198 | 6 | 16 | 1 | 1 | 1 | `UserId` | 65 | 0 | Users & Identity |
| 7 | `DentistProfiles` | Dòng 224 | 8 | 10 | 1 | 1 | 0 | `UserId` | 75 | 0 | Users & Identity |
| 8 | `Departments` | Dòng 249 | 7 | 4 | 1 | 1 | 1 | `DepartmentId` | 46 | 12 | Users & Identity |
| 9 | `DentistDepartments` | Dòng 276 | 5 | 17 | 2 | 1 | 1 | `DentistDepartmentId` | 13 | 0 | Users & Identity |
| 10 | `Rooms` | Dòng 306 | 11 | 9 | 1 | 1 | 1 | `RoomId` | 14 | 0 | Clinic Master & Facilities |
| 11 | `DentalChairs` | Dòng 342 | 4 | 16 | 1 | 1 | 2 | `DentalChairId` | 22 | 0 | Clinic Master & Facilities |
| 12 | `DepartmentServices` | Dòng 369 | 15 | 34 | 1 | 3 | 2 | `DepartmentServiceId` | 46 | 14 | Services Catalog |
| 13 | `DepartmentWorkSchedules` | Dòng 424 | 6 | 28 | 1 | 2 | 1 | `ScheduleId` | 4 | 0 | Appointments & Scheduling |
| 14 | `ImagingEquipments` | Dòng 453 | 12 | 1 | 1 | 1 | 2 | `EquipmentId` | 9 | 0 | Imaging & Diagnostics |
| 15 | `ImagingServiceConfigs` | Dòng 493 | 8 | 3 | 1 | 0 | 1 | `ConfigId` | 4 | 0 | Imaging & Diagnostics |
| 16 | `ServiceCombos` | Dòng 521 | 17 | 4 | 2 | 0 | 1 | `ComboId` | 8 | 2 | Marketing & Combos |
| 17 | `ServiceWorkflows` | Dòng 562 | 12 | 34 | 1 | 0 | 0 | `WorkflowId` | 10 | 0 | Clinical Workflows |
| 18 | `ServiceWorkflowSteps` | Dòng 596 | 10 | 112 | 1 | 0 | 0 | `StepId` | 6 | 0 | Clinical Workflows |
| 19 | `AttendancePolicies` | Dòng 632 | 8 | 1 | 0 | 0 | 0 | `PolicyId` | 4 | 0 | Staff Attendance & Timekeeping |
| 20 | `AttendancePeriodClosures` | Dòng 656 | 16 | 1 | 2 | 2 | 1 | `ClosureId` | 4 | 0 | Staff Attendance & Timekeeping |
| 21 | `StaffAttendanceRecords` | Dòng 706 | 22 | 1 | 2 | 1 | 0 | `AttendanceId` | 8 | 0 | Staff Attendance & Timekeeping |
| 22 | `StaffAttendanceEvents` | Dòng 757 | 8 | 2 | 2 | 1 | 0 | `EventId` | 4 | 0 | Staff Attendance & Timekeeping |
| 23 | `StaffAttendanceCorrectionRequests` | Dòng 788 | 13 | 0 | 3 | 1 | 0 | `CorrectionRequestId` | 5 | 0 | Staff Attendance & Timekeeping |
| 24 | `ReceptionistShiftTemplates` | Dòng 828 | 9 | 3 | 0 | 2 | 0 | `ShiftTemplateId` | 8 | 0 | Staff Workforce & Schedules |
| 25 | `ReceptionistWorkSchedules` | Dòng 857 | 12 | 20 | 3 | 2 | 0 | `ReceptionistScheduleId` | 14 | 0 | Staff Workforce & Schedules |
| 26 | `StaffLeaveRequests` | Dòng 900 | 16 | 10 | 2 | 3 | 0 | `LeaveRequestId` | 1 | 0 | Staff Workforce & Leaves |
| 27 | `StaffLeaveRequestPeriods` | Dòng 946 | 6 | 6 | 1 | 1 | 0 | `PeriodId` | 1 | 0 | Staff Workforce & Leaves |
| 28 | `ReceptionistShiftSwapRequests` | Dòng 970 | 14 | 2 | 5 | 2 | 0 | `SwapRequestId` | 7 | 0 | Staff Workforce & Schedules |
| 29 | `ReceptionistWorkHandovers` | Dòng 1022 | 13 | 0 | 3 | 1 | 0 | `HandoverId` | 5 | 0 | Staff Workforce & Schedules |
| 30 | `ReceptionistCoverageIncidents` | Dòng 1065 | 18 | 0 | 4 | 1 | 0 | `IncidentId` | 6 | 0 | Staff Workforce & Schedules |
| 31 | `DentistWorkSchedules` | Dòng 1115 | 8 | 577 | 2 | 2 | 1 | `ScheduleId` | 33 | 0 | Staff Workforce & Schedules |
| 32 | `DentistAvailability` | Dòng 1150 | 7 | 1 | 1 | 2 | 0 | `AvailabilityId` | 8 | 0 | Staff Workforce & Schedules |
| 33 | `XrayDentistRosters` | Dòng 1177 | 14 | 2 | 5 | 3 | 0 | `RosterId` | 6 | 0 | Staff Workforce & Schedules |
| 34 | `DentalMedicalRecords` | Dòng 1238 | 4 | 18 | 1 | 0 | 1 | `MedicalRecordId` | 10 | 0 | Clinical Care & Records |
| 35 | `TreatmentPlans` | Dòng 1259 | 13 | 288 | 3 | 2 | 0 | `TreatmentPlanId` | 25 | 14 | Clinical Care & Records |
| 36 | `TreatmentPlanItems` | Dòng 1302 | 14 | 297 | 3 | 3 | 1 | `TreatmentPlanItemId` | 22 | 0 | Clinical Care & Records |
| 37 | `Appointments` | Dòng 1356 | 28 | 2492 | 14 | 4 | 2 | `AppointmentId` | 98 | 71 | Appointments & Scheduling |
| 38 | `AppointmentChangeProposals` | Dòng 1459 | 11 | 0 | 4 | 2 | 0 | `ProposalId` | 13 | 0 | Appointments & Scheduling |
| 39 | `AppointmentDisruptions` | Dòng 1503 | 21 | 2 | 6 | 2 | 0 | `DisruptionId` | 10 | 0 | Appointments & Scheduling |
| 40 | `AppointmentReminderJobs` | Dòng 1568 | 11 | 10 | 1 | 0 | 0 | `ReminderJobId` | 3 | 0 | Appointments & Scheduling |
| 41 | `AppointmentStatusHistories` | Dòng 1596 | 7 | 4181 | 2 | 0 | 0 | `HistoryId` | 13 | 0 | Appointments & Scheduling |
| 42 | `CareEpisodes` | Dòng 1627 | 11 | 1 | 3 | 0 | 2 | `CareEpisodeId` | 9 | 0 | Clinical Care & Records |
| 43 | `ClinicalConsultations` | Dòng 1669 | 13 | 5 | 4 | 2 | 0 | `ConsultationId` | 5 | 0 | Clinical Care & Records |
| 44 | `ClinicalEntries` | Dòng 1717 | 11 | 191 | 3 | 0 | 0 | `ClinicalEntryId` | 16 | 4 | Clinical Care & Records |
| 45 | `OdontogramEntries` | Dòng 1751 | 6 | 116 | 1 | 0 | 0 | `OdontogramId` | 6 | 0 | Clinical Care & Records |
| 46 | `TreatmentSessions` | Dòng 1771 | 7 | 372 | 3 | 0 | 0 | `TreatmentSessionId` | 28 | 5 | Clinical Care & Records |
| 47 | `PerformedServices` | Dòng 1801 | 6 | 188 | 2 | 2 | 0 | `PerformedServiceId` | 14 | 8 | Clinical Services Execution |
| 48 | `EpisodeServiceTasks` | Dòng 1832 | 21 | 1 | 5 | 3 | 1 | `ServiceTaskId` | 5 | 0 | Clinical Services Execution |
| 49 | `EpisodeServiceTaskStepProgresses` | Dòng 1900 | 12 | 0 | 3 | 0 | 1 | `StepProgressId` | 4 | 0 | Clinical Services Execution |
| 50 | `DiagnosticOrders` | Dòng 1949 | 13 | 441 | 3 | 1 | 0 | `DiagnosticOrderId` | 15 | 0 | Imaging & Diagnostics |
| 51 | `ImagingSessions` | Dòng 1990 | 22 | 369 | 8 | 2 | 0 | `ImagingSessionId` | 11 | 0 | Imaging & Diagnostics |
| 52 | `DiagnosticResults` | Dòng 2065 | 17 | 7 | 5 | 1 | 0 | `DiagnosticResultId` | 11 | 0 | Imaging & Diagnostics |
| 53 | `ImagingRecords` | Dòng 2119 | 6 | 3 | 1 | 0 | 0 | `ImagingRecordId` | 11 | 4 | Imaging & Diagnostics |
| 54 | `Medicines` | Dòng 2145 | 13 | 112 | 0 | 2 | 1 | `MedicineId` | 23 | 0 | Pharmacy & Inventory |
| 55 | `MedicineBatches` | Dòng 2180 | 11 | 114 | 1 | 2 | 2 | `BatchId` | 18 | 0 | Pharmacy & Inventory |
| 56 | `MedicineStockTransactions` | Dòng 2221 | 10 | 323 | 2 | 1 | 0 | `TransactionId` | 8 | 0 | Pharmacy & Inventory |
| 57 | `Prescriptions` | Dòng 2253 | 4 | 317 | 1 | 0 | 1 | `PrescriptionId` | 17 | 0 | Pharmacy & Inventory |
| 58 | `PrescriptionItems` | Dòng 2274 | 7 | 351 | 1 | 0 | 0 | `PrescriptionItemId` | 13 | 0 | Pharmacy & Inventory |
| 59 | `MedicineDispensations` | Dòng 2295 | 9 | 300 | 4 | 0 | 0 | `DispensationId` | 10 | 0 | Pharmacy & Inventory |
| 60 | `MedicineDispensationItems` | Dòng 2333 | 9 | 300 | 5 | 1 | 0 | `DispensationItemId` | 6 | 0 | Pharmacy & Inventory |
| 61 | `Invoices` | Dòng 2381 | 8 | 452 | 3 | 3 | 1 | `InvoiceId` | 37 | 14 | Billing & Invoicing |
| 62 | `InvoiceItems` | Dòng 2423 | 12 | 615 | 5 | 3 | 0 | `InvoiceItemId` | 17 | 0 | Billing & Invoicing |
| 63 | `Payments` | Dòng 2475 | 9 | 395 | 2 | 3 | 0 | `PaymentId` | 20 | 0 | Billing & Invoicing |
| 64 | `DepartmentReferrals` | Dòng 2517 | 16 | 57 | 9 | 3 | 0 | `ReferralId` | 14 | 0 | Appointments & Scheduling |
| 65 | `FollowUpSchedules` | Dòng 2590 | 12 | 100 | 6 | 1 | 0 | `FollowUpId` | 17 | 0 | Follow-up & Recalls |
| 66 | `VisitFeedback` | Dòng 2641 | 13 | 123 | 4 | 2 | 1 | `FeedbackId` | 13 | 6 | Marketing, Content & Feedback |
| 67 | `Notifications` | Dòng 2688 | 10 | 58 | 2 | 2 | 0 | `NotificationId` | 11 | 0 | System & Notifications |
| 68 | `SupportChatMessages` | Dòng 2723 | 8 | 0 | 1 | 0 | 0 | `MessageId` | 4 | 0 | System & Communications |
| 69 | `BlogCategories` | Dòng 2747 | 4 | 6 | 0 | 0 | 1 | `CategoryId` | 7 | 0 | Marketing, Content & Feedback |
| 70 | `BlogPosts` | Dòng 2765 | 21 | 10 | 3 | 2 | 1 | `BlogPostId` | 9 | 2 | Marketing, Content & Feedback |
| 71 | `BlogPostMedia` | Dòng 2822 | 11 | 0 | 2 | 1 | 0 | `MediaId` | 5 | 0 | Marketing, Content & Feedback |
| **TỔNG** | **71 BẢNG VẬT LÝ** | -- | **789** | **14,469** | **178** | **92** | **32** | **71 PKs** | -- | -- | **13 Nhóm Nghiệp Vụ** |

---

## 4. PHÂN TÍCH CHUYÊN SÂU CÁC KHU VỰC ĐƯỢC CHỈ ĐẠO TẠM DỪNG / DUYỆT ĐỊNH HƯỚNG

Tuân thủ nghiêm ngặt yêu cầu của Project Owner:
* **Đã tạm dừng:** Không thực hiện bất kỳ lệnh DROP, MERGE, ALTER hoặc xóa cột nào trên source code hay database.
* **Chứng minh bằng chứng thực tế:** Dưới đây là phân tích chi tiết dữ liệu, ràng buộc quan hệ và người tiêu thụ (consumers) cho từng nhóm.

### 4.1. Đánh giá Định hướng: Chuyển Leave Management sang Single Entity `StaffLeaveRequest`
* **Vấn đề đã xác thực:** Mô hình Table-Per-Hierarchy (TPH) của EF Core kết hợp với `[NotMapped]` trên các thuộc tính như `DentistUserId` hay `ReceptionistUserId` tạo ra bẫy lỗi Runtime nghiêm trọng khi truy vấn qua LINQ.
* **Phương án kiến trúc đơn giản, an toàn:**
  * Bảng vật lý SQL duy nhất: `StaffLeaveRequests` (16 cột).
  * Trong C#: Chỉ duy trì 1 lớp thực thể duy nhất: `StaffLeaveRequest`.
  * Không còn các lớp con kế thừa `DentistLeaveRequest` và `ReceptionistLeaveRequest`.
  * Không cần bất kỳ thuộc tính `[NotMapped]` nào.
  * Cả `DentistLeaveService` và `ReceptionistLeaveService` đều làm việc với `StaffLeaveRequest`, phân biệt bằng `StaffRole = 'Dentist'` hoặc `'Receptionist'`.
  * **Trạng thái:** **Chờ phê duyệt chính thức** trước khi cập nhật mã nguồn C#.

### 4.2. `PerformedServices` (Dòng 1801) vs `EpisodeServiceTasks` (Dòng 1832)
* **Bằng chứng dữ liệu:**
  * `PerformedServices` có **188 dòng dữ liệu** liên kết trực tiếp với 372 `TreatmentSessions` và là căn cứ tính tiền cho các dòng hóa đơn `InvoiceItems`.
  * `EpisodeServiceTasks` chỉ có **1 dòng dữ liệu**, đại diện cho luồng quy trình điều trị mới (Care Episode Workflows).
* **Kết luận bảo vệ luận điểm:** **TUYỆT ĐỐI KHÔNG GỘP BẢNG.** 
  Việc gộp bảng sẽ phá hủy 188 bản ghi lịch sử điều trị cũ hoặc buộc phải sinh ra các đợt khám giả lập (`CareEpisodes`), gây rủi ro sai lệch số liệu doanh thu trước hội đồng chấm thi. Giữ nguyên 2 bảng và kiểm soát qua ràng buộc `CK_InvoiceItems_SourceIntegrity` trên `InvoiceItems`.

### 4.3. `ImagingRecords` (Dòng 2119) vs `DiagnosticResults` (Dòng 2065)
* **Bằng chứng quan hệ & Consumer:**
  * `ImagingRecords`: 6 cột, 3 dòng dữ liệu, có FK trỏ về `ClinicalEntries(ClinicalEntryId)`. Được dùng ở 11 file BE và 4 file FE (chủ yếu là màn hình ghi chú lâm sàng nhanh của Bác sĩ).
  * `DiagnosticResults`: 17 cột, 7 dòng dữ liệu, có FK trỏ về `DiagnosticOrders`, `ImagingSessions`, `ImagingEquipments` và `DentistProfiles` (bác sĩ đọc kết quả).
* **Quyết định:** **TẠM DỪNG MERGE theo chỉ đạo.** Giữ nguyên cả 2 bảng. `ImagingRecords` phục vụ ảnh đính kèm hồ sơ nhanh, `DiagnosticResults` phục vụ hệ thống chẩn đoán hình ảnh chuyên sâu (PACS).

### 4.4. `DentalMedicalRecords` (Dòng 1238)
* **Bằng chứng:** 4 cột (`MedicalRecordId`, `PatientUserId`, `RecordType`, `Description`), 18 dòng dữ liệu, liên kết với `PatientProfiles`.
* **Đánh giá:** Bảng lưu tiền sử bệnh lý nền dạng danh sách (ví dụ: Dị ứng Penicillin, Huyết áp cao). Việc giữ bảng này giúp chuẩn hóa quan hệ 1:N (một bệnh nhân có nhiều cảnh báo y khoa) thay vì gộp thành 1 chuỗi text trong `PatientProfiles`.
* **Quyết định:** **TẠM DỪNG MERGE.** Giữ nguyên để bảo vệ tính chuẩn hóa 1NF/2NF/3NF trước giảng viên.

### 4.5. `VisitFeedback` (Dòng 2641)
* **Bằng chứng:** 13 cột, 123 dòng dữ liệu, 4 FKs, 13 file BE, 6 file FE.
* **Đánh giá:** Không có bảng nào khác tên là `Feedbacks`. `VisitFeedback` là bảng duy nhất của hệ thống quản lý đánh giá của bệnh nhân.
* **Quyết định:** **GIỮ NGUYÊN VẸN.** Không có sự trùng lặp để merge.

### 4.6. `DentistAvailability` (Dòng 1150) vs `DentistWorkSchedules` (Dòng 1115)
* **Bằng chứng:**
  * `DentistWorkSchedules`: 577 dòng, lưu ca trực định kỳ theo Chuyên khoa (`DepartmentId`) và ngày làm việc.
  * `DentistAvailability`: 1 dòng, lưu ngoại lệ thời gian (ví dụ: bận đột xuất, hội thảo).
* **Quyết định:** **TẠM DỪNG MERGE.** Giữ nguyên để tách biệt lịch phân công định kỳ và ngoại lệ khả dụng đột xuất của bác sĩ.

### 4.7. Cột trong `InvoiceItems` (Dòng 2423)
* **Bằng chứng:** Bảng có 12 cột, 615 dòng dữ liệu.
* **Đánh giá:** Các cột `AppointmentId` và `PerformingDentistUserId` cung cấp khả năng truy vấn snapshot trực tiếp tại thời điểm xuất hóa đơn mà không cần JOIN ngược qua nhiều tầng bảng lâm sàng.
* **Quyết định:** **TẠM DỪNG XÓA CỘT.** Giữ nguyên toàn bộ 12 cột để đảm bảo các báo cáo kế toán và API xuất hóa đơn không bị ảnh hưởng.

---

## 5. THIẾT KẾ CƠ SỞ DỮ LIỆU TINH GỌN VÀ CÓ THỂ BẢO VỆ ĐƯỢC (DEFENSIBLE DATABASE DESIGN)

### 5.1. Triết lý Thiết kế Bảo vệ Trước Giảng viên (Academic & Engineering Defense)
1. **Không chạy theo số lượng bảng giảm:** Một cơ sở dữ liệu bệnh viện/nha khoa đạt chuẩn cần phản ánh đúng các miền nghiệp vụ (Domain Boundaries): Tiếp đón -> Khám tổng quát -> Chẩn đoán hình ảnh -> Điều trị chuyên sâu -> Kê đơn phát thuốc -> Thanh toán viện phí -> Chăm sóc sau điều trị.
2. **Loại bỏ sự trùng lặp thực chất:** Điểm trùng lặp thực sự duy nhất trong hệ thống cũ là chia đôi bảng nghỉ phép theo vai trò (`DentistLeaveRequests` vs `ReceptionistLeaveRequests`). Việc hợp nhất thành `StaffLeaveRequests` là hoàn toàn chính đáng và chuẩn mực về mặt thiết kế hướng đối tượng lẫn cơ sở dữ liệu quan hệ.
3. **Bảo toàn 100% dữ liệu lịch sử:** Không xóa bỏ bất kỳ dòng dữ liệu nào của 14,469 bản ghi hiện hành. Mọi hóa đơn, bệnh án, lịch khám đều được toàn vẹn.

### 5.2. Đề xuất Kiến trúc Cụ thể cho Bước Tiếp theo (Chờ Project Owner Duyệt)

| Nhóm Đề Xuất | Thay đổi Cụ thể | Lý do Kỹ thuật & Nghiệp vụ | Rủi ro |
| :--- | :--- | :--- | :---: |
| **Đề xuất 1 (Khuyên dùng)** | Chuyển hoàn toàn Leave Management sang Single Entity `StaffLeaveRequest` trong C#. | Triệt tiêu lỗi Runtime LINQ do `[NotMapped]`, làm code C# và bảng SQL đồng nhất 100%. | Rất thấp |
| **Đề xuất 2** | Xóa bỏ cột thừa `ComboCode` trong bảng `Appointments` (giữ khóa ngoại `ComboId`). | `ComboCode` trùng lặp hoàn toàn với `ComboId` trỏ sang `ServiceCombos`. | Rất thấp |
| **Đề xuất 3** | Đổi tên/loại bỏ cột `ManagerUserId` trong bảng `Departments`. | Tàn tích của vai trò `DepartmentManager` đã bị bãi bỏ trong hệ thống 4 roles. | Rất thấp |
| **Bảo lưu (Không sửa)** | Giữ nguyên 71 bảng hiện hành, không gộp `PerformedServices`, `ImagingRecords`, `DentalMedicalRecords`, `VisitFeedback`, `DentistAvailability`. | Đảm bảo tính ổn định tối đa cho mã nguồn Backend/Frontend và dễ bảo vệ cấu trúc trước hội đồng chấm thi. | Không có |

---

## 6. TRẠNG THÁI GIT & CAM KẾT AN TOÀN TÀI NGUYÊN

1. **Trạng thái Git:**
   * Worktree độc lập: `D:\SWP391\DentalCare-DB-Simplification`.
   * Nhánh làm việc: `refactor/database-domain-simplification`.
   * Nhánh `develop` và `main`: **Tuyệt đối không bị can thiệp, nguyên vẹn 100%**.
   * Các thay đổi đã thực hiện trước đó trong worktree được giữ nguyên trạng, không bị reset.
2. **An toàn Tài nguyên:**
   * Không có background task nào đang chạy.
   * Không có tiến trình `dotnet`, `testhost`, `playwright` hay script kiểm thử nào tồn tại trên máy.
   * Agent đã dừng toàn bộ hoạt động thực thi code và kiểm thử, sẵn sàng chờ chỉ đạo tiếp theo từ Project Owner.
