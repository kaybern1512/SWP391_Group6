# HƯỚNG DẪN CẤU HÌNH & CHẠY LOCAL MEDICAL IMAGING (DENTALCARE)
**Dành cho toàn bộ thành viên nhóm SWP391_Group6**

---

## 1. Giới Thiệu
Chức năng **Medical Imaging (Chẩn đoán hình ảnh y tế / Phim X-quang)** trong DentalCare hiện đã được chuyển sang cơ chế **Local File Storage**:
* **Không yêu cầu tài khoản Cloudinary** hay credentials bên ngoài.
* File ảnh X-quang (.jpg, .jpeg, .png) và tệp y tế chuyên dụng DICOM (.dcm, .dicom) được lưu trữ an toàn trực tiếp trên ổ đĩa của máy chạy Backend.
* File được bảo vệ qua endpoint bảo mật (kiểm tra JWT & quyền truy cập bệnh án của Bác sĩ / Bệnh nhân), không mở trực tiếp ra web.
* Dễ dàng sao lưu, di chuyển và demo trên bất kỳ laptop nào của nhóm vào cuối kỳ.

---

## 2. Thư Mục Lưu Trữ Ảnh X-Quang Nằm Ở Đâu?
Theo mặc định, Backend lưu toàn bộ file hình ảnh X-quang tại thư mục Application Data của người dùng:
* **Đường dẫn trên Windows:**
  ```text
  %LOCALAPPDATA%\DentalCare\MedicalImaging
  ```
  *(Ví dụ thực tế: `C:\Users\<Tên_User>\AppData\Local\DentalCare\MedicalImaging`)*
* Thư mục này nằm **bên ngoài mã nguồn** và **bên ngoài wwwroot**, đảm bảo file **không bị xóa** khi `clean/rebuild solution`, không bị commit nhầm vào Git và vẫn tồn tại sau khi restart máy.
* Cấu trúc thư mục con bên trong:
  ```text
  MedicalImaging/
  └── entry_<ClinicalEntryId>/
      ├── img_20261027133000_a1b2c3d4e5f6.png
      └── img_20261027133500_9876543210ab.dcm
  ```
* *(Tùy chọn nâng cao)*: Nếu muốn đổi sang thư mục khác (ví dụ `D:\DentalCareData`), chỉ cần đặt trong `appsettings.json` hoặc User Secrets:
  ```json
  "MedicalImagingStorage": {
    "Provider": "Local",
    "LocalPath": "D:\\DentalCareData"
  }
  ```

---

## 3. Cách Pull Code & Chạy Ứng Dụng Trên Máy Cá Nhân

### Bước 1: Pull code mới nhất
Mở Git Bash / Terminal tại thư mục dự án:
```bash
git checkout feature/imaging-local-storage
git pull
```

### Bước 2: Kiểm tra kết nối SQL Server
Dự án sử dụng cơ sở dữ liệu `DentalClinicManagementDB`.
* Nếu máy bạn đã có sẵn SQL Server và database `DentalClinicManagementDB`, chỉ cần đảm bảo chuỗi kết nối chính xác trong file `BE/DentalClinic.Api/appsettings.json` hoặc cấu hình qua User Secrets:
  ```bash
  dotnet user-secrets set "ConnectionStrings:DentalClinicDatabase" "Server=.\\<Tên_Instance_SQL>;Database=DentalClinicManagementDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" --project BE/DentalClinic.Api
  ```
  *(Ví dụ instance: `.\\SQL2025_DUC`, `.\\SQLEXPRESS`, hoặc `.` nếu là default instance)*.
* **Lưu ý:** Tuyệt đối không tạo lại database mới nếu database đã có sẵn dữ liệu của nhóm.

### Bước 3: Khởi động Backend API
Mở một cửa sổ Terminal:
```bash
cd BE/DentalClinic.Api
dotnet run
```
Backend sẽ lắng nghe tại:
* HTTPS: `https://localhost:7350`
* HTTP: `http://localhost:5350`
* Swagger UI: `https://localhost:7350/swagger`

