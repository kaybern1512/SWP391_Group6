# HƯỚNG DẪN VẬN HÀNH VÀ KHỞI ĐỘNG HỆ THỐNG DENTALCARE
## ZERO-AMBIGUITY OPERATIONAL & DEPLOYMENT STARTUP GUIDE

- **Hệ thống:** DentalCare Clinic Management System
- **Nhóm phát triển:** SWP391 - Group 6
- **Công nghệ chính:** ASP.NET Core 8 Web API + ASP.NET Core 8 MVC + SQL Server EF Core 8
- **Mục đích:** Tài liệu hướng dẫn cài đặt, khởi động, kiểm tra và khắc phục sự cố phục vụ buổi bảo vệ đồ án tốt nghiệp và bàn giao vận hành.
- **Trạng thái hệ thống:** **SẴN SÀNG TRÌNH DIỄN CÓ KIỂM SOÁT (READY FOR CONTROLLED DEMO)**

---

## 1. YÊU CẦU MÔI TRƯỜNG & HẠ TẦNG (SYSTEM PREREQUISITES)

| Thành phần | Phiên bản yêu cầu | Ghi chú & Kiểm tra |
|:---|:---|:---|
| **Hệ Điều Hành** | Windows 10 / 11 64-bit | Môi trường phát triển & kiểm thử chính thức |
| **.NET SDK** | .NET 8.0 SDK (8.0.x hoặc 9.0 tương thích) | Kiểm tra bằng lệnh: `dotnet --version` |
| **SQL Server** | SQL Server 2019 / 2022 Express | Instance: `DESKTOP-1TUBGEU\SQLEXPRESS` (Windows Authentication) |
| **Node.js** | Node.js v18.x trở lên | Dùng chạy bộ kiểm thử tự động Puppeteer E2E |
| **Trình duyệt** | Google Chrome (bản mới nhất) | Phục vụ người dùng và Puppeteer Headless runner |

---

## 2. KIỂM TRA TRẠNG THÁI CƠ SỞ DỮ LIỆU (DATABASE STATE)

Database chính thức phục vụ trình diễn: **`DentalClinicManagementDB`**

### 2.1. Cấu hình chuỗi kết nối (`BE/DentalClinic.Api/appsettings.json`)
```json
{
  "ConnectionStrings": {
    "DentalClinicDatabase": "Server=DESKTOP-1TUBGEU\\SQLEXPRESS;Database=DentalClinicManagementDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=false"
  }
}
```

### 2.2. Kiểm tra tính toàn vẹn của Migration
Bản migration an toàn đã được triển khai hoàn tất trên cơ sở dữ liệu:
- **Tệp script triển khai:** `docs/system-audit/migrations/20261031_AddQueueNumberUniqueIndex_And_RoomExclusivity.sql`
- **Cột phân loại phòng:** `Rooms.IsExclusive` (BIT NOT NULL, DEFAULT 0):
  - **Phòng Chia Sẻ Đa Ghế (`IsExclusive = 0`):**
    - `ROOM-101`: Phòng Khám Răng Tổng Quát 101 (3 ghế nha khoa)
    - `ROOM-102`: Phòng Điều Trị Răng Tổng Quát 102 (2 ghế nha khoa)
    - `ROOM-301`: Phòng Chỉnh Nha & Thẩm Mỹ 301 (2 ghế nha khoa)
    - `ROOM-401`: Phòng Khám Răng Trẻ Em Vui Nhộn 401 (2 ghế nha khoa)
  - **Phòng Sử Dụng Độc Quyền (`IsExclusive = 1`):**
    - `ROOM-201`: Phòng Phẫu Thuật & Implant 201 (1 ca/thời điểm)
    - `ROOM-202`: Phòng Cấy Ghép Implant Kỹ Thuật Cao 202
    - `ROOM-302`: Phòng Thẩm Mỹ Nụ Cười & Laser 302
    - `ROOM-402`: Phòng Điều Trị Nha Nhi Hoàng Gia 402
