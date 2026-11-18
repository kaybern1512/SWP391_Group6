# Hướng Dẫn Cấu Hình Cơ Sở Dữ Liệu DentalCare V2

Tài liệu này hướng dẫn cách khởi tạo, nạp dữ liệu mẫu và cấu hình kết nối cơ sở dữ liệu cho dự án **DentalCare SWP391** trên Microsoft SQL Server 2022 hoặc SQL Server Express (`.\SQLEXPRESS`).

---

## 1. NGUYÊN TẮC QUẢN LÝ DỮ LIỆU CHUẨN

Quy trình dựng database DentalCare được tối giản và chuẩn hóa thành **đúng 2 file SQL chính thức**:

1. **`DB/DentalCare_Schema.sql`**: Chứa toàn bộ câu lệnh `DDL` tạo cấu trúc bảng, khóa chính, khóa ngoại, chỉ mục (Index) và ràng buộc toàn vẹn (Check Constraints) cho **38 bảng vật lý** (37 bảng nghiệp vụ + `__EFMigrationsHistory`).
2. **`DB/DentalCare_Seed.sql`**: Chứa toàn bộ câu lệnh `DML` nạp dữ liệu nền tảng cho 3 chuyên khoa, 12 dịch vụ kỹ thuật, 10 nhân sự (1 Admin, 6 Bác sĩ, 3 Lễ tân), 32 bệnh nhân, 187 ca trực nhân sự, và 62 lịch hẹn lâm sàng chuẩn hóa.

> [!IMPORTANT]
> **Không cần chạy thêm bất kỳ file vá lỗi hay script patch nào.** Hai file SQL trên đã bao gồm toàn bộ các bản sửa lỗi bảng mã Unicode tiếng Việt, hotfix ánh xạ chỉ định X-quang, và sửa bất nhất lịch trực lịch sử.

---

## 2. QUY TRÌNH KHỞI TẠO DATABASE AN TOÀN

### Bước 1: Tạo Database Mới trên SQL Server
Mở **SQL Server Management Studio (SSMS)** hoặc **Azure Data Studio**, kết nối tới `.\SQLEXPRESS` và thực hiện lệnh:

```sql
-- Đặt tên database demo độc lập, không ghi đè lên database khác
CREATE DATABASE DentalCare_SWP391_DEMO_V2_TEST
COLLATE Latin1_General_CI_AS;
GO
```

### Bước 2: Thực thi Schema Bootstrap
Chạy file script `DB/DentalCare_Schema.sql` trong ngữ cảnh database vừa tạo:

```powershell
sqlcmd -S .\SQLEXPRESS -d DentalCare_SWP391_DEMO_V2_TEST -i "D:\SWP391\DentalCare-Integration\DB\DentalCare_Schema.sql"
```

### Bước 3: Nạp Dữ liệu Mẫu (Seed Bootstrap) với Mật khẩu Demo
File `DB/DentalCare_Seed.sql` hỗ trợ nạp mật khẩu an toàn thông qua biến phiên `SESSION_CONTEXT('DentalCareDemoPasswordHash')`. Để kích hoạt đăng nhập cho toàn bộ 42 tài khoản demo với mật khẩu dùng thử chuẩn (`DentalCare@2026!`), chạy lệnh:

```powershell
$sql = @"
EXEC sp_set_session_context @key = N'DentalCareDemoPasswordHash', @value = 'AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==';
:r "D:\SWP391\DentalCare-Integration\DB\DentalCare_Seed.sql"
"@
$sql | sqlcmd -S .\SQLEXPRESS -d DentalCare_SWP391_DEMO_V2_TEST
```

> [!NOTE]
> Hash trên được sinh bởi chuẩn `Microsoft.AspNetCore.Identity.PasswordHasher<UserAccount>` tương ứng với mật khẩu `DentalCare@2026!`. Nếu không truyền session context, tài khoản sẽ có `PasswordHash = NULL` để đảm bảo an toàn tuyệt đối khi triển khai trên môi trường bảo mật cao.

---

## 3. CẤU HÌNH CONNECTION STRING

