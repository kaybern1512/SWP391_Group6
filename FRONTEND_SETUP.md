# Hướng Dẫn Cấu Hình & Vận Hành Frontend MVC (FRONTEND_SETUP.md)

**Dự án**: Multi-Specialty Dental Clinic Management System (Đồ án tốt nghiệp)  
**Phân hệ**: Frontend ASP.NET Core MVC .NET 8 (`DentalClinic.Web`)  
**Vị trí mã nguồn**: `D:\SWP391\SWP391_Group6\FE\DentalClinic.Web`  

> [!IMPORTANT]
> **Lưu ý xác thực End-to-End**:  
> *Authentication UI and API client are implemented, but end-to-end authentication requires DentalClinic Backend API.*  
> Giao diện người dùng, ViewModel DataAnnotations validation, Cookie Authentication, Quản lý Token qua Session, DelegatingHandler và các Service Client đã được lập trình hoàn chỉnh. Khi khởi chạy Backend Web API, toàn bộ các luồng sẽ hoạt động đồng bộ qua HTTP REST API.

---

## 1. Cấu Trúc Thư Mục Dự Án (Project Structure)

```
D:\SWP391\SWP391_Group6\
├── DB/
│   └── Dental_Clinic_Final_Database_Reviewed.sql    # Schema cơ sở dữ liệu (38 bảng)
├── FE/
│   ├── DentalClinic.Web.sln                         # Solution quản lý Frontend MVC & Tests
│   ├── DentalClinic.Web/                            # Dự án Frontend MVC .NET 8 chính
│   │   ├── Controllers/
│   │   │   ├── HomeController.cs                    # Trang chủ giới thiệu chuyên khoa & liên hệ
│   │   │   ├── AccountController.cs                 # Xử lý Đăng ký, OTP, Đăng nhập, Google, Đổi/Quên MK
│   │   │   ├── ProfileController.cs                 # Xem & Chỉnh sửa hồ sơ, Đổi ảnh đại diện
│   │   │   ├── Patient/PatientDashboardController.cs          # Dashboard Bệnh nhân [Authorize(Roles="Patient")]
│   │   │   ├── Receptionist/ReceptionistDashboardController.cs# Dashboard Lễ tân [Authorize(Roles="Receptionist")]
│   │   │   ├── Dentist/DentistDashboardController.cs          # Dashboard Bác sĩ [Authorize(Roles="Dentist")]
│   │   │   ├── Department/DepartmentDashboardController.cs    # Dashboard Trưởng khoa [Authorize(Roles="DepartmentManager")]
│   │   │   └── Admin/AdminDashboardController.cs              # Dashboard Admin [Authorize(Roles="SystemAdministrator")]
│   │   ├── Handlers/
│   │   │   └── ApiAuthorizationHandler.cs           # DelegatingHandler: Gắn Bearer Token & Auto Refresh 401
│   │   ├── Services/
│   │   │   ├── IAuthApiService.cs & AuthApiService.cs         # Gọi API xác thực Backend
│   │   │   ├── IProfileApiService.cs & ProfileApiService.cs   # Gọi API hồ sơ & Upload Avatar
│   │   │   ├── ITokenService.cs & TokenService.cs             # Quản lý Token trên Server Session (ISession)
│   │   │   └── IAuthCookieService.cs & AuthCookieService.cs   # Quản lý Cookie Claims & Re-issue claims
│   │   ├── Models/
│   │   │   ├── Api/ (ApiResponse.cs, ApiResult.cs)
│   │   │   └── ApiDtos/ (Provisional DTOs: Register, Login, Verify, Refresh, Profile, Avatar...)
│   │   ├── ViewModels/
│   │   │   ├── Account/ (Login, Register, VerifyEmail, Forgot, Reset, ChangePassword)
│   │   │   ├── Profile/ (Profile, EditProfile, AvatarUpload)
│   │   │   └── Dashboard/ (Dashboard, EmptyState)
│   │   ├── Views/
│   │   │   ├── Account/ (Login, Register, VerifyEmail, ForgotPassword, ResetPassword, ChangePassword, AccessDenied)
│   │   │   ├── Profile/ (Index, Edit)
│   │   │   ├── Patient/ (Dashboard)
│   │   │   ├── Receptionist/ (Dashboard)
│   │   │   ├── Dentist/ (Dashboard)
│   │   │   ├── Department/ (Dashboard)
│   │   │   ├── Admin/ (Dashboard)
│   │   │   ├── Home/ (Index)
│   │   │   └── Shared/ (_Layout, _AuthLayout, _Navbar, _Sidebar, _UserMenu, _AlertMessage, _EmptyState, Error)
│   │   ├── wwwroot/
│   │   │   ├── css/ (site.css, auth.css, dashboard.css, profile.css)
│   │   │   ├── js/ (site.js - chỉ hiệu ứng UI)
│   │   │   └── images/ (clinic-logo.svg, default-avatar.svg)
│   │   ├── Program.cs                               # Cấu hình Cookie Auth, Session, HttpClients, Pipeline
│   │   └── appsettings.json                         # BaseUrl API & Google settings
│   └── DentalClinic.Web.Tests/                      # Dự án xUnit Tests (31 test cases pass 100%)
└── FRONTEND_SETUP.md                                # Tài liệu hướng dẫn này
```

