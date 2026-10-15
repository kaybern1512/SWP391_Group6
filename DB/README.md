# THƯ MỤC CƠ SỞ DỮ LIỆU - DENTAL CLINIC MANAGEMENT SYSTEM (SWP391 GROUP 6)

Thư mục này chứa toàn bộ cấu trúc cơ sở dữ liệu và dữ liệu mẫu (33 tài khoản cùng toàn bộ quy trình khám chữa bệnh, hóa đơn, đơn thuốc, lịch hẹn tái khám) phục vụ chạy kiểm thử và thuyết trình bảo vệ đồ án.

---

## 📦 Danh Sách Các Tệp Chính

| Tên Tệp | Ý Nghĩa / Mục Đích Sử Dụng | Cách Dùng |
| :--- | :--- | :--- |
| **`DentalClinic_Full_Database_And_Data.sql`** | **Script TỔNG HỢP TOÀN DIỆN (1 BƯỚC)**: Tự động khởi tạo database, toàn bộ bảng, khóa ngoại, index, triggers và nạp 33 tài khoản mẫu kèm quy trình khám bệnh hoàn chỉnh. | Chạy 1 lần duy nhất trong SSMS hoặc PowerShell. |
| **`DentalClinicManagementDB.bak`** | **File Backup SQL Server chuẩn**: Chứa toàn bộ database và dữ liệu đã được backup trực tiếp từ hệ thống. | Dùng tính năng *Restore Database* trong SSMS hoặc lệnh `RESTORE DATABASE`. |
| **`Dental_Clinic_Final_Database_Clean.sql`** | Script tạo cấu trúc database sạch (Schema Only: Bảng, Ràng buộc, Triggers, Views, Indexes). | Dành cho ai muốn tự khởi tạo database rỗng. |
| **`Seed_Complete_Demo_Dataset.sql`** | Script nạp 33 tài khoản demo và toàn bộ dữ liệu lâm sàng (Hôm nay 15/10/2026, sảnh chờ, ghế khám, đơn thuốc, chuyển khoa, tái khám). | Nạp dữ liệu vào database đã có schema. |

---

## 🚀 Hướng Dẫn Nạp Dữ Liệu Nhanh (Chọn 1 trong 2 cách)

### Cách 1: Chạy Script SQL Tổng Hợp (Khuyên dùng)
Chạy lệnh sau trên terminal PowerShell tại thư mục gốc:
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

## 🔑 Thông Tin Đăng Nhập
* **Mật khẩu dùng chung cho toàn bộ 33 tài khoản:** `Password123@`
* Xem chi tiết bảng phân công tài khoản và kịch bản demo 7 bước tại file [SETUP.md](../SETUP.md).
