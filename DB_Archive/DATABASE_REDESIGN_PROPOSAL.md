# DentalCare SWP391 – Database Redesign Proposal & Target Architecture

> **Tài liệu kiến trúc:** Đề xuất Tái Cấu Trúc Toàn Diện Cơ Sở Dữ Liệu & Chuẩn Hóa Single Source of Truth  
> **Dự án:** Hệ thống Quản lý Phòng khám Nha khoa DentalCare (SWP391 - Nhóm 6)  
> **Mục tiêu:** Thiết lập kiến trúc dữ liệu chuẩn hóa, loại bỏ hoàn toàn các điểm nghẽn toàn vẹn và rủi ro tính tiền trùng lặp, bảo đảm tính mở rộng cho các giai đoạn tiếp theo mà không phá vỡ mã nguồn hiện hành.

---

## 1. NGUYÊN TẮC THIẾT KẾ CỐT LÕI (CORE DESIGN PRINCIPLES)

1. **Single Source of Truth (Nguồn dữ liệu chân lý duy nhất):**
   - Mỗi thực thể nghiệp vụ chỉ có đúng một bảng chịu trách nhiệm lưu trữ chính thức.
   - Dịch vụ thực hiện lâm sàng: Thống nhất trên `EpisodeServiceTasks`.
   - Chẩn đoán hình ảnh: Thống nhất trên `DiagnosticOrders` -> `ImagingSessions` -> `DiagnosticResults`.
   - Hóa đơn viện phí: Thống nhất 1 Đợt khám (`CareEpisode`) chỉ tương ứng với tối đa 1 Hóa đơn (`Invoice`).
2. **Database-Enforced Integrity (Toàn vẹn thực thi tại tầng CSDL):**
   - Không phó mặc quy tắc nghiệp vụ quan trọng (như chống double-billing, chống xếp nhầm phòng/ghế, chống sai lệch khoa/lô thuốc) hoàn toàn cho tầng ứng dụng C#.
   - Khai thác triệt để các tính năng của SQL Server: Composite Foreign Keys, Filtered Unique Indexes, Check Constraints.
3. **Zero Breaking Changes (Không gây đứt gãy hệ thống hiện hành):**
   - Không thực hiện các thao tác phá hủy đột ngột (`DROP TABLE`, `DROP COLUMN`) trên database đang hoạt động.
   - Áp dụng kỹ thuật **Adapter Pattern & Phased Migration**: Thêm ràng buộc bảo vệ trước, di chuyển dữ liệu (backfill), chuyển hướng luồng đọc/ghi của code, rồi mới đóng băng các bảng cũ sang chế độ Read-Only.

---

## 2. MA TRẬN PHÂN LOẠI TOÀN BỘ 72 BẢNG (TABLE CATEGORIZATION MATRIX)

Dưới đây là bảng phân loại toàn bộ 69 bảng hiện hữu trong `DentalClinic_Schema_Full_Standalone.sql` cộng với 3 bảng trên các nhánh workflow mới (`ServiceWorkflows`, `ServiceWorkflowSteps`, `EpisodeServiceTaskStepProgresses`):

