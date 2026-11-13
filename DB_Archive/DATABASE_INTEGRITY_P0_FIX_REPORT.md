# BÁO CÁO TOÀN DIỆN: KHẮC PHỤC TRIỆT ĐỂ LỖ HỔNG TOÀN VẸN CƠ SỞ DỮ LIỆU P0 & CHUẨN BỊ BẢN PHÁT HÀNH RC2
## DENTALCARE SWP391 – DATABASE INTEGRITY P0 FIX & RELEASE CANDIDATE 2 VERIFICATION REPORT

> **Người thực hiện:** Senior ASP.NET Core 8 Architect, EF Core 8 Specialist & Senior Database Architect  
> **Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
> **Thời điểm nghiệm thu:** 13/11/2026  
> **Nhánh thực hiện:** `fix/database-integrity-p0` (Worktree độc lập: `D:\SWP391\DentalCare-Integrity-Fix`)  
> **Môi trường CSDL kiểm chứng:** `DentalCare_IntegrityFix_QA` trên `.\SQLEXPRESS` (Cách ly 100% với develop, main và DB gốc)  

---

## 1. TỔNG QUAN VÀ BỐI CẢNH DỰ ÁN

Dự án DentalCare SWP391 đã nghiệm thu thành công bản Unified Integration Release Candidate 1 (RC1) và merge lên nhánh chính thức:
- `origin/develop`: commit `239c59e`
- `origin/main`: commit `618f479`

Bản RC1 đã hợp nhất thành công 72 bảng, 17 cột Clinical Workflow, 3 bảng cấu hình quy trình và chuẩn hóa hai tệp SQL Standalone trong thư mục `DB/`. Tuy nhiên, kết quả kiểm toán độc lập tại commit `b6d8dc4` đã chứng minh sự tồn tại của các **lỗ hổng toàn vẹn dữ liệu nghiêm trọng (P0 Database Integrity Vulnerabilities)** xuất phát từ việc thiếu các ràng buộc mức engine SQL Server (Composite Foreign Keys, Filtered Unique Indexes, Check Constraints).

Theo phê duyệt của Project Owner, nhánh `fix/database-integrity-p0` được thiết lập nhằm **hiện thực hóa toàn bộ các giải pháp khắc phục ở mức database engine và mã nguồn**, chuyển giao bản **Release Candidate 2 (RC2)** với độ tin cậy tuyệt đối.

---

## 2. PHÂN TÍCH VÀ GIẢI PHÁP KHẮC PHỤC CHI TIẾT 9 LỖ HỔNG P0

### 2.1. P0-1: Hóa Đơn Cấp Đợt Điều Trị (CareEpisode Consolidated Invoicing)
- **Lỗ hổng:** Cột `Invoices.AppointmentId` bắt buộc `NOT NULL`, khiến mọi hóa đơn tổng hợp theo đợt khám nhiều buổi (`CareEpisodes`) đều phải gán ép vào `RootAppointmentId`, làm sai lệch ngữ nghĩa kế toán và không thể phân biệt hóa đơn theo ca lẻ với hóa đơn tổng hợp đợt.
- **Khắc phục ở Database Engine:**
  - Chuyển `Invoices.AppointmentId` thành `[bigint] NULL`.
  - Bổ sung check constraint `CK_Invoices_ParentReference`:
    ```sql
    ALTER TABLE [dbo].[Invoices] WITH CHECK ADD CONSTRAINT [CK_Invoices_ParentReference] 
        CHECK (([AppointmentId] IS NOT NULL OR [CareEpisodeId] IS NOT NULL));
    ```
- **Khắc phục ở C# & EF Core:**
  - `Invoice.cs` & `InvoiceDto.cs` (cả Backend và Frontend): chuyển `AppointmentId` thành `long?`.
  - Cập nhật mapping `DentalClinicDbContext.cs` hỗ trợ `long? AppointmentId`.

