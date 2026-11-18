# BÁO CÁO TRIỂN KHAI DEMO TRỰC TUYẾN DENTALCARE SWP391
## ASP.NET Core 8 MVC + Web API + SQL Server Express + Cloudflare Tunnel

- **Dự án:** Hệ thống quản lý phòng khám nha khoa đa chuyên khoa DentalCare (SWP391 - Group 6).
- **Mã nguồn:** `D:\SWP391\DentalCare-Integration`
- **Nhánh Git:** `develop` (HEAD: `eb33159`, đồng bộ `main: a70ad77`).
- **Thời gian triển khai:** 18/11/2026.
- **Trạng thái triển khai:** **DEPLOYED (HOÀN TẤT & ĐÃ XÁC THỰC E2E 100%)**.

---

## 1. TỔNG QUAN & PHƯƠNG ÁN TRIỂN KHAI

### 1.1. Mục tiêu
Triển khai hệ thống DentalCare thành một đường dẫn HTTPS công khai trên Internet để hội đồng chấm đồ án, giảng viên và các thành viên nhóm có thể truy cập, thao tác và tương tác trực tiếp qua trình duyệt từ mạng ngoài (mạng 4G/5G hoặc Wi-Fi khác).

### 1.2. Kiến trúc mạng & Định tuyến
```
[External Browser / Phone]
        │ (HTTPS)
        ▼
[Cloudflare Edge Network]
        │ (QUIC / HTTP/2 Encrypted Tunnel)
        ▼
[cloudflared.exe (Host Daemon)]
        │ (HTTPS Loopback - https://localhost:7128)
        ▼
[DentalClinic.Web (MVC BFF - Port 7128)]
        │ (Internal Server-to-Server - https://localhost:7351)
        ▼
[DentalClinic.Api (Web API - Port 7351)]
        │ (ADO.NET / EF Core)
        ▼
[Microsoft SQL Server Express (.\SQLEXPRESS)]
        └── Database: DentalCare_SWP391_ONLINE_DEMO
```

