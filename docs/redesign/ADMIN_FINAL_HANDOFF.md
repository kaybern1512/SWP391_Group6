# TÀI LIỆU BÀN GIAO CUỐI CÙNG – PHÂN HỆ SYSTEM ADMINISTRATOR (ANTI 1)
## DentalCare System Administrator Final Integration Handoff

- **Phân hệ phụ trách:** Anti 1 – SystemAdministrator
- **Dự án:** `D:\SWP391\DentalCare-Admin`
- **Git Branch:** `feature/admin-redesign`
- **Baseline Commit:** `5ad6d95` (nhánh `refactor/phase-0-database-redesign`)
- **Ngày bàn giao:** 09/11/2026
- **Trạng thái:** HOÀN THÀNH TOÀN DIỆN & ĐÓNG BĂNG MÃ NGUỒN (Ready for Integration Takeover)

---

## 1. Phân công Chính thức Giữa 4 Anti

Nhằm thống nhất toàn bộ quy trình tích hợp giữa các tác tử và đội ngũ phát triển, ma trận phân công chính thức được xác lập như sau:

| Mã Anti | Vai trò phụ trách (Role) | Phạm vi & Trách nhiệm chính |
|---|---|---|
| **Anti 1** | **SystemAdministrator** | Dashboard, User/Staff Management (3 roles), Quản lý 31 dịch vụ nha khoa & thời lượng, Phân công Bác sĩ & ca trực, Quản lý phòng/ghế vô trùng, Quản lý Kho thuốc độc quyền, Duyệt bài viết CMS & đăng Tin tức/Thông báo, Kiểm duyệt Đánh giá bệnh nhân. |
| **Anti 2** | **Receptionist** | Đón tiếp bệnh nhân walk-in, Điều phối lịch hẹn, Check-in, Lập hóa đơn/Thanh toán, Tạo bài viết kiến thức nha khoa (Draft/PendingReview) gửi Admin duyệt. |
| **Anti 3** | **Patient** | Đặt lịch khám trực tuyến, Xem hồ sơ bệnh án cá nhân, Xem danh sách bác sĩ/dịch vụ, Đọc bài viết kiến thức (`KnowledgeBlog`), Gửi đánh giá sau khi hoàn tất khám. |
| **Anti 4** | **Dentist** | Xem lịch làm việc & lịch hẹn được phân công, Khám & chẩn đoán điều trị, Lập phác đồ, Kê đơn & cấp phát thuốc (dựa trên contract thuốc khả dụng do Admin cung cấp). Cấm viết blog. |

---

## 2. Chuẩn hóa Migration Cơ sở dữ liệu: `CK_BlogPosts_Status`

### 2.1. Vấn đề Phát hiện & Phân tích
- Schema Phase 0 kế thừa ràng buộc từ `Add_Blog_Tables.sql`:
  `CONSTRAINT CK_BlogPosts_Status CHECK (Status IN ('Draft', 'PendingReview', 'Published', 'Archived'))`.
- Nghiệp vụ Redesign 4 role quy định Admin có thẩm quyền **Từ chối duyệt bài viết (`Rejected`)** kèm lý do (`RejectionReason`) để Lễ tân chỉnh sửa.
- Việc lưu trạng thái `Rejected` bị chặn bởi ràng buộc CHECK nếu chưa được cập nhật trên schema gốc.

