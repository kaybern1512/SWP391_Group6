# THƯ MỤC CƠ SỞ DỮ LIỆU - DENTAL CLINIC MANAGEMENT SYSTEM (SWP391 GROUP 6)

Thư mục này chứa toàn bộ cấu trúc cơ sở dữ liệu (**69 bảng, Views, Stored Procedures khớp 100% với kiến trúc phát hành chính thức**), file backup SQL Server `.bak` và dữ liệu mẫu chuẩn hóa UTF-8 tiếng Việt hoàn chỉnh phục vụ kiểm thử, nghiệm thu và bảo vệ đồ án.

Hệ thống hoạt động theo **mô hình phân quyền 4 vai trò chính thức**:
1. `Patient`
2. `Receptionist`
3. `Dentist`
4. `SystemAdministrator`

---

## 📦 Danh Sách Các Tệp Phát Hành Cốt Lõi

| Tên Tệp | Ý Nghĩa / Mục Đích Sử Dụng | Cách Dùng |
| :--- | :--- | :--- |
| **`DentalClinic_Full_Database_Release.sql`** / **`DentalClinic_Full_Database_And_Data.sql`** | **Script TỔNG HỢP TOÀN DIỆN (1 BƯỚC KHUYÊN DÙNG)**: Khởi tạo toàn bộ 69 bảng, ràng buộc khóa ngoại, indexes, views, stored procedures và nạp đầy đủ dữ liệu mẫu chuẩn UTF-8 vào database đích rỗng. Tích hợp cơ chế **Target Database Safety Guard** ngăn chặn hoàn toàn việc chạy nhầm vào database hệ thống, database đang sử dụng hoặc database có dữ liệu sẵn có. | Chỉ định database rỗng và chạy qua `sqlcmd -b` hoặc SSMS. |
| **`DentalClinic_Release_Final.bak`** / **`DentalClinicManagementDB.bak`** | **File Backup SQL Server chuẩn phát hành (22.8 MB)**: Chứa toàn bộ 69 bảng và dữ liệu demo 4 vai trò hoàn chỉnh, backup trực tiếp từ SQL Server. Khuyên dùng cho hội đồng chấm thi và thành viên nhóm để khôi phục tức thì. | Dùng tính năng *Restore Database* trong SSMS hoặc lệnh `RESTORE DATABASE`. |
| **`Dental_Clinic_Final_Database_Clean.sql`** | Script tạo cấu trúc database sạch (Schema Only: 69 Bảng, Views, Stored Procedures, Ràng buộc, Computed Columns, Indexes). | Dành cho môi trường muốn tự khởi tạo database rỗng (Production/Staging). |
| **`DentalClinicManagementDB_legacy_backup.bak`** | Bản sao lưu trữ an toàn của file backup cũ trước khi tích hợp CareEpisode, X-ray Roster & Role Cleanup. | Lưu trữ đối chiếu (Archive). |

---

## 🚀 Hướng Dẫn Khôi Phục Nhanh (Chọn 1 trong 2 cách)

### Cách 1: Chạy Script SQL Tổng Hợp Vào Database Rỗng
Chạy các lệnh sau trên terminal PowerShell tại thư mục gốc của dự án:

```powershell
# Bước 1: Tạo database rỗng mới cho DentalCare (ví dụ: DentalCare_Release_DB)
sqlcmd -S .\SQLEXPRESS -E -b -Q "CREATE DATABASE DentalCare_Release_DB COLLATE SQL_Latin1_General_CP1_CI_AS;"

# Bước 2: Thực thi script SQL Master vào database rỗng (-b để fail-fast dừng ngay lập tức nếu gặp lỗi)
sqlcmd -S .\SQLEXPRESS -E -d DentalCare_Release_DB -b -f 65001 -i "DB\DentalClinic_Full_Database_Release.sql"
```

> [!IMPORTANT]
> **Cơ Chế Bảo Vệ An Toàn (Target Database Safety Guard):**
> - **Chặn database hệ thống:** Tuyệt đối không cho phép thực thi script vào các database hệ thống (`master`, `tempdb`, `model`, `msdb`).
> - **Chặn database đang hoạt động/được bảo vệ:** Ngăn chặn tuyệt đối việc ghi đè lên các database được bảo vệ: `DentalClinicManagementDB`, `DentalCare_Integration_Dev`, `DentalCare_Release_Clean_QA`, `DentalCare_Restore_Verification_QA`.
> - **Chống xóa dữ liệu database không rỗng:** Script sẽ **từ chối thực thi ngay lập tức (fail-fast)** nếu database đích chứa bất kỳ bảng người dùng nào sẵn có, tuyệt đối không tự động xóa bảng hoặc xóa dữ liệu cũ.
> - **Tham số `-b` bắt buộc:** Đảm bảo `sqlcmd` dừng ngay lập tức khi gặp bất kỳ lỗi nào, không tiếp tục chạy các batch còn lại.

---

### Cách 2: Restore từ file Backup (`DentalClinic_Release_Final.bak` hoặc `DentalClinicManagementDB.bak`)
Chạy lệnh PowerShell:
```powershell
sqlcmd -S .\SQLEXPRESS -E -b -Q "RESTORE DATABASE DentalClinicManagementDB FROM DISK = '$(Get-Location)\DB\DentalClinic_Release_Final.bak' WITH REPLACE;"
```