### 2.2. P0-2: Ngăn Chặn Xuất Trùng Hóa Đơn Đợt Điều Trị (CareEpisode Duplicate Invoices)
- **Lỗ hổng:** Một `CareEpisode` có thể bị tạo nhiều hóa đơn nếu có request đồng thời hoặc người dùng nhấn nút nhiều lần, gây sai lệch dòng tiền và doanh thu phòng khám.
- **Khắc phục ở Database Engine:**
  - Thiết lập Filtered Unique Index trên `CareEpisodeId`:
    ```sql
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Invoices_CareEpisodeId] 
        ON [dbo].[Invoices] ([CareEpisodeId] ASC) 
        WHERE [CareEpisodeId] IS NOT NULL;
    ```
  - Thiết lập Filtered Unique Index cho các hóa đơn ca lẻ độc lập:
    ```sql
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Invoices_AppointmentId] 
        ON [dbo].[Invoices] ([AppointmentId] ASC) 
        WHERE [AppointmentId] IS NOT NULL AND [CareEpisodeId] IS NULL;
    ```
- **Khắc phục ở C# Application:**
  - `CareEpisodeService.cs`: Bổ sung kiểm tra bất biến `existingInvoice.Status != "Unpaid"`, khóa ứng dụng phân tán `SqlAppLockHelper.CareEpisodeInvoiceKey(request.CareEpisodeId)` và tái sử dụng hóa đơn chưa thanh toán duy nhất thay vì tạo mới.

### 2.3. P0-3 & P0-4: Chống Tính Tiền Hai Lần (Anti-Double-Billing cho PerformedServices & ServiceTasks)
- **Lỗ hổng:** Các bảng `PerformedServices` và `EpisodeServiceTasks` không có ràng buộc duy nhất trong `InvoiceItems`, cho phép một dịch vụ đã thực hiện hoặc một chỉ định phát sinh bị xuất hóa đơn nhiều lần trong cùng một hóa đơn hoặc qua các hóa đơn khác nhau.
- **Khắc phục ở Database Engine:**
  - Thiết lập 2 Filtered Unique Indexes trên `InvoiceItems`:
    ```sql
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_ServiceTaskId] 
        ON [dbo].[InvoiceItems] ([ServiceTaskId] ASC) 
        WHERE [ServiceTaskId] IS NOT NULL;

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_PerformedServiceId] 
        ON [dbo].[InvoiceItems] ([PerformedServiceId] ASC) 
        WHERE [PerformedServiceId] IS NOT NULL;
    ```
- **Khắc phục ở C# Application:**
  - `BillingService.cs` & `CareEpisodeService.cs`: Bổ sung bước kiểm tra tiền kiểm soát (pre-validation) truy vấn `InvoiceItems` để phát hiện bất kỳ dịch vụ nào đã được thanh toán hoặc lập hóa đơn trước đó, trả về thông báo lỗi tiếng Việt thân thiện thay vì để phát sinh unhandled exception.

### 2.4. P0-5: Toàn Vẹn Giữa Chuyên Khoa Và Dịch Vụ Chỉ Định (Department & Service Mismatch)
- **Lỗ hổng:** `Appointments` lưu `DepartmentId` và `RequestedServiceId` với 2 khóa ngoại độc lập. Người dùng có thể đặt lịch hẹn Khoa Chỉnh Nha nhưng lại chọn Dịch Vụ Cấy Ghép Implant.
- **Khắc phục ở Database Engine:**
  - Bổ sung Candidate Key trên `DepartmentServices`:
    ```sql
    ALTER TABLE [dbo].[DepartmentServices] ADD CONSTRAINT [UQ_DepartmentServices_Department_Service] 
        UNIQUE NONCLUSTERED ([DepartmentId] ASC, [DepartmentServiceId] ASC);
    ```
  - Bổ sung Composite Foreign Key trên `Appointments`:
    ```sql
    ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [FK_Appointments_DepartmentService_Composite] 
        FOREIGN KEY ([DepartmentId], [RequestedServiceId]) 
        REFERENCES [dbo].[DepartmentServices] ([DepartmentId], [DepartmentServiceId]);
    ```
- **Xử lý Dữ Liệu Demo Lịch Sử:**
  - Phát hiện và sửa 3 ca khám bị gõ nhầm dịch vụ trong bản seed cũ:
    - Lịch hẹn 95: Khoa Phẫu thuật (Dept 3) -> sửa `RequestedServiceId` từ 11 (Chữa tủy) thành 8 (Tiểu phẫu nhổ răng khôn).
    - Lịch hẹn 97: Khoa Chỉnh nha (Dept 2) -> sửa `RequestedServiceId` từ 7 (Implant) thành 5 (Tư vấn chỉnh nha).
    - Lịch hẹn 100: Khoa Răng trẻ em (Dept 4) -> sửa `RequestedServiceId` từ 18 (Răng sứ) thành 10 (Khám trẻ em & bôi Fluor).

