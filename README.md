# DentalCare — Multi-Specialty Dental Clinic Management System

> **Hệ thống Quản lý Phòng khám Nha khoa Đa Chuyên khoa DentalCare**
> Dự án môn học SWP391 — Nhóm 6 (Group 6)
> Nền tảng: ASP.NET Core 8 (.NET 8 Clean Architecture) & SQL Server 2022 / Express

---

## 1. TỔNG QUAN HỆ THỐNG

**DentalCare** là giải pháp quản lý phòng khám nha khoa toàn diện, hỗ trợ xuyên suốt từ bước tiếp nhận bệnh nhân, đặt lịch khám trực tuyến, khám lâm sàng, chỉ định và thực hiện chụp X-quang chuyên khoa, lập phác đồ điều trị đa giai đoạn, kê đơn thuốc điện tử đến xuất hóa đơn thanh toán và chăm sóc tái khám.

### 1.1. Mô hình Phân quyền 4 Roles (RBAC)
Hệ thống tuân thủ chặt chẽ nguyên tắc bảo mật và phân quyền 4 vai trò y tế tiêu chuẩn:
- **Patient (Bệnh nhân):** Tra cứu danh mục dịch vụ, đặt lịch hẹn trực tuyến, theo dõi tiến trình điều trị, xem hồ sơ bệnh án cá nhân, kết quả chụp X-quang và lịch sử thanh toán hóa đơn.
- **Receptionist (Nhân viên Lễ tân):** Tiếp đón bệnh nhân walk-in, tiếp nhận và xác nhận lịch hẹn trực tuyến, điều phối phân bổ phòng khám/ghế nha/bác sĩ, cấp số thứ tự khám (Queue Number), thu viện phí, xuất hóa đơn tài chính và quản lý ca làm việc.
- **Dentist (Bác sĩ Nha khoa):** Tiếp nhận ca khám, ghi nhận chẩn đoán và sơ đồ răng (Odontogram), chỉ định chụp X-quang chuyên khoa, lập kế hoạch điều trị đa buổi, kê đơn thuốc và phê duyệt kết quả cận lâm sàng.
- **SystemAdministrator (Quản trị viên Hệ thống):** Quản trị danh mục chuyên khoa, danh mục dịch vụ kỹ thuật, quản lý hồ sơ nhân sự/bác sĩ/lễ tân, cấu hình phòng khám/ghế nha và giám sát nhật ký vận hành (Audit Logs).

### 1.2. Cơ cấu 3 Chuyên khoa & 12 Dịch vụ Kỹ thuật Chuẩn

| Mã Khoa | Tên Chuyên Khoa | Danh mục Dịch vụ Kỹ thuật |
| :---: | :--- | :--- |
| **Dept 1** | **Khoa Răng Tổng Quát** | 1. Khám nha khoa tổng quát (`GEN-EXAM`)<br>2. Lấy cao răng siêu âm (`GEN-SCALING`)<br>3. Trám răng Composite (`GEN-FILLING`)<br>4. Điều trị tủy răng (`GEN-ROOTCANAL`) |
| **Dept 2** | **Khoa Chỉnh Nha & Phục Hình** | 5. Tư vấn chỉnh nha & phục hình (`ORT-CONSULT`)<br>6. Niềng răng mắc cài kim loại (`ORT-BRACES`)<br>7. Bọc răng sứ Cercon (`ORT-CROWN`)<br>8. Phục hình răng tháo lắp (`ORT-DENTURE`) |
| **Dept 3** | **Khoa Chẩn Đoán Hình Ảnh X-quang** | 9. X-quang Panorama toàn cảnh (`XR-PANO`)<br>10. X-quang quanh chóp răng (`XR-PERI`)<br>11. X-quang Bitewing cánh cắn (`XR-BITE`)<br>12. X-quang Cephalometric sọ nghiêng (`XR-CEPH`) |

---

## 2. KIẾN TRÚC KỸ THUẬT

DentalCare được xây dựng theo kiến trúc **Clean Architecture** kết hợp mô hình kết nối Web API và MVC:

```
                  ┌─────────────────────────────────────┐
                  │          DentalClinic.Web           │
                  │   (ASP.NET Core 8 MVC, Razor UI)    │
                  └──────────────────┬──────────────────┘
                                     │ HTTPS / REST API
                                     ▼
                  ┌─────────────────────────────────────┐
                  │          DentalClinic.Api           │
                  │      (ASP.NET Core 8 Web API)       │
                  └──────────────────┬──────────────────┘
                                     │
           ┌─────────────────────────┼─────────────────────────┐
           ▼                         ▼                         ▼
┌─────────────────────┐   ┌─────────────────────┐   ┌─────────────────────┐
│ DentalClinic.Domain │   │DentalClinic.Applic..│   │DentalClinic.Infrast.│
│ (Entities & Enums)  │   │  (Interfaces & DTOs)│   │ (EF Core & Services)│
└─────────────────────┘   └─────────────────────┘   └──────────┬──────────┘
                                                               │ ADO.NET / EF Core 8
                                                               ▼
                                                    ┌─────────────────────┐
                                                    │  MS SQL Server 2022 │
                                                    │  (Database V2 Test) │
                                                    └─────────────────────┘
```