| STT | Tên Bảng | Phân Loại | Lý Do Phân Loại & Định Hướng Kiến Trúc |
| :---: | :--- | :---: | :--- |
| 1 | `__EFMigrationsHistory` | **KEEP** | Bảng hạ tầng EF Core quản lý lịch sử migration. |
| 2 | `ClinicProfile` | **KEEP** | Thông tin cấu hình phòng khám, địa chỉ, hotline, giờ mở cửa (1 dòng duy nhất). |
| 3 | `UserAccounts` | **KEEP** | Bảng định danh người dùng cốt lõi, quản lý 4 vai trò chính thức (`Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`), bảo mật mật khẩu và trạng thái tài khoản. |
| 4 | `PatientProfiles` | **KEEP** | Hồ sơ bệnh nhân, dùng `UserId` làm PK 1-1, mã nghiệp vụ `PatientCode` duy nhất. |
| 5 | `StaffProfiles` | **KEEP** | Hồ sơ nhân sự (Nhân viên, Lễ tân, Bác sĩ), dùng `UserId` làm PK 1-1, mã `EmployeeCode`. |
| 6 | `DentistProfiles` | **KEEP** | Hồ sơ chuyên môn bác sĩ (chứng chỉ hành nghề, an toàn bức xạ), dùng `UserId` làm PK 1-1. |
| 7 | `Departments` | **KEEP** | Danh mục chuyên khoa lâm sàng. |
| 8 | `DentistDepartments` | **KEEP** | Bảng liên kết Bác sĩ và Chuyên khoa (Many-to-Many). |
| 9 | `Rooms` | **KEEP** | Danh mục phòng khám, phòng thủ thuật, phòng chụp X-quang. |
| 10 | `DentalChairs` | **KEEP** | Danh mục ghế nha khoa gắn với từng phòng khám (`RoomId`). |
| 11 | `ImagingEquipments` | **KEEP** | Thiết bị chẩn đoán hình ảnh kỹ thuật số (CS 8200 3D, Sensor, OPG). |
| 12 | `SupportChatMessages` | **KEEP** | Tin nhắn hỗ trợ trực tuyến giữa Bệnh nhân và Lễ tân. |
| 13 | `Notifications` | **KEEP** | Thông báo hệ thống cho người dùng. |
| 14 | `AuditLogs` | **KEEP** | Nhật ký kiểm toán các thao tác nhạy cảm trong hệ thống. |
| 15 | `DepartmentServices` | **MODIFY** | Danh mục dịch vụ nha khoa. Cần bổ sung `ServiceCode` (từ nhánh Admin Workflow) và Composite Unique Index `(DepartmentId, DepartmentServiceId)` để hỗ trợ Composite FK từ Appointments. |
| 16 | `ServiceCombos` | **MODIFY** | Gói dịch vụ nha khoa. Cần bổ sung quan hệ liên kết dịch vụ có cấu trúc thay vì chỉ lưu JSON text thuần túy trong `IncludedServices`. |
| 17 | `ImagingServiceConfigs` | **MODIFY** | Cấu hình thời lượng ca chụp X-quang. Cần loại bỏ việc lưu trùng lặp `ServiceName` và `Price`, tham chiếu trực tiếp qua `DepartmentServiceId`. |
| 18 | `Appointments` | **MODIFY** | Đặt lịch hẹn và tiếp đón. Cần bổ sung Composite FK kiểm soát `DepartmentId + RequestedServiceId` và `RoomId + DentalChairId`. |
| 19 | `AppointmentChangeProposals` | **KEEP** | Đề xuất đổi lịch hẹn giữa Bệnh nhân và Phòng khám. |
| 20 | `AppointmentStatusHistories` | **KEEP** | Lịch sử chuyển trạng thái lịch hẹn (`Confirmed`, `CheckedIn`, `InService`, `Completed`). |
| 21 | `AppointmentReminderJobs` | **KEEP** | Hàng đợi tác vụ gửi email nhắc lịch hẹn tự động (T24, T8). |
| 22 | `AppointmentDisruptions` | **KEEP** | Quản lý các biến cố ảnh hưởng lịch hẹn (bác sĩ nghỉ đột xuất). |
| 23 | `CareEpisodes` | **MODIFY** | Đợt điều trị đa chuyên khoa. Cần bổ sung ràng buộc toàn vẹn `PatientUserId` khớp với `RootAppointment.PatientUserId`. |
| 24 | `EpisodeServiceTasks` | **MODIFY** | **Single Source of Truth** cho các dịch vụ/thủ thuật lâm sàng. Bổ sung các cột liên kết phiên bản quy trình điều trị từ nhánh Dentist Workflow. |
| 25 | `DentalMedicalRecords` | **KEEP** | Hồ sơ bệnh án nha khoa trọn đời của từng bệnh nhân. |
| 26 | `ClinicalEntries` | **KEEP** | Các lượt khám lâm sàng, tiền sử bệnh, triệu chứng chính và chẩn đoán. |
| 27 | `OdontogramEntries` | **KEEP** | Sơ đồ răng tương tác (tình trạng từng răng 11-48, chẩn đoán, mặt răng). |
| 28 | `ImagingRecords` | **LEGACY CANDIDATE** | Bảng ảnh X-quang cũ (chỉ có 3 dòng từ tháng 10). Toàn bộ chức năng đã được thay thế hoàn chỉnh bởi `DiagnosticResults`. |
| 29 | `DiagnosticOrders` | **KEEP** | Y lệnh chỉ định chụp phim chẩn đoán hình ảnh từ Bác sĩ. |
| 30 | `ImagingSessions` | **MODIFY** | Ca chụp X-quang tại phòng máy. Cần tinh gọn bỏ các cột denormalized dư thừa (`SourceAppointmentId`, `PatientUserId`). |
| 31 | `DiagnosticResults` | **KEEP** | Kết quả chẩn đoán hình ảnh, file ảnh, hình thu nhỏ, kết luận chuyên môn và phê duyệt của Bác sĩ. |
| 32 | `TreatmentPlans` | **KEEP** | Kế hoạch điều trị tổng thể dài hạn cho bệnh nhân. |
| 33 | `TreatmentPlanItems` | **KEEP** | Các giai đoạn điều trị trong kế hoạch. |
| 34 | `TreatmentSessions` | **MODIFY** | Phiên điều trị theo kế hoạch. Cần tách bỏ logic sinh dummy plan khi ghi nhận thủ thuật tại ghế. |
| 35 | `PerformedServices` | **MERGE CANDIDATE** | Bảng ghi nhận dịch vụ cũ. Đang chồng chéo chức năng với `EpisodeServiceTasks`. Cần di chuyển dữ liệu sang `EpisodeServiceTasks` và ngưng sử dụng. |
| 36 | `ClinicalConsultations` | **KEEP** | Hội chẩn chuyên môn liên khoa giữa các Bác sĩ. |
| 37 | `Prescriptions` | **KEEP** | Đơn thuốc xuất viện cho bệnh nhân. |
| 38 | `PrescriptionItems` | **KEEP** | Chi tiết các loại thuốc, liều dùng, số lượng trong đơn thuốc. |
| 39 | `Medicines` | **KEEP** | Danh mục thuốc và vật tư y tế. |
| 40 | `MedicineBatches` | **KEEP** | Quản lý lô thuốc, hạn sử dụng, giá vốn và tồn kho khả dụng. |
| 41 | `MedicineStockTransactions` | **KEEP** | Nhật ký xuất nhập tồn kho dược phẩm. |
| 42 | `MedicineDispensations` | **KEEP** | Phiếu phát thuốc thực tế cho bệnh nhân tại quầy dược. |
| 43 | `MedicineDispensationItems` | **MODIFY** | Chi tiết phát thuốc. Cần bổ sung Composite FK `(MedicineId, MedicineBatchId)` để ngăn xuất sai thuốc khác lô. |
| 44 | `Invoices` | **MODIFY** | Hóa đơn viện phí. **CỰC KỲ QUAN TRỌNG:** Phải thêm Filtered Unique Index trên `CareEpisodeId` để bảo đảm 1 Đợt khám chỉ có đúng 1 Hóa đơn. |
| 45 | `InvoiceItems` | **MODIFY** | Chi tiết hóa đơn. **CỰC KỲ QUAN TRỌNG:** Phải thêm Unique Index chống tính tiền trùng lặp một dịch vụ trên nhiều hóa đơn. |
| 46 | `Payments` | **KEEP** | Giao dịch thanh toán viện phí (Tiền mặt, VietQR, Chuyển khoản). |
| 47 | `DepartmentReferrals` | **KEEP** | Giấy chuyển tuyến chuyên khoa nội bộ giữa các khoa. |
| 48 | `FollowUpSchedules` | **KEEP** | Lịch hẹn tái khám và theo dõi sau điều trị. |
| 49 | `VisitFeedback` | **KEEP** | Đánh giá và phản hồi chất lượng dịch vụ từ Bệnh nhân. |
| 50 | `BlogCategories` | **KEEP** | Danh mục bài viết kiến thức nha khoa. |
| 51 | `BlogPosts` | **KEEP** | Bài viết truyền thông và hướng dẫn chăm sóc răng miệng. |
| 52 | `BlogPostMedia` | **KEEP** | Quản lý media đính kèm bài viết. |
| 53 | `DepartmentWorkSchedules` | **KEEP** | Khung giờ hoạt động theo tuần của từng phòng ban/khoa. |
| 54 | `DentistWorkSchedules` | **KEEP** | Lịch phân công ca làm việc theo ngày của Bác sĩ. |
| 55 | `DentistAvailability` | **MERGE CANDIDATE** | Bảng có 0 dòng dữ liệu thực tế. Cần sáp nhập logic chặn lịch vào `DentistLeaveRequests` và `DentistWorkSchedules`. |
| 56 | `DentistLeaveRequests` | **KEEP** | Quy trình xin nghỉ phép của Bác sĩ và điều phối lịch hẹn bị gián đoạn. |
| 57 | `XrayDentistRosters` | **KEEP** | Lịch trực chuyên trách phòng chụp X-quang của Bác sĩ theo ca. |
| 58 | `AttendancePolicies` | **KEEP** | Chính sách chấm công, thời gian ân hạn và quy tắc phạt vắng mặt. |
| 59 | `AttendancePeriodClosures` | **KEEP** | Khóa kỳ công và chốt dữ liệu bảng lương hàng tháng. |
| 60 | `ReceptionistShiftTemplates` | **KEEP** | Mẫu ca làm việc của Lễ tân (Sáng, Chiều, Tối). |
| 61 | `ReceptionistWorkSchedules` | **KEEP** | Lịch trực và dữ liệu chấm công thực tế của Lễ tân. |
| 62 | `ReceptionistLeaveRequests` | **KEEP** | Đơn xin nghỉ phép của Lễ tân. |
| 63 | `ReceptionistLeaveRequestPeriods` | **KEEP** | Chi tiết các khoảng thời gian nghỉ phép của Lễ tân. |
| 64 | `ReceptionistShiftSwapRequests` | **KEEP** | Đơn xin đổi ca trực giữa các nhân viên Lễ tân. |
| 65 | `ReceptionistWorkHandovers` | **KEEP** | Biên bản bàn giao công việc cuối ca trực của Lễ tân. |
| 66 | `ReceptionistCoverageIncidents` | **KEEP** | Ghi nhận sự cố thiếu nhân sự trực và phương án bù ca. |
| 67 | `StaffAttendanceRecords` | **KEEP** | Bản ghi chấm công tổng hợp theo ca của nhân viên. |
| 68 | `StaffAttendanceEvents` | **KEEP** | Nhật ký sự kiện quẹt thẻ / điểm danh vào-ra (Check-in/Check-out). |
| 69 | `StaffAttendanceCorrectionRequests` | **KEEP** | Yêu cầu giải trình và điều chỉnh dữ liệu chấm công. |
| 70 | `ServiceWorkflows` *(Pending)* | **KEEP** | *(Từ nhánh Admin Workflow)* Định nghĩa phiên bản quy trình lâm sàng cho dịch vụ nha khoa. |
| 71 | `ServiceWorkflowSteps` *(Pending)* | **KEEP** | *(Từ nhánh Admin Workflow)* Các bước thực hiện tuần tự trong quy trình lâm sàng. |
| 72 | `EpisodeServiceTaskStepProgresses` *(Pending)* | **KEEP** | *(Từ nhánh Dentist Workflow)* Ghi nhận tiến độ Bác sĩ hoàn thành từng bước quy trình tại ghế. |

