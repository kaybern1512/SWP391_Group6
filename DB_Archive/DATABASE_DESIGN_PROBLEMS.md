# DentalCare SWP391 – Database Design Problems & Architectural Audit

> **Tài liệu kiểm toán:** Báo cáo Phân tích Khiếm khuyết Thiết kế Cơ sở Dữ liệu & Xung đột Kiến trúc  
> **Repository:** `https://github.com/kaybern1512/SWP391_Group6.git`  
> **Căn cứ phân tích:** Toàn bộ tệp `DentalClinic_Schema_Full_Standalone.sql` (Commit `2fb8de7`), mã nguồn C# ASP.NET Core 8 Backend, và dữ liệu thực tế trên `DentalCare_Integration_Dev` / `DentalCare_StandaloneSchema_QA`.  
> **Mục tiêu:** Nhận diện toàn bộ các vấn đề thiết kế chồng chéo, dữ liệu dư thừa, lỗ hổng toàn vẹn tham chiếu do nhiều giai đoạn phát triển tích tụ, kèm bằng chứng thực thi cụ thể.

---

## MỤC LỤC
1. [P0 – Vấn Đề Thiết Kế Phân Hệ Viện Phí & Hóa Đơn (Billing Architecture)](#1-p0--vấn-đề-thiết-kế-phân-hệ-viện-phí--hóa-đơn-billing-architecture)
2. [P0 – Lỗ Hổng Toàn Vẹn Tham Chiếu Khóa Ngoại Tổ Hợp (Referential Consistency)](#2-p0--lỗ-hổng-toàn-vẹn-tham-chiếu-khóa-ngoại-tổ-hợp-referential-consistency)
3. [P1 – Dữ Liệu Trùng Lặp & Các Cặp Bảng Chồng Chéo Chức Năng (Duplicate & Redundant Tables)](#3-p1--dữ-liệu-trùng-lặp--các-cặp-bảng-chồng-chéo-chức-năng-duplicate--redundant-tables)
4. [P1 – Đánh Giá Định Danh Người Dùng & Hồ Sơ (User & Profile Identity)](#4-p1--đánh-giá-định-danh-người-dùng--hồ-sơ-user--profile-identity)
5. [P1 – Đánh Giá Kiến Trúc Quy Trình Lâm Sàng Mới (Workflow Schema Evaluation)](#5-p1--đánh-giá-kiến-trúc-quy-trình-lâm-sàng-mới-workflow-schema-evaluation)
6. [Bằng Chứng Tái Hiện Lỗ Hổng Tự Động (Empirical QA Reproduction)](#6-bằng-chứng-tái-hiện-lỗ-hổng-tự-động-empirical-qa-reproduction)

---

## 1. P0 – VẤN ĐỀ THIẾT KẾ PHÂN HỆ VIỆN PHÍ & HÓA ĐƠN (BILLING ARCHITECTURE)

### 1.1. Tại sao bảng `Invoices` vừa có `AppointmentId` vừa có `CareEpisodeId`?
- **Vị trí SQL:** `DentalClinic_Schema_Full_Standalone.sql` (Bảng `Invoices`, dòng 1278–1296).
  ```sql
  CREATE TABLE [dbo].[Invoices](
      [InvoiceId] [bigint] IDENTITY(1,1) NOT NULL,
      [InvoiceCode] [varchar](30) NOT NULL,
      [AppointmentId] [bigint] NOT NULL,          -- RÀNG BUỘC NOT NULL CŨ
      [GeneratedByUserId] [bigint] NOT NULL,
      [TotalAmount] [decimal](18, 2) NOT NULL,
      [Status] [varchar](30) NOT NULL,
      [CreatedAt] [datetime2](7) NOT NULL,
      [CareEpisodeId] [bigint] NULL,              -- BỔ SUNG Ở GIAI ĐOẠN SAU
      CONSTRAINT [PK_Invoices] PRIMARY KEY CLUSTERED ([InvoiceId] ASC),
      UNIQUE NONCLUSTERED ([AppointmentId] ASC)   -- 1 APPOINTMENT = 1 INVOICE
  );
  ```
- **Mã nguồn C# đọc/ghi:**
  - `BE/DentalClinic.Infrastructure/Services/BillingService.cs` (Dòng 146):
    `invoice.AppointmentId = appointment.AppointmentId;`
  - `BE/DentalClinic.Infrastructure/Services/CareEpisodeService.cs` (Dòng 1181–1182):
    ```csharp
    invoice = new Invoice
    {
        InvoiceCode = invoiceCode,
        CareEpisodeId = episode.CareEpisodeId,
        AppointmentId = episode.RootAppointmentId, // BUỘC PHẢI GÁN VÌ CỘT NOT NULL
        GeneratedByUserId = receptionistUserId,
        ...
    };
    ```
- **Nguyên nhân thiết kế chồng chéo:**
  - *Giai đoạn 1 (Single Appointment Billing):* Mỗi lần khám là một hóa đơn khép kín. `AppointmentId` được đặt là `NOT NULL` và có `UNIQUE NONCLUSTERED` constraint (ràng buộc 1-1 giữa Appointment và Invoice).
  - *Giai đoạn 2 (CareEpisode Billing):* Nghiệp vụ phát triển mô hình Đợt khám đa chuyên khoa (`CareEpisodes`), gom nhiều cuộc hẹn (`Appointments`) thành một đợt điều trị với 1 hóa đơn tổng duy nhất.
  - *Hậu quả kiến trúc:* Do không tái cấu trúc làm `AppointmentId` thành `NULL`, lập trình viên buộc phải gán `invoice.AppointmentId = episode.RootAppointmentId` để vượt qua ràng buộc `NOT NULL` cũ của database.
  - Điều này tạo ra **phụ thuộc kép (Dual Parent Dependency)**: Bảng `Invoices` vừa phụ thuộc vào một cuộc hẹn đơn lẻ, vừa phụ thuộc vào một đợt khám tổng thể. Nếu một đợt khám có 3 cuộc hẹn, hai cuộc hẹn sau không thể có hóa đơn độc lập nếu cần, đồng thời ngữ nghĩa của `AppointmentId` trên hóa đơn tổng bị méo mó (chỉ đại diện cho cuộc hẹn gốc chứ không đại diện cho toàn bộ hóa đơn).
- **Mức độ nghiêm trọng:** `HIGH`.
- **Cần sửa ngay không:** Cần chuẩn hóa trong kế hoạch tái cấu trúc (chuyển `AppointmentId` sang `NULL`, tạo quan hệ rõ ràng).

---

### 1.2. Database bảo đảm mỗi `CareEpisode` chỉ có một `Invoice` bằng cách nào?
- **Vị trí SQL:** `DentalClinic_Schema_Full_Standalone.sql` (Bảng `Invoices`).
- **Thực tế kiểm toán:** **DATABASE HOÀN TOÀN KHÔNG BẢO ĐẢM ĐIỀU NÀY!**
- **Bằng chứng:**
  - Trên cột `CareEpisodeId` của bảng `Invoices`, **KHÔNG CÓ UNIQUE CONSTRAINT**, cũng **KHÔNG CÓ FILTERED UNIQUE INDEX**!
  - Ràng buộc duy nhất là `FK_Invoices_CareEpisodes` (Khóa ngoại quan hệ 1-Nhiều thông thường).
  - Duy nhất chỉ có ứng dụng C# dùng khóa ứng dụng `SqlAppLockHelper.CareEpisodeInvoiceKey(careEpisodeId)` trong một phương thức `GenerateOrUpdateConsolidatedInvoiceAsync` (`CareEpisodeService.cs`, dòng 1103) để khóa xử lý tại tầng code.
  - Tuy nhiên, nếu gọi API `BillingController.CreateInvoice` cho một cuộc hẹn con trong đợt khám, hoặc có script chèn SQL trực tiếp: **DATABASE CHO PHÉP TẠO KHÔNG GIỚI HẠN SỐ LƯỢNG HÓA ĐƠN CHO CÙNG MỘT CARE EPISODE!**
  - Thực nghiệm kiểm thử tự động tại `scratch/reproduce_db_design_flaws_clean.js` (Test 1) đã chứng minh: SQL Server chèn thành công cùng lúc `Invoice 475` và `Invoice 476` trỏ vào cùng `CareEpisodeId = 9` mà không gặp bất kỳ lỗi ràng buộc nào.
- **Mức độ nghiêm trọng:** `CRITICAL` (Lỗ hổng kiểm soát dòng tiền và gian lận/sai lệch doanh thu).
- **Phương án Before → After:**
  - *Before:* `[CareEpisodeId] BIGINT NULL` không có index duy nhất.
  - *After:* Bổ sung Filtered Unique Index:
    ```sql
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Invoices_CareEpisodeId]
    ON [dbo].[Invoices]([CareEpisodeId])
    WHERE [CareEpisodeId] IS NOT NULL;
    ```
- **Cần sửa ngay không:** **SỬA NGAY Ở PHASE 1**.

---

### 1.3. Tại sao `InvoiceItems` vừa có `PerformedServiceId`, `ServiceTaskId`, `SourceType`, `SourceId`?
- **Vị trí SQL:** `DentalClinic_Schema_Full_Standalone.sql` (Bảng `InvoiceItems`, dòng 1298–1320).
  ```sql
  CREATE TABLE [dbo].[InvoiceItems](
      [InvoiceItemId] [bigint] IDENTITY(1,1) NOT NULL,
      [InvoiceId] [bigint] NOT NULL,
      [PerformedServiceId] [bigint] NULL,       -- KHÓA NGOẠI GIAI ĐOẠN 1
      [Description] [nvarchar](250) NOT NULL,
      [Quantity] [int] NOT NULL,
      [UnitPrice] [decimal](18, 2) NOT NULL,
      [Amount] AS ([Quantity]*[UnitPrice]) PERSISTED,
      [AppointmentId] [bigint] NULL,
      [PerformingDentistUserId] [bigint] NULL,
      [ServiceTaskId] [bigint] NULL,            -- KHÓA NGOẠI GIAI ĐOẠN 2
      [SourceType] [varchar](50) NULL,          -- ĐA HÌNH GIAI ĐOẠN 3
      [SourceId] [bigint] NULL                  -- ĐA HÌNH GIAI ĐOẠN 3
  );
  ```
- **Mã nguồn C# đọc/ghi:**
  - `BillingService.cs` (Dòng 155–161):
    Gán `PerformedServiceId = item.PerformedServiceId`, để `ServiceTaskId`, `SourceType`, `SourceId` hoàn toàn bằng `NULL`.
  - `CareEpisodeService.cs` (Dòng 1194–1205):
    Gán `ServiceTaskId = item.ServiceTaskId`, `SourceType = "ServiceTask"`, `SourceId = item.ServiceTaskId`.
  - Dữ liệu thực tế (`DentalCare_Integration_Dev`):
    Trong 593 dòng `InvoiceItems` hiện hữu:
    - 84 dòng có `PerformedServiceId` (nhưng `SourceType` = NULL).
    - 0 dòng có `ServiceTaskId`.
    - 509 dòng để `NULL` toàn bộ cả 3 cơ chế, chỉ lưu chuỗi text `Description` (như tiền thuốc, tiền chụp X-quang, tiền khám)!
- **Nguyên nhân kiến trúc:**
  - Bảng `InvoiceItems` đang là "chiếc túi hỗn hợp" chứa 3 thế hệ thiết kế:
    1. *Thế hệ 1 (Legacy FK):* Nối cứng vào `PerformedServices`.
    2. *Thế hệ 2 (CareEpisode FK):* Nối cứng vào `EpisodeServiceTasks`.
    3. *Thế hệ 3 (Polymorphic Pattern):* Cố gắng gom tất cả các loại dịch vụ (chẩn đoán hình ảnh, đơn thuốc, thuế, task điều trị) thành cặp `(SourceType, SourceId)` nhưng không có FK tầng DB để bảo vệ tính toàn vẹn.
- **Mức độ nghiêm trọng:** `HIGH`.
- **Phương án Before → After:**
  - *Before:* 4 cột rời rạc không nhất quán.
  - *After:* Chuẩn hóa `InvoiceItems` theo kiến trúc Single Source of Truth: Dùng `SourceType` và `SourceId` có Check Constraint hợp lệ (`'ServiceTask'`, `'DiagnosticOrder'`, `'Prescription'`, `'Consultation'`, `'Tax'`), hoặc phân rã rõ ràng bảng chi tiết viện phí.

---

### 1.4. Một dịch vụ có thể bị tính phí hai lần qua các nguồn khác nhau không (Double Billing)?
- **CÂU TRẢ LỜI: CÓ, RỦI RO DOUBLE BILLING LÀ CÓ THẬT VÀ ĐÃ ĐƯỢC TÁI HIỆN THÀNH CÔNG!**
- **Bằng chứng thực nghiệm:**
  - *Trường hợp A (Tính trùng trong cùng 1 hóa đơn qua PerformedService):*
    Do index `UQ_InvoiceItems_Invoice_Source` có bộ lọc `WHERE SourceType IS NOT NULL AND SourceId IS NOT NULL`, khi Bác sĩ ghi nhận dịch vụ qua `BillingService` (nơi `SourceType` = NULL), database cho phép chèn 2 dòng `InvoiceItems` có cùng `PerformedServiceId` vào cùng một hóa đơn (Test 2a trong script tái hiện, `InvoiceItem 631` và `632` đều thanh toán cho `PerformedServiceId = 1`).
  - *Trường hợp B (Tính trùng một ServiceTask trên 2 hóa đơn khác nhau):*
    Do index `UQ_InvoiceItems_Invoice_Source` bao gồm cả `InvoiceId` trong khóa duy nhất (`(InvoiceId, SourceType, SourceId)`), database **cho phép chèn cùng một `ServiceTaskId` vào 2 hóa đơn khác nhau** (Test 2b trong script tái hiện, `ServiceTaskId = 1` bị tính tiền đồng thời trên `Invoice 478` và `Invoice 479`).
  - *Trường hợp C (Chồng chéo giữa 2 luồng khám):*
    Nếu một thủ thuật vừa được lưu vào `PerformedServices` ở màn hình điều trị tại ghế, vừa được tạo thành `EpisodeServiceTask` trong đợt khám, hai phân hệ `BillingService` và `CareEpisodeService` sẽ tính tiền độc lập cho cùng một thủ thuật.
- **Mức độ nghiêm trọng:** `CRITICAL` (Sai lệch tài chính trực tiếp của khách hàng).
- **Cần sửa ngay không:** **SỬA NGAY Ở PHASE 1**. Cần tạo Unique Filtered Index trên toàn cục: Mỗi `ServiceTaskId` và mỗi `PerformedServiceId` chỉ được xuất hiện tối đa 1 lần trên các hóa đơn có hiệu lực (`Status <> 'Cancelled'`).

---

### 1.5. Bảng `PerformedServices` và `EpisodeServiceTasks` có bị chồng chức năng không?
- **Kết luận:** **`REAL DUPLICATION / ARCHITECTURAL OVERLAP` (Chồng chéo chức năng thực tế nghiêm trọng).**
- **Đối chiếu DDL:**
  | Tiêu chí | `PerformedServices` (Model cũ) | `EpisodeServiceTasks` (Model mới) |
  | :--- | :--- | :--- |
  | **Thực thể gắn liền** | `TreatmentSessions` (thông qua dummy `TreatmentPlanItems`) | `CareEpisodes` và `Appointments` trực tiếp |
  | **Dịch vụ nha khoa** | `DepartmentServiceId` | `DepartmentServiceId` |
  | **Bác sĩ thực hiện** | Lấy gián tiếp qua `TreatmentSessions.DentistUserId` | `PerformingDentistUserId` trực tiếp |
  | **Số lượng & Đơn giá** | `Quantity`, `UnitPrice`, `Amount` (Computed) | `Quantity`, `UnitPriceSnapshot`, `EstimatedDurationMinutes` |
  | **Trạng thái thực thi** | Không có (mặc định đã làm xong) | `Planned`, `InProgress`, `Completed`, `Cancelled` |
  | **Thời gian bắt đầu/kết thúc** | Chỉ có `SessionDate` | `PlannedAtUtc`, `StartedAtUtc`, `CompletedAtUtc` |
- **Code phải tạo dữ liệu giả để phục vụ PerformedServices:**
  Tại `TreatmentService.cs` (Dòng 1134–1184), khi Bác sĩ ghi nhận dịch vụ thực hiện tại ghế: Vì bảng `PerformedServices` bắt buộc phải có `TreatmentSessionId`, mà `TreatmentSession` lại bắt buộc phải có `TreatmentPlanItemId` và `TreatmentPlanId`:
  Code buộc phải tự động chèn một `TreatmentPlan` "giả" với `Status = 'Completed'` và một `TreatmentPlanItem` "giả" với ghi chú `directCareTag`!
- **Hệ quả:** Làm ô nhiễm bảng Kế hoạch điều trị (`TreatmentPlans`), sinh ra hàng trăm kế hoạch giả chỉ để lưu một dịch vụ nhổ răng hay lấy cao răng đơn giản.
- **Mức độ nghiêm trọng:** `HIGH`.

---

### 1.6. Có thể thống nhất nguồn ghi nhận dịch vụ thực tế mà không mất dữ liệu lịch sử không?
- **CÂU TRẢ LỜI: HOÀN TOÀN KHẢ THI THEO QUY TRÌNH 3 BƯỚC AN TOÀN:**
  1. **Bước 1 (Giữ nguyên cấu trúc hiện hành & Thêm Adapter):** Lấy `EpisodeServiceTasks` làm **Single Source of Truth** cho toàn bộ dịch vụ thực hiện trên lâm sàng.
  2. **Bước 2 (Backfill dữ liệu lịch sử):** Viết script chuyển đổi toàn bộ 181 bản ghi từ `PerformedServices` sang `EpisodeServiceTasks`:
     Mỗi dòng `PerformedServices` -> tạo 1 dòng `EpisodeServiceTasks` với trạng thái `'Completed'`, nối với `AppointmentId` của `TreatmentSession`, thời gian hoàn thành lấy từ `SessionDate`.
  3. **Bước 3 (Thống nhất luồng ghi nhận):** Cập nhật `TreatmentService.AddPerformedServiceToAppointmentAsync` để ghi trực tiếp vào `EpisodeServiceTasks`, loại bỏ hoàn toàn việc sinh dummy `TreatmentPlan` và `TreatmentPlanItem`.
  4. **Bước 4 (Phần mềm tương thích ngược):** Bảng `PerformedServices` được giữ ở chế độ Read-Only để không làm gãy các báo cáo cũ, không thực hiện `DROP` vật lý.

---

## 2. P0 – LỖ HỔNG TOÀN VẸN THAM CHIẾU KHÓA NGOẠI TỔ HỢP (REFERENTIAL CONSISTENCY)

Kiểm toán phát hiện một loạt các quan hệ khóa ngoại đơn lẻ đều hợp lệ (Individual FKs Valid) nhưng khi kết hợp lại thì quan hệ tổng thể sai logic nghiệp vụ (Composite Relationship Invalid):

### 2.1. `Appointments.DepartmentId` + `RequestedServiceId`
- **Vấn đề:** Bảng `Appointments` có 2 khóa ngoại độc lập:
  - `FK_Appointments_Departments: Appointments.DepartmentId -> Departments.DepartmentId`
  - `FK_Appointments_RequestedServices: Appointments.RequestedServiceId -> DepartmentServices.DepartmentServiceId`
- **Lỗ hổng:** Database cho phép đặt lịch hẹn tại Khoa A nhưng dịch vụ yêu cầu lại thuộc Khoa B.
- **Bằng chứng dữ liệu thực tế (`DentalCare_Integration_Dev`):**
  Có **3 cuộc hẹn thực tế** trong lịch sử đang vi phạm quy tắc này:
  - `AppointmentId 95` (`APP-20261015-DONE1`): `DepartmentId = 3` (Phẫu Thuật Miệng & Implant), nhưng `RequestedServiceId = 11` (Điều trị tủy răng hàm - thuộc Khoa 1: Răng Tổng Quát & Nội Nha).
  - `AppointmentId 97` (`APP-20261015-PEND1`): `DepartmentId = 2` (Chỉnh Nha & Thẩm Mỹ), nhưng `RequestedServiceId = 7` (Khám tư vấn Implant - thuộc Khoa 3).
  - `AppointmentId 100` (`APP-20261015-PK4`): `DepartmentId = 4` (Răng Trẻ Em), nhưng `RequestedServiceId = 18` (Bọc răng sứ Cercon - thuộc Khoa 2).
- **Thực nghiệm QA:** Test 3 trong script tái hiện chèn thành công cuộc hẹn `APP-REPRO-MISMATCH` với Khoa 1 nhưng dịch vụ số 18 (Khoa 2).
- **Giải pháp đề xuất:** Bổ sung ràng buộc toàn vẹn Composite FK hoặc Validation Guard. Do dữ liệu cũ đang có 3 dòng vi phạm, cần chuẩn hóa 3 dòng dữ liệu này trước khi áp dụng constraint.

---

### 2.2. `Appointments.RoomId` + `DentalChairId`
- **Vấn đề:** Bảng `DentalChairs` đã có sẵn cột `RoomId` để xác định ghế nha khoa nằm trong phòng nào. Bảng `Appointments` lại lưu cả `RoomId` và `DentalChairId`.
- **Lỗ hổng:** Hai khóa ngoại độc lập cho phép Bệnh nhân được xếp vào Phòng 101 nhưng Ghế lại thuộc Phòng 201.
- **Thực nghiệm QA:** Test 4 trong script tái hiện chèn thành công cuộc hẹn `APP-REPRO-ROOMCHAIR` với Phòng 4 nhưng Ghế 1 (vốn thuộc Phòng 1).
- **Giải pháp đề xuất:**
  - Ngắn hạn: Bổ sung Composite Foreign Key:
    `FOREIGN KEY (RoomId, DentalChairId) REFERENCES DentalChairs(RoomId, DentalChairId)`.
  - Dài hạn: Bỏ cột `Appointments.RoomId` vì `DentalChair.RoomId` đã xác định duy nhất phòng khám (chuẩn hóa 3NF).

---

### 2.3. `CareEpisodes.PatientUserId` + `RootAppointmentId`
- **Vấn đề:** Bảng `CareEpisodes` lưu `PatientUserId` và `RootAppointmentId`. Nhưng bản thân bảng `Appointments` đã có `PatientUserId`.
- **Lỗ hổng:** Database cho phép tạo Đợt khám cho Bệnh nhân A nhưng Cuộc hẹn gốc lại thuộc về Bệnh nhân B.
- **Thực nghiệm QA:** Test 5 trong script tái hiện chèn thành công `CareEpisode 10` cho Bệnh nhân 7 nhưng trỏ vào Cuộc hẹn gốc của Bệnh nhân 5.
- **Mức độ nghiêm trọng:** `HIGH` (Nguy cơ lộ thông tin bệnh án giữa các bệnh nhân).
- **Giải pháp đề xuất:** Thêm Check/Trigger validation hoặc Composite Foreign Key đảm bảo `CareEpisodes.PatientUserId == Appointments.PatientUserId`.

---

### 2.4. `DiagnosticOrders` + `ImagingSessions` + `DiagnosticResults`
- **Vấn đề:**
  - `DiagnosticOrders` lưu `AppointmentId` và `PatientUserId`.
  - `ImagingSessions` lưu `DiagnosticOrderId`, `SourceAppointmentId`, và `PatientUserId`.
  - `DiagnosticResults` lưu `DiagnosticOrderId` và `ImagingSessionId`.
- **Lỗ hổng:** Dư thừa dữ liệu dẫn đến nguy cơ bất nhất: `ImagingSessions` có thể trỏ vào `DiagnosticOrder` của Bệnh nhân A nhưng lại ghi `PatientUserId` của Bệnh nhân B; hoặc `DiagnosticResults` trỏ vào `DiagnosticOrder 1` nhưng lại gắn với `ImagingSession` của `DiagnosticOrder 2`.
- **Giải pháp đề xuất:** Loại bỏ các cột denormalized dư thừa trong `ImagingSessions` (`SourceAppointmentId`, `PatientUserId` có thể lấy trực tiếp qua `DiagnosticOrderId`).

---

### 2.5. `MedicineDispensationItems.MedicineId` + `MedicineBatchId`
- **Vấn đề:** Bảng `MedicineBatches` đã có `MedicineId` để xác định lô thuốc thuộc về loại thuốc nào. Bảng `MedicineDispensationItems` lại lưu cả `MedicineId` và `MedicineBatchId`.
- **Lỗ hổng:** Cho phép xuất kho một mặt hàng thuốc A nhưng lại trừ tồn kho vào Lô của thuốc B.
- **Thực nghiệm QA:** Test 6 trong script tái hiện chèn thành công dòng xuất kho với `MedicineId = 2` nhưng `MedicineBatchId = 2` (vốn là lô của Thuốc 1).
- **Mức độ nghiêm trọng:** `CRITICAL` (Sai lệch quản lý dược phẩm và an toàn dùng thuốc của người bệnh).
- **Giải pháp đề xuất:**
  - Do `MedicineBatches` đã có unique constraint `UQ_MedicineBatches_Medicine_Batch([MedicineId], [BatchNumber])`, cần bổ sung Composite Unique Index trên `MedicineBatches([MedicineId], [MedicineBatchId])` và áp đặt Composite Foreign Key trên `MedicineDispensationItems`:
    ```sql
    ALTER TABLE [dbo].[MedicineDispensationItems]
    ADD CONSTRAINT [FK_DispensationItem_Batch_Medicine]
    FOREIGN KEY ([MedicineId], [MedicineBatchId])
    REFERENCES [dbo].[MedicineBatches] ([MedicineId], [MedicineBatchId]);
    ```

---

## 3. P1 – DỮ LIỆU TRÙNG LẶP & CÁC CẶP BẢNG CHỒNG CHÉO CHỨC NĂNG

| Cặp Bảng Đối Chiếu | Kết Luận Phân Loại | Phân Tích Chi Tiết & Bằng Chứng | Đề Xuất Xử Lý |
| :--- | :---: | :--- | :--- |
| **`ImagingRecords` vs `DiagnosticResults`** | `LEGACY CANDIDATE` (ImagingRecords) / `KEEP` (DiagnosticResults) | `ImagingRecords` chỉ có 3 dòng dữ liệu cũ từ tháng 10, gắn trực tiếp vào `ClinicalEntries` mà không có quản lý phòng/máy. Toàn bộ quy trình chẩn đoán hình ảnh hiện đại đã chuyển sang `DiagnosticOrders` -> `ImagingSessions` -> `DiagnosticResults` (425 orders, 355 sessions, 7 results). | Đóng băng `ImagingRecords`, chuyển dữ liệu cũ sang `DiagnosticResults` và ngưng sử dụng ở phiên bản tiếp theo. |
| **`ImagingServiceConfigs` vs `DepartmentServices`** | `MODIFY CANDIDATE` | `ImagingServiceConfigs` lưu trùng lặp cột `ServiceName` và `Price` với `DepartmentServices`. Khi Admin cập nhật giá dịch vụ trong danh mục chính, giá trong cấu hình X-quang bị lệch trôi (drift). | Giữ lại `ImagingServiceConfigs` để quản lý thời lượng ca chụp (`SlotDurationMinutes`, `BufferMinutes`), nhưng bỏ `ServiceName` và `Price`, tham chiếu trực tiếp qua `DepartmentServiceId`. |
| **`PerformedServices` vs `EpisodeServiceTasks`** | `MERGE CANDIDATE` (PerformedServices) / `KEEP` (EpisodeServiceTasks) | Cả 2 bảng cùng lưu thủ thuật nha khoa thực hiện tại ghế. `PerformedServices` sinh dummy treatment plan gây rác DB. `EpisodeServiceTasks` quản lý đầy đủ trạng thái, thời gian, bác sĩ. | Thống nhất dùng `EpisodeServiceTasks` làm Single Source of Truth. Backfill dữ liệu cũ và chuyển `PerformedServices` sang chế độ tương thích ngược. |
| **`ServiceCombos.IncludedServices` vs `DepartmentServices`** | `DIFFERENT RESPONSIBILITY` | `DepartmentServices` là danh mục dịch vụ thanh toán chính thức. `ServiceCombos.IncludedServices` là mảng JSON chuỗi text thuần túy phục vụ hiển thị marketing cho Bệnh nhân. | Giữ nguyên vì phục vụ mục đích truyền thông, không xung đột bảng quan hệ. Nếu muốn tự động hóa tạo task cho combo trong tương lai, mới cần tạo bảng nối `ServiceComboItems`. |
| **`DentistAvailability` vs `DentistWorkSchedules` & `DentistLeaveRequests`** | `MODIFY CANDIDATE` / `MERGE CANDIDATE` | `DentistAvailability` có **0 dòng dữ liệu** trên database thực tế. Phân hệ lịch trực dùng `DentistWorkSchedules` (574 dòng), còn nghỉ phép dùng `DentistLeaveRequests`. Việc duy trì thêm `DentistAvailability` gây kiểm tra kép phân tán trong code. | Hợp nhất việc chặn lịch bác sĩ bận/nghỉ vào `DentistLeaveRequests` và `DentistWorkSchedules`, dọn dẹp logic kiểm tra dư thừa. |

---

## 4. P1 – ĐÁNH GIÁ ĐỊNH DANH NGƯỜI DÙNG & HỒ SƠ (USER & PROFILE IDENTITY)

Kiểm toán 4 bảng: `UserAccounts`, `PatientProfiles`, `StaffProfiles`, `DentistProfiles`:

1. **Khóa chính Khóa ngoại 1-1 Chuẩn mực:**
   - Cả 3 bảng hồ sơ (`PatientProfiles`, `StaffProfiles`, `DentistProfiles`) đều sử dụng chính cột `UserId` làm Khóa chính (Primary Key Clustered), đồng thời là Khóa ngoại trỏ về `UserAccounts(UserId)`.
   - **Xác nhận:** Hệ thống **KHÔNG sinh ra các surrogate ID dư thừa** như `PatientProfileId`, `StaffProfileId`, hay `DentistProfileId`. Đây là thiết kế Shared Primary Key 1-1 chuẩn mực của hệ quản trị cơ sở dữ liệu quan hệ.
2. **Mã nghiệp vụ tự nhiên (Natural Business Identifiers):**
   - `PatientProfiles.PatientCode`: Mã số bệnh nhân phục vụ in thẻ khám, hồ sơ giấy (`UQ_PatientProfiles_PatientCode`).
   - `StaffProfiles.EmployeeCode`: Mã nhân viên phục vụ chấm công, thẻ nhân viên (`UQ_StaffProfiles_EmployeeCode`).
   - `DentistProfiles.LicenseNumber`: Số chứng chỉ hành nghề y tế của Bác sĩ theo quy định Bộ Y tế.
   - **Xác nhận:** Toàn bộ các mã trên là định danh nghiệp vụ hợp lệ, bắt buộc phải có trong quản lý phòng khám, **tuyệt đối KHÔNG được loại bỏ**.
3. **Phân tách thông tin Bác sĩ:**
   - Bác sĩ vừa có thông tin nhân sự hành chính (`StaffProfiles`: `FullName`, `EmployeeCode`, `EmploymentStatus`), vừa có thông tin chuyên môn lâm sàng (`DentistProfiles`: `LicenseNumber`, `Qualification`, `HasRadiationSafetyCert`).
   - Thiết kế này hoàn toàn hợp lý, cho phép Bác sĩ dùng chung hạ tầng nhân sự và chấm công với các nhân viên khác.

---

## 5. P1 – ĐÁNH GIÁ KIẾN TRÚC QUY TRÌNH LÂM SÀNG MỚI (WORKFLOW SCHEMA EVALUATION)

Kiểm toán các bảng trên hai nhánh tính năng đang hoàn thiện:
- `ServiceWorkflows` & `ServiceWorkflowSteps` (Nhánh `origin/feature/admin-service-workflow-configuration` - Commit `2ca7764`)
- `EpisodeServiceTaskStepProgresses` (Nhánh `origin/feature/service-specific-task-workflows` - Commit `88bdc9a`)

### Đánh giá quan hệ với các bảng hiện hữu:
1. **Bản chất của các bảng Workflow mới:**
   - `ServiceWorkflows` và `ServiceWorkflowSteps`: Là **Bảng Mẫu / Định nghĩa Quy trình Chuẩn (SOP Template)** cho từng dịch vụ nha khoa (ví dụ: Quy trình Nhổ răng khôn gồm 4 bước: 1. Thăm khám/Gây tê, 2. Rạch lợi/Mở xương, 3. Chia chân răng/Gắp răng, 4. Bơm rửa/Khâu vết thương).
   - `EpisodeServiceTaskStepProgresses`: Là **Bảng Thực thi Tiến độ (Runtime Execution Log)** ghi nhận Bác sĩ đã tích hoàn thành bước nào trong buổi khám cho một `ServiceTaskId` cụ thể.
2. **Nguy cơ chồng chéo ghi nhận lâm sàng (Clinical Redundancy Risk):**
   - Hiện tại hệ thống có tới 3 nơi để ghi nhận kết quả/ghi chú thủ thuật của Bác sĩ:
     - Nơi 1: `ClinicalEntries.TreatmentNotes` (thuộc Bệnh án `DentalMedicalRecords`).
     - Nơi 2: `TreatmentSessions.ClinicalNote` / `TreatmentResult`.
     - Nơi 3: `EpisodeServiceTaskStepProgresses.Notes` / `ResultValue`.
   - **Khuyến nghị kiến trúc:**
     - `EpisodeServiceTaskStepProgresses` là sự bổ sung rất giá trị cho việc chuẩn hóa quy trình điều trị nha khoa kỹ thuật số.
     - TUY NHIÊN, cần kết nối `EpisodeServiceTasks` chặt chẽ với `ClinicalEntries` thông qua cột `ClinicalRecordId` sẵn có. Tránh bắt Bác sĩ phải nhập ghi chú điều trị 3 lần ở 3 màn hình khác nhau. Kết quả của các bước trong `EpisodeServiceTaskStepProgresses` nên được tự động tổng hợp (aggregate) thành bản tóm tắt điều trị trong `ClinicalEntries`.

---

## 6. BẰNG CHỨNG TÁI HIỆN LỖ HỔNG TỰ ĐỘNG (EMPIRICAL QA REPRODUCTION)

Tất cả các lỗ hổng thiết kế nêu trên đã được kiểm chứng và tái hiện thành công thông qua kịch bản kiểm thử tự động tại `scratch/reproduce_db_design_flaws_clean.js` chạy trên cơ sở dữ liệu `DentalCare_StandaloneSchema_QA`:

```text
========================================================================
REPRODUCING DATABASE DESIGN FLAWS ON DentalCare_StandaloneSchema_QA
========================================================================

--- Test 1: Creating Multiple Invoices for Single CareEpisode ---
RESULT: [VULNERABILITY CONFIRMED] DB ALLOWED 2 INVOICES FOR 1 CARE EPISODE!
InvoiceId            InvoiceCode                    AppointmentId        CareEpisodeId        TotalAmount         
-------------------- ------------------------------ -------------------- -------------------- --------------------
                 475 INV-TEST-001                                    110                    9            500000.00
                 476 INV-TEST-002                                      2                    9            750000.00

--- Test 2a: Adding Duplicate PerformedServices to the Same Invoice ---
RESULT: [VULNERABILITY CONFIRMED] DB ALLOWED DUPLICATE BILLING OF SAME PERFORMED SERVICE!
InvoiceItemId        InvoiceId            Description                                  UnitPrice            PerformedServiceId   SourceType                                        
-------------------- -------------------- -------------------------------------------- -------------------- -------------------- --------------------------------------------------
                 631                  477 Hàn răng Composite (Lần 1)                               500000.00                    1 NULL                                              
                 632                  477 Hàn răng Composite (Lần 2 - Bị tính trùng!)              500000.00                    1 NULL                                              

--- Test 2b: Billing the SAME ServiceTaskId Across Two Different Invoices ---
RESULT: [VULNERABILITY CONFIRMED] DB ALLOWED SAME SERVICETASK TO BE BILLED ON TWO SEPARATE INVOICES!
InvoiceItemId        InvoiceId            Description                                  UnitPrice            ServiceTaskId        SourceType          SourceId            
-------------------- -------------------- -------------------------------------------- -------------------- -------------------- ------------------- --------------------
                 633                  478 Dịch vụ nhổ răng khôn                                    500000.00                    1 ServiceTask                            1
                 634                  479 Dịch vụ nhổ răng khôn (Bị tính lại!)                     500000.00                    1 ServiceTask                            1

--- Test 3: Appointment Department Mismatched with Service Department ---
RESULT: [VULNERABILITY CONFIRMED] DB ALLOWED CROSS-DEPARTMENT SERVICE ASSIGNMENT!
AppointmentId        AppointmentCode                ApptDept             SvcDept              ServiceName                                                                 
-------------------- ------------------------------ -------------------- -------------------- ----------------------------------------------------------------------------
                2813 APP-REPRO-MISMATCH                                1                    2 Bọc răng toàn sứ Cercon HT chính hãng

--- Test 4: Appointment Room Mismatched with Chair Room ---
RESULT: [VULNERABILITY CONFIRMED] DB ALLOWED CHAIR TO BE BOOKED IN A DIFFERENT ROOM!
AppointmentId        AppointmentCode                ApptRoom             ChairRoom            DentalChairId       
-------------------- ------------------------------ -------------------- -------------------- --------------------
                2814 APP-REPRO-ROOMCHAIR                               4                    1                    1

--- Test 5: CareEpisode Patient Mismatched with RootAppointment Patient ---
RESULT: [VULNERABILITY CONFIRMED] DB ALLOWED CARE EPISODE PATIENT MISMATCH WITH ROOT APPOINTMENT!
CareEpisodeId        EpisodeCode                    EpisodePatient       ApptPatient         
-------------------- ------------------------------ -------------------- --------------------
                  10 EP-REPRO-PATIENT-MISMATCH                         7                    5

--- Test 6: MedicineDispensationItem Medicine Mismatched with Batch Medicine ---
RESULT: [VULNERABILITY CONFIRMED] DB ALLOWED DISPENSATION ITEM TO DISPENSE BATCH OF DIFFERENT MEDICINE!
DispensationItemId   ItemMed              BatchActualMed       MedicineBatchId     
-------------------- -------------------- -------------------- --------------------
                 311                    2                    1                    2
```