### 2.2. Tệp tin Migration Quản lý bằng Git
Đã tạo hai tệp tin migration SQL chuẩn hóa trong thư mục [`DB/redesign_migrations/`](file:///D:/SWP391/DentalCare-Admin/DB/redesign_migrations/):
1. **Forward Migration:** [`DB/redesign_migrations/20261109_Update_BlogPosts_Status_CheckConstraint_Add_Rejected.sql`](file:///D:/SWP391/DentalCare-Admin/DB/redesign_migrations/20261109_Update_BlogPosts_Status_CheckConstraint_Add_Rejected.sql)
   - *Preflight Data Integrity Check:* Kiểm tra toàn bộ dữ liệu lịch sử bảng `BlogPosts`, hủy giao dịch nếu có dữ liệu dị thường.
   - *Tính Idempotent:* Tự động nhận diện nếu constraint đã có `'Rejected'` thì bỏ qua an toàn.
   - *Bảo toàn Dependency:* Không ảnh hưởng tới các chỉ mục (`IX_BlogPosts_Status_PublishedAt`, `IX_BlogPosts_CategoryId`) hay Foreign Keys (`FK_BlogPosts_ApprovedByUser`, `FK_BlogPosts_UserAccounts`).
   - Ràng buộc mới:
     ```sql
     ALTER TABLE [dbo].[BlogPosts] WITH CHECK ADD CONSTRAINT [CK_BlogPosts_Status] 
     CHECK ([Status] IN ('Draft', 'PendingReview', 'Published', 'Archived', 'Rejected'));
     ```
2. **Rollback Migration:** [`DB/redesign_migrations/20261109_Rollback_BlogPosts_Status_CheckConstraint_Add_Rejected.sql`](file:///D:/SWP391/DentalCare-Admin/DB/redesign_migrations/20261109_Rollback_BlogPosts_Status_CheckConstraint_Add_Rejected.sql)
   - *Data Safety Check:* Hủy rollback nếu còn bất kỳ bài viết nào có `Status = 'Rejected'`, ngăn chặn triệt để nguy cơ mất mát dữ liệu bài viết.
   - Khôi phục an toàn về 4 trạng thái cũ (`'Draft'`, `'PendingReview'`, `'Published'`, `'Archived'`).

### 2.3. Hướng dẫn Thứ tự Thực thi cho Integration Team
1. **Bước 1:** Sau khi clone database staging từ baseline Phase 0, chạy script:
   `sqlcmd -S <SERVER> -d <DATABASE> -i DB/redesign_migrations/20261109_Update_BlogPosts_Status_CheckConstraint_Add_Rejected.sql`
2. **Bước 2:** Xác nhận dòng log: `>>> Migration 20261109_Update_BlogPosts_Status_CheckConstraint_Add_Rejected completed successfully!`.
3. **Bước 3:** Khởi chạy Backend 7351 và chạy kiểm thử Admin Blog Moderation.

---

## 3. Đồng bộ Toàn diện Blog `PostType`

Để đảm bảo tính nhất quán giữa Patient FE, Receptionist FE và Admin CMS:

### 3.1. Quy tắc Ánh xạ Contract Thống nhất
Ràng buộc SQL Server `CK_BlogPosts_PostType` chỉ chấp nhận: `('Announcement', 'ClinicNews', 'Blog')`.

| Loại bài viết | Patient FE gửi / hiển thị | Backend & Database lưu | Admin FE hiển thị |
|---|---|---|---|
| **Kiến thức Nha khoa** | `KnowledgeBlog` hoặc `Blog` | `'Blog'` | `Blog` (Kiến thức Nha khoa) |
| **Tin tức Phòng khám** | `ClinicNews` | `'ClinicNews'` | `ClinicNews` (Tin tức Phòng khám) |
| **Thông báo / Tuyển dụng** | `Announcement` | `'Announcement'` | `Announcement` (Thông báo Phòng khám) |

### 3.2. Hiện thực trong `BlogService.cs`
- Phương thức chuẩn hóa tĩnh [`NormalizePostType(string? postType)`](file:///D:/SWP391/DentalCare-Admin/BE/DentalClinic.Infrastructure/Services/BlogService.cs#L1074-L1103) tự động map:
  - `KnowledgeBlog`, `DentalKnowledge`, `Blog` ➔ `"Blog"`.
  - `ClinicNews`, `News` ➔ `"ClinicNews"`.
  - `Announcement`, `ThongBao` ➔ `"Announcement"`.
- **Áp dụng đồng bộ tại mọi điểm chạm API:**
  - `POST /api/Blog/posts`: Chuẩn hóa loại bài viết trước khi insert.
  - `PUT /api/Blog/posts/{id}`: Chuẩn hóa loại bài viết khi cập nhật.
  - `GET /api/Blog/posts?postType={type}`: Tự động lọc `(p.PostType ?? "Blog") == NormalizePostType(postType)`.
  - `GET /api/Blog/admin/posts?postType={type}`: Lọc chính xác theo loại bài viết cho Admin.
  - Dữ liệu lịch sử có `PostType = NULL` tự động được coi là `"Blog"`, không làm biến dạng dữ liệu cũ.

---

## 4. Chi tiết Báo cáo Kiểm thử Toàn bộ Hệ thống

### 4.1. Kết quả Phân hệ Admin & Frontend Web (100% Passed)
- **Frontend Web Tests (`DentalClinic.Web.Tests`):** **289/289 PASSED (100%)**.
- **Backend Admin Tests (`DentalClinic.Tests`):** **47/47 PASSED (100%)**.
  - Bao gồm 4 kiểm thử E2E trực tiếp trên SQL Server `DentalCare_Admin_Dev`:
    * Tạo bác sĩ đồng bộ đầy đủ profile (`StaffProfile`, `DentistProfile`, `DentistDepartment`, `AuditLog`).
    * Cấu hình 31 dịch vụ nha khoa, thời lượng 90-120 phút, yêu cầu phòng độc quyền, Booking API đọc trực tiếp.
    * Quy trình duyệt bài CMS: Lễ tân tạo bài ➔ Ẩn khỏi trang chủ ➔ Admin từ chối kèm lý do ➔ Admin duyệt xuất bản.
    * Kiểm duyệt phản hồi: Ẩn đánh giá kèm ghi chú, gửi phản hồi chính thức, bảo toàn tuyệt đối 100% số sao.

### 4.2. Bảng Thống kê & Phân loại Chi tiết 69 Tests Thất bại của Module Khác
Khi thực thi toàn bộ 639 tests của `DentalClinic.Tests` (không dùng bộ lọc Admin), có 569 tests Passed và 69 tests Failed. Toàn bộ 69 tests này thuộc về các module cũ chưa migrate từ Phase 0 hoặc do môi trường kết nối cá nhân:

| STT | Tên Test Case (Fully Qualified Name) | Nguyên nhân Xác minh từ Log | Phân hệ Owner | Yêu cầu Môi trường | Hướng xử lý tại Integration |
|---|---|---|---|---|---|
| 1 | `DentalClinic.Tests.EndToEnd.RealDatabaseEndToEndSmokeTests.ScenarioA_PatientBooking_EndToEndLifecycle_Succeeds` | `Bác sĩ không có ca làm việc trong ngày được chọn.` (Gọi `ManagerConfirmAsync` 5 role cũ) | Anti 2 (Receptionist) & Anti 4 (Dentist) | DB có seed ca trực bác sĩ trên test date | Cập nhật kịch bản theo 4-role flow (Lễ tân confirm) |
| 2 | `DentalClinic.Tests.EndToEnd.RealDatabaseEndToEndSmokeTests.ScenarioB_ReceptionistWalkInBooking_EndToEndLifecycle_Succeeds` | `Bác sĩ không có ca làm việc trong ngày được chọn.` | Anti 2 (Receptionist) | DB có seed ca trực bác sĩ trên test date | Cập nhật kịch bản theo 4-role flow |
| 3 | `DentalClinic.Tests.EndToEnd.RealDatabaseEndToEndSmokeTests.ScenarioC_RoomChairAllocation_SharedVsExclusive_EnforcesCorrectRules` | `Bác sĩ không có ca làm việc trong ngày được chọn.` | Anti 2 (Receptionist) | DB có seed ca trực bác sĩ trên test date | Cập nhật kịch bản theo 4-role flow |
| 4 | `DentalClinic.Tests.EndToEnd.RealDatabaseEndToEndSmokeTests.ScenarioD_Concurrency_LockProtectionAndUniqueQueueNumbers_Enforced` | `Assert.Equal() Failure: Expected 1, Actual 0` (Hàng đợi xác nhận cũ) | Integration / Booking Concurrency | DB có seed ca trực bác sĩ trên test date | Cập nhật lock key theo 4-role flow |
| 5 | `DentalClinic.Tests.EndToEnd.RealDatabaseEndToEndSmokeTests.ScenarioE_RootCanal_CompleteLifecycle_WithChairAllocation_Discount_AndPaymentSecurity` | `Bác sĩ không có ca làm việc trong ngày được chọn.` | Anti 4 (Dentist) & Anti 2 (Receptionist) | DB có seed ca trực bác sĩ trên test date | Cập nhật kịch bản theo 4-role flow |
| 6 | `DentalClinic.Tests.Concurrency.SqlServerConcurrencyIntegrationTests.SqlServer_ParallelConfirmations_SameDentist_OneSucceeds_OneFailsWithConflict` | `Cannot open database "DentalClinic_IntegrationTests" requested by the login.` | Integration Concurrency | Windows LocalDB `(localdb)\mssqllocaldb` | Khởi tạo database test trên LocalDB hoặc cấu hình ConnectionString |
| 7 | `DentalClinic.Tests.Concurrency.SqlServerConcurrencyIntegrationTests.SqlServer_SharedRoom_ParallelConfirmations_DifferentChairs_BothSucceed` | `Cannot open database "DentalClinic_IntegrationTests" requested by the login.` | Integration Concurrency | Windows LocalDB `(localdb)\mssqllocaldb` | Khởi tạo database test trên LocalDB |
| 8-19 | 12 tests trong `DentalClinic.Tests.FollowUp.FollowUpSqlServerIntegrationTests`: <br>• `SqlServer_CallProvenance_AuthenticAuditLogPreserved_AndReconstructedYieldsNullTimestamp`<br>• `SqlServer_UpdateReminderCallStatus_PreservesStatusAndPersistsAuditLog`<br>• `SqlServer_UpdateReminderCallStatus_CancelledFollowUp_FailsGracefully`<br>• `SqlServer_Migration_DiscoversAndReplacesOldTableLevelConstraint_AndIsIdempotent`<br>• `SqlServer_Migration_InfersClinicalStatusFromAppointment_CancelledAndCompleted`<br>• `SqlServer_UpdateReminderCallStatus_CompletedFollowUp_FailsGracefully`<br>• `SqlServer_Rollback_AbortsWhenConfirmedOrProposedRowsExist_PreservingData`<br>• `SqlServer_ValidStatuses_CanBeSavedAndQueried`<br>• `SqlServer_Migration_PreservesUnrelatedConstraintsOnOtherColumns`<br>• `SqlServer_Rollback_SucceedsWhenNoExtendedStatusesExist`<br>• `SqlServer_InvalidStatuses_CalledAndUnreachable_RejectedByCheckConstraint`<br>• `SqlServer_CheckConstraint_IsActiveAndTrusted` | `FAILED TO CONNECT to SQL Server QA database (Server=.\SQL2025_DUC, Database=DentalClinic_FollowUp_QA).` | Anti 2 (Receptionist) & Anti 3 (Patient) - FollowUp | SQL Server instance `.\SQL2025_DUC` (máy tác giả cũ) | Tham số hóa chuỗi kết nối qua biến môi trường `DENTAL_CLINIC_QA_CONNECTION_STRING` |
| 20-69 | 50 tests trong `DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests`: <br>(B01, B02, B03, B04, B05, B06, B06B, B07, B07B, B08, B09, B10, B11, B12, B13, B14, B15, B15B, B15C, B16, B17, B18, B19, B20, B21, B22, B23, B24, B25, B26, B27, B28, B29, B30, B31, B32, B33, B34, B35, B36, B37, B38, B39, B40, B41, B41B, B42, B42B, B43, B44) | `A network-related or instance-specific error occurred while establishing a connection to SQL Server (provider: SQL Network Interfaces, error: 26 - Error Locating Server/Instance Specified).` | Anti 2 (Receptionist) & Anti 4 (Dentist) - FollowUp & Concurrency | SQL Server instance `.\SQL2025_DUC` từ `ConcurrencyTestDatabaseFixture` | Cấu hình fixture trỏ về instance SQL Server khả dụng trong CI/CD pipeline |

---

## 5. Phân biệt Rõ Ràng các Tầng Kiểm thử

1. **SQL Server Integration Tests (Tầng Tích hợp Dữ liệu Thực):**
   - Chạy trên SQL Server instance thật (`DentalCare_Admin_Dev` trên `DESKTOP-1TUBGEU\SQLEXPRESS`).
   - Kiểm tra trực tiếp transaction ACID, tính toán tồn kho, các ràng buộc check constraint, audit logs và dữ liệu quan hệ FK.
   - Các file: `AdminMedicineInventorySqlServerIntegrationTests.cs`, `AdminEndToEndSqlServerIntegrationTests.cs` (100% Passed).
2. **Web Controller Unit & Component Tests:**
   - Chạy trên xUnit & Moq độc lập với database, kiểm tra routing, view model mapping, session isolation, authorization attribute.
   - File: `DentalClinic.Web.Tests` (289 tests Passed).
3. **Browser E2E Tests (Tầng Trình duyệt Người dùng):**
   - Yêu cầu khởi chạy đồng thời Backend HTTPS `7351` và Frontend HTTPS `7121`.
   - Admin đăng nhập qua form Web, thao tác trên giao diện Razor, duyệt modal, tương tác AJAX APIs.
   - Quy trình E2E trên trình duyệt phụ thuộc vào việc hoàn tất 3 phân hệ còn lại để phối hợp toàn bộ vòng đời bệnh nhân.

---

## 6. Danh mục Bàn giao & Phụ thuộc Tiếp nhận

- **Branch bàn giao:** `feature/admin-redesign`
- **Mã Commit:** Xem phần thông báo Git commit bên dưới.
- **Tài liệu bàn giao kèm theo:**
  - `docs/redesign/ADMIN_FINAL_HANDOFF.md` (Tài liệu này)
  - `docs/redesign/ADMIN_IMPLEMENTATION_REPORT.md` (Báo cáo triển khai kỹ thuật)
  - `docs/redesign/handoffs/ADMIN_API_CONTRACT.md` (Quy ước API chi tiết)
  - `DB/redesign_migrations/20261109_Update_BlogPosts_Status_CheckConstraint_Add_Rejected.sql` (Forward Migration)
  - `DB/redesign_migrations/20261109_Rollback_BlogPosts_Status_CheckConstraint_Add_Rejected.sql` (Rollback Migration)
- **Tình trạng Phân hệ:** Anti 1 SystemAdministrator hoàn thành 100% phạm vi được giao, dừng phát triển và bàn giao cho Integration Team.