---

## 3. LỘ TRÌNH TÁI CẤU TRÚC THEO THỨ TỰ ƯU TIÊN (PRIORITIZED ROADMAP)

### ƯU TIÊN 1 (P0): Khắc Phục Lỗ Hổng Toàn Vẹn Dữ Liệu & Viện Phí (Data Integrity & Billing)

#### 1. Bảo đảm mỗi `CareEpisode` chỉ có tối đa 1 Hóa đơn:
- **Giải pháp:** Tạo Unique Filtered Index trên bảng `Invoices`:
  ```sql
  CREATE UNIQUE NONCLUSTERED INDEX [UQ_Invoices_CareEpisodeId]
  ON [dbo].[Invoices]([CareEpisodeId])
  WHERE [CareEpisodeId] IS NOT NULL;
  ```
- **Lợi ích:** Ngăn chặn tuyệt đối việc sinh nhiều hóa đơn cho cùng một đợt khám ở tầng Database.

#### 2. Triệt tiêu rủi ro Double-Billing trên `InvoiceItems`:
- **Giải pháp:** Bổ sung Unique Filtered Index toàn cục chống tính tiền trùng lặp:
  ```sql
  -- Chống một ServiceTaskId bị tính tiền 2 lần (dù cùng hóa đơn hay khác hóa đơn)
  CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_Unique_ServiceTask]
  ON [dbo].[InvoiceItems]([ServiceTaskId])
  WHERE [ServiceTaskId] IS NOT NULL;

  -- Chống một PerformedServiceId bị tính tiền 2 lần
  CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_Unique_PerformedService]
  ON [dbo].[InvoiceItems]([PerformedServiceId])
  WHERE [PerformedServiceId] IS NOT NULL;
  ```