- **Cột tính toán ngày:** `Appointments.ScheduledDate` (`CAST(ScheduledStart AS DATE) PERSISTED`).
- **Chỉ mục duy nhất có điều kiện:** `[dbo].[Appointments].[UQ_Appointments_Department_Date_QueueNumber]` trên `(DepartmentId, ScheduledDate, QueueNumber)` với bộ lọc:
  ```sql
  ([DepartmentId] IS NOT NULL AND [ScheduledStart] IS NOT NULL AND [QueueNumber] IS NOT NULL AND [QueueNumber]<>'')
  ```
  *(Lưu ý: Chỉ mục không chứa điều kiện `Status <> 'Cancelled'` để đảm bảo số thứ tự khám đã cấp trong ngày không bị cấp trùng ngay cả khi ca khám bị hủy).*

Lệnh kiểm tra nhanh trạng thái Database:
```powershell
sqlcmd -S "DESKTOP-1TUBGEU\SQLEXPRESS" -E -d "DentalClinicManagementDB" -Q "SELECT COUNT(*) AS AppointmentsCount FROM Appointments; SELECT COUNT(*) AS ExclusiveRooms FROM Rooms WHERE IsExclusive = 1;"
```

---

## 3. CÁC BƯỚC KHỞI ĐỘNG HỆ THỐNG (STEP-BY-STEP STARTUP)

Mở 2 cửa sổ terminal PowerShell riêng biệt tại thư mục gốc dự án (`D:\SWP391\SWP391_Group6`):

### Bước 1: Khởi động Backend API
Tại Terminal 1:
```powershell
cd D:\SWP391\SWP391_Group6\BE\DentalClinic.Api
dotnet run --launch-profile "https"
```
- **Cổng lắng nghe:**
  - HTTPS: `https://localhost:7350`
  - HTTP: `http://localhost:5350`
- **Kiểm tra Swagger API:** Truy cập trình duyệt tại `https://localhost:7350/swagger` để kiểm tra toàn bộ API endpoints đã sẵn sàng.

### Bước 2: Khởi động Frontend Web MVC
Tại Terminal 2:
```powershell
cd D:\SWP391\SWP391_Group6\FE\DentalClinic.Web
dotnet run --launch-profile "https"
```
- **Cổng lắng nghe:**
  - HTTPS: `https://localhost:7129`
  - HTTP: `http://localhost:5049`
- **Giao diện người dùng:** Mở trình duyệt truy cập `https://localhost:7129`. Màn hình Trang chủ DentalCare sẽ xuất hiện.

---

## 4. BẢNG TÀI KHOẢN ĐĂNG NHẬP THEO PHÂN QUYỀN (DEMO ACCOUNTS)

| Vai trò | Email đăng nhập | Mật khẩu mặc định | Ghi chú vai trò |
|:---|:---|:---|:---|
| **Bệnh Nhân (Patient)** | `patient1@dentalcare.com` | `Password123@` | Bệnh nhân chính thức (PAT-202610-0001) |
| **Lễ Tân (Receptionist)** | `receptionist@dentalcare.com` | `Password123@` | Tiếp nhận, chuyển khoa, check-in, thu ngân |
| **Trưởng Khoa (DepartmentManager)** | `manager1@dentalcare.com` | `Password123@` | Trưởng Khoa Khám Tổng Quát |
| **Bác Sĩ (Dentist)** | `dentist@dentalcare.com` | `Password123@` | BS. Trần Văn Hùng (Khoa Tổng Quát) |
| **Quản Trị Viên (SystemAdmin)** | `admin@dentalcare.com` | `Password123@` | Toàn quyền quản trị hệ thống |

> [!NOTE]
> **Về Bệnh Nhân Vãng Lai (Walk-In Patients):** Tài khoản bệnh nhân vãng lai được Lễ tân tạo tại quầy có `PasswordHash = NULL` và `Status = Unverified`. Quy trình kích hoạt tài khoản trực tuyến tự phục vụ (Self-service online activation) **chưa được cài đặt trong phiên bản này** (phương thức quên mật khẩu chỉ xử lý tài khoản đã kích hoạt `Active` để tránh rủi ro bảo mật). Bệnh nhân vãng lai được nhân viên Lễ tân hỗ trợ hoàn toàn tại quầy tiếp đón.

---

## 5. CHẠY BỘ KIỂM THỬ TỰ ĐỘNG (AUTOMATED TESTS EXECUTION)