Hoặc qua giao diện đồ họa **SQL Server Management Studio (SSMS)**:
1. Chuột phải vào mục **Databases** $\rightarrow$ chọn **Restore Database...**
2. Chọn **Device** $\rightarrow$ click nút `...` $\rightarrow$ chọn **Add** $\rightarrow$ duyệt đến file `DentalClinic_Release_Final.bak` (hoặc `DentalClinicManagementDB.bak`) trong thư mục `DB/`.
3. Trong mục **Files**, kiểm tra đường dẫn lưu trữ `.mdf` và `.ldf`.
4. Trong mục **Options**, tích chọn **Overwrite the existing database (WITH REPLACE)** và **Close existing connections**.
5. Nhấn **OK** để khôi phục.

---

## 🔑 Thông Tin Đăng Nhập Mẫu (Chuẩn 4 Vai Trò)

* **Mật khẩu dùng chung cho TOÀN BỘ tài khoản:** `Password123@`

| Vai trò | Email đăng nhập | Tên người dùng đại diện | Quyền hạn chính |
| :--- | :--- | :--- | :--- |
| **SystemAdministrator** | `admin@dentalcare.com` | Quản Trị Hệ Thống | Toàn quyền kiểm soát tài khoản, nhân sự, lịch trực X-quang, kho thuốc, kiểm duyệt bài viết, báo cáo doanh thu. |
| **Receptionist** | `receptionist@dentalcare.com` | Trần Thị Mai Anh | Tiếp đón, điều phối hàng đợi lâm sàng, xác nhận lịch hẹn (cấp QueueNumber ngay khi Confirmed), thu ngân và thanh toán hóa đơn. |
| **Dentist** | `dentist@dentalcare.com` (hoặc `dentist1@dentalcare.com`) | BS. Trần Văn Hùng | Khám lâm sàng, chỉ định dịch vụ trong đợt khám (CareEpisode), hội chẩn chuyên khoa, cấp phát thuốc, trực phòng X-quang theo ca. |
| **Dentist** | `dentist2@dentalcare.com` | BS. Phương Thảo | Bác sĩ Chỉnh nha - Tiếp nhận bệnh nhân chuyển khoa từ Bác sĩ Hùng trong cùng CareEpisode. |
| **Patient** | `patient1@dentalcare.com` | Trần Mai Chi | Đặt lịch hẹn trực tuyến, nhận email xác nhận kèm QueueNumber, theo dõi tiến trình khám và thanh toán hóa đơn VietQR. |

---

## 🛠️ Cấu Hình Chuỗi Kết Nối Mẫu (Sample Connection Strings)

Không lưu thông tin mật khẩu nhạy cảm trực tiếp trong mã nguồn. Dưới đây là cấu hình mẫu:

### 1. Windows Authentication (Môi trường cục bộ / Local Dev)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=DentalClinicManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
}
```

### 2. SQL Server Authentication (Môi trường Server / Staging)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER_HOST;Database=DentalClinicManagementDB;User Id=YOUR_DB_USER;Password=YOUR_SECURE_PASSWORD;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
}
```

---

## 📋 Danh Sách Các Bản Nâng Cấp Schema (Database Migrations)

| Thời Gian | File Migration | Nội Dung Nghiệp Vụ Tích Hợp |
| :--- | :--- | :--- |
| **2026-11-09** | `20261109_Phase0_Database_Standardization_4Roles.sql` | Chuẩn hóa hệ thống về đúng 4 roles: `Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`. Ràng buộc `CK_UserAccounts_Role`. |
| **2026-11-09** | `20261109_Add_DoctorLeave_And_AppointmentDisruption.sql` | Quản lý nghỉ phép bác sĩ, tự động phát hiện và xử lý gián đoạn lịch hẹn (`AppointmentDisruptions`). |
| **2026-11-10** | `20261110_Add_Imaging_Room_Equipment_And_Queue_Schema.sql` | Phân hệ chẩn đoán hình ảnh: phòng chụp, thiết bị, phiên chụp X-quang/CT. |
| **2026-11-10** | `20261110_Receptionist_Shift_Leave_Workforce_Schema.sql` | Quản lý ca trực, xin nghỉ và đổi ca cho nhân sự Lễ tân. |
| **2026-11-10** | `20261110_Add_Staff_Attendance_And_Operations_Tables.sql` | Quản lý chấm công, bảng công và chính sách chấm công nhân sự. |
| **2026-11-10** | `20261110_Add_ServiceCombos_And_Appointment_Combo_Tracking.sql` | Gói dịch vụ nha khoa (ServiceCombos) và theo dõi dịch vụ combo trong lịch hẹn. |
| **2026-11-11** | `20261111_Add_CareEpisodes_ServiceTasks_And_SingleInvoiceAggregation.sql` | Quản lý đợt khám tập trung (`CareEpisodes`), tác vụ điều trị bổ sung (`EpisodeServiceTasks`) và gộp một hóa đơn tổng thanh toán. |
| **2026-11-12** | `20261112_Add_Dedicated_Xray_Dentist_Roster.sql` | Lịch trực bác sĩ chuyên trách phòng X-quang theo ca (`XrayDentistRosters`). |
