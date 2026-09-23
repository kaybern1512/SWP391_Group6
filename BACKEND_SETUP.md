# Hướng Dẫn Cấu Hình & Vận Hành Backend Web API (BACKEND_SETUP.md)

**Dự án**: Multi-Specialty Dental Clinic Management System (Đồ án tốt nghiệp)  
**Phân hệ**: Backend ASP.NET Core Web API .NET 8 (`DentalClinic.Api`)  
**Vị trí mã nguồn**: `D:\SWP391\SWP391_Group6\BE`  

---

## 1. Cấu Trúc Dự Án (Clean Architecture .NET 8)

Hệ thống Backend được thiết kế tuân thủ nghiêm ngặt mô hình **Clean Architecture**, phân tách độc lập giữa Domain, Application, Infrastructure và Presentation:

```
D:\SWP391\SWP391_Group6\BE\
├── DentalClinic.Backend.sln                         # Solution Backend & Test Suite
├── DentalClinic.Domain/                             # Core Domain Layer
│   └── Enums/                                       # Enums chuẩn hệ thống
│       ├── UserRole.cs                              # Patient, Receptionist, Dentist, DepartmentManager, SystemAdministrator
│       ├── AccountStatus.cs                         # Unverified, Active, Locked, Inactive
│       ├── StaffType.cs                             # Receptionist, Dentist, DepartmentManager, SystemAdministrator
│       └── VerificationPurpose.cs                   # EmailVerification, PasswordReset...
├── DentalClinic.Application/                        # Application Business Rules
│   ├── Common/
│   │   ├── Interfaces/                              # Interfaces chuẩn hóa (IAuthService, IProfileService, IImageStorageService...)
│   │   └── Models/                                  # ApiResponse<T>, ApiResult
│   └── Features/                                    # DTOs & FluentValidation Validators
│       ├── Auth/DTOs/ & Validators/                 # Register, Login, VerifyOtp, RefreshToken, PasswordReset...
│       └── Profile/DTOs/ & Validators/              # GetProfile, UpdateProfile, UploadAvatar
├── DentalClinic.Infrastructure/                     # External Concerns & Persistence
│   ├── Persistence/
│   │   ├── DentalClinicDbContext.cs                 # EF Core DbContext scaffolded từ DB thật (38 tables)
│   │   └── Entities/                                # Toàn bộ 38 thực thể database
│   └── Services/                                    # Triển khai dịch vụ cụ thể
│       ├── AuthService.cs                           # Toàn bộ nghiệp vụ Auth, Token rotation, Lockout
│       ├── ProfileService.cs                       # Quản lý hồ sơ đa vai trò & cập nhật thông tin
│       ├── CloudinaryImageStorageService.cs         # Upload avatar Cloudinary (500x500 auto-gravity)
│       ├── PasswordHasherService.cs                 # Microsoft.AspNetCore.Identity.PasswordHasher<T>
│       ├── OtpService.cs                            # Mã OTP 6 chữ số + bảo mật HMAC-SHA256
│       ├── JwtTokenService.cs                       # JWT Token generation, validation, refresh hash
│       ├── GoogleAuthService.cs                     # GoogleJsonWebSignature ID Token validation
│       ├── EmailService.cs                          # MailKit SMTP gửi OTP kích hoạt & link reset MK
│       └── AuditLogService.cs                       # Ghi vết nhật ký kiểm toán hệ thống
├── DentalClinic.Api/                                # Presentation Layer (REST API)
│   ├── Controllers/                                 # AuthController, ProfileController
│   ├── Middleware/                                  # GlobalExceptionHandlerMiddleware
│   ├── Program.cs                                   # Cấu hình Services, JWT Bearer, CORS, Swagger
│   ├── appsettings.json                             # Cấu hình mẫu (không chứa secret)
│   └── Properties/launchSettings.json               # HTTPS: 7350, HTTP: 5350
└── DentalClinic.Tests/                              # Unit & Integration Test Suite (xUnit + Moq)
```

---

## 2. Cổng Kết Nối & Địa Chỉ Mạng (Ports & URLs)

Backend Web API được cấu hình chạy cố định trên các cổng:
- **HTTPS**: `https://localhost:7350`
- **HTTP**: `http://localhost:5350`
- **Swagger UI**: `https://localhost:7350/swagger`

> [!IMPORTANT]
> Frontend ASP.NET Core MVC (`DentalClinic.Web`) chạy trên cổng `https://localhost:7129` và gọi trực tiếp đến `https://localhost:7350`. Không thay đổi cổng backend sang `7250` hoặc bất kỳ cổng nào khác.

---

## 3. Cấu Hình Cơ Sở Dữ Liệu (Database Setup)

Backend sử dụng cơ chế **Database First** trên Microsoft SQL Server.

1. **Chuỗi kết nối mặc định** (`appsettings.json`):
   ```json
   "ConnectionStrings": {
     "DentalClinicDatabase": "Server=.\\SQLEXPRESS;Database=DentalClinicManagementDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
   }
   ```
2. **Khởi tạo schema (38 bảng)**:
   Nếu database chưa tồn tại hoặc cần tạo mới, chạy file script:
   `D:\SWP391\SWP391_Group6\DB\Dental_Clinic_Final_Database_Reviewed.sql`