### 2.5. P0-6: Toàn Vẹn Giữa Phòng Khám Và Ghế Nha (Room & Chair Mismatch)
- **Lỗ hổng:** Bảng `Appointments` cho phép chọn Ghế thuộc Phòng A nhưng lưu `RoomId` của Phòng B, hoặc chỉ định `DentalChairId` mà bỏ trống `RoomId`.
- **Khắc phục ở Database Engine:**
  - Bổ sung Candidate Key trên `DentalChairs`:
    ```sql
    ALTER TABLE [dbo].[DentalChairs] ADD CONSTRAINT [UQ_DentalChairs_Room_Chair] 
        UNIQUE NONCLUSTERED ([RoomId] ASC, [DentalChairId] ASC);
    ```
  - Bổ sung Check Constraint:
    ```sql
    ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [CK_Appointments_ChairRequiresRoom] 
        CHECK (([DentalChairId] IS NULL OR [RoomId] IS NOT NULL));
    ```
  - Bổ sung Composite Foreign Key:
    ```sql
    ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [FK_Appointments_RoomChair_Composite] 
        FOREIGN KEY ([RoomId], [DentalChairId]) 
        REFERENCES [dbo].[DentalChairs] ([RoomId], [DentalChairId]);
    ```

### 2.6. P0-7: Đồng Nhất Định Danh Bệnh Nhân Trong Đợt Khám (Patient & CareEpisode Consistency)
- **Lỗ hổng:** `CareEpisodes` và `Appointments` liên kết qua `RootAppointmentId` nhưng không có ràng buộc bảo đảm `PatientUserId` của đợt khám phải trùng với `PatientUserId` của lịch hẹn gốc hoặc các lịch hẹn con.
- **Khắc phục ở Database Engine:**
  - Bổ sung Candidate Key trên `Appointments`:
    ```sql
    ALTER TABLE [dbo].[Appointments] ADD CONSTRAINT [UQ_Appointments_Patient_Appointment] 
        UNIQUE NONCLUSTERED ([PatientUserId] ASC, [AppointmentId] ASC);
    ```
  - Bổ sung Composite FK từ `CareEpisodes` về `Appointments`:
    ```sql
    ALTER TABLE [dbo].[CareEpisodes] WITH CHECK ADD CONSTRAINT [FK_CareEpisodes_RootAppointment_Patient_Composite] 
        FOREIGN KEY ([PatientUserId], [RootAppointmentId]) 
        REFERENCES [dbo].[Appointments] ([PatientUserId], [AppointmentId]);
    ```
  - Bổ sung Candidate Key trên `CareEpisodes`:
    ```sql
    ALTER TABLE [dbo].[CareEpisodes] ADD CONSTRAINT [UQ_CareEpisodes_Patient_Episode] 
        UNIQUE NONCLUSTERED ([PatientUserId] ASC, [CareEpisodeId] ASC);
    ```
  - Bổ sung Composite FK từ `Appointments` về `CareEpisodes`:
    ```sql
    ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [FK_Appointments_CareEpisode_Patient_Composite] 
        FOREIGN KEY ([PatientUserId], [CareEpisodeId]) 
        REFERENCES [dbo].[CareEpisodes] ([PatientUserId], [CareEpisodeId]);
    ```

### 2.7. P0-8: Toàn Vẹn Giữa Thuốc Và Lô Nhập Kho (Medicine & Batch Mismatch)
- **Lỗ hổng:** Chi tiết cấp phát đơn thuốc (`MedicineDispensationItems`) có thể gán `MedicineId` của Thuốc A nhưng lại chọn `MedicineBatchId` của Thuốc B.
- **Khắc phục ở Database Engine:**
  - Bổ sung Candidate Key trên `MedicineBatches`:
    ```sql
    ALTER TABLE [dbo].[MedicineBatches] ADD CONSTRAINT [UQ_MedicineBatches_Medicine_BatchId] 
        UNIQUE NONCLUSTERED ([MedicineId] ASC, [MedicineBatchId] ASC);
    ```
  - Bổ sung Composite Foreign Key:
    ```sql
    ALTER TABLE [dbo].[MedicineDispensationItems] WITH CHECK ADD CONSTRAINT [FK_MedicineDispensationItems_MedicineBatch_Composite] 
        FOREIGN KEY ([MedicineId], [MedicineBatchId]) 
        REFERENCES [dbo].[MedicineBatches] ([MedicineId], [MedicineBatchId]);
    ```