#### 3. Chống xuất kho sai lô thuốc (`MedicineDispensationItems`):
- **Giải pháp:**
  Tạo Composite Unique Index trên `MedicineBatches([MedicineId], [MedicineBatchId])` và ràng buộc Composite Foreign Key:
  ```sql
  IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_MedicineBatches_Med_BatchId')
  BEGIN
      CREATE UNIQUE NONCLUSTERED INDEX [UQ_MedicineBatches_Med_BatchId]
      ON [dbo].[MedicineBatches]([MedicineId], [MedicineBatchId]);
  END;

  ALTER TABLE [dbo].[MedicineDispensationItems]
  ADD CONSTRAINT [FK_DispensationItems_Medicine_Batch_Composite]
  FOREIGN KEY ([MedicineId], [MedicineBatchId])
  REFERENCES [dbo].[MedicineBatches] ([MedicineId], [MedicineBatchId]);
  ```

#### 4. Khắc phục sai lệch Khoa - Dịch vụ và Phòng - Ghế trên `Appointments`:
- **Giải pháp:**
  - Đối với Phòng và Ghế: Bổ sung Composite Unique Index trên `DentalChairs([RoomId], [DentalChairId])` và Composite FK trên `Appointments([RoomId], [DentalChairId])`.
  - Đối với Khoa và Dịch vụ: Chuẩn hóa 3 bản ghi dữ liệu lịch sử cũ (ID 95, 97, 100), sau đó thiết lập Composite FK `Appointments([DepartmentId], [RequestedServiceId]) REFERENCES DepartmentServices([DepartmentId], [DepartmentServiceId])`.

