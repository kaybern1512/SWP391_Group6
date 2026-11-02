# THƯ MỤC CƠ SỞ DỮ LIỆU - DENTAL CLINIC MANAGEMENT SYSTEM (SWP391 GROUP 6)

Thư mục này chứa toàn bộ cấu trúc cơ sở dữ liệu (38 bảng khớp 100% với EF Core DbContext), file backup `.bak` và dữ liệu mẫu chuẩn UTF-8 (toàn bộ 33 tài khoản cùng quy trình khám chữa bệnh, hóa đơn, đơn thuốc, bài viết truyền thông, tin tức sự kiện, tin nhắn hỗ trợ) phục vụ kiểm thử và thuyết trình bảo vệ đồ án.

---

## 📦 Danh Sách Các Tệp Chính Đã Chuẩn Hóa

| Tên Tệp | Ý Nghĩa / Mục Đích Sử Dụng | Cách Dùng |
| :--- | :--- | :--- |
| **`DentalClinic_Full_Database_And_Data.sql`** | **Script TỔNG HỢP TOÀN DIỆN (1 BƯỚC)**: Tự động khởi tạo database, toàn bộ 38 bảng, khóa ngoại, indexes, computed columns và nạp đầy đủ dữ liệu mẫu chuẩn UTF-8 (không lỗi font). | Chạy 1 lần duy nhất trong SSMS hoặc PowerShell. |
| **`DentalClinicManagementDB.bak`** | **File Backup SQL Server chuẩn nén (1.4 MB)**: Chứa toàn bộ 38 bảng và dữ liệu hoàn chỉnh, backup trực tiếp từ SQL Server. | Dùng tính năng *Restore Database* trong SSMS hoặc lệnh `RESTORE DATABASE`. |
| **`Dental_Clinic_Final_Database_Clean.sql`** | Script tạo cấu trúc database sạch (Schema Only: 38 Bảng, Ràng buộc, Computed Columns, Indexes). | Dành cho môi trường muốn tự khởi tạo database rỗng. |
| **`Generate_Standardized_DB_Files.ps1`** | Script PowerShell tự động xuất khẩu Schema, Full Data và Backup `.bak` từ Database đang chạy bằng SMO. | Chạy khi cập nhật cấu trúc DB để đồng bộ lại các file `.sql` và `.bak`. |

---

## 🚀 Hướng Dẫn Nạp Dữ Liệu Nhanh (Chọn 1 trong 2 cách)

### Cách 1: Chạy Script SQL Tổng Hợp (Khuyên dùng)
Chạy lệnh sau trên terminal PowerShell tại thư mục gốc (thay `.\SQLEXPRESS` bằng instance của bạn nếu dùng `.\MSSQLSERVER01` hoặc `(localdb)\mssqllocaldb`):
```powershell
sqlcmd -S .\SQLEXPRESS -E -i "DB\DentalClinic_Full_Database_And_Data.sql"
```

### Cách 2: Restore từ file Backup (`DentalClinicManagementDB.bak`)
```powershell
sqlcmd -S .\SQLEXPRESS -E -Q "RESTORE DATABASE DentalClinicManagementDB FROM DISK = '$(Get-Location)\DB\DentalClinicManagementDB.bak' WITH REPLACE;"
```
Hoặc mở **SQL Server Management Studio (SSMS)**:
1. Chuột phải vào mục **Databases** $\rightarrow$ chọn **Restore Database...**
2. Chọn **Device** $\rightarrow$ click nút `...` $\rightarrow$ chọn **Add** $\rightarrow$ duyệt đến file `DentalClinicManagementDB.bak` trong thư mục `DB/`.
3. Nhấn **OK** để khôi phục.

---

## 🔑 Thông Tin Đăng Nhập Mẫu
* **Mật khẩu dùng chung cho toàn bộ tài khoản:** `Password123@`
* **Admin:** `admin@dentalcare.com`
* **Trưởng khoa Răng Tổng Quát:** `manager.tongquat@dentalcare.com`
* **Bác sĩ Chuyên khoa:** `dentist.hung@dentalcare.com`
* **Lễ tân bàn tiếp đón:** `receptionist@dentalcare.com`
* **Bệnh nhân:** `patient.tuan@gmail.com`
* Xem chi tiết bảng phân công tài khoản và kịch bản demo 7 bước tại file [SETUP.md](../SETUP.md).
