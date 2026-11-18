# DENTALCARE CLINIC MANAGEMENT SYSTEM — DATABASE SETUP GUIDE
**Project:** DentalCare Clinic Management System (SWP391 Group 6)  
**Target RDBMS:** Microsoft SQL Server 2017+ / 2019 / 2022 / SQL Server Express / LocalDB  
**Canonical Schema:** Exactly 38 physical tables (37 business tables + 1 `__EFMigrationsHistory`)  

---

## 1. TỔNG QUAN VÀ NGUYÊN TẮC AN TOÀN DỮ LIỆU

> [!IMPORTANT]
> **Quy Tắc An Toàn Tuyệt Đối (Safety Boundary):**
> 1. Script DDL `DB/DentalCare_Schema.sql` được thiết kế **độc lập môi trường** (Environment-agnostic). File này **không** chứa lệnh `CREATE DATABASE`, `DROP DATABASE` hay `USE [Database]`.
> 2. Script có cơ chế tự bảo vệ: Sẽ tự động hủy (`RAISERROR` + `SET NOEXEC ON`) nếu chạy trên cơ sở dữ liệu hệ thống (`master`, `model`, `msdb`, `tempdb`) hoặc cơ sở dữ liệu đã có sẵn bảng.
> 3. Tuyệt đối **không** can thiệp, xóa hoặc sửa đổi cơ sở dữ liệu bảo vệ (`DentalCare_SWP391` và `DentalCare_SWP391_Refactor_UAT`).
> 4. Môi trường kiểm thử / chấm điểm mặc định sử dụng database: `DentalCare_SWP391_UAT_Fixture_TEST` trên instance `.\SQLEXPRESS`.

---

## 2. HƯỚNG DẪN THIẾT LẬP CƠ SỞ DỮ LIỆU MỚI (TỪNG BƯỚC)

Nếu giảng viên hoặc hội đồng muốn tạo một cơ sở dữ liệu mới tinh (ví dụ: `DentalCare_SWP391_UAT_Fixture_TEST` hoặc `DentalCare_Demo`), thực hiện theo 3 bước sau:

### Bước 2.1: Tạo Database Mới Trong SQL Server

Mở **SQL Server Management Studio (SSMS)**, **Azure Data Studio**, hoặc **PowerShell / sqlcmd**, kết nối tới SQL Server instance (`.\SQLEXPRESS` hoặc `localhost`):

```sql
-- Tạo database mới với bảng mã UTF-8 khuyến nghị
CREATE DATABASE [DentalCare_SWP391_UAT_Fixture_TEST]
COLLATE Latin1_General_100_CI_AS_SC_UTF8;
GO
```
*(Nếu phiên bản SQL Server cũ không hỗ trợ UTF8 Collation, collation mặc định của SQL Server vẫn hoạt động bình thường nhờ các trường tiếng Việt đều sử dụng kiểu dữ liệu `NVARCHAR` chuẩn).*

---

### Bước 2.2: Khởi Tạo Schema Chuẩn 38 Bảng

Chạy script standalone DDL trực tiếp vào database vừa tạo:

#### Cách 1: Sử dụng Command Line (`sqlcmd`)
Mở PowerShell tại thư mục gốc repository:
```powershell
sqlcmd -S .\SQLEXPRESS -d DentalCare_SWP391_DEMO_V2_TEST -E -i "DB\DentalCare_Schema.sql"
```

#### Cách 2: Sử dụng SSMS (SQL Server Management Studio)
1. Mở file `DB\DentalCare_Schema.sql` trong SSMS.
2. Chọn database mục tiêu trong dropdown menu: `DentalCare_SWP391_DEMO_V2_TEST`.
3. Bấm **Execute** (hoặc nhấn `F5`).
4. Kết quả thông báo: `Command(s) completed successfully.`

---

### Bước 2.3: Nạp Dữ Liệu Khởi Tạo (Seed Data)

Nha khoa DentalCare cung cấp sẵn bộ dữ liệu mẫu với đầy đủ 4 vai trò, danh mục dịch vụ, ca khám và thiết bị:

#### Nạp Seed Data Qua `sqlcmd`:
```powershell
sqlcmd -S .\SQLEXPRESS -d DentalCare_SWP391_DEMO_V2_TEST -E -i "DB\DentalCare_Seed.sql"
```

---

### Bước 2.4: Nâng Cấp In-Place Cho Database Hiện Hữu (Tùy Chọn)

Nếu thành viên trong nhóm hoặc hội đồng đã có sẵn cơ sở dữ liệu từ trước và muốn cập nhật các ràng buộc trạng thái mới (`AwaitingPatientResponse`, `PendingDepartmentReview`, `Rejected`, `Withdrawn`, `Expired`) mà **không muốn xóa database hoặc mất dữ liệu cũ**, hãy chạy đoạn script sau:

