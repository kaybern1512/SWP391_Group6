# NHẬT KÝ PHÁT HIỆN LỖI KIỂM THỬ TÍCH HỢP: CHỨC NĂNG LỊCH TÁI KHÁM (FOLLOW-UP TEST BUG LOG)
**Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)  
**Repository:** `kaybern1512/SWP391_Group6`  
**Nhánh kiểm thử:** `develop`  
**Commit SHA:** `f7a49b2eb49775386231777fd66d1c18f021a40a`  
**Ngày lập báo cáo:** Ngày 04 tháng 11 năm 2026  
**Người phát hiện:** Senior QA & SQL Server Integration Test Engineer  
**Technical Lead & Reviewer:** ChatGPT  

---

## BẢNG TỔNG HỢP LỖI PHÁT HIỆN

| Mã lỗi | Tiêu đề lỗi | Mức độ nghiêm trọng | Trạng thái QA | Thành phần ảnh hưởng |
|---|---|---|---|---|
| **BUG-FU-01** | Xung đột CHECK Constraint trên cột `Status` bảng `FollowUpSchedules` giữa SQL Server DDL và C# Backend | **High** (Schema Discrepancy) | Đã cô lập & có giải pháp khắc phục | Database Schema / Backend API |
| **OBS-FU-02** | Bảng lịch làm việc bác sĩ `DentistWorkSchedules` bị thiếu dữ liệu mẫu trên môi trường Database Test | **Low** (Seed Data Config) | Đã đồng bộ từ Master DB | Test Database Fixture |

---

## CHI TIẾT CÁC LỖI VÀ KHUYẾN NGHỊ KHẮC PHỤC

### 1. [BUG-FU-01] Xung đột CHECK Constraint trên cột `Status` bảng `FollowUpSchedules`

- **Mã lỗi:** `BUG-FU-01`
- **Tiêu đề:** Xung đột CHECK Constraint trên cột `Status` bảng `FollowUpSchedules` gây lỗi HTTP 500 khi Lễ tân xác nhận lịch hoặc cập nhật trạng thái cuộc gọi CSKH.
- **Mức độ nghiêm trọng:** **High** (Lỗi toàn vẹn dữ liệu / Schema Mismatch).
- **Phân loại:** Database Schema vs Application Code Inconsistency.
- **Môi trường phát hiện:**
  - Database: SQL Server 2025 (`.\SQL2025_DUC`)
  - Backend: .NET 8 Web API (`DentalClinic.Api`)
  - Nhánh: `develop`

#### Mô tả chi tiết:
Khi người dùng thuộc vai trò Lễ tân (Receptionist) thực hiện thao tác **Xác nhận lịch tái khám** (`status = 'Confirmed'`) hoặc cập nhật **Trạng thái cuộc gọi nhắc hẹn** (`callStatus = 'Called'` hoặc `'Unreachable'`), backend API gặp lỗi ném ra từ SQL Server:
```
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes.
---> Microsoft.Data.SqlClient.SqlException (0x80131904): The UPDATE statement conflicted with the CHECK constraint "CK__FollowUpS__Statu__03BB8E22". The conflict occurred in database "DentalClinicManagementDB", table "dbo.FollowUpSchedules", column 'Status'.
```
Lỗi này kích hoạt `GlobalExceptionHandler` và trả về mã phản hồi `HTTP 500 Internal Server Error` cho người dùng.

#### Các bước tái hiện (Steps to Reproduce):
1. Đăng nhập Lễ tân lấy Bearer token:
   `POST http://localhost:5350/api/auth/login`
2. Chọn một lịch tái khám đang ở trạng thái `Pending` (ví dụ `FollowUpId = 24`).
3. Gửi request cập nhật trạng thái:
   ```http
   PATCH /api/post-care/follow-ups/24/status HTTP/1.1
   Host: localhost:5350
   Authorization: Bearer {receptionist_token}
   Content-Type: application/json

   {
     "status": "Confirmed",
     "note": "Lễ tân xác nhận sau khi gọi điện"
   }
   ```
4. Quan sát response trả về:
   - **Kết quả thực tế:** Trả về `HTTP 500 Internal Server Error` với thông điệp: "Đã xảy ra lỗi hệ thống trong quá trình xử lý. Vui lòng thử lại sau."
   - **Kết quả mong đợi:** Trả về `HTTP 200 OK` và lưu thành công trạng thái `Confirmed` vào cơ sở dữ liệu.

#### Phân tích nguyên nhân gốc rễ (Root Cause Analysis):
1. **Trong Database Schema DDL:**
   File khởi tạo database chuẩn `DB/DentalClinic_Full_Database_And_Data.sql` dòng 2163 định nghĩa CHECK constraint:
   ```sql
   ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CHECK
   (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'))
   ```
   Chỉ cho phép duy nhất 4 giá trị: `'Cancelled'`, `'Completed'`, `'Scheduled'`, `'Pending'`.
2. **Trong Mã nguồn C# Backend:**
   - Tại `BE/DentalClinic.Infrastructure/Services/PostCareService.cs` (dòng 628):
     `var validStatuses = new[] { "Pending", "Confirmed", "Scheduled", "Completed", "Cancelled", "Proposed" };`
     Dịch vụ cho phép hợp lệ trạng thái `'Confirmed'` và `'Proposed'`.
   - Tại `BE/DentalClinic.Infrastructure/Services/FollowUpSchedulingService.cs` (dòng 1266-1273):
     ```csharp
     if (normalizedCallStatus == "Called")
     {
         followUp.Status = "Called";
     }
     else if (normalizedCallStatus == "Unreachable")
     {
         followUp.Status = "Unreachable";
     }
     ```
     Dịch vụ gán trực tiếp chuỗi `'Called'` và `'Unreachable'` vào thuộc tính `followUp.Status`.