Cập nhật chuỗi kết nối trong file cấu hình Backend (`BE/DentalClinic.Api/appsettings.json`) hoặc truyền qua biến môi trường:

```json
{
  "ConnectionStrings": {
    "DentalClinicDatabase": "Server=.\\SQLEXPRESS;Database=DentalCare_SWP391_DEMO_V2_TEST;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

Hoặc thiết lập biến môi trường trước khi chạy `dotnet run`:
```powershell
$env:ConnectionStrings__DentalClinicDatabase = "Server=.\SQLEXPRESS;Database=DentalCare_SWP391_DEMO_V2_TEST;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

---

## 4. KIỂM TRA TÍNH TOÀN VẸN SAU KHI NẠP

Chạy truy vấn kiểm tra số lượng dòng thực tế của các bảng cốt lõi:

```sql
SELECT
    (SELECT COUNT(*) FROM UserAccounts) AS TotalUsers,          -- Kỳ vọng: 42
    (SELECT COUNT(*) FROM Departments) AS TotalDepartments,     -- Kỳ vọng: 3
    (SELECT COUNT(*) FROM DepartmentServices) AS TotalServices, -- Kỳ vọng: 12
    (SELECT COUNT(*) FROM Appointments) AS TotalAppointments,   -- Kỳ vọng: 62
    (SELECT COUNT(*) FROM AppointmentServices) AS TotalApptSvc, -- Kỳ vọng: 84
    (SELECT COUNT(*) FROM Invoices) AS TotalInvoices,           -- Kỳ vọng: 48
    (SELECT COUNT(*) FROM Payments) AS TotalPayments;           -- Kỳ vọng: 44
```

Kiểm tra số lượng ca khám ngoài ca trực (kỳ vọng bằng **0**):
```sql
SELECT COUNT(*) AS OutOfShiftCount
FROM AppointmentServices aps
JOIN StaffWorkSchedules ws
  ON aps.AssignedDentistUserId = ws.StaffUserId
 AND aps.ServiceDate = ws.WorkDate
WHERE (CAST(aps.ScheduledStart AS TIME) < ws.StartTime OR CAST(aps.ScheduledEnd AS TIME) > ws.EndTime)
  AND NOT EXISTS (
      SELECT 1 FROM StaffWorkSchedules ws2
      WHERE ws2.StaffUserId = aps.AssignedDentistUserId
        AND ws2.WorkDate = aps.ServiceDate
        AND CAST(aps.ScheduledStart AS TIME) >= ws2.StartTime
        AND CAST(aps.ScheduledEnd AS TIME) <= ws2.EndTime
  );
```

---

## 5. CÁC LỖI THƯỜNG GẶP & CÁCH KHẮC PHỤC

| Lỗi gặp phải | Nguyên nhân | Cách khắc phục |
| :--- | :--- | :--- |
| `Cannot connect to .\SQLEXPRESS` | Dịch vụ SQL Server chưa bật | Chạy `Start-Service 'MSSQL$SQLEXPRESS'` trong PowerShell với quyền Administrator. |
| `Login failed for user` | Quyền Windows Authentication chưa cấp cho user hiện tại | Đảm bảo Windows user đang đăng nhập có quyền `sysadmin` hoặc `db_owner` trên SQL Server. |
| `Cannot open database requested by the login` | Tên database trong ConnectionString chưa được tạo | Thực hiện Bước 1 tạo database trước khi chạy lệnh nạp dữ liệu. |
| `String or binary data would be truncated` | Chạy nhầm script SQL bản cũ chưa fix encoding | Đảm bảo chỉ dùng đúng 2 file `DB/DentalCare_Schema.sql` và `DB/DentalCare_Seed.sql`. |
| Cột hiển thị chữ tiếng Việt bị dấu `?` | Nạp file không qua UTF-8 encoding | Sử dụng lệnh `sqlcmd` với cờ UTF-8 hoặc mở và thực thi trực tiếp bằng SSMS/Azure Data Studio hỗ trợ UTF-8 with BOM. |