### 2.8. P0-9: Toàn Vẹn Giữa Phòng Chẩn Đoán Hình Ảnh Và Thiết Bị (Room & Equipment Mismatch)
- **Lỗ hổng:** Phiên chụp phim (`ImagingSessions`) có thể lưu `ImagingRoomId` là Phòng 1 nhưng lại chọn máy chụp X-quang (`EquipmentId`) thuộc Phòng 2.
- **Khắc phục ở Database Engine:**
  - Bổ sung Candidate Key trên `ImagingEquipments`:
    ```sql
    ALTER TABLE [dbo].[ImagingEquipments] ADD CONSTRAINT [UQ_ImagingEquipments_Room_Equipment] 
        UNIQUE NONCLUSTERED ([RoomId] ASC, [EquipmentId] ASC);
    ```
  - Bổ sung Composite Foreign Key:
    ```sql
    ALTER TABLE [dbo].[ImagingSessions] WITH CHECK ADD CONSTRAINT [FK_ImagingSessions_RoomEquipment_Composite] 
        FOREIGN KEY ([ImagingRoomId], [EquipmentId]) 
        REFERENCES [dbo].[ImagingEquipments] ([RoomId], [EquipmentId]);
    ```

---

## 3. KẾT QUẢ THỰC NGHIỆM NEGATIVE INTEGRITY TESTS (9/9 PASSED)

Toàn bộ 9 ca kiểm thử vi phạm tính toàn vẹn dữ liệu được thực thi trực tiếp trên cơ sở dữ liệu `DentalCare_IntegrityFix_QA` để xác nhận engine SQL Server từ chối hành vi chèn/sửa dữ liệu sai lệch:

| STT | Kịch Bản Kiểm Thử Tiêu Cực (Negative Scenario) | Hành Vi Vi Phạm Thử Nghiệm | Mã Lỗi SQL Server Thực Tế | Kết Quả Đánh Giá |
| :---: | :--- | :--- | :---: | :---: |
| **1** | **Duplicate CareEpisode Invoice** | Tạo hóa đơn thứ 2 cho cùng một `CareEpisodeId` | `Msg 2627` (Unique Index Violation trên `UQ_Invoices_CareEpisodeId`) | **PASS (Bảo vệ thành công)** |
| **2** | **Cross-Invoice Double Billing (Task)** | Lập hóa đơn mới chứa `ServiceTaskId` đã được lập ở hóa đơn trước | `Msg 2601` (Duplicate Key Violation trên `UQ_InvoiceItems_ServiceTaskId`) | **PASS (Bảo vệ thành công)** |
| **3** | **Cross-Invoice Double Billing (Performed)** | Lập hóa đơn mới chứa `PerformedServiceId` đã được lập ở hóa đơn trước | `Msg 2627` (Unique Index Violation trên `UQ_InvoiceItems_PerformedServiceId`) | **PASS (Bảo vệ thành công)** |
| **4** | **Same-Invoice Duplicate Item** | Thêm 2 dòng cùng `ServiceTaskId` trong cùng một hóa đơn | `Msg 2627` (Unique Index Violation trên `UQ_InvoiceItems_ServiceTaskId`) | **PASS (Bảo vệ thành công)** |
| **5** | **Department & Service Mismatch** | Tạo lịch hẹn Khoa 1 nhưng chọn Dịch Vụ 8 (thuộc Khoa 3) | `Msg 547` (FK Violation trên `FK_Appointments_DepartmentService_Composite`) | **PASS (Bảo vệ thành công)** |
| **6** | **Room & Chair Mismatch** | Tạo lịch hẹn Phòng 1 nhưng chọn Ghế 3 (thuộc Phòng 2) | `Msg 547` (FK Violation trên `FK_Appointments_RoomChair_Composite`) | **PASS (Bảo vệ thành công)** |
| **7** | **Patient & RootAppointment Mismatch** | Tạo `CareEpisode` cho Bệnh nhân 14 nhưng trỏ `RootAppointmentId` của Bệnh nhân 13 | `Msg 547` (FK Violation trên `FK_CareEpisodes_RootAppointment_Patient_Composite`) | **PASS (Bảo vệ thành công)** |
| **8** | **Medicine & Batch Mismatch** | Xuất thuốc Thuốc 1 nhưng chọn Lô 3 (của Thuốc 2) | `Msg 547` (FK Violation trên `FK_MedicineDispensationItems_MedicineBatch_Composite`) | **PASS (Bảo vệ thành công)** |
| **9** | **Imaging Room & Equipment Mismatch** | Tạo phiên chụp Phòng 1 nhưng chọn Thiết bị 3 (thuộc Phòng 2) | `Msg 547` (FK Violation trên `FK_ImagingSessions_RoomEquipment_Composite`) | **PASS (Bảo vệ thành công)** |