3. **Nạp dữ liệu mẫu Staff (Tất cả 5 vai trò)**:
   Chạy file script seed tài khoản mẫu:
   `D:\SWP391\SWP391_Group6\DB\Seed_Staff_Test_Accounts.sql`
   ```powershell
   sqlcmd -S ".\SQLEXPRESS" -d "DentalClinicManagementDB" -i "D:\SWP391\SWP391_Group6\DB\Seed_Staff_Test_Accounts.sql"
   ```

---

## 4. Danh Sách Tài Khoản Mẫu Để Đăng Nhập & Test (Test Accounts)

Tất cả tài khoản nhân viên dưới đây đều đã được kích hoạt (`Active`), xác thực email và có mật khẩu chung là **`Password123@`** (được hash bằng thuật toán chuẩn PBKDF2 của ASP.NET Core Identity):

| Vai trò (Role) | Email đăng nhập | Mật khẩu | Mã nhân viên | Họ và tên | Chuyên khoa / Ghi chú |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **SystemAdministrator** | `admin@dentalcare.com` | `Password123@` | `EMP-ADM-001` | Quản Trị Viên Hệ Thống | Quản lý toàn bộ hệ thống |
| **Receptionist** | `receptionist@dentalcare.com` | `Password123@` | `EMP-REC-001` | Lễ Tân Nguyễn Thị Mai | Bàn tiếp đón bệnh nhân |
| **Dentist** | `dentist@dentalcare.com` | `Password123@` | `EMP-DEN-001` | Bác Sĩ Trần Văn Hùng | Khoa Phẫu Thuật Miệng & Cấy Ghép Implant |
| **DepartmentManager** | `manager@dentalcare.com` | `Password123@` | `EMP-MGR-001` | Trưởng Khoa Lê Hoàng Nam | Trưởng Khoa Phẫu Thuật Miệng |
| **Patient** | `lehoangminh.test@dentalcare.com` | `Password123@` | `PAT-202609-0001` | Lê Hoàng Minh | Bệnh nhân cá nhân (hoặc tự Đăng ký mới) |

---

## 5. Cấu Hình Bảo Mật & Quản Lý Secrets (Secret Management)

> [!CAUTION]
> **Quy định bảo mật**: Tuyệt đối **KHÔNG** commit các API Secret, Google Client Secret hoặc Cloudinary Secret vào file `appsettings.json` trong Git để tránh bị GitHub Push Protection từ chối commit. Tất cả secrets môi trường phát triển cục bộ phải được lưu qua công cụ `dotnet user-secrets`.

### 5.1. Cấu hình Cloudinary (Lưu trữ ảnh đại diện Avatar)
1. Đăng ký tài khoản miễn phí tại [Cloudinary.com](https://cloudinary.com).
2. Lấy thông tin `CloudName`, `ApiKey` và `ApiSecret` trên Dashboard Cloudinary.
3. Thiết lập user-secrets tại thư mục `BE/DentalClinic.Api`:
   ```powershell
   cd D:\SWP391\SWP391_Group6\BE\DentalClinic.Api
   dotnet user-secrets set "Cloudinary:CloudName" "YOUR_CLOUDINARY_CLOUD_NAME"
   dotnet user-secrets set "Cloudinary:ApiKey" "YOUR_CLOUDINARY_API_KEY"
   dotnet user-secrets set "Cloudinary:ApiSecret" "YOUR_CLOUDINARY_API_SECRET"
   ```
4. **Quy tắc xử lý ảnh đại diện của hệ thống**:
   - Định dạng chấp nhận: `.jpg`, `.jpeg`, `.png`, `.webp`.
   - Giới hạn dung lượng: Tối đa **5 MB**.
   - Public ID xác định duy nhất: `dental-clinic/avatars/user_{UserId}`.
   - Cờ: `Overwrite = true`, `Invalidate = true` (ghi đè và xóa cache CDN).
   - Biến đổi ảnh tự động: `500x500`, cắt `fill`, tiêu điểm nhận diện khuôn mặt `gravity: auto`, tối ưu chất lượng `quality: auto`, định dạng `fetch_format: auto`.

### 5.2. Cấu hình Google OAuth
Google Client ID và Client Secret đã được cấp phát cho đồ án:
- **Client ID**: `482754989911-krfsu4jokuuqcttqbtmuh6bdal15a107.apps.googleusercontent.com`
- Thiết lập tại Backend:
  ```powershell
  cd D:\SWP391\SWP391_Group6\BE\DentalClinic.Api
  dotnet user-secrets set "Authentication:Google:ClientId" "482754989911-krfsu4jokuuqcttqbtmuh6bdal15a107.apps.googleusercontent.com"
  ```

---

## 6. Hướng Dẫn Khởi Chạy & Kiểm Thử Backend

### 6.1. Chạy toàn bộ Test Suite (xUnit)
Kiểm tra toàn diện 54 ca kiểm thử tự động của Backend:
```powershell
cd D:\SWP391\SWP391_Group6
dotnet test BE\DentalClinic.Backend.sln
```
Kết quả: `Total: 54, Passed: 54, Failed: 0` (100% Pass).

### 6.2. Khởi chạy Backend Web API
```powershell
cd D:\SWP391\SWP391_Group6\BE\DentalClinic.Api
dotnet run
```
Màn hình console sẽ hiển thị:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7350
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5350
```
Truy cập tài liệu API tương tác tại: `https://localhost:7350/swagger`.