---

### ƯU TIÊN 2 (P1): Thống Nhất Nguồn Dữ Liệu Chân Lý (Single Source of Truth)

#### 1. Hợp nhất `PerformedServices` vào `EpisodeServiceTasks`:
- Toàn bộ dịch vụ thực hiện tại ghế sẽ được tạo thành `EpisodeServiceTasks` gắn với cuộc hẹn lâm sàng.
- Không sinh dummy `TreatmentPlan` và `TreatmentPlanItem` nữa.
- Bảng `PerformedServices` được giữ lại ở chế độ Read-Only cho các báo cáo lịch sử.

#### 2. Đóng băng bảng ảnh cũ `ImagingRecords`:
- Dữ liệu 3 bản ghi cũ trong `ImagingRecords` được chuyển đổi sang bảng chuẩn `DiagnosticResults`.
- Các API upload ảnh mới (`MedicalRecordController`) được chuyển hướng lưu trữ và tra cứu sang `DiagnosticResults`.

#### 3. Chuẩn hóa `ImagingServiceConfigs`:
- Bỏ hai cột `ServiceName` và `Price` khỏi `ImagingServiceConfigs` để chấm dứt hiện tượng trôi lệch giá so với `DepartmentServices`.
- Code đọc giá và tên dịch vụ chụp phim trực tiếp qua quan hệ `DepartmentServiceId`.

---

### ƯU TIÊN 3 (P2): Tinh Gọn Bảng Dư Thừa & Tối Ưu Hóa Cấu Trúc Khóa

1. **Sáp nhập logic `DentistAvailability`:**
   - Chuyển toàn bộ các trường hợp bác sĩ xin nghỉ phép / bận việc về bảng chuẩn `DentistLeaveRequests`.
   - Bảng `DentistAvailability` (hiện có 0 dòng) được đánh dấu deprecate và thu hồi các endpoint không còn sử dụng.
2. **Chuẩn hóa quan hệ `Invoices.AppointmentId`:**
   - Cho phép `Invoices.AppointmentId` nhận giá trị `NULL` khi hóa đơn là hóa đơn tổng hợp của `CareEpisode`.
   - Xóa bỏ việc code phải gán ép `invoice.AppointmentId = episode.RootAppointmentId`.
