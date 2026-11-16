# DENTALCARE CLINIC MANAGEMENT SYSTEM — HƯỚNG DẪN CÀI ĐẶT
**Dự án:** DentalCare Clinic Management System (SWP391 Group 6)  
**Kiến trúc:** .NET 8 Web API (Backend) + .NET 8 MVC Razor (Frontend) + SQL Server Express  

---

## 1. YÊU CẦU HỆ THỐNG (PREREQUISITES)

Trước khi tiến hành cài đặt, đảm bảo máy tính đáp ứng các yêu cầu phần mềm sau:

1. **Hệ Điều Hành:** Windows 10/11 (khuyến nghị 64-bit), hoặc Linux / macOS.
2. **.NET SDK:** Phiên bản **.NET 8.0 SDK** (bản 8.0.4xx hoặc mới hơn).
   - Kiểm tra bằng lệnh: `dotnet --version`
   - Tải về từ Microsoft nếu chưa có: [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
3. **Cơ Sở Dữ Liệu:** Microsoft SQL Server 2017+ / 2019 / 2022 / SQL Server Express / LocalDB.
   - Khuyến nghị: SQL Server Express (Service name: `MSSQL$SQLEXPRESS`).
4. **Công Cụ Quản Lý DB:** SQL Server Management Studio (SSMS) 19+ hoặc Azure Data Studio.
5. **Môi Trường Phát Triển (Tùy chọn):** Visual Studio 2022 (v17.8+) với workload "ASP.NET and web development" hoặc VS Code + C# Dev Kit.
6. **Shell / Terminal:** Windows PowerShell 5.1+ hoặc PowerShell 7+.

---

## 2. CẤU TRÚC THƯ MỤC DỰ ÁN

```text
DentalCare-DB-Simplification/
├── BE/                                    # Phân hệ Backend API
│   ├── DentalClinic.Api/                 # Host API, Controllers, Middleware, Hubs
│   ├── DentalClinic.Application/         # Use Cases, DTOs, Validators, Interfaces
│   ├── DentalClinic.Domain/              # Entities, Enums, Domain Exceptions
│   ├── DentalClinic.Infrastructure/      # EF Core DbContext, Repositories, Services
│   ├── DentalClinic.Tests/               # XUnit Tests cho Backend (309 tests)
│   └── DentalClinic.Backend.sln          # Solution Backend
├── FE/                                    # Phân hệ Frontend Web MVC
│   ├── DentalClinic.Web/                 # MVC Controllers, Razor Views, ViewModels
│   ├── DentalClinic.Web.Tests/           # XUnit Tests cho Frontend (339 tests)
│   └── DentalClinic.Web.sln              # Solution Frontend
├── DB/                                    # Tài nguyên Cơ sở dữ liệu
│   ├── DentalClinic_Schema_Full_Standalone.sql # Schema DDL độc lập 38 bảng
│   └── DentalClinic_UAT_Seed_Data.sql    # Dữ liệu khởi tạo mẫu
├── release/                               # Bộ tài liệu & cấu hình bàn giao
│   ├── configuration/                    # File cấu hình mẫu & env
│   ├── DATABASE_SETUP.md                 # Hướng dẫn thiết lập SQL Server
│   ├── INSTALLATION_GUIDE.md             # Tài liệu này
│   ├── RUN_GUIDE.md                      # Hướng dẫn chạy và tài khoản demo
│   ├── TESTING_GUIDE.md                  # Hướng dẫn chạy test suite tự động
│   ├── RELEASE_CHECKLIST.md              # Bảng kiểm tra trước nghiệm thu
│   └── RELEASE_VERIFICATION_REPORT.md    # Báo cáo kỹ thuật xác minh hệ thống
├── run_demo.ps1                           # Script khởi động demo tự động 1-click
└── stop_demo.ps1                          # Script dừng toàn bộ tiến trình hệ thống
```

---

## 3. CÁC BƯỚC CÀI ĐẶT CHI TIẾT

### Bước 3.1: Mở Terminal Tại Thư Mục Gốc Dự Án
Mở PowerShell và chuyển đến thư mục repository:
```powershell
cd D:\SWP391\DentalCare-DB-Simplification
```

### Bước 3.2: Kiểm Tra Phiên Bản .NET SDK
```powershell
dotnet --version
```
*Kết quả phải trả về phiên bản dạng `8.0.xxx`.*

### Bước 3.3: Thiết Lập Cơ Sở Dữ Liệu SQL Server
Thực hiện tạo database và nạp schema theo hướng dẫn tại [release/DATABASE_SETUP.md](file:///D:/SWP391/DentalCare-DB-Simplification/release/DATABASE_SETUP.md).

Tóm tắt lệnh tạo nhanh trên PowerShell:
```powershell
# 1. Tạo database
sqlcmd -S .\SQLEXPRESS -E -Q "CREATE DATABASE [DentalCare_SWP391_UAT_Fixture_TEST];"

# 2. Chạy DDL schema 38 bảng
sqlcmd -S .\SQLEXPRESS -d DentalCare_SWP391_UAT_Fixture_TEST -E -i "DB\DentalClinic_Schema_Full_Standalone.sql"

# 3. Nạp dữ liệu mẫu khởi tạo
sqlcmd -S .\SQLEXPRESS -d DentalCare_SWP391_UAT_Fixture_TEST -E -i "DB\DentalClinic_UAT_Seed_Data.sql"
```

### Bước 3.4: Restore Các Gói Nuget Phụ Thuộc
Chạy lệnh restore cho cả 2 solution:
```powershell
# Restore Backend Dependencies
dotnet restore BE\DentalClinic.Backend.sln

# Restore Frontend Dependencies
dotnet restore FE\DentalClinic.Web.sln
```

### Bước 3.5: Biên Dịch (Build) Toàn Bộ Hệ Thống
Thực hiện build ở cấu hình `Debug` hoặc `Release`:
```powershell
# Build Backend API
dotnet build BE\DentalClinic.Backend.sln --no-restore

# Build Frontend MVC
dotnet build FE\DentalClinic.Web.sln --no-restore
```
*Xác nhận cả hai lệnh build thành công với `0 Warning(s)`, `0 Error(s)`.*

---

## 4. CẤU HÌNH THÔNG SỐ KẾT NỐI (CONFIGURATION)

Hệ thống đã có sẵn cấu hình mặc định trong file `appsettings.Development.json` cho phép chạy ngay với database `DentalCare_SWP391_UAT_Fixture_TEST` trên `.\SQLEXPRESS`.

Nếu bạn muốn tùy chỉnh môi trường:

### Cách 1: Sử dụng File JSON (Khuyến nghị cho Visual Studio / Localhost)
Kiểm tra hoặc sao chép file từ thư mục `release/configuration/`:
- Backend: sao chép `release/configuration/appsettings.api.example.json` vào `BE\DentalClinic.Api\appsettings.json`.
- Frontend: sao chép `release/configuration/appsettings.web.example.json` vào `FE\DentalClinic.Web\appsettings.json`.

### Cách 2: Sử dụng Biến Môi Trường (`env_server` và `env_client`)
Nếu chạy trên môi trường CI/CD, Docker hoặc terminal độc lập:
- Backend: load các biến từ `release/configuration/env_server`
- Frontend: load các biến từ `release/configuration/env_client`

---

## 5. CÀI ĐẶT SSL CERTIFICATE CHO LOCALHOST (HTTPS)

Dự án sử dụng giao thức bảo mật HTTPS chuẩn:
- Backend: `https://localhost:7350`
- Frontend: `https://localhost:7129`

Để trình duyệt tin cậy chứng chỉ phát triển cục bộ của ASP.NET Core, chạy lệnh sau một lần duy nhất:
```powershell
dotnet dev-certs https --trust
```
Bấm **Yes** khi hộp thoại xác nhận của Windows xuất hiện.

---

## 6. XÁC MINH CÀI ĐẶT THÀNH CÔNG

Chạy script kiểm thử đồng bộ tự động để đảm bảo môi trường đã hoàn toàn sẵn sàng:
```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\dentalcare-testing-sync\scripts\sync-and-test.ps1
```
Khi thấy dòng thông báo:
```text
=================================================================
 All DentalCare Test Suites Passed with 100% Success!            
=================================================================
```
Bạn đã hoàn tất cài đặt thành công và có thể chuyển sang [release/RUN_GUIDE.md](file:///D:/SWP391/DentalCare-DB-Simplification/release/RUN_GUIDE.md) để khởi động hệ thống.