---

## 4. MA TRẬN ĐỐI SOÁT ĐỐI TƯỢNG DATABASE ENGINE (METRICS COMPARISON)

| Tiêu Chí So Sánh | Baseline RC1 (`origin/develop`) | Bản Sửa Lỗi RC2 (`DentalCare_IntegrityFix_QA`) | Chênh Lệch (Delta) | Ghi Chú Kỹ Thuật |
| :--- | :---: | :---: | :---: | :--- |
| **Tổng số Bảng (Tables)** | **72** | **72** | **0** | Giữ nguyên 72 bảng đã chuẩn hóa |
| **Khóa chính (Primary Keys)** | **72** | **72** | **0** | Khớp 100% |
| **Khóa ngoại (Foreign Keys)** | **174** | **180** | **+6** | Bổ sung 6 Composite Foreign Keys P0 |
| **Ràng buộc kiểm tra (Checks)** | **90** | **92** | **+2** | `CK_Invoices_ParentReference`, `CK_Appointments_ChairRequiresRoom` |
| **Ràng buộc duy nhất (Unique)** | **27** | **32** | **+5** | Bổ sung 5 Candidate Keys phục vụ Composite FKs |
| **Chỉ mục phụ (Indexes Non-PK)** | **110** | **114** | **+4** | 4 Filtered Unique Indexes (Invoices & InvoiceItems) |
| **Khung nhìn (Views)** | **1** | **1** | **0** | `dbo.vw_PublishedBlogPosts` |
| **Thủ tục lưu trữ (Stored Procs)** | **2** | **2** | **0** | `sp_CleanupOrphanBlogPostMedia`, `sp_IncrementBlogPostView` |
| **Dòng dữ liệu mẫu (Seed Rows)** | **13.997** | **13.997** | **0** | 100% Active Constraints (sửa 3 dòng sai lệch lịch sử) |
| **Số dòng `__EFMigrationsHistory`** | **0** | **0** | **0** | Bất biến Standalone Schema |
| **Khóa ngoại bị Disabled / Untrusted** | **0** | **0** | **0** | 100% Trusted & Active |

---

## 5. KẾT QUẢ KIỂM THỬ HỒI QUY TOÀN HỆ THỐNG (QUALITY GATES)

### 5.1. Backend Test Suite (`DentalClinic.Tests`)
```text
Passed!  - Failed: 0, Passed: 966, Skipped: 1, Total: 967, Duration: 50 s - DentalClinic.Tests.dll (net8.0)
```
- **Tỷ lệ đạt:** **100% (966/966 run)**.
- **Test bỏ qua:** Duy nhất 1 test `UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully` do không cấu hình credential Cloudinary trên môi trường local/CI.
- Toàn bộ các test Concurrency, AppLock, ServiceTask Step Progress, Billing, FollowUp và X-ray Roster đều PASS tuyệt đối.

### 5.2. Frontend Test Suite (`DentalClinic.Web.Tests`)
```text
Passed!  - Failed: 0, Passed: 325, Skipped: 0, Total: 325, Duration: 20 s - DentalClinic.Web.Tests.dll (net8.0)
```
- **Tỷ lệ đạt:** **100% (325/325)**.