### Bước 4: Khởi động Frontend Web
Mở một cửa sổ Terminal thứ hai:
```bash
cd FE/DentalClinic.Web
dotnet run
```
Frontend sẽ lắng nghe tại:
* HTTPS: `https://localhost:7129`
* HTTP: `http://localhost:5049`

---

## 4. Hướng Dẫn Sử Dụng & Kiểm Thử Tính Năng

### Kịch bản 1: Bác sĩ Upload và xem Gallery
1. Mở trình duyệt truy cập: `https://localhost:7129`
2. Đăng nhập tài khoản Bác sĩ:
   * **Email:** `dentist@dentalcare.com`
   * **Mật khẩu:** `Password123@`
3. Truy cập mục **Hồ Sơ Bệnh Án** (`/Dentist/MedicalRecords`).
4. Chọn một bệnh nhân ở danh sách bên trái (ví dụ: `BN000008 - Lê Hoàng Minh`).
5. Bấm nút **"Ghi Nhận Chẩn Đoán Mới"**:
   * Nhập chẩn đoán lâm sàng.
   * Tại mục *Chỉ Định Chụp Phim X-Quang / Cận Lâm Sàng*, chọn loại phim (ví dụ: `Toàn cảnh Panorama`).
   * Bấm nút **"Chọn ảnh X-quang"** và chọn tệp ảnh thật (.jpg/.png hoặc .dcm).
   * Kiểm tra giao diện xem trước (Thumbnail preview + dung lượng + nút hủy chọn ảnh).
   * Bấm **"Lưu Bệnh Án & Sơ Đồ Răng"**.
6. Hệ thống tự động ghi nhận đợt khám và lưu file ảnh vào thư mục local.
7. Xem thư viện ảnh (Gallery) ngay tại đợt khám:
   * Bấm vào ảnh để mở **Lightbox phóng to full-size chuẩn nét**.
   * Bấm nút tải ảnh hoặc xem kết luận đọc phim.

### Kịch bản 2: Bệnh nhân xem ảnh phim của chính mình
1. Đăng xuất tài khoản Bác sĩ.
2. Đăng nhập tài khoản Bệnh nhân sở hữu hồ sơ (ví dụ: `lehoangminh_new2@gmail.com` / `Password123@`).
3. Truy cập mục **Hồ Sơ Y Tế Của Tôi** (`/Patient/MedicalRecord`).
4. Bệnh nhân có thể xem toàn bộ phim chụp X-quang của mình qua Lightbox và tải file, nhưng **không có nút Upload hay Xóa**.

### Kịch bản 3: Kiểm tra tính bền vững (Persistence)
1. Tắt tiến trình Backend API (Ctrl + C).
2. Tắt máy tính hoặc khởi động lại.
3. Chạy lại `dotnet run` ở Backend và Frontend.
4. Mở lại trang Bệnh án: Toàn bộ ảnh X-quang vẫn hiển thị bình thường, không bị mất.

---

## 5. Hướng Dẫn Sao Lưu (Backup) Để Chuyển Máy Demo Cuối Kỳ

Để chuẩn bị một laptop bất kỳ demo trước Hội đồng bảo vệ:

1. **Bước 1: Backup Database SQL Server**
   Mở SQL Server Management Studio (SSMS) trên máy chuẩn:
   ```sql
   BACKUP DATABASE DentalClinicManagementDB 
   TO DISK = 'C:\Backup\DentalClinicManagementDB.bak' 
   WITH FORMAT, INIT, NAME = 'Full Backup';
   ```
2. **Bước 2: Sao chép thư mục ảnh MedicalImaging**
   Copy toàn bộ thư mục:
   ```text
   %LOCALAPPDATA%\DentalCare\MedicalImaging
   ```
   sang USB hoặc Google Drive.
3. **Bước 3: Khôi phục trên laptop demo**
   * Restore file `.bak` vào SQL Server trên laptop mới.
   * Dán thư mục `MedicalImaging` vào `%LOCALAPPDATA%\DentalCare\MedicalImaging` của laptop mới.
   * Chạy `dotnet run` ở Backend và Frontend -> Toàn bộ dữ liệu bệnh nhân, sơ đồ răng và ảnh X-quang sẽ hoạt động trơn tru 100% độc lập, không cần kết nối mạng hay cấu hình Cloudinary.