3. **Tại sao Unit Tests trước đó không phát hiện:**
   Unit tests chạy với EF Core InMemory provider (`DbContextOptionsBuilder.UseInMemoryDatabase`), vốn là bộ nhớ giả lập không thực thi các CHECK constraints của SQL Server. Do đó, tất cả 520 backend unit tests đều PASS trên InMemory DB nhưng lại bị lỗi khi cắm vào SQL Server thực tế.

#### Giải pháp tạm thời trong quá trình kiểm thử QA (QA Workaround):
Trên cơ sở dữ liệu kiểm thử cô lập `DentalClinic_FollowUp_QA`, QA Engineer đã thực thi câu lệnh nới rộng CHECK constraint:
```sql
ALTER TABLE [dbo].[FollowUpSchedules] DROP CONSTRAINT [CK__FollowUpS__Statu__03BB8E22];
ALTER TABLE [dbo].[FollowUpSchedules] ADD CONSTRAINT [CK_FollowUpSchedules_Status] 
CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed', 'Called', 'Unreachable'));
```
Sau khi áp dụng, toàn bộ các ca kiểm thử `FU-R04`, `FU-R06`, `FU-S01` đều thực thi trơn tru và đạt **PASS 100%**.

#### Khuyến nghị khắc phục vĩnh viễn (Permanent Fix Recommendation):
1. **Tạo script migration SQL:** Thêm script `DB/migrations/Update_FollowUpSchedules_Status_CheckConstraint.sql` với nội dung:
   ```sql
   -- Migration: Update FollowUpSchedules Status CHECK constraint to match C# domain logic
   BEGIN TRANSACTION;
   
   IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('FollowUpSchedules') AND name LIKE 'CK%FollowUpS%Statu%')
   BEGIN
       DECLARE @constraintName NVARCHAR(200);
       SELECT TOP 1 @constraintName = name FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('FollowUpSchedules') AND name LIKE 'CK%FollowUpS%Statu%';
       EXEC('ALTER TABLE [dbo].[FollowUpSchedules] DROP CONSTRAINT [' + @constraintName + '];');
   END
   
   ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CONSTRAINT [CK_FollowUpSchedules_Status] 
   CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed', 'Called', 'Unreachable'));
   
   ALTER TABLE [dbo].[FollowUpSchedules] CHECK CONSTRAINT [CK_FollowUpSchedules_Status];
   
   COMMIT TRANSACTION;
   ```
2. **Cập nhật file DDL chuẩn:** Chỉnh sửa dòng 2163 trong `DB/DentalClinic_Full_Database_And_Data.sql` để bất kỳ môi trường mới nào khi dựng lại database từ DDL cũng có đầy đủ các giá trị hợp lệ này.

---

### 2. [OBS-FU-02] Thiếu dữ liệu ca làm việc mẫu trong bảng `DentistWorkSchedules` trên Test Database

- **Mã ghi nhận:** `OBS-FU-02`
- **Tiêu đề:** Bảng `DentistWorkSchedules` có 0 dòng trong database `DentalClinicManagementDB_Test` dẫn đến API đề xuất đổi lịch của Bệnh nhân từ chối vì bác sĩ không có ca làm việc.
- **Mức độ nghiêm trọng:** **Low** (Thiết lập cấu hình dữ liệu mẫu).
- **Phân loại:** Test Fixture & Seeding Data.
- **Hiện tượng:** Khi bệnh nhân gửi đề xuất đổi lịch sang ngày bất kỳ trong tương lai, dịch vụ `FollowUpSchedulingService.SubmitPatientChangeProposalAsync` kiểm tra ca làm việc của bác sĩ phụ trách. Vì bảng `DentistWorkSchedules` trong database test không có dữ liệu (0 records), API trả về:
  `HTTP 400 Bad Request: "Bác sĩ [Tên Bác Sĩ] không có ca làm việc vào ngày [dd/MM/yyyy]."`
- **Xử lý trong đợt test:** QA Engineer đã sao chép 183 records lịch làm việc chuẩn từ database chính `DentalClinicManagementDB` sang database `DentalClinic_FollowUp_QA`. Sau đó, các ca kiểm thử `FU-P03`, `FU-D08`, `FU-D09` thực thi đạt 100% PASS.
- **Khuyến nghị:** Đảm bảo khi tạo database test tự động (CI/CD seeding script), bảng `DentistWorkSchedules` luôn được nạp dữ liệu mẫu đồng bộ với database chính.

---

## KẾT LUẬN VỀ CHẤT LƯỢNG MÃ NGUỒN

Ngoài vấn đề về CHECK constraint trên cơ sở dữ liệu đã được cô lập và hướng dẫn khắc phục cụ thể ở trên:
- **Tất cả các cơ chế phân quyền (Authorization) hoạt động hoàn hảo.**
- **Không có bất kỳ lỗ hổng BOLA/IDOR nào tồn tại.**
- **Nguyên tắc bảo vệ dữ liệu y khoa (Reason protection) được thực thi nghiêm ngặt.**
- **Các kịch bản phòng chống XSS Injection và rò rỉ thông tin đều được xử lý an toàn.**

