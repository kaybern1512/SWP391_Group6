# THƯ MỤC LƯU TRỮ CÁC SCRIPT PATCH VÀ SEED LỊCH SỬ (ARCHIVE)

Thư mục này lưu trữ các tệp script `.sql` và `.ps1` được sử dụng trong các đợt cập nhật và kiểm thử thành phần xuyên suốt quá trình phát triển dự án.

> [!NOTE]
> **Toàn bộ nội dung của các tệp trong thư mục này đã được tổng hợp, kiểm tra tính toàn vẹn khóa ngoại và chuẩn hóa 100% vào tệp master duy nhất tại thư mục cha:**
> 
> 👉 **`DB/DentalClinic_Full_Database_And_Data.sql`** (Khởi tạo toàn diện 44 bảng, ràng buộc, views, stored procedures và toàn bộ dữ liệu mẫu UTF-8).
>
> 👉 **`DB/DentalClinicManagementDB.bak`** (Bản sao lưu SQL Server nén chuẩn 2.0 MB).

## Danh sách tệp lưu trữ:
- `Add_AppointmentReminderJobs_Table.sql`: Patch bổ sung bảng `AppointmentReminderJobs` (WF-12).
- `Add_Medicine_Inventory_Tables.sql`: Patch bổ sung 5 bảng quản lý kho thuốc và cấp phát (WF-15, WF-16).
- `Add_Blog_Tables.sql` & `Optimize_Blog_Tables.sql`: Patch bổ sung các bảng truyền thông tin tức nha khoa (WF-20, WF-21).
- `Add_DepartmentReferral_Urgency.sql`: Patch cột Urgency cho chuyển tuyến chuyên khoa.
- `Fix_All_Database_Vietnamese_Complete.sql`, `Fix_Font_Mojikake_Clinical_Data.sql`, `Fix_Prescriptions_Vietnamese.sql`: Các patch xử lý sửa lỗi font tiếng Việt lịch sử.
- `Auto_Fix_All_Database_Mojikake.ps1`, `Deep_Scan_Mojikake.sql`, `Scan_All_Tables_Mojikake.sql`: Công cụ quét tự động lỗi mã hóa.
- `Seed_...`: Các kịch bản nạp dữ liệu kiểm thử theo từng giai đoạn (Test Scenarios, Booking, Demo Appointments, Clinical Data).
- `DentalClinicManagementDB_Live_Clone_Source.bak`: Bản sao lưu thô ban đầu trước khi chuẩn hóa và nén.