```sql
-- Cập nhật đồng bộ CHECK constraints cho Appointments & AppointmentServices
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Appointments_Status')
    ALTER TABLE dbo.Appointments DROP CONSTRAINT CK_Appointments_Status;
GO

ALTER TABLE dbo.Appointments ADD CONSTRAINT [CK_Appointments_Status] CHECK (
    [Status] IN (
        'PendingReceptionReview', 'PendingDepartmentReview', 'AwaitingPatientResponse',
        'Confirmed', 'CheckedIn', 'InProgress', 'Waiting', 'Called',
        'Completed', 'Cancelled', 'NoShow', 'Rejected', 'Withdrawn', 'Expired'
    )
);
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AppointmentServices_Status')
    ALTER TABLE dbo.AppointmentServices DROP CONSTRAINT CK_AppointmentServices_Status;
GO

ALTER TABLE dbo.AppointmentServices ADD CONSTRAINT [CK_AppointmentServices_Status] CHECK (
    [Status] IN (
        'PendingReceptionReview', 'PendingDepartmentReview', 'AwaitingPatientResponse',
        'Confirmed', 'CheckedIn', 'Waiting', 'Called',
        'InService', 'Completed', 'Cancelled', 'NoShow', 'Rejected', 'Withdrawn', 'Expired'
    )
);
GO
```

---

## 3. KIỂM TRA VÀ XÁC MINH CƠ SỞ DỮ LIỆU

Sau khi chạy xong schema và seed data, chạy câu lệnh truy vấn kiểm tra:

```sql
USE [DentalCare_SWP391_UAT_Fixture_TEST];
GO

-- 1. Kiểm tra tổng số bảng vật lý (Phải đúng 38 bảng)
SELECT COUNT(*) AS [TotalPhysicalTables] 
FROM sys.tables 
WHERE is_ms_shipped = 0;

-- 2. Kiểm tra danh sách tài khoản theo 4 vai trò
SELECT 
    u.UserId, 
    COALESCE(p.FullName, s.FullName, d.FullName, N'(Chưa cập nhật)') AS [FullName],
    u.Email, 
    u.Role, 
    u.Status, 
    u.CreatedAt
FROM dbo.UserAccounts u
LEFT JOIN dbo.PatientProfiles p ON u.UserId = p.UserId
LEFT JOIN dbo.StaffProfiles s ON u.UserId = s.UserId
LEFT JOIN dbo.DentistProfiles d ON u.UserId = d.UserId
ORDER BY u.UserId;

-- 3. Kiểm tra danh mục dịch vụ chuyên khoa
SELECT 
    d.Name AS [DepartmentName],
    s.ServiceCode,
    s.ServiceName,
    s.Price,
    s.DurationMinutes,
    s.Status
FROM dbo.DepartmentServices s
INNER JOIN dbo.Departments d ON s.DepartmentId = d.DepartmentId
ORDER BY d.DepartmentId, s.Price ASC;
```

**Kỳ vọng kết quả:**
- `TotalPhysicalTables`: **38** bảng.
- Danh sách Roles chỉ chứa đúng 4 vai trò: `Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`.
- Các dịch vụ khám, nhổ răng, trám răng, chụp X-quang hiển thị đầy đủ trạng thái `Active`.

---

## 4. DANH MỤC 38 BẢNG VẬT LÝ THEO PHÂN HỆ

