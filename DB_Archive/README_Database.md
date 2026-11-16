# DentalCare SWP391 – Database Architecture & Standalone Schema Delivery (RC2)

> **Dự án:** Hệ thống Quản lý Phòng khám Nha khoa DentalCare (SWP391 - Nhóm 6)  
> **Phiên bản:** Release Candidate 2 (RC2) – P0 Database Integrity Hardened  
> **Hệ quản trị CSDL hỗ trợ:** Microsoft SQL Server 2017 / 2019 / 2022 / Azure SQL Edge / LocalDB / SQLEXPRESS  
> **Quy chuẩn bàn giao:** EXACTLY TWO STANDALONE SQL FILES trong thư mục `DB/`  

---

## 1. Tổng Quan & Cấu Trúc Bàn Giao Thư Mục `DB/`

Dự án DentalCare chuẩn hóa toàn bộ cơ sở dữ liệu vào **ĐÚNG HAI FILE SQL ĐỘC LẬP** chuẩn phong cách T-SQL chuyên nghiệp, dễ đọc, sẵn sàng thực thi trực tiếp bằng phím F5 trong **SQL Server Management Studio (SSMS)**:

| Tệp Bàn Giao | Dung Lượng | Đối Tượng Quản Lý | Mô Tả & Tiêu Chuẩn Kỹ Thuật |
| :--- | :---: | :---: | :--- |
| **`DB/DentalClinic_Schema_Full_Standalone.sql`** | ~240 KB | **72 Bảng, 180 Khóa Ngoại, 92 Check, 32 Unique, 110 Indexes, 1 View, 2 SPs** | **File Cấu Trúc CSDL Độc Lập Duy Nhất (Schema Only)**: Tự động khởi tạo database `DentalCare_SWP391` (nếu chưa có), chuyển ngữ cảnh `USE [DentalCare_SWP391]`, tạo toàn bộ bảng domain, các ràng buộc toàn vẹn P0 (composite FK, filtered unique indexes, check constraints) và 0 dòng `__EFMigrationsHistory`. |
| **`DB/DentalClinic_Demo_Data.sql`** | ~6.1 MB | **58 Bảng, 13.997 Dòng Dữ Liệu Demo** | **Dữ liệu mẫu chuẩn hóa toàn diện**: Dữ liệu tiếng Việt UTF-8 chuẩn xác, đúng 4 vai trò chính thức (`Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`), mật khẩu Identity `Password123@`, bảo đảm 100% active constraints khi nạp, hoàn toàn sạch lỗi tham chiếu chéo. |

---

## 2. Hướng Dẫn Thực Thi Bằng SSMS (SQL Server Management Studio)

Cả hai file SQL đã được thiết kế sẵn để thực thi trực tiếp bằng SSMS mà không cần tạo trước database hay chuyển context thủ công:

### Bước 1: Khởi Tạo Cấu Trúc Cơ Sở Dữ Liệu
1. Mở **SQL Server Management Studio (SSMS)** và kết nối tới SQL Server instance (ví dụ: `.\SQLEXPRESS`).
2. Mở file: `DB/DentalClinic_Schema_Full_Standalone.sql`.
3. Nhấn **F5** (hoặc nút **Execute** trên thanh công cụ).
4. *Script sẽ tự động:*
   - Kiểm tra và tạo database `[DentalCare_SWP391]` với collation `SQL_Latin1_General_CP1_CI_AS`.
   - Chuyển ngữ cảnh sang database vừa tạo.
   - Kiểm tra an toàn (Safety Guard): Chặn chạy trên system DB hoặc DB đã có dữ liệu.
   - Khởi tạo đầy đủ 72 bảng cùng toàn bộ ràng buộc toàn vẹn.
5. Thông báo hoàn tất: `>>> DENTAL CLINIC STANDALONE SCHEMA INITIALIZED SUCCESSFULLY (72 TABLES, VIEWS & PROCEDURES)!`