### 1.3. Nguyên tắc bảo vệ hệ thống & dữ liệu
1. **Bảo toàn tuyệt đối dữ liệu Demo V2 tại chỗ:** Database `DentalCare_SWP391_DEMO_V2_TEST` (chứa các bộ fixture #55–#62 chuẩn bị cho buổi demo trực tiếp ngày 19/11/2026) được cô lập hoàn toàn, không có bất kỳ kết nối hay thao tác ghi/xóa nào tác động đến nó.
2. **Cơ sở dữ liệu Online Demo độc lập:** Khởi tạo riêng biệt `DentalCare_SWP391_ONLINE_DEMO` từ canonical schema và seed data chuẩn.
3. **Mã nguồn không bị sửa đổi ngoài ý muốn:** Toàn bộ runtime online demo được publish và đóng gói tại thư mục độc lập `D:\SWP391\DentalCare-OnlineDemo-Runtime`, nằm ngoài cây theo dõi của Git working tree.
4. **An toàn bảo mật:** SQL Server và Web API (Port 7351) giữ trạng thái nội bộ 100% loopback, không mở port firewall công khai, không public cổng SQL Server ra Internet. Trình duyệt ngoài Internet chỉ giao tiếp duy nhất với Frontend MVC qua đường hầm mã hóa Cloudflare Tunnel.

---

## 2. THÔNG TIN KẾT NỐI & ĐỊA CHỈ TRUY CẬP

| Hạng mục | Giá trị thực tế |
|---|---|
| **Public HTTPS URL** | **`https://filters-deserve-vat-championships.trycloudflare.com`** |
| **Loại đường dẫn** | Temporary Quick Tunnel (trycloudflare.com) |
| **Giao thức bảo mật** | TLS 1.3, HTTPS End-to-End |
| **Frontend MVC nội bộ** | `https://localhost:7128` (HTTP: `http://localhost:5128`) |
| **Backend API nội bộ** | `https://localhost:7351` (HTTP: `http://localhost:5351`) |
| **Database RDBMS** | `.\SQLEXPRESS` / `DentalCare_SWP391_ONLINE_DEMO` |
| **Snapshot phục hồi** | `D:\SWP391\DentalCare_SWP391_ONLINE_DEMO_baseline.bak` |
| **Thư mục lưu trữ ảnh X-quang** | `D:\SWP391\DentalCare-OnlineDemo-Runtime\Storage\MedicalImaging` |

> [!NOTE]
> Đường dẫn trên là một Quick Tunnel do Cloudflare cấp phát tự động không yêu cầu tài khoản trả phí. Đường dẫn này duy trì liên tục chừng nào tiến trình `cloudflared.exe` vẫn đang hoạt động trên máy chủ Windows. Nếu tắt máy hoặc khởi động lại tunnel, Cloudflare sẽ cấp một subdomain ngẫu nhiên mới (được hiển thị tự động khi chạy `run_online_demo.ps1`).

---

## 3. TÀI KHOẢN TRUY CẬP DEMO (4 VAI TRÒ)

Mật khẩu chuẩn chung cho toàn bộ các tài khoản demo trên hệ thống online:
**(không công bố trong repo - xem hướng dẫn nội bộ của nhóm)**

| Vai trò | Email đăng nhập | Mô tả & Chức năng |
|---|---|---|
| **SystemAdministrator** | `admin.demo@dentalcare.test` | Quản trị dịch vụ, phòng khám, ghế nha khoa, báo cáo thống kê |
| **Receptionist** | `reception.demo01@dentalcare.test` | Tiếp đón, điều phối, xác nhận lịch hẹn, check-in, cấp số thứ tự |
| **Dentist (Tổng quát)** | `dentist.gen02@dentalcare.test` | Bác sĩ khám lâm sàng, hồ sơ bệnh án (EMR), kê đơn, chỉ định X-quang |
| **Dentist (Chỉnh nha)** | `dentist.ort01@dentalcare.test` | Bác sĩ điều trị chỉnh nha, lập phác đồ điều trị |
| **Dentist (Chẩn đoán hình ảnh)** | `dentist.xray01@dentalcare.test` | Bác sĩ trực phòng X-quang, chụp phim và tải lên kết quả |
| **Patient** | `patient.demo01@dentalcare.test` | Bệnh nhân đặt lịch, xem lịch hẹn, đổi lịch, rút yêu cầu, xem hóa đơn |
| **Patient (Đã có lịch #60)** | `patient.demo22@dentalcare.test` | Bệnh nhân trong ca khám demo #60 (chiều 19/11/2026) |

---

## 4. KẾT QUẢ KIỂM THỬ THỰC TẾ BẰNG TRÌNH DUYỆT (REAL BROWSER E2E)

Bộ kiểm thử tự động hóa E2E (`e2e/test_online_demo_public_e2e.js`) đã được thực thi trực tiếp qua **PUBLIC HTTPS URL** (`https://filters-deserve-vat-championships.trycloudflare.com`) bằng Chromium/Edge headless browser, tương tác qua toàn bộ 4 vai trò và đối soát dữ liệu thực tế tại SQL Server:

```
================================================================
 REAL BROWSER E2E OVER PUBLIC CLOUDFLARE TUNNEL — SUMMARY
================================================================
 Total Scenarios : 20
 Passed          : 20
 Failed          : 0
 Pass Rate       : 100.0%

ALL E2E WORKFLOWS OVER PUBLIC CLOUDFLARE TUNNEL PASSED 100%!
```

### Chi tiết 20 kịch bản kiểm thử đã PASS 100%:

| STT | Phân nhóm | Tên kịch bản | Kết quả | Bằng chứng xác thực |
|:---:|---|---|:---:|---|
| 1 | **Public Gate** | Trang chủ tải thành công qua HTTPS | **PASS** | HTTP 200 OK, Title: `Trang Chủ - Hệ Thống Quản Lý Phòng Khám Nha Khoa DentalCare` |
| 2 | **Public Gate** | Tải tài nguyên tĩnh CSS/JS qua Tunnel | **PASS** | `/css/site.css` trả về HTTP 200 |
| 3 | **Public Gate** | Trang đăng nhập & Antiforgery Token | **PASS** | Form `#accountLoginForm` kèm `__RequestVerificationToken` hợp lệ |
| 4 | **Patient** | Đăng nhập Bệnh nhân & Cookie xác thực | **PASS** | Thiết lập cookie bảo mật `.DentalCare.Integration.Auth` (`Secure; HttpOnly`), chuyển hướng `/Patient/Dashboard` |
| 5 | **Patient** | Tải giao diện Wizard đặt lịch khám | **PASS** | Wizard 5 bước `#bookingForm` kết xuất đầy đủ danh mục dịch vụ |
| 6 | **Patient** | Gửi form đặt lịch hẹn khám qua UI | **PASS** | Tạo thành công Lịch hẹn #63, ghi nhận trong SQL với trạng thái `PendingReceptionReview` |
| 7 | **Patient** | Rút/Hủy yêu cầu đặt lịch hẹn | **PASS** | Bệnh nhân rút lịch hẹn #63 qua UI modal, cập nhật `Status = 'Withdrawn'` trong SQL |
| 8 | **Patient** | Kiểm soát phân quyền RBAC | **PASS** | Bệnh nhân cố tình vào `/Admin/Dashboard` bị chặn chuyển hướng `/Account/AccessDenied` |
| 9 | **Receptionist** | Đăng nhập Lễ tân qua UI | **PASS** | Đăng nhập thành công với vai trò `Receptionist`, nhận cookie phiên làm việc |
| 10 | **Receptionist** | Truy cập cổng điều phối yêu cầu | **PASS** | Danh sách lịch hẹn chờ duyệt tại `/Receptionist/Appointments/Requests` hiển thị đúng |
| 11 | **Receptionist** | Phê duyệt lịch hẹn & phân bổ tài nguyên | **PASS** | Phê duyệt Lịch #55, chỉ định Bác sĩ 5, Phòng 1, Ghế 1; SQL cập nhật `Status = 'Confirmed'` |
| 12 | **Receptionist** | Tiếp đón & Check-in tại quầy | **PASS** | Check-in ca khám #60 tại `/Receptionist/Appointments/TodayCheckIn`, gán số thứ tự tiếp đón `QueueNumber = 2` |
| 13 | **Dentist** | Đăng nhập Bác sĩ điều trị qua UI | **PASS** | Đăng nhập thành công với vai trò `Dentist`, nhận cookie phiên làm việc |
| 14 | **Dentist** | Xem lịch trực & ca khám được phân công | **PASS** | Tải danh sách ca hẹn tại `/Dentist/Appointments` |
| 15 | **Dentist** | Mở không gian làm việc bệnh án (EMR) | **PASS** | Tải sơ đồ răng Odontogram và hồ sơ bệnh án bệnh nhân #32 tại `/Dentist/MedicalRecords` |
| 16 | **Dentist** | Lưu chẩn đoán & diễn tiến lâm sàng | **PASS** | Ghi nhận bệnh án vào bảng `ClinicalEntries`, trường `TreatmentDone` lưu trữ chính xác |
| 17 | **Admin** | Đăng nhập Quản trị viên qua UI | **PASS** | Đăng nhập thành công với vai trò `SystemAdministrator` |
| 18 | **Admin** | Truy cập Bảng điều khiển Quản trị | **PASS** | Tải thành công `/Admin/Dashboard` với đầy đủ chỉ số vận hành |
| 19 | **Admin** | Quản lý danh mục dịch vụ nha khoa | **PASS** | Tải danh mục 12 dịch vụ kỹ thuật tại `/Admin/Services` |
| 20 | **Admin** | Quản lý buồng khám & ghế điều trị | **PASS** | Tải danh sách phòng khám và ghế nha khoa tại `/Admin/RoomChair` |

---

## 5. HƯỚNG DẪN VẬN HÀNH CHO ĐỘI NGŨ DEMO

### 5.1. Khởi động hệ thống Demo Online
Mở PowerShell tại thư mục dự án và chạy:
```powershell
.\run_online_demo.ps1
```
Script sẽ tự động:
1. Giải phóng các tiến trình cũ trên cổng 7351 và 7128.
2. Khởi chạy Backend API (Port 7351) với database `DentalCare_SWP391_ONLINE_DEMO`.
3. Khởi chạy Frontend MVC (Port 7128).
4. Khởi chạy Cloudflare Tunnel và in ra đường dẫn HTTPS công khai để gửi cho hội đồng/giảng viên.

### 5.2. Khởi động kèm Reset Database về trạng thái ban đầu
Nếu trong lúc demo, dữ liệu bị thay đổi và cần đưa về đúng 62 lịch hẹn mẫu:
```powershell
.\run_online_demo.ps1 -ResetDatabase
```
Hoặc chạy riêng script phục hồi (mất chưa đầy 1 giây):
```powershell
.\reset_online_demo_db.ps1
```

### 5.3. Tắt hệ thống Demo Online an toàn
Khi kết thúc buổi demo, chạy:
```powershell
.\stop_online_demo.ps1
```
Script sẽ đóng đường hầm Cloudflare Tunnel và tắt toàn bộ tiến trình Backend/Frontend trên cổng 7351/7128.

### 5.4. Phương án dự phòng tại chỗ (Fallback Phương Án 2)
Nếu máy chủ bị mất kết nối Internet hoặc Cloudflare Tunnel gặp sự cố gián đoạn từ phía nhà mạng:
- Hệ thống demo cục bộ chuẩn vẫn sẵn sàng 100% tại: **`https://localhost:7129`**
- Sử dụng database được bảo vệ: **`DentalCare_SWP391_DEMO_V2_TEST`** (đã kiểm thử 854/854 tests PASS).
- Lệnh chạy demo cục bộ: `.\run_demo.ps1` (hoặc `.\run.bat`).

---

## 6. LƯU Ý VẬN HÀNH QUAN TRỌNG

1. **Chế độ ngủ của máy tính (Sleep Mode):**
   - Khi laptop gập màn hình hoặc rơi vào chế độ Sleep, card mạng sẽ tắt và Cloudflare Tunnel sẽ bị ngắt kết nối ngay lập tức (người dùng bên ngoài sẽ gặp lỗi 502/504 Bad Gateway).
   - **Biện pháp:** Trong thời gian diễn ra buổi demo, cắm sạc máy tính và chuyển chế độ Power & Sleep của Windows sang **"Never sleep when plugged in"**.
2. **Lưu trữ tệp hình ảnh & Phim X-quang:**
   - Hệ thống lưu trữ tệp chẩn đoán hình ảnh trên ổ cứng tại `D:\SWP391\DentalCare-OnlineDemo-Runtime\Storage\MedicalImaging`.
   - Toàn bộ ảnh X-quang tải lên trong quá trình demo được duy trì bền vững, không bị mất khi ứng dụng khởi động lại.
3. **Chế độ Email Sandbox:**
   - Cấu hình email trên môi trường online demo đã được kích hoạt chế độ sandbox (mật khẩu SMTP để trống), đảm bảo hệ thống không gửi email thông báo giả định tới hòm thư cá nhân thực của các tài khoản mẫu.

---

**Xác nhận nghiệm thu:**
- Kỹ sư DevOps: Senior .NET DevOps Engineer.
- Kết quả nghiệm thu: **100% ĐẠT YÊU CẦU BẢO VỆ DỮ LIỆU & TRUY CẬP INTERNET.**