---

## 2. API Base URL

- Được cấu hình tại `D:\SWP391\SWP391_Group6\FE\DentalClinic.Web\appsettings.json`:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7350"
  }
}
```
- Khi Backend chạy trên cổng khác (ví dụ: `http://localhost:5000` hoặc `https://localhost:7001`), chỉ cần cập nhật giá trị `BaseUrl` tại file này hoặc qua biến môi trường `ApiSettings__BaseUrl`.

---

## 3. Cách Chạy Backend Web API (Khi Backend Sẵn Sàng)

1. Mở cửa sổ Terminal tại thư mục chứa dự án Backend API.
2. Kiểm tra chuỗi kết nối cơ sở dữ liệu `DentalClinicManagementDB` tại SQL Server (`.\SQLEXPRESS`).
3. Khởi chạy:
```powershell
dotnet run --project <Path-To-Backend-Api-Project>
```
Backend API sẽ lắng nghe tại URL đã cấu hình (ví dụ: `https://localhost:7350`).

---

## 4. Cách Chạy Frontend MVC

Mở Terminal tại thư mục Frontend:
```powershell
cd D:\SWP391\SWP391_Group6\FE\DentalClinic.Web
dotnet run
```
Mặc định ứng dụng Frontend MVC sẽ chạy tại:
- HTTPS: `https://localhost:7129` (hoặc port do Kestrel gán)
- HTTP: `http://localhost:5049`

Truy cập trình duyệt tại: `https://localhost:7129` để xem giao diện.

---

## 5. Cấu Hình Google Login

Frontend MVC sử dụng gói thư viện chuẩn `Microsoft.AspNetCore.Authentication.Google`.

