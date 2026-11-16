# BÁO CÁO ĐÓNG BĂNG PHÁT HÀNH KHẨN CẤP (EMERGENCY RELEASE FREEZE)
## DentalCare Clinic Management System — SWP391 Group 6
**Thời điểm thực hiện:** Ngày 16/11/2026  
**Nhánh Git:** `develop`  
**Môi trường Database:** `DentalCare_SWP391_UAT_Fixture_TEST` trên `.\SQLEXPRESS`  
**Backend API:** `https://localhost:7350` (HTTP: `http://localhost:5350`)  
**Frontend MVC:** `https://localhost:7129` (HTTP: `http://localhost:5129`)  

---

## 1. TỔNG KẾT HÀNH TRÌNH KIỂM THỬ 7 BƯỚC CỐT LÕI (E2E LIVE RUN)

Toàn bộ 7 bước nghiệp vụ yêu cầu đã được chạy kiểm thử trực tiếp trên live server với database test thật. Kết quả: **7/7 BƯỚC PASS 100%**.

| Bước | Role thực hiện | Hành động nghiệp vụ | Dữ liệu đầu vào / Kết quả thực tế | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **1** | **SystemAdministrator** (`admin@dentalcare.test`) | Phân công bác sĩ (Lê Văn Bác Sĩ Một - ID 3) vào ca sáng chuẩn tương lai | Ngày: `2026-12-07`, Ca sáng: `08:00 - 12:00`. DB ghi nhận: `ScheduleId = 130`, trạng thái `Scheduled`. | **PASS** |
| **2** | **Patient** (`patient@dentalcare.test`) | Xem danh mục dịch vụ và đặt lịch khám đúng bác sĩ & khung giờ | Dịch vụ: *Khám & Lấy Cao Răng* (200.000 VNĐ). Thời gian: `2026-12-07 08:30:00`. Đặt thành công: `AppointmentId = 51`, trạng thái `PendingReceptionReview`. | **PASS** |
| **3** | **Receptionist** (`receptionist@dentalcare.test`) | Tiếp nhận xác nhận lịch hẹn, phân bổ tài nguyên và check-in | Phân bổ: Phòng 1, Ghế 1, Bác sĩ 3. Xác nhận thành công -> trạng thái `Confirmed`. Check-in bệnh nhân -> trạng thái `CheckedIn`. | **PASS** |
| **4** | **Dentist** (`dentist1@dentalcare.test`) | Bắt đầu khám, ghi bệnh án, ghi nhận dịch vụ thực hiện và hoàn tất ca khám | Bắt đầu khám -> trạng thái `InService`. Ghi bệnh án: `EntryId = 9` (Chẩn đoán: *Viêm nướu răng độ 1*). Dịch vụ thực hiện: `PerformedServiceId = 16`. Hoàn tất -> trạng thái `Completed`. | **PASS** |
| **5** | **Dentist** (`dentist1@dentalcare.test`) | Kê đơn thuốc và xuất cấp thuốc từ kho dược phòng khám | Kê đơn: *Paracetamol 500mg* (10 viên) -> `PrescriptionId = 6`, `ItemId = 15`. Xuất kho dược: `DispensationId = 6`, trạng thái `Dispensed`, trừ tồn kho tự động. | **PASS** |
| **6** | **Receptionist** (`receptionist@dentalcare.test`) | Lấy thông tin thanh toán chuẩn bị, lập hóa đơn viện phí và thu tiền | Tự động tải 2 hạng mục (Dịch vụ 200.000 + Thuốc 20.000 = 220.000 VNĐ). Lập hóa đơn: `InvoiceId = 14`, mã `INV-20261116-0003`. Thu tiền mặt -> trạng thái `Paid`. | **PASS** |
| **7** | **Patient** (`patient@dentalcare.test`) | Bệnh nhân xem lịch sử khám và hóa đơn viện phí đã thanh toán | Ca khám `Id = 51` hiển thị trạng thái `Completed`. Hóa đơn `Id = 14` hiển thị đầy đủ chi tiết với số tiền 220.000 VNĐ, trạng thái `Paid`. | **PASS** |

---

## 2. KẾT QUẢ AUTOMATED REGRESSION TEST SUITE

Cả hai dự án Backend và Frontend đều đã được chạy test tự động:

```
[Backend Unit & Integration Tests]
Test run for BE\DentalClinic.Tests\bin\Debug\net8.0\DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
Passed!  - Failed: 0, Passed: 309, Skipped: 0, Total: 309, Duration: 15 s

[Frontend Web Tests]
Test run for FE\DentalClinic.Web.Tests\bin\Debug\net8.0\DentalClinic.Web.Tests.dll (.NETCoreApp,Version=v8.0)
Passed!  - Failed: 0, Passed: 339, Skipped: 0, Total: 339, Duration: 20 s

========================================================================================
TỔNG HỢP: 648 / 648 TESTS PASSED (100% PASS RATE, 0 FAILED, 0 SKIPPED)
========================================================================================
```

---

## 3. DANH SÁCH TÀI KHOẢN VÀ MẬT KHẨU DEMO (4 ROLES)

Tất cả tài khoản demo trong hệ thống đều dùng chung một mật khẩu chuẩn:  
🔑 **Mật khẩu:** `DentalCare@2026!`

| Role | Tên người dùng | Email đăng nhập | Ghi chú vai trò |
| :--- | :--- | :--- | :--- |
| **SystemAdministrator** | Hoàng Quản Trị Viên | `admin@dentalcare.test` | Toàn quyền cấu hình phòng, ghế, dịch vụ, ca trực bác sĩ |
| **Receptionist 1** | Trần Thị Lễ Tân | `receptionist@dentalcare.test` | Tiếp đón, xác nhận lịch, check-in, lập hóa đơn & thu ngân |
| **Receptionist 2** | Lê Thị Lễ Tân Hai | `receptionist2@dentalcare.test` | Lễ tân ca chiều / dự phòng |
| **Dentist 1** | Lê Văn Bác Sĩ Một | `dentist1@dentalcare.test` | Bác sĩ chuyên khoa Răng Tổng Quát (Khoa 1) |
| **Dentist 2** | Bác Sĩ Hùng | `dentist.hung@dentalcare.test` | Bác sĩ chuyên khoa / trực X-quang |
| **Patient** | Nguyễn Văn Bệnh Nhân | `patient@dentalcare.test` | Đặt lịch khám, theo dõi hồ sơ điều trị & hóa đơn |

---

## 4. HƯỚNG DẪN KHỞI ĐỘNG VÀ CHẠY DEMO THỦ CÔNG

### Bước 1: Mở Command Prompt / PowerShell tại thư mục dự án
```powershell
cd D:\SWP391\DentalCare-DB-Simplification
```

### Bước 2: Khởi động Backend API
Mở một cửa sổ terminal riêng và chạy:
```powershell
cd BE\DentalClinic.Api
dotnet run --launch-profile https
```
*API sẽ chạy tại: `https://localhost:7350` (Swagger UI: `https://localhost:7350/swagger`).*

### Bước 3: Khởi động Frontend Web MVC
Mở một cửa sổ terminal thứ hai và chạy:
```powershell
cd FE\DentalClinic.Web
dotnet run --launch-profile https
```
*Web App sẽ chạy tại: `https://localhost:7129`.*

### Bước 4: Mở trình duyệt và truy cập
- Trang chủ / Đăng nhập: `https://localhost:7129/Account/Login`
- Đăng nhập bằng các tài khoản ở Bảng mục 3 để thực hiện demo.