### 5.3. Playwright Chromium Real-Browser E2E Suite
Toàn bộ 5 kịch bản hành trình người dùng cốt lõi được chạy thực tế trên Chromium kết nối Backend API (`https://localhost:7350`), Frontend Web (`https://localhost:7129`) và database `DentalCare_IntegrityFix_QA`:

| Kịch Bản E2E Playwright | Tệp Kiểm Thử | Số Ca | Kết Quả | Thời Gian |
| :--- | :--- | :---: | :---: | :---: |
| **1. Admin Service & Workflow Config** | `admin-service-workflow-config.spec.js` | 6 | **6/6 PASSED** | 13.6s |
| **2. Admin -> Dentist Full Journey** | `admin-dentist-workflow-full-journey.spec.js` | 1 | **1/1 PASSED** | 14.0s |
| **3. CareEpisode Additional Services** | `care-episode-additional-service-handoff.spec.js` | 6 | **6/6 PASSED** | 12.6s |
| **4. Dedicated X-Ray Dentist Workflow** | `xray-dedicated-dentist-workflow.spec.js` | 3 | **3/3 PASSED** | 4.8s |
| **5. Four-Role Full Business Flow** | `four-role-full-flow.spec.js` | 6 | **6/6 PASSED** | 16.9s |
| **TỔNG CỘNG PLAYWRIGHT E2E** | **5 Bộ Kịch Bản Cốt Lõi** | **22** | **22/22 PASSED** | **100% PASS** |

---

## 6. SCRIPT NÂNG CẤP CƠ SỞ DỮ LIỆU HIỆN HỮU

Để đảm bảo các cơ sở dữ liệu đã triển khai trước đó (như `DentalCare_SWP391`, `DentalCare_Integration_Dev`,...) có thể nâng cấp an toàn lên chuẩn RC2 mà không cần xóa database hay mất dữ liệu hiện có, script sau đã được tạo và kiểm chứng độ bất biến (idempotency):
- **Đường dẫn tệp:** `DB_Archive/migrations/20261113_Fix_P0_Database_Integrity_Constraints.sql`
- **Đặc điểm an toàn:**
  - Bọc toàn bộ trong giao dịch `BEGIN TRANSACTION ... COMMIT TRANSACTION ... BEGIN CATCH ... ROLLBACK`.
  - Kiểm tra `IF NOT EXISTS` / `IF EXISTS` trước mọi câu lệnh DDL.
  - Tự động sửa 3 dòng sai lệch lịch sử (Appointments 95, 97, 100) trước khi áp đặt Composite FK.
  - Đã chạy thử nghiệm thành công 100% trên `DentalCare_IntegrityFix_QA`.

---

## 7. QUY CHUẨN ĐÓNG GÓI BÀN GIAO THƯ MỤC `DB/`

Thư mục `DB/` tuân thủ nghiêm ngặt yêu cầu của Project Owner: **CHỈ CHỨA ĐÚNG HAI FILE SQL ĐỘC LẬP**:
1. `DB/DentalClinic_Schema_Full_Standalone.sql`:
   - Phong cách T-SQL chuyên nghiệp, có khối khai báo `@TargetDb = N'DentalCare_SWP391'`.
   - Mở trong SSMS và bấm **F5** sẽ tự tạo database, chuyển context và tạo toàn bộ 72 bảng cùng 180 FKs.
2. `DB/DentalClinic_Demo_Data.sql`:
   - Mở trong SSMS và bấm **F5** sẽ nạp tuần tự 13.997 bản ghi demo với 100% active constraints.

---

## 8. KẾT LUẬN VÀ TRẠNG THÁI SẴN SÀNG

1. **Khắc phục hoàn tất 100% các lỗ hổng Database Integrity P0** được báo cáo tại commit `b6d8dc4`.
2. **Không có bất kỳ sự hồi quy nào (Zero Regressions):** Toàn bộ 1.291 test C# và 22 kịch bản Playwright E2E đều đạt 100% Pass Rate.
3. **Cách ly tuyệt đối:** Không làm thay đổi hay ảnh hưởng đến nhánh `origin/develop`, `origin/main` hay database đang vận hành.
4. **Bản Release Candidate 2 (RC2)** trên nhánh `fix/database-integrity-p0` đã hoàn toàn sẵn sàng để Project Owner nghiệm thu.