### Các bước lấy ClientId và ClientSecret:
1. Truy cập [Google Cloud Console](https://console.cloud.google.com/).
2. Tạo một Project mới hoặc chọn Project hiện có.
3. Vào **APIs & Services** > **Credentials** > **Create Credentials** > **OAuth client ID**.
4. Application type: **Web application**.
5. Trong phần **Authorized redirect URIs**, thêm địa chỉ callback của Frontend MVC:
   - `https://localhost:7129/signin-google`
6. Nhận `Client ID` và `Client Secret`.

---

## 6. Cơ Chế Email Verification & SMTP (Nằm Ở Backend)

- Tính năng gửi mã xác thực email (OTP) và liên kết đặt lại mật khẩu do **Backend API** đảm nhiệm thông qua dịch vụ SMTP (như Gmail SMTP, SendGrid, Amazon SES, hoặc Mailkit).
- Frontend gửi yêu cầu đăng ký hoặc quên mật khẩu tới Backend -> Backend tạo mã hash lưu vào bảng `AccountVerifications` / `PasswordResetTokens` và gửi mail -> Người dùng nhận mã và nhập vào màn hình `/Account/VerifyEmail` hoặc `/Account/ResetPassword`.

---

## 7. Cấu Hình User Secrets

Để bảo mật thông tin bí mật (không commit lên GitHub), thực hiện lệnh sau tại thư mục `DentalClinic.Web`:

```powershell
cd D:\SWP391\SWP391_Group6\FE\DentalClinic.Web

# Khởi tạo user-secrets
dotnet user-secrets init

# Thiết lập Google Client ID và Client Secret
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_ACTUAL_GOOGLE_CLIENT_ID.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_ACTUAL_GOOGLE_CLIENT_SECRET"
```

---

## 8. Danh Sách Tài Khoản Mẫu (Dự Kiến Cho Demo)

Sau khi Backend API nạp dữ liệu khởi tạo (Seed data) theo 5 role chính:

| Vai trò (Role) | Email Đăng Nhập | Mật Khẩu Mẫu | Chức Năng Chính |
| :--- | :--- | :--- | :--- |
| **Patient** | `patient@dentalclinic.vn` | `Patient@123` | Xem bệnh án, sơ đồ răng, đặt lịch khám |
| **Receptionist** | `receptionist@dentalclinic.vn` | `Reception@123` | Tiếp đón check-in, duyệt lịch, thu ngân |
| **Dentist** | `dentist@dentalclinic.vn` | `Dentist@123` | Bàn làm việc bác sĩ, chẩn đoán Odontogram, kê đơn |
| **DepartmentManager** | `manager@dentalclinic.vn` | `Manager@123` | Phân công lịch trực, hội chẩn chuyển tuyến khoa |
| **SystemAdministrator**| `admin@dentalclinic.vn` | `Admin@123` | Quản lý người dùng, phân quyền, nhật ký kiểm toán |

---

## 9. Hướng Dẫn Kịch Bản Kiểm Thử (Demo Flows)

### Kịch bản 1: Đăng Ký & Kích Hoạt Email (Register -> Verify Email)
1. Truy cập `/Account/Register`.
2. Điền thông tin: Họ tên, Email, Số điện thoại, Ngày sinh, Giới tính, Mật khẩu (có chữ hoa, thường, số, ký tự đặc biệt) và tích chọn Điều khoản.
3. Bấm **Hoàn Tất Đăng Ký** -> Hệ thống chuyển hướng sang `/Account/VerifyEmail?email=...`.
4. Nhập mã OTP đã nhận qua email và bấm **Kích Hoạt Tài Khoản** -> Chuyển về `/Account/Login` với thông báo thành công.

### Kịch bản 2: Đăng Nhập & Điều Hướng Theo Role (Login -> Role Dashboard)
1. Truy cập `/Account/Login`.
2. Nhập email và mật khẩu của một tài khoản hợp lệ.
3. Bấm **Đăng Nhập**:
   - MVC lưu JWT Access Token và Refresh Token vào server-side `ISession`.
   - Tạo Cookie Authentication ticket với Claims.
   - Chuyển hướng chính xác vào Dashboard tương ứng:
     - Patient -> `/Patient/Dashboard`
     - Dentist -> `/Dentist/Dashboard`
     - Receptionist -> `/Receptionist/Dashboard`
     - DepartmentManager -> `/Department/Dashboard`
     - SystemAdministrator -> `/Admin/Dashboard`

### Kịch bản 3: Xem & Chỉnh Sửa Hồ Sơ Cá Nhân (Profile -> Edit Profile)
1. Từ Navbar hoặc Sidebar, bấm vào **Hồ Sơ Cá Nhân** (`/Profile`).
2. Xem đầy đủ thông tin: Mã bệnh nhân/nhân viên, email (readonly), ngày sinh, số CMND/CCCD, thẻ BHYT, liên hệ khẩn cấp (và thông tin chứng chỉ hành nghề nếu là Bác sĩ).
3. Bấm **Chỉnh Sửa Hồ Sơ** (`/Profile/Edit`), cập nhật họ tên, địa chỉ, số điện thoại và bấm **Lưu Thay Đổi**.
4. Hệ thống cập nhật thông tin và **tự động làm mới Cookie Claims** -> Họ tên trên góc phải Navbar thay đổi ngay mà không cần đăng nhập lại.

### Kịch bản 4: Tải Lên Ảnh Đại Diện (Upload Avatar)
1. Tại trang `/Profile`, bấm vào biểu tượng máy ảnh trên ảnh đại diện.
2. Modal hiện ra: Chọn tập tin ảnh (JPG, PNG, WEBP <= 5MB).
3. Hình ảnh được xem trước (preview) ngay trên modal.
4. Bấm **Tải Lên & Lưu** -> Hệ thống gửi multipart/form-data sang API, cập nhật Claim `AvatarUrl` và ảnh đại diện trên Navbar cập nhật ngay lập tức.

### Kịch bản 5: Đổi Mật Khẩu (Change Password)
1. Truy cập `/Account/ChangePassword`.
2. Nhập mật khẩu hiện tại và mật khẩu mới (kèm xác nhận).
3. Bấm **Cập Nhật Mật Khẩu** -> Khi đổi thành công, hệ thống xóa session token, đăng xuất cookie và chuyển về `/Account/Login` yêu cầu đăng nhập bằng mật khẩu mới.

### Kịch bản 6: Quên Mật Khẩu & Đặt Lại Mật Khẩu (Forgot -> Reset Password)
1. Tại trang đăng nhập, bấm **Quên mật khẩu?** (`/Account/ForgotPassword`).
2. Nhập email và gửi yêu cầu -> Hệ thống hiển thị thông báo an toàn (không để lộ email có tồn tại hay không).
3. Nhấp vào liên kết reset mật khẩu từ email dẫn đến `/Account/ResetPassword?email=...&token=...`.
4. Nhập mật khẩu mới và hoàn tất đặt lại mật khẩu.

### Kịch bản 7: Đăng Nhập Google (Google Authentication)
1. Tại trang `/Account/Login`, bấm nút **Tiếp tục với Google**.
2. Thực hiện đăng nhập và cấp quyền trên giao diện Google.
3. Callback trả về `/Account/ExternalLoginCallback` -> Lấy `id_token` gửi sang Backend xác minh và đăng nhập vào Dashboard.

### Kịch bản 8: Đăng Xuất & Kiểm Soát Phân Quyền (Logout & Access Control)
1. Bấm nút **Đăng Xuất** từ menu góc phải (sử dụng phương thức HTTP POST kèm Anti-Forgery Token an toàn).
2. Session và Cookie đăng nhập bị xóa hoàn toàn, người dùng được chuyển về `/Account/Login`.
3. Thử gõ trực tiếp URL `/Profile` hoặc `/Patient/Dashboard` trên thanh địa chỉ trình duyệt -> Hệ thống tự động chặn và chuyển hướng về `/Account/Login`.