### Bước 2: Nạp Dữ Liệu Mẫu Chuẩn Hóa
1. Mở file: `DB/DentalClinic_Demo_Data.sql` trong SSMS.
2. Nhấn **F5** (hoặc nút **Execute**).
3. *Script sẽ tự động:*
   - Kết nối tới `[DentalCare_SWP391]`.
   - Kiểm tra điều kiện tiên quyết (đã có schema 72 bảng).
   - Nạp tuần tự 13.997 bản ghi demo với `IDENTITY_INSERT` được quản lý chặt chẽ.
   - Kiểm tra và tái kích hoạt (`WITH CHECK CHECK CONSTRAINT ALL`) 100% các khóa ngoại.
4. Thông báo hoàn tất: `>>> DENTAL CLINIC DATABASE & DATA SUCCESSFULLY INITIALIZED 100% (72 TABLES, 13,997 ROWS)!`

> [!TIP]
> **Tùy biến tên Database đích:**  
> Nếu bạn muốn triển khai vào một database với tên khác (ví dụ: `DentalCare_Test_DB`), chỉ cần sửa giá trị biến `@TargetDb` ở đầu mỗi file SQL:
> ```sql
> DECLARE @TargetDb SYSNAME = N'DentalCare_Test_DB';
> ```

---

## 3. Các Lỗ Hổng Database Integrity P0 Đã Được Khắc Phục (RC2)

Bản Release Candidate 2 (RC2) khắc phục triệt để các vấn đề toàn vẹn dữ liệu được phát hiện từ đợt kiểm toán kiến trúc CSDL:

| Phân Hệ & Mã Lỗ Hổng | Tình Trạng Ban Đầu (P0 Vulnerability) | Giải Pháp Khắc Phục Ở Tầng Database Engine & Ứng Dụng |
| :--- | :--- | :--- |
| **Billing: Episode-Level Invoicing**<br>`P0-1: CareEpisode Invoicing` | Cột `AppointmentId` trong `Invoices` bắt buộc `NOT NULL`, khiến hóa đơn tổng hợp đợt điều trị (CareEpisode) phải gán ép vào `RootAppointmentId`, làm sai lệch báo cáo viện phí. | Đã chuyển `Invoices.AppointmentId` thành `NULLABLE`. Thêm ràng buộc `CK_Invoices_ParentReference` đảm bảo hóa đơn phải thuộc về ít nhất một `AppointmentId` hoặc `CareEpisodeId`. |
| **Billing: Double Invoicing**<br>`P0-2: Duplicate Episode Invoices` | Một `CareEpisode` có thể bị tạo nhiều hóa đơn thanh toán nếu các request được gửi đồng thời. | Tạo filtered unique index `UQ_Invoices_CareEpisodeId` trên `Invoices(CareEpisodeId)` khi `CareEpisodeId IS NOT NULL`. Đảm bảo mỗi đợt khám chỉ có DUY NHẤT một hóa đơn. |
| **Billing: Anti-Double-Billing**<br>`P0-3 & P0-4: Service Over-Billing` | Dịch vụ đã làm (`PerformedServices`) và chỉ định phát sinh (`EpisodeServiceTasks`) không có ràng buộc duy nhất trong `InvoiceItems`, có thể bị thanh toán nhiều lần. | Tạo filtered unique index `UQ_InvoiceItems_ServiceTaskId` và `UQ_InvoiceItems_PerformedServiceId` trên `InvoiceItems`. Tầng Service kiểm tra trùng lặp và chặn đứng lập hóa đơn kép. |
| **Clinical: Dept & Service Mismatch**<br>`P0-5: Cross-Department Booking` | Bệnh nhân hoặc nhân viên có thể đặt lịch hẹn với Khoa A nhưng chọn Dịch vụ của Khoa B do `Appointments` chỉ có FK đơn lẻ. | Thêm unique constraint `UQ_DepartmentServices_Department_Service` và composite FK `FK_Appointments_DepartmentService_Composite` trên `Appointments(DepartmentId, RequestedServiceId)`. |
| **Infrastructure: Room & Chair Mismatch**<br>`P0-6: Chair-Room Discrepancy` | Lịch hẹn có thể xếp Ghế của Phòng A vào Phòng B, hoặc chỉ định Ghế mà không có Phòng. | Thêm unique constraint `UQ_DentalChairs_Room_Chair`, composite FK `FK_Appointments_RoomChair_Composite` và check constraint `CK_Appointments_ChairRequiresRoom`. |
| **Clinical: CareEpisode & Patient Identity**<br>`P0-7: Episode-Patient Mismatch` | `CareEpisodes` và `Appointments` liên kết qua `RootAppointmentId` nhưng không ràng buộc `PatientUserId`, dẫn đến nguy cơ đợt khám thuộc bệnh nhân X nhưng lịch hẹn gốc thuộc bệnh nhân Y. | Thêm unique constraint `UQ_Appointments_Patient_Appointment`, composite FK `FK_CareEpisodes_RootAppointment_Patient_Composite` và composite FK `FK_Appointments_CareEpisode_Patient_Composite`. |
| **Pharmacy: Medicine & Batch Mismatch**<br>`P0-8: Cross-Batch Dispensation` | Chi tiết cấp phát thuốc (`MedicineDispensationItems`) có thể gán Thuốc A nhưng lại chọn Lô của Thuốc B. | Thêm unique constraint `UQ_MedicineBatches_Medicine_BatchId` và composite FK `FK_MedicineDispensationItems_MedicineBatch_Composite`. |
| **Diagnostics: Room & Equipment Mismatch**<br>`P0-9: Cross-Room Imaging Session` | Phiên chụp chẩn đoán hình ảnh (`ImagingSessions`) có thể chọn Phòng chụp X-quang 1 nhưng thiết bị lại ở Phòng 2. | Thêm unique constraint `UQ_ImagingEquipments_Room_Equipment` và composite FK `FK_ImagingSessions_RoomEquipment_Composite`. |