| STT | Tên Bảng (Physical Table) | Phân Hệ Nghiệp Vụ | Chức Năng |
|:---:|:---|:---|:---|
| 1 | `__EFMigrationsHistory` | Hệ thống | Quản lý phiên bản Entity Framework Core |
| 2 | `ClinicProfile` | Phòng khám | Thông tin pháp lý, giờ mở/đóng cửa phòng khám |
| 3 | `UserAccounts` | Identity & RBAC | Tài khoản người dùng định danh duy nhất (4 vai trò) |
| 4 | `AuditLogs` | Kiểm toán | Nhật ký thao tác nhạy cảm hệ thống |
| 5 | `Departments` | Chuyên khoa | Danh mục khoa chuyên môn lâm sàng |
| 6 | `PatientProfiles` | Hồ sơ người dùng | Thông tin chi tiết bệnh nhân (Họ tên, ngày sinh, địa chỉ) |
| 7 | `StaffProfiles` | Hồ sơ nhân sự | Thông tin nhân viên tiếp tân & quản trị |
| 8 | `DentistProfiles` | Hồ sơ bác sĩ | Hồ sơ bác sĩ điều trị, bằng cấp, chuyên khoa |
| 9 | `Rooms` | Cơ sở vật chất | Danh mục phòng khám, phòng chụp X-quang R003 |
| 10 | `DentalChairs` | Cơ sở vật chất | Ghế nha khoa thuộc từng phòng khám |
| 11 | `DepartmentServices` | Dịch vụ | Danh mục kỹ thuật điều trị, mã dịch vụ và giá |
| 12 | `ServiceCombos` | Gói dịch vụ | Danh mục gói combo điều trị tích hợp |
| 13 | `ServiceComboItems` | Gói dịch vụ | Chi tiết dịch vụ cấu thành gói combo |
| 14 | `StaffWorkSchedules` | Lịch trực | Phân công ca làm việc của Bác sĩ và Tiếp tân |
| 15 | `StaffLeaveRequests` | Nghỉ phép | Đơn xin nghỉ phép và trạng thái phê duyệt |
| 16 | `StaffAttendanceRecords` | Điểm danh | Chấm công vào/ra theo ca làm việc |
| 17 | `DentalMedicalRecords` | Hồ sơ bệnh án | Bệnh án điện tử nha khoa (EMR) |
| 18 | `TreatmentPlans` | Kế hoạch điều trị | Kế hoạch điều trị tổng thể theo lộ trình |
| 19 | `TreatmentPlanItems` | Kế hoạch điều trị | Chi tiết các giai đoạn điều trị |
| 20 | `Appointments` | Đặt lịch khám | Ca khám chính (1:N dịch vụ, liên kết bệnh nhân) |
| 21 | `AppointmentServices` | Đặt lịch khám | Dịch vụ được chỉ định trong ca khám (phòng, ghế, bác sĩ) |
| 22 | `ClinicalEntries` | Lâm sàng | Ghi nhận chẩn đoán và triệu chứng lâm sàng |
| 23 | `OdontogramEntries` | Nha đồ | Sơ đồ răng điện tử (trạng thái từng răng) |
| 24 | `TreatmentSessions` | Điều trị | Phiên điều trị thực tế của bác sĩ |
| 25 | `PerformedServices` | Dịch vụ thực hiện | Ghi nhận dịch vụ đã hoàn thành trong phiên khám |
| 26 | `DepartmentReferrals` | Chuyển khoa | Phiếu chuyển tuyến nội bộ giữa các chuyên khoa |
| 27 | `Diagnostics` | Chẩn đoán hình ảnh | Quản lý chỉ định chụp, kết quả và URL phim X-quang |
| 28 | `Medicines` | Dược phẩm | Danh mục thuốc, hoạt chất, quy cách và tồn kho |
| 29 | `Prescriptions` | Đơn thuốc | Toa thuốc do bác sĩ kê theo ca khám |
| 30 | `PrescriptionItems` | Đơn thuốc | Chi tiết tên thuốc, số lượng, liều dùng |
| 31 | `Invoices` | Tài chính | Hóa đơn viện phí (tổng tiền, giảm giá, trạng thái) |
| 32 | `InvoiceItems` | Tài chính | Dòng chi tiết dịch vụ/thuốc tính phí trên hóa đơn |
| 33 | `Payments` | Tài chính | Giao dịch thanh toán tiền mặt hoặc VietQR |
| 34 | `FollowUpSchedules` | Tái khám | Nhắc lịch hẹn tái khám sau điều trị |
| 35 | `VisitFeedback` | Đánh giá | Phản hồi chất lượng phục vụ của bệnh nhân |
| 36 | `Notifications` | Thông báo | Thông báo người dùng và sự kiện hệ thống |
| 37 | `BlogCategories` | Truyền thông | Chuyên mục tin tức nha khoa |
| 38 | `BlogPosts` | Truyền thông | Bài viết kiến thức chăm sóc răng miệng |
| 37 | `FollowUpAppointments` | Tái khám | Nhắc lịch tái khám sau thủ thuật |
| 38 | `BlogPosts` | Tin tức | Bài viết kiến thức nha khoa |

---

## 5. CẤU HÌNH CONNECTION STRING

Sau khi chuẩn bị cơ sở dữ liệu, cập nhật chuỗi kết nối trong file cấu hình Backend (`BE\DentalClinic.Api\appsettings.json` hoặc file môi trường `env_server`):

```json
"ConnectionStrings": {
  "DentalClinicDatabase": "Server=.\\SQLEXPRESS;Database=DentalCare_SWP391_UAT_Fixture_TEST;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

> [!TIP]
> - Nếu sử dụng **SQL Server mặc định (Default Instance)** không có tên `SQLEXPRESS`, đổi `Server=.\\SQLEXPRESS` thành `Server=localhost` hoặc `Server=.`.
> - Nếu sử dụng **SQL Server Authentication (sa)**:
>   `Server=localhost;Database=DentalCare_SWP391_UAT_Fixture_TEST;User Id=sa;Password=YourPassword;TrustServerCertificate=True;MultipleActiveResultSets=true`
> - Đảm bảo cờ `TrustServerCertificate=True` luôn được bật để tránh lỗi từ chối SSL certificate tự ký trên máy phát triển cục bộ.