### 5.1. Chạy Unit & Flow Tests (647 Tests)
Tại thư mục gốc dự án:
```powershell
# Chạy toàn bộ 388 Backend Tests (bao gồm kiểm thử hồi quy bảo mật phân quyền Lễ tân)
dotnet test D:\SWP391\SWP391_Group6\BE\DentalClinic.Tests\DentalClinic.Tests.csproj

# Chạy toàn bộ 259 Frontend MVC Tests
dotnet test D:\SWP391\SWP391_Group6\FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj
```
- **Kết quả mong đợi:** Toàn bộ **647 / 647 tests đạt PASS 100%**.

### 5.2. Chạy Kiểm Thử Trình Duyệt Thực Tế Puppeteer E2E (13 Steps)
Hệ thống kiểm thử E2E sử dụng Database clone độc lập để không làm thay đổi dữ liệu sạch của Database chính:
```powershell
cd C:\Users\ASUS\.gemini\antigravity\brain\3f2a7316-c8f2-45b9-97c9-e38ffdd8b62a\scratch
node execute_e2e_workflows.js
```
- **Kết quả mong đợi:** 13/13 bước đạt PASS, toàn bộ ảnh chụp màn hình được tự động lưu vào `D:\SWP391\SWP391_Group6\docs\system-audit\screenshots\`.

---

## 6. XỬ LÝ SỰ CỐ THƯỜNG GẶP (TROUBLESHOOTING GUIDE)

### Sự cố 1: Lỗi cảnh báo chứng chỉ SSL khi truy cập `https://localhost:7129` hoặc gọi API
- **Nguyên nhân:** Chứng chỉ phát triển ASP.NET Core HTTPS chưa được cài đặt tin cậy trên máy.
- **Cách khắc phục:** Chạy lệnh sau trong PowerShell với quyền Administrator:
  ```powershell
  dotnet dev-certs https --clean
  dotnet dev-certs https --trust
  ```

### Sự cố 2: Lỗi cổng mạng bị chiếm dụng (Port already in use: 7350 hoặc 7129)
- **Nguyên nhân:** Có tiến trình .NET hoặc debug cũ đang chạy ngầm giữ cổng.
- **Cách khắc phục:** Kiểm tra và dừng tiến trình ứng dụng đang giữ cổng:
  ```powershell
  Get-Process -Name "DentalClinic.Api", "DentalClinic.Web" -ErrorAction SilentlyContinue | Stop-Process
  ```

### Sự cố 3: Lỗi kết nối SQL Server (SqlException: Cannot open database)
- **Nguyên nhân:** Dịch vụ SQL Server Express chưa được khởi động.
- **Cách khắc phục:** Mở Services (`services.msc`) $\rightarrow$ Tìm `SQL Server (SQLEXPRESS)` $\rightarrow$ Nhấn **Start / Restart**. Hoặc chạy qua PowerShell:
  ```powershell
  Start-Service -Name "MSSQL`$SQLEXPRESS"
  ```

### Sự cố 4: Chế độ kiểm thử cách ly và phục hồi Database
Nếu muốn chạy lại kiểm thử ghi dữ liệu E2E mà không ảnh hưởng Database chính:
1. Sao lưu `DentalClinicManagementDB`:
   ```powershell
   sqlcmd -S "DESKTOP-1TUBGEU\SQLEXPRESS" -E -Q "BACKUP DATABASE DentalClinicManagementDB TO DISK = 'D:\SWP391\SWP391_Group6\docs\system-audit\backups\Manual_PreTest_Backup.bak' WITH INIT;"
   ```
2. Phục hồi sang Database clone độc lập để chạy test ghi dữ liệu:
   ```powershell
   sqlcmd -S "DESKTOP-1TUBGEU\SQLEXPRESS" -E -Q "RESTORE DATABASE DentalClinic_Disposable_PreDeployClone FROM DISK = 'D:\SWP391\SWP391_Group6\docs\system-audit\backups\Manual_PreTest_Backup.bak' WITH REPLACE, MOVE 'DentalClinicManagementDB' TO 'D:\DATA\Clone.mdf', MOVE 'DentalClinicManagementDB_log' TO 'D:\DATA\Clone_log.ldf';"
   ```
