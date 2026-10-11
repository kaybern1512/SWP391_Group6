# 🛡️ HƯỚNG DẪN THIẾT LẬP BẢO MẬT & KHỞI CHẠY HỆ THỐNG (SETUP.MD)

Tài liệu này hướng dẫn cách cấu hình các thông tin bí mật (Gmail App Password, Cloudinary, Google OAuth) trên máy cá nhân mà **KHÔNG ĐƯỢC PUSH LÊN GITHUB** để tránh bị GitHub Secret Scanning quét bản quyền, thu hồi mật khẩu hoặc khóa tài khoản.

---

## ⚠️ I. NGUYÊN TẮC BẢO MẬT GITHUB (QUAN TRỌNG)

1. **Tuyệt đối KHÔNG commit mật khẩu/API key thật vào `appsettings.json`**:
   - GitHub có hệ thống tự động quét bí mật (**Secret Scanning**).
   - Nếu bạn push mật khẩu ứng dụng Gmail (`kdnc...`) hoặc Google Client Secret lên commit công khai/nhóm, Google sẽ **ngay lập tức hủy mật khẩu** đó và GitHub có thể cảnh báo hoặc đình chỉ repository.
2. **Giải pháp chuẩn của Microsoft ASP.NET Core: .NET User Secrets**:
   - Cơ chế `dotnet user-secrets` lưu trữ thông tin nhạy cảm ở thư mục nội bộ máy tính của bạn:
     - Windows: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
   - File này nằm **HOÀN TOÀN NGOÀI THƯ MỤC DỰ ÁN** và **KHÔNG BAO GIỜ BỊ GIT THEO DÕI HOẶC PUSH LÊN GITHUB**.
   - Khi chạy ứng dụng ở môi trường Development, .NET sẽ tự động nạp các giá trị này vào `IConfiguration`.

---

## 🚀 II. CÁC LỆNH SETUP SECRETS TRÊN MÁY CÁ NHÂN (1-CLICK)

Khi clone dự án về máy mới hoặc cấu hình cho thành viên mới trong nhóm, mở **PowerShell** hoặc **Terminal** tại thư mục gốc của solution (`D:\SWP391\SWP391_Group6`) và chạy các lệnh sau:

### 1. Cấu hình Mật khẩu Gmail Gửi OTP (Email Service)
```powershell
dotnet user-secrets set "EmailSettings:Password" "kdncljszxdvzbsji" --project BE/DentalClinic.Api
```

### 2. Cấu hình Upload Ảnh (Cloudinary)
```powershell
dotnet user-secrets set "Cloudinary:CloudName" "dky5fqcsj" --project BE/DentalClinic.Api
dotnet user-secrets set "Cloudinary:ApiKey" "439674342134428" --project BE/DentalClinic.Api
dotnet user-secrets set "Cloudinary:ApiSecret" "Wf3TpfG8Vziho-IxT7cl-U-tyTw" --project BE/DentalClinic.Api
```

### 3. Cấu hình Đăng nhập Google (Google OAuth)
```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "482754989911-krfsu4jokuuqcttqbtmuh6bdal15a107.apps.googleusercontent.com" --project BE/DentalClinic.Api
```

---

## 🔍 III. KIỂM TRA SECRETS ĐÃ THIẾT LẬP THÀNH CÔNG CHƯA

Chạy lệnh:
```powershell
dotnet user-secrets list --project BE/DentalClinic.Api
```

**Kết quả hiển thị đúng chuẩn:**
```text
EmailSettings:Password = kdncljszxdvzbsji
Cloudinary:CloudName = dky5fqcsj
Cloudinary:ApiKey = 439674342134428
Cloudinary:ApiSecret = Wf3TpfG8Vziho-IxT7cl-U-tyTw
Authentication:Google:ClientId = 482754989911-krfsu4jokuuqcttqbtmuh6bdal15a107.apps.googleusercontent.com
```

---

## 🗄️ IV. THIẾT LẬP CƠ SỞ DỮ LIỆU (DATABASE FIRST)

Hệ thống sử dụng SQL Server với file database gốc `Dental_Clinic_Final_Database_Clean.sql`.

### Bước 1: Tạo Database sạch
Mở PowerShell chạy lệnh:
```powershell
sqlcmd -S .\SQLEXPRESS -E -i "DB\Dental_Clinic_Final_Database_Clean.sql"
```

### Bước 2: Nạp dữ liệu nền tảng (Phòng khám, Bác sĩ, Lịch trực, Ca khám mẫu)
```powershell
sqlcmd -S .\SQLEXPRESS -d DentalClinicManagementDB -E -i "DB\Seed_Booking_Foundational_Data.sql"
```

---

## 💻 V. KHỞI CHẠY HỆ THỐNG

### 1. Khởi chạy Backend Web API (Terminal 1)
```powershell
cd BE\DentalClinic.Api
dotnet run
```
- **API URL (HTTPS)**: `https://localhost:7350`
- **Swagger Documentation**: `https://localhost:7350/swagger`

### 2. Khởi chạy Frontend MVC (Terminal 2)
```powershell
cd FE\DentalClinic.Web
dotnet run
```
- **Web App URL (HTTPS)**: `https://localhost:7129`

---

## 👥 VI. TÀI KHOẢN MẪU ĐỂ TEST HỆ THỐNG

| Vai trò | Email đăng nhập | Mật khẩu mặc định |
| :--- | :--- | :--- |
| **System Admin** | `admin@dentalclinic.com` | `Password123@` |
| **Department Manager** | `manager@dentalclinic.com` | `Password123@` |
| **Dentist** | `dentist1@dentalclinic.com` | `Password123@` |
| **Receptionist** | `receptionist1@dentalclinic.com` | `Password123@` |
| **Patient** | `patient1@dentalclinic.com` | `Password123@` |