- **Backend API:** ASP.NET Core 8 Web API, Swagger OpenAPI, JWT Authentication, EF Core 8.
- **Frontend Web:** ASP.NET Core 8 MVC, Razor Views, Bootstrap 5, Cookie Authentication.
- **Cơ sở Dữ liệu:** Microsoft SQL Server 2022 / Express (`.\SQLEXPRESS`), Collation hỗ trợ Unicode tiếng Việt đầy đủ (`Latin1_General_CI_AS` / UTF-8 Strings).

---

## 3. YÊU CẦU MÔI TRƯỜNG & CÀI ĐẶT

### 3.1. Phần mềm Yêu cầu
- **.NET 8.0 SDK** (phiên bản `8.0.100` trở lên)
- **SQL Server 2019/2022 hoặc SQL Server Express** (`.\SQLEXPRESS`)
- **Trình duyệt web hiện đại:** Chrome, Edge, Firefox

### 3.2. Thiết lập Cơ sở Dữ liệu
Hệ thống sử dụng đúng **2 file SQL bootstrap chính thức** đặt tại thư mục `DB/`:
1. `DB/DentalCare_Schema.sql`: Tạo cấu trúc 38 bảng vật lý chuẩn.
2. `DB/DentalCare_Seed.sql`: Nạp toàn bộ dữ liệu mẫu, 3 khoa, 6 bác sĩ, 3 lễ tân, 1 admin, 32 bệnh nhân, và 62 ca khám mẫu từ 01/11/2026 đến 19/11/2026.

Xem hướng dẫn chi tiết tại: **[DB/README.md](DB/README.md)**.

---

## 4. HƯỚNG DẪN KHỞI CHẠY ỨNG DỤNG

### 4.1. Khởi chạy Nhanh (Khuyến nghị cho Demo)
Mở PowerShell tại thư mục gốc repository và chạy:

```powershell
.\run_demo.ps1
```

Script sẽ tự động:
1. Kiểm tra kết nối SQL Server tới database `DentalCare_SWP391_DEMO_V2_TEST`.
2. Dọn dẹp các cổng mạng xung đột (7350, 7129).
3. Khởi chạy Backend API (port `7350`) và Frontend Web (port `7129`).
4. Kiểm tra sức khỏe HTTP 200 và tự động mở trình duyệt.

Để dừng hệ thống, chạy:
```powershell
.\stop_demo.ps1
```

### 4.2. Khởi chạy Thủ công bằng CLI

**Terminal 1 — Khởi chạy Backend API:**
```powershell
cd BE\DentalClinic.Api
$env:ConnectionStrings__DentalClinicDatabase = "Server=.\SQLEXPRESS;Database=DentalCare_SWP391_DEMO_V2_TEST;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet run --launch-profile https
```
> Backend API lắng nghe tại: `https://localhost:7350` (Swagger: `https://localhost:7350/swagger`)

**Terminal 2 — Khởi chạy Frontend Web:**
```powershell
cd FE\DentalClinic.Web
$env:ApiSettings__BaseUrl = "https://localhost:7350"
dotnet run --launch-profile https
```
> Frontend Web truy cập tại: `https://localhost:7129`

---

## 5. TÀI KHOẢN ĐĂNG NHẬP MẪU

Tất cả tài khoản demo sử dụng mật khẩu dùng thử đã provision: `DentalCare@2026!`

| Vai trò (Role) | Email Đăng Nhập | Họ và Tên Nhân sự | Ghi chú Phân bổ |
| :--- | :--- | :--- | :--- |
| **SystemAdministrator** | `admin.demo@dentalcare.test` | Quản trị viên Demo | Quản trị toàn hệ thống |
| **Receptionist** | `reception.demo01@dentalcare.test` | Nguyễn Thu Hương | Quầy tiếp đón & Thu ngân |
| **Dentist (Dept 1)** | `dentist.gen01@dentalcare.test` | BS. Lê Minh Quân | Khoa Răng Tổng Quát |
| **Dentist (Dept 2)** | `dentist.ort01@dentalcare.test` | BS. Hoàng Gia Bảo | Khoa Chỉnh Nha & Phục Hình |
| **Dentist (Dept 3)** | `dentist.img01@dentalcare.test` | BS. Trương Hải Đăng | Khoa Chẩn Đoán Hình Ảnh X-quang |
| **Patient** | `patient.demo01@dentalcare.test` | Nguyễn Minh Anh | Bệnh nhân đặt lịch trực tuyến |

---

## 6. TÀI LIỆU THAM KHẢO & LIÊN KẾT

- **[DB/README.md](DB/README.md):** Hướng dẫn tạo lập và cấu hình cơ sở dữ liệu DentalCare V2.
- **[docs/DEMO_GUIDE.md](docs/DEMO_GUIDE.md):** Kịch bản chi tiết 6 luồng demo nghiệp vụ cho buổi báo cáo.
- **[docs/TEST_SUMMARY.md](docs/TEST_SUMMARY.md):** Báo cáo kết quả kiểm thử tự động (803/803 tests pass).
- **[ARCHITECTURE.md](ARCHITECTURE.md):** Tài liệu phân tích thiết kế kiến trúc hệ thống chi tiết.
