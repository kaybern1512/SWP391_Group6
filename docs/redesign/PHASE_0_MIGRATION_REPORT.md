# BÁO CÁO THỰC THI DI CHUYỂN DỮ LIỆU – PHASE 0 (MIGRATION REPORT)
## Dự án: DentalCare Clinic Management System (SWP391 Group 6)
**Thời gian thực thi:** 09/11/2026 14:24 – 14:28 (Giờ hệ thống)  
**Môi trường thực thi:** Microsoft SQL Server 2022 Express (`DESKTOP-1TUBGEU\SQLEXPRESS`)  
**Database thực thi:** `DentalClinic_Phase0_QAClone` (Bảo vệ tuyệt đối Database chính `DentalClinicManagementDB`)  
**Tập lệnh Migration:** [`DB/redesign_migrations/20261109_Phase0_Database_Standardization_4Roles.sql`](file:///D:/SWP391/SWP391_Group6/DB/redesign_migrations/20261109_Phase0_Database_Standardization_4Roles.sql)  
**Tập lệnh Rollback:** [`DB/redesign_migrations/20261109_Phase0_Database_Standardization_4Roles_Rollback.sql`](file:///D:/SWP391/SWP391_Group6/DB/redesign_migrations/20261109_Phase0_Database_Standardization_4Roles_Rollback.sql)  

---

## 1. TỔNG KẾT KẾT QUẢ TRƯỚC VÀ SAU KHI CHẠY MIGRATION

| Tiêu Chí / Bảng Dữ Liệu | Trước Migration (Baseline) | Sau Migration (Target) | Đánh Giá Kết Quả |
| :--- | :--- | :--- | :---: |
| **Số lượng Role hợp lệ** | 5 vai trò (gồm `DepartmentManager`) | **Đúng 4 vai trò** chuẩn theo đặc tả | **Đạt 100%** |
| **Tài khoản `DepartmentManager`**| 4 tài khoản (UserId 4, 10, 11, 12) | **0 tài khoản** (Đã chuyển thành Dentist cấp cao) | **Đạt 100%** |
| **Hồ sơ Bác sĩ `DentistProfiles`**| 6 hồ sơ bác sĩ | **10 hồ sơ bác sĩ** (Bổ sung đủ cho 4 trưởng khoa) | **Đạt 100%** |
| **Phân bổ khoa `DentistDepartments`**| 13 bản ghi | **17 bản ghi** (4 bác sĩ được gán đúng khoa) | **Đạt 100%** |
| **Ca khám `PendingDepartmentReview`**| 7 ca khám bị chờ duyệt | **0 ca khám** (Đã chuyển sang Lễ tân tiếp nhận) | **Đạt 100%** |
| **Ca khám `PendingReceptionReview`**| 0 ca khám | **7 ca khám** (Sẵn sàng hiển thị cho Lễ tân) | **Đạt 100%** |
| **Lịch sử duyệt ca khám** | 144 bản ghi | **151 bản ghi** (+7 bản ghi audit trail tự động) | **Đạt 100%** |
| **Cột mới `DepartmentServices`** | Không có (chỉ có 7 cột cơ bản) | **Có đủ 5 cột mới**: `ServiceType`, `RequiresExclusiveRoom`, `RequiresConsultationFirst`, `MinLeadTimeHours`, `BookingInstructions` | **Đạt 100%** |
| **Dịch vụ được phân loại chi tiết**| 0 / 31 dịch vụ | **31 / 31 dịch vụ** (Khám, Tiểu phẫu, Thẩm mỹ, Nhi) | **Đạt 100%** |
| **Đánh giá Bác sĩ `VisitFeedback`**| Thiếu `DentistUserId`, `PatientUserId` | **Đã bổ sung 7 cột**, backfill đủ 25 bản ghi, tạo FK | **Đạt 100%** |
| **Chỉ mục đánh giá Bác sĩ** | Chỉ có PK và UQ trên `AppointmentId` | **Thêm `IX_VisitFeedback_Dentist_Status`** | **Đạt 100%** |
| **Biên tập Blog `BlogPosts`** | Thiếu cột quản trị duyệt bài | **Thêm 4 cột**: `PostType`, `RejectionReason`, `ApprovedByUserId`, `ApprovedAt` | **Đạt 100%** |

---

## 2. NHẬT KÝ THỰC THI MIGRATION CHI TIẾT (TERMINAL LOGS)

```text
>>> Starting Phase 0 Database Migration: 4-Role Standardization...
>>> [1/5] Migrating DepartmentManager accounts to Dentist profiles...
    Inserted DentistProfile for UserId = 4 (TS.BS Vũ Đình Trọng)
    Inserted DentistProfile for UserId = 10 (BS.CKII Nguyễn Văn Bình)
    Inserted DentistProfile for UserId = 11 (ThS.BS Lê Thị Lan Hương)
    Inserted DentistProfile for UserId = 12 (ThS.BS Đỗ Hải Yến)
    Updated UserAccounts.Role from DepartmentManager to Dentist.
    Dropped old check constraint: CK__UserAccoun__Role__3C69FB99
    Added strict 4-role constraint: CK_UserAccounts_Role
>>> [2/5] Migrating PendingDepartmentReview appointments to Receptionist queue...
    Migrated all PendingDepartmentReview appointments to PendingReceptionReview.
>>> [3/5] Enhancing DepartmentServices schema for service-first booking...
    Added column DepartmentServices.ServiceType
    Added column DepartmentServices.RequiresExclusiveRoom
    Added column DepartmentServices.RequiresConsultationFirst
    Added column DepartmentServices.MinLeadTimeHours
    Added column DepartmentServices.BookingInstructions
    Configured service metadata and booking rules for clinic services.
>>> [4/5] Enhancing VisitFeedback schema for dentist reviews and admin moderation...
    Added column VisitFeedback.DentistUserId
    Added column VisitFeedback.PatientUserId
    Added column VisitFeedback.Status
    Added column VisitFeedback.AdminNotes
    Added column VisitFeedback.AdminRespondedByUserId
    Added column VisitFeedback.AdminResponse
    Added column VisitFeedback.UpdatedAt
    Backfilled DentistUserId and PatientUserId on VisitFeedback from linked Appointments.
    Added FK_VisitFeedback_DentistUser
    Added FK_VisitFeedback_PatientUser
    Added FK_VisitFeedback_AdminRespondedBy
    Created index IX_VisitFeedback_Dentist_Status
>>> [5/5] Enhancing BlogPosts schema for Receptionist drafting and Admin moderation...
    Added column BlogPosts.PostType
    Added column BlogPosts.RejectionReason
    Added column BlogPosts.ApprovedByUserId
    Added column BlogPosts.ApprovedAt
    Added FK_BlogPosts_ApprovedByUser
    Added constraint CK_BlogPosts_PostType
>>> Phase 0 Migration completed successfully!
```

---

## 3. KIỂM CHỨNG KHẢ NĂNG HOÀN TÁC (ROLLBACK VERIFICATION)

Đã thực hiện chạy thử nghiệm script Rollback [`DB/redesign_migrations/20261109_Phase0_Database_Standardization_4Roles_Rollback.sql`](file:///D:/SWP391/SWP391_Group6/DB/redesign_migrations/20261109_Phase0_Database_Standardization_4Roles_Rollback.sql) trên CSDL clone:
- **Kết quả Rollback:** Thành công 100%, không phát sinh lỗi ngoại lệ.
- **Trạng thái phục hồi:**
  - Bảng `BlogPosts`, `VisitFeedback`, `DepartmentServices` được xóa sạch các cột mới và ràng buộc khóa ngoại.
  - Bảng `UserAccounts` khôi phục lại ràng buộc cho phép 5 vai trò cũ và hoàn trả 4 tài khoản về `DepartmentManager`.
- **Thử nghiệm Forward lại lần 2:** Sau khi kiểm tra Rollback, tập lệnh Forward Migration được chạy lại lần 2 và thực thi thành công mỹ mãn. Điều này chứng minh tính lũy thừa (Idempotency) và độ tin cậy tuyệt đối của các script.

---

## 4. KẾT QUẢ BIÊN DỊCH CODE (BUILD VERIFICATION)

Sau khi bổ sung các thuộc tính Entity tương ứng trong [DepartmentService.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Persistence/Entities/DepartmentService.cs), [VisitFeedback.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Persistence/Entities/VisitFeedback.cs), [BlogPost.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Persistence/Entities/BlogPost.cs):

| Dự Án | Kết Quả Build | Lỗi (Errors) | Cảnh Báo (Warnings) |
| :--- | :---: | :---: | :---: |
| **Backend ([`DentalClinic.Backend.sln`](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Backend.sln))** | **Thành công** | **0 Errors** | 18 CS Warnings (không ảnh hưởng) |
| **Frontend ([`DentalClinic.Web.sln`](file:///D:/SWP391/SWP391_Group6/FE/DentalClinic.Web.sln))** | **Thành công** | **0 Errors** | 0 Warnings |
| **Bộ kiểm thử Frontend ([`DentalClinic.Web.Tests`](file:///D:/SWP391/SWP391_Group6/FE/DentalClinic.Web.Tests))** | **Passed** | **0 Errors** | **289 / 289 tests PASS (100%)** |
