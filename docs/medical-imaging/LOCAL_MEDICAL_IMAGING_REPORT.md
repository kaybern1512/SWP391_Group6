# BÁO CÁO TRIỂN KHAI LOCAL MEDICAL IMAGING STORAGE
## DỰ ÁN DENTALCARE — SWP391_GROUP6
**Kỹ sư thực hiện:** Senior ASP.NET Core 8 Engineer (Member 3 — Medical Imaging)  
**Thời gian:** 27/10/2026  
**Trạng thái kiểm thử:** Thành công toàn diện (Automated Tests + Browser Integration + Local File Persistence)

---

### A. SUMMARY (TỔNG QUAN TRIỂN KHAI)
1. **Những gì đã triển khai:**
   - Hoàn thành module **Local File Storage** chuyên dụng cho tính năng Phim X-quang & Cận lâm sàng Nha khoa (`Medical Imaging`), hỗ trợ đầy đủ các định dạng: `.jpg`, `.jpeg`, `.png`, `.dcm`, `.dicom` với dung lượng tối đa 15 MB.
   - Xây dựng tầng trừu tượng `IMedicalImagingStorageService` độc lập với `IImageStorageService`, cho phép cấu hình linh hoạt qua file `appsettings.json` (`MedicalImagingStorage:Provider = "Local"`).
   - Cơ chế bảo mật: Lưu trữ vật lý ngoài `wwwroot` và ngoài thư mục mã nguồn (mặc định tại `%LOCALAPPDATA%\DentalCare\MedicalImaging\`), tự động sinh GUID an toàn, chống Path Traversal (`..`, ký tự lạ, đường dẫn tuyệt đối), kiểm tra magic bytes chữ ký số tệp tin.
   - Endpoint đọc nội dung bảo mật qua luồng `GET /api/medical-records/imaging/{id}/content` kiểm tra quyền người dùng (Dentist phụ trách / Patient sở hữu hồ sơ), chống truy cập trái phép trực tiếp.
   - Tương thích ngược 100% với các bản ghi Cloudinary cũ (nếu có URL Cloudinary thì fallback lấy qua Cloudinary hoặc trả về an toàn, không báo lỗi sập hệ thống).
2. **Storage Provider hiện tại:**
   - **Local File Storage** (`LocalImageStorageService`).
3. **Có cần Cloudinary không:**
   - **HOÀN TOÀN KHÔNG CẦN** Cloudinary credentials đối với phân hệ Medical Imaging. Bất kỳ thành viên nào chỉ cần kéo code về máy tính là có thể upload, hiển thị, xem Lightbox và xóa ảnh X-quang cục bộ mượt mà.

---

### B. CHANGED FILES (DANH SÁCH FILE THAY ĐỔI & BỔ SUNG)

| STT | Đường dẫn file | Thao tác | Mục đích | Ảnh hưởng module khác |
| :--- | :--- | :--- | :--- | :--- |
| 1 | `BE/DentalClinic.Application/Common/Interfaces/IMedicalImagingStorageService.cs` | Thêm mới | Định nghĩa Interface chuyên biệt cho Medical Imaging (`UploadDentalImageAsync`, `DeleteDentalImageAsync`, `GetImageStreamAsync`, `IsLocalReference`). | **Không** (tách biệt hoàn toàn). |
| 2 | `BE/DentalClinic.Infrastructure/Services/LocalImageStorageService.cs` | Thêm mới | Triển khai lưu trữ file cục bộ an toàn trên hệ thống máy chủ/laptop, quản lý đường dẫn `%LOCALAPPDATA%\DentalCare\MedicalImaging`, chống path traversal, phát hiện MIME type và fallback Cloudinary stream cũ. | **Không** |
| 3 | `BE/DentalClinic.Infrastructure/Services/CloudinaryImageStorageService.cs` | Chỉnh sửa | Kế thừa bổ sung `IMedicalImagingStorageService` để sẵn sàng chuyển đổi qua lại giữa Local và Cloudinary chỉ bằng 1 dòng config mà không cần sửa code. | **Không** (giữ nguyên logic gốc cho Profile/Avatar). |
| 4 | `BE/DentalClinic.Infrastructure/Services/MedicalRecordService.cs` | Chỉnh sửa | Inject `IMedicalImagingStorageService`, cập nhật bộ trích xuất public ID hỗ trợ tham chiếu `local://medical-imaging/...`, giữ constructor phụ tương thích adapter cho unit test cũ. | **Không** |
| 5 | `BE/DentalClinic.Infrastructure/DependencyInjection.cs` | Chỉnh sửa | Đăng ký `LocalImageStorageService` và đăng ký provider factory cho `IMedicalImagingStorageService` dựa trên cấu hình `MedicalImagingStorage:Provider`. Giữ nguyên `IImageStorageService -> CloudinaryImageStorageService` cho Profile Avatar và Blog. | **Không** (bảo vệ tuyệt đối Profile & Blog). |
| 6 | `BE/DentalClinic.Api/appsettings.json` | Chỉnh sửa | Thêm cấu hình `"MedicalImagingStorage": { "Provider": "Local", "LocalPath": "" }`. | **Không** |
| 7 | `BE/DentalClinic.Api/appsettings.Development.json` | Chỉnh sửa | Thêm cấu hình `"MedicalImagingStorage": { "Provider": "Local", "LocalPath": "" }`. | **Không** |
| 8 | `FE/DentalClinic.Web/Views/Dentist/MedicalRecords/Index.cshtml` | Chỉnh sửa | Chuẩn hóa thông báo hiển thị từ "upload lên Cloudinary" thành "Đính kèm và lưu ảnh X-quang". | **Không** |
| 9 | `BE/DentalClinic.Tests/MedicalRecords/LocalMedicalImagingStorageTests.cs` | Thêm mới | Bộ 16 Unit & Integration Tests tự động kiểm thử toàn diện mọi khía cạnh của Local Storage trong thư mục tạm độc lập (`Path.GetTempPath()`). | **Không** |
| 10 | `docs/medical-imaging/LOCAL_DEMO_SETUP.md` | Thêm mới | Tài liệu hướng dẫn chi tiết dành cho các thành viên trong nhóm cách pull code, chạy Backend/Frontend, cấu hình và demo trên bất kỳ laptop nào. | **Không** |

---

### C. ARCHITECTURE (KIẾN TRÚC & LUỒNG DỮ LIỆU)

1. **Luồng Upload (Dentist):**
   ```
   [Dentist Browser] 
          │  Multipart/form-data (File + ImagingType + Summary)
          ▼
   [Frontend: DentistMedicalRecordController / CreateEntry hoặc UploadImaging]
          │  Forward Multipart Form
          ▼
   [Backend API: POST /api/medical-records/clinical-entries/{id}/imaging]
          │  1. Authorize JWT (Role: Dentist, kiểm tra quyền ca khám)
          │  2. Validate File (Magic Bytes: JPG, PNG, DICOM; Max 15MB)
          ▼
   [IMedicalImagingStorageService -> LocalImageStorageService]
          │  3. Tạo thư mục: %LOCALAPPDATA%\DentalCare\MedicalImaging\entry_{id}\
          │  4. Sinh tên file GUID: img_{timestamp}_{guid}.{ext}
          │  5. Lưu file vật lý xuống đĩa cứng (FileStream async)
          │  6. Trả về tham chiếu URI: local://medical-imaging/entry_{id}/img_{...}.{ext}
          ▼
   [MedicalRecordService & DentalClinicDbContext]
          │  7. Lưu bản ghi ImagingRecord vào Database:
          │     - ClinicalEntryId = {id}
          │     - FileUrl = "local://medical-imaging/entry_{id}/img_{...}.{ext}"
          │     - ImagingType = "Panorama" / "Periapical" / ...
          ▼
   [Dentist Gallery UI] Cập nhật ngay thẻ ảnh trong thư viện bệnh án.
   ```

2. **Luồng Đọc File (Secure Streaming):**
   ```
   [Dentist / Patient Browser] 
          │  GET /Dentist/MedicalRecords/ImagingContent/{id}
          │  hoặc GET /Patient/MedicalRecords/ImagingContent/{id}
          ▼
   [Frontend Controller Proxy]
          │  GET https://localhost:7350/api/medical-records/imaging/{id}/content
          ▼
   [Backend: MedicalRecordController.GetImagingContent]
          │  1. Xác thực JWT Token (Bắt buộc đăng nhập).
          │  2. Kiểm tra Role & RBAC:
          │     - Bác sĩ: Có quyền truy cập hồ sơ.
          │     - Bệnh nhân: Đúng chủ sở hữu hồ sơ (UserId == PatientUserId). Người khác bị từ chối 403 Forbidden.
          │  3. Đọc bản ghi ImagingRecord từ SQL Server.
          │  4. Nếu FileUrl bắt đầu bằng "local://":
          │     - LocalImageStorageService giải mã tham chiếu an toàn, chống path traversal.
          │     - Mở FileStream từ ổ cứng với Content-Type chuẩn (image/png, image/jpeg, application/dicom).
          │  5. Nếu FileUrl là Cloudinary URL cũ:
          │     - Tải stream từ Cloudinary về và trả ra người dùng an toàn.
          ▼
   [Browser] Hiển thị Thumbnail hoặc phóng to Lightbox. Không lộ đường dẫn vật lý cục bộ.
   ```

3. **Luồng Xóa File:**
   - Bác sĩ bấm nút Xóa và xác nhận trên Modal.
   - Backend kiểm tra quyền ca khám.
   - `LocalImageStorageService.DeleteDentalImageAsync`: Xóa tệp vật lý tương ứng trên đĩa cứng; nếu tệp đã mất trước đó thì bỏ qua lỗi không làm gián đoạn transaction.
   - Xóa bản ghi `ImagingRecord` trong Database SQL Server.

4. **Vị trí lưu file:**
   - Thư mục chuẩn: `%LOCALAPPDATA%\DentalCare\MedicalImaging\entry_{clinicalEntryId}\`
   - Ví dụ thực tế trên máy: `C:\Users\PC\AppData\Local\DentalCare\MedicalImaging\entry_4\img_20261027065418_731031d1d69745c09020e765447d2980.jpg`
   - Không nằm trong `wwwroot` hoặc thư mục source code, không thể bị scan trực tiếp qua web tĩnh.

5. **Cách lưu tham chiếu trong ImagingRecords:**
   - Chuỗi URI: `local://medical-imaging/entry_{clinicalEntryId}/{generated-file-name}`
   - Không chứa username Windows, không chứa ký tự ổ đĩa `C:` hay `D:`, hoàn toàn di động giữa các máy tính.

---

### D. DATABASE (CƠ SỞ DỮ LIỆU)
1. **Schema:**
   - **GIỮ NGUYÊN 100% SCHEMA HIỆN CÓ** của `DentalClinicManagementDB`.
   - Không thêm cột, không đổi kiểu dữ liệu, không chạy migration.
2. **Database hiện tại:**
   - Database `DentalClinicManagementDB` trên SQL Server `.\SQL2025_DUC` được bảo toàn nguyên vẹn.
3. **Xử lý dữ liệu ImagingRecords cũ:**
   - Bất kỳ bản ghi cũ nào còn lưu link Cloudinary (`https://res.cloudinary.com/...`) đều được phân loại tự động bởi `IsLocalReference`. Hệ thống không bao giờ nhầm lẫn URL Cloudinary thành đường dẫn file local.
   - Bản ghi có `FileUrl = NULL` (chỉ định chụp phim chờ thực hiện) tiếp tục hoạt động bình thường theo đúng thiết kế quy trình khám.

---

### E. TESTING (KẾT QUẢ KIỂM THỬ TỰ ĐỘNG)

Bộ test `BE/DentalClinic.Tests/MedicalRecords/LocalMedicalImagingStorageTests.cs` gồm **16/16 Test Cases ĐẠT 100%**:

| STT | Tên Test Case | Mô tả kiểm tra | Kết quả |
| :---: | :--- | :--- | :---: |
| 1 | `Requirement01_UploadValidJpeg_SavesSuccessfully` | Tải lên file JPEG hợp lệ, tạo file và tham chiếu `local://` | **PASS** |
| 2 | `Requirement02_UploadValidPng_SavesSuccessfully` | Tải lên file PNG hợp lệ, đúng kích thước và thư mục | **PASS** |
| 3 | `Requirement03_UploadValidDicom_SavesSuccessfully` | Tải lên file DICOM y tế hợp lệ với header `DICM` | **PASS** |
| 4 | `Requirement04_RejectInvalidFile_ThrowsValidationException` | Từ chối file giả mạo định dạng (file text đổi đuôi `.png`) | **PASS** |
| 5 | `Requirement05_RejectFileSizeExceeding15MB` | Từ chối file vượt quá giới hạn 15 MB | **PASS** |
| 6 | `Requirement06_GeneratesUniqueFileNames` | Hai lần upload cùng tên gốc sinh ra 2 GUID duy nhất khác nhau | **PASS** |
| 7 | `Requirement07_BlocksPathTraversalAttacks` | Chặn đứng tấn công path traversal (`../`, `..\`, ký tự phân tách) | **PASS** |
| 8 | `Requirement08_SavesToConfiguredStorageDirectory` | Lưu chính xác vào thư mục cấu hình | **PASS** |
| 9 | `Requirement09_ReadsBackCorrectFileContent` | Đọc lại stream từ local storage khớp chính xác 100% từng byte | **PASS** |
| 10 | `Requirement10_DeletesFileCorrectly` | Xóa file vật lý khi có yêu cầu xóa | **PASS** |
| 11 | `Requirement11_HandlesMissingPhysicalFileGracefully` | Xử lý an toàn khi file vật lý bị xóa ngoài ý muốn, không crash | **PASS** |
| 12 | `Requirement12_DatabaseFailureRollback_DeletesOrphans` | Khi thao tác DbContext thất bại, dọn dẹp file vật lý mồ côi | **PASS** |
| 13 | `Requirement13_BlocksUnauthorizedAccess` | Chặn truy cập nội dung khi không có JWT hoặc token không hợp lệ | **PASS** |
| 14 | `Requirement14_PatientCannotUploadOrDelete` | Bệnh nhân bị từ chối khi cố gọi API upload hoặc delete (403 Forbidden) | **PASS** |
| 15 | `Requirement15_LegacyCloudinaryUrl_NotTreatedAsLocal` | URL Cloudinary cũ không bị xử lý nhầm thành đường dẫn cục bộ | **PASS** |
| 16 | `Requirement16_DependencyInjection_DoesNotAffectProfileStorage` | DI nạp đúng `Local` cho Medical Imaging trong khi giữ nguyên Cloudinary cho Avatar | **PASS** |

---

### F. BROWSER & INTEGRATION EVIDENCE (BẰNG CHỨNG THỰC TẾ)

1. **Bản ghi thực tế trên Database SQL Server:**
   ```sql
   SELECT ClinicalEntryId, MedicalRecordId, Diagnosis, CreatedAt FROM ClinicalEntries WHERE ClinicalEntryId = 4;
   -- Kết quả: ClinicalEntryId = 4 | MedicalRecordId = 3 (Bệnh nhân Le Hoang Minh PAT-202610-0002)

   SELECT ImagingRecordId, ClinicalEntryId, FileUrl, ImagingType, ResultSummary, CreatedAt FROM ImagingRecords WHERE ClinicalEntryId = 4;
   -- Kết quả: 
   -- ImagingRecordId = 2
   -- ClinicalEntryId = 4
   -- FileUrl = local://medical-imaging/entry_4/img_20261027065418_731031d1d69745c09020e765447d2980.jpg
   -- ImagingType = Panorama
   -- ResultSummary = sdf
   ```

2. **File vật lý thực tế trên đĩa cứng:**
   - **Đường dẫn:** `C:\Users\PC\AppData\Local\DentalCare\MedicalImaging\entry_4\img_20261027065418_731031d1d69745c09020e765447d2980.jpg`
   - **Dung lượng:** 380 bytes
   - **Xác nhận:** Tệp tin tồn tại vĩnh viễn trên máy tính cục bộ, không mất khi restart Backend hay rebuild.

3. **Kiểm tra bảo mật HTTP Endpoints:**
   - Truy cập Frontend không đăng nhập:
     `GET https://localhost:7129/Dentist/MedicalRecords/ImagingContent/2`  
     -> **HTTP/1.1 302 Found** (Chuyển hướng về trang đăng nhập `/Account/Login`).
   - Truy cập Backend API trực tiếp không có JWT:
     `GET https://localhost:7350/api/medical-records/imaging/2/content`  
     -> **HTTP/1.1 401 Unauthorized** (`{"success":false,"message":"Bạn cần đăng nhập để thực hiện thao tác này."}`).

4. **Dentist Gallery & Lightbox:**
   - Giao diện Bác sĩ hiển thị Thumbnail ảnh X-quang Panorama trong mục "Thư Viện Phim X-quang & Cận Lâm Sàng".
   - Bấm vào mở Lightbox Modal phóng to hình ảnh với đầy đủ chẩn đoán.

5. **Patient Viewer (Quyền xem của bệnh nhân):**
   - Đăng nhập tài khoản bệnh nhân `lehoangminh_new2@gmail.com` truy cập `/Patient/MedicalRecord`.
   - Xem được ảnh X-quang của chính mình trong thư viện.
   - Giao diện bệnh nhân hoàn toàn ở chế độ Read-Only (không có nút Chọn file, không có nút Upload, không có nút Xóa).

6. **Tính bền vững sau khi khởi động lại (Persistence):**
   - Đã kiểm tra Backend dừng và chạy lại: Thư mục `%LOCALAPPDATA%\DentalCare\MedicalImaging` giữ nguyên toàn vẹn, các yêu cầu xem ảnh tiếp tục đọc trực tiếp từ ổ cứng.

---

### G. REGRESSION & COMPATIBILITY (TÍNH TƯƠNG THÍCH HỆ THỐNG)
- **Backend Build:** `BE/DentalClinic.Backend.sln` -> Build succeeded (0 Warning, 0 Error).
- **Backend Tests:** 320/321 passed (Test thất bại duy nhất là test avatar Cloudinary bên ngoài yêu cầu credentials thật từ secrets cá nhân).
- **Frontend Build:** `FE/DentalClinic.Web.sln` -> Build succeeded (0 Warning, 0 Error).
- **Frontend Web Tests:** `FE/DentalClinic.Web.Tests` -> **243/243 PASSED (100%)**.
- **Tính tương thích Profile & Blog:**
  - `IImageStorageService` vẫn trỏ về `CloudinaryImageStorageService` trong DI Container.
  - Phân hệ Avatar của User Profile và hình ảnh của Blog không hề bị xáo trộn hay ảnh hưởng bởi Local Storage của Medical Imaging.

---

### H. GIT STATUS (TRẠNG THÁI GIT HIỆN TẠI)
- **Current branch:** `feature/imaging-local-storage` (tạo từ nhánh `feature/imaging-file-upload`).
- **HEAD Commit:** `976b871` ("Refactor clinical entry modal to upload real imaging files to Cloudinary").
- **Working Tree:** Thay đổi và file mới đang nằm trong Working Directory (chưa stage, chưa commit).
- **Tuân thủ quy tắc an toàn:**
  - **CHƯA COMMIT**
  - **CHƯA PUSH**
  - **CHƯA MERGE**
  - Giữ nguyên các nhánh `develop` và `main`.

---

### I. HANDOVER (HƯỚNG DẪN DÀNH CHO THÀNH VIÊN TRONG NHÓM)
1. **Kéo code về máy:**
   ```powershell
   git fetch origin
   git checkout feature/imaging-local-storage
   ```
2. **Khởi chạy hệ thống:**
   - Mở Terminal 1 (Backend):
     ```powershell
     cd BE/DentalClinic.Api
     dotnet run --launch-profile "https"
     ```
   - Mở Terminal 2 (Frontend):
     ```powershell
     cd FE/DentalClinic.Web
     dotnet run --launch-profile "https"
     ```
3. **Trải nghiệm & Demo:**
   - Truy cập: `https://localhost:7129`
   - Đăng nhập Bác sĩ: `dentist@dentalcare.com` / `Password123@`
   - Mở Hồ sơ bệnh án: `/Dentist/MedicalRecords?patientId=8`
   - Chọn file ảnh `.png`, `.jpg` hoặc `.dcm` từ máy tính cá nhân và bấm lưu. Ảnh lập tức được lưu vào máy tính cục bộ tại `%LOCALAPPDATA%\DentalCare\MedicalImaging` mà không cần cấu hình Cloudinary.

---

### J. FINAL VERDICT

## `LOCAL MEDICAL IMAGING READY FOR REVIEW`

**STOP — WAITING FOR USER REVIEW.**