---

## 4. Ma Trận Thống Kê Đối Tượng Database (Authoritative Metrics)

| Loại Đối Tượng Database | Bản Gốc RC1 (`develop`) | Bản Nâng Cấp Toàn Vẹn RC2 | Biến Động (Delta) | Trạng Thái Kiểm Chứng |
| :--- | :---: | :---: | :---: | :---: |
| **Tổng số Bảng (Tables)** | **72** | **72** | 0 | 100% Khớp |
| **Khóa chính (Primary Keys)** | **72** | **72** | 0 | 100% Khớp |
| **Khóa ngoại (Foreign Keys)** | **174** | **180** | **+6 composite FKs** | 100% Trusted & Active |
| **Ràng buộc kiểm tra (Check Constraints)** | **90** | **92** | **+2 checks** | 100% Trusted & Active |
| **Ràng buộc duy nhất (Unique Constraints)** | **27** | **32** | **+5 candidate keys** | 100% Active |
| **Chỉ mục phụ (Indexes Non-PK)** | **110** | **114** | **+4 filtered unique indexes** | 100% Active |
| **Khung nhìn (Views)** | **1** | **1** | 0 | `dbo.vw_PublishedBlogPosts` |
| **Thủ tục lưu trữ (Stored Procedures)** | **2** | **2** | 0 | Khớp 100% |
| **Dòng dữ liệu mẫu (Demo Data Rows)** | **13.997** | **13.997** | 0 (sửa 3 dòng sai lệch) | 100% Active Constraints |
| **Hàng lịch sử EF (`__EFMigrationsHistory`)** | **0** | **0** | 0 | Standalone Invariant |

---

## 5. Kết Quả Kiểm Nghiệm Toàn Diện (Quality Assurance)

- **Negative Integrity Tests:** 9/9 ca kiểm thử vi phạm tính toàn vẹn (thử nghiệm chèn dữ liệu sai lệch) bị SQL Server Engine chặn đứng ngay tại tầng dữ liệu (`Msg 2627`, `Msg 2601`, `Msg 547`).
- **Backend API Test Suite (`DentalClinic.Tests`):** **966 PASSED**, 0 FAILED, 1 SKIPPED (Cloudinary third-party cloud credential test).
- **Frontend Web Test Suite (`DentalClinic.Web.Tests`):** **325 PASSED**, 0 FAILED.
- **Playwright Chromium E2E Browser Suite:** **22/22 PASSED** trên cả 5 luồng người dùng cốt lõi.
- **Migration Tương Thích Database Cũ:** Cung cấp script `DB_Archive/migrations/20261113_Fix_P0_Database_Integrity_Constraints.sql` nâng cấp an toàn các database hiện hữu không mất dữ liệu.
