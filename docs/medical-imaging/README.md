# HƯỚNG DẪN TÍCH HỢP VÀ VẬN HÀNH CHỨC NĂNG MEDICAL IMAGING (DAY 07 HANDOFF)

**Dự án:** Dental Clinic Management System (`DentalCare`)<br/>
**Phân hệ:** Hồ sơ bệnh án & Chẩn đoán hình ảnh nha khoa (Medical Records / Dental Imaging)<br/>
**Phụ trách:** Member 3 (`buiminhduc1403-ctrl` &lt;buiminhduc1403@gmail.com&gt;)<br/>
**Nhánh phát triển:** `feature/imaging-final-validation`<br/>
**Ngày cập nhật:** 30/10/2026<br/>

---

## 1. Feature Overview (Tổng quan tính năng)

Phân hệ Chẩn đoán hình ảnh nha khoa (Dental Imaging) cung cấp giải pháp trọn vẹn cho việc số hóa, lưu trữ, hiển thị và bảo vệ dữ liệu phim X-quang trong hệ thống phòng khám nha khoa:

- **Upload ảnh X-quang nha khoa:** Cho phép Bác sĩ (Dentist) tải lên các hình ảnh chẩn đoán gắn liền với đợt khám điều trị (`ClinicalEntry`) của bệnh nhân.
- **Phân loại phim chuẩn nha khoa:** Hỗ trợ 4 nhóm phim chính:
  1. `Panorama`: Phim chụp toàn cảnh xương hàm và cung răng.
  2. `Periapical`: Phim chụp cận chóp từng nhóm răng.
  3. `Cephalometric`: Phim sọ nghiêng phân tích chỉnh nha.
  4. `Intraoral`: Ảnh chụp cận cảnh trong khoang miệng.
- **Lưu trữ đám mây với Cloudinary:** Lưu trữ an toàn trên hạ tầng Cloudinary, tự động phân nhóm thư mục theo đợt khám (`dental-clinic/medical-imaging/entry_{id}/`) và gắn public ID có kiểm soát.
- **Thực thể dữ liệu `ImagingRecord`:** Lưu trữ thông tin metadata (loại phim, kết luận đọc phim `ResultSummary`, URL lưu trữ, thời gian chụp) liên kết khóa ngoại với `ClinicalEntry` và `DentalMedicalRecord`.
- **Thư viện ảnh của Bác sĩ (Dentist Imaging Gallery):** Hiển thị danh sách phim theo dạng lưới (grid), hỗ trợ xem trước (thumbnail), phân loại badge màu sắc, và xem chi tiết kết luận.
- **Thư viện ảnh của Bệnh nhân (Patient Imaging Gallery):** Cho phép Bệnh nhân (Patient) tra cứu lịch sử phim chụp của chính mình trong hồ sơ bệnh án cá nhân.
- **Trình xem ảnh chuyên dụng (Lightbox):** Modal xem ảnh kích thước lớn, hỗ trợ tải file gốc, có tính năng xoay, phóng to thu nhỏ và dọn dẹp bộ nhớ tự động khi đóng modal.
- **Xóa phim an toàn (Delete Imaging):** Chỉ Bác sĩ điều trị được phân công vào đợt khám mới có quyền xóa phim; có modal xác nhận thao tác và dọn dẹp asset đám mây.
- **Truy xuất nội dung bảo mật (Secure Content Streaming):** Trình duyệt không truy cập trực tiếp URL Cloudinary thô; toàn bộ dữ liệu ảnh được stream qua authenticated proxy của hệ thống kèm header chống lưu cache (`Cache-Control: no-store, no-cache`).

---

## 2. Architecture & Data Flow (Kiến trúc & Luồng dữ liệu)

### 2.1. Luồng tải lên phim X-quang (Dentist Upload Flow)

```
[Dentist Browser]
       │
       ▼ (1. Multipart Form: File, ImagingType, ResultSummary)
[Frontend: DentistMedicalRecordController.UploadImaging]
       │
       ▼ (2. IMedicalRecordApiService.UploadImagingRecordAsync)
[Backend API: MedicalRecordController.UploadImagingRecord]
       │
       ▼ (3. Authorization: Assigned Dentist Check)
[Backend Service: MedicalRecordService.UploadImagingRecordAsync]
       ├─► [DentalImageFileValidator.Validate] (Magic bytes, MIME, Max 15MB)
       ├─► [CloudinaryImageStorageService.UploadDentalImageAsync] (CDN Upload)
       └─► [EF Core / SQL Server] (Insert ImagingRecord linked to ClinicalEntry)
       │
       ▼ (4. Return Metadata Response)
[Dentist Browser] (Auto-refresh Gallery)
```

### 2.2. Luồng đọc và hiển thị ảnh bảo mật (Secure Image Proxy Flow)

```
[Dentist / Patient Browser]
       │
       ▼ (1. GET /Dentist/MedicalRecords/ImagingContent/{id} or /Patient/MedicalRecords/ImagingContent/{id})
[Frontend Proxy Controller] (Extracts Session Cookie / JWT)
       │
       ▼ (2. GET /api/medical-records/imaging/{id}/content)
[Backend API: MedicalRecordController.GetImagingContent]
       │
       ▼ (3. Authorization: Assigned Dentist OR Owner Patient)
[Backend Service: MedicalRecordService.GetImagingFileStreamAsync]
       │
       ▼ (4. CloudinaryImageStorageService.GetImageStreamAsync)
       ├─► Validate SSRF: Domain whitelist, Reject IP, Reject UserInfo, Reject non-443 port
       ├─► HttpClient: AllowAutoRedirect = false, Timeout = 15s, Max 20MB
       └─► Read Stream from Cloudinary CDN
       │
       ▼ (5. Stream File Content with Headers: Cache-Control: no-store)
[Dentist / Patient Browser] (Renders in Lightbox / Gallery without exposing raw Cloudinary URL)
```

---

## 3. API Reference (Đặc tả API Backend)

### 3.1. POST `/api/medical-records/imaging`
- **Mô tả:** Tải lên một tệp hình ảnh chẩn đoán mới cho đợt khám điều trị.
- **Quyền hạn (Role):** `Dentist` (Bác sĩ được phân công vào đợt khám).
- **Content-Type:** `multipart/form-data`
- **Tham số Request:**
  - `ClinicalEntryId` (long, bắt buộc): Mã đợt khám.
  - `ImagingType` (string, bắt buộc): `Panorama`, `Periapical`, `Cephalometric`, `Intraoral`.
  - `ResultSummary` (string, tùy chọn): Kết luận hoặc ghi chú chẩn đoán đọc phim (tối đa 500 ký tự).
  - `File` (IFormFile, bắt buộc): Tệp hình ảnh JPEG, PNG, hoặc DICOM (tối đa 15 MB).
- **Mã phản hồi HTTP:**
  - `200 OK`: Tải lên và lưu trữ thành công.
  - `400 Bad Request`: Định dạng tệp không hợp lệ, magic bytes không khớp, hoặc thiếu tham số.
  - `401 Unauthorized`: Chưa xác thực JWT.
  - `403 Forbidden`: Bác sĩ không được phân công phụ trách đợt khám này.
  - `404 Not Found`: Không tìm thấy `ClinicalEntryId`.
  - `502 Bad Gateway`: Lỗi giao tiếp với dịch vụ lưu trữ đám mây.
- **Ví dụ Phản hồi Thành công:**
```json
{
  "success": true,
  "message": "Tải lên và lưu hồ sơ hình ảnh X-quang thành công.",
  "data": {
    "imagingRecordId": 105,
    "clinicalEntryId": 501,
    "imagingType": "Panorama",
    "fileUrl": "https://res.cloudinary.com/demo/image/upload/v1/dental-clinic/medical-imaging/entry_501/pano_xray.jpg",
    "resultSummary": "Xương hàm phát triển bình thường, răng 38 mọc lệch nghiêng gần 45 độ.",
    "createdAt": "2026-10-30T08:30:00Z",
    "originalFileName": "pano_xray.jpg",
    "mimeType": "image/jpeg",
    "fileSizeBytes": 2048576,
    "storageProvider": "Cloudinary"
  }
}
```

### 3.2. GET `/api/medical-records/clinical-entry/{clinicalEntryId}/imaging`
- **Mô tả:** Lấy danh sách phim X-quang thuộc một đợt khám.
- **Quyền hạn (Role):** `Dentist` (bác sĩ phụ trách hoặc bác sĩ phòng khám theo chính sách), `Patient` (chính chủ sở hữu hồ sơ), `Admin`.
- **Mã phản hồi HTTP:** `200 OK`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`.
- **Ví dụ Phản hồi Thành công:**
```json
{
  "success": true,
  "message": "Lấy danh sách hồ sơ hình ảnh thành công.",
  "data": [
    {
      "imagingRecordId": 105,
      "clinicalEntryId": 501,
      "imagingType": "Panorama",
      "fileUrl": "https://res.cloudinary.com/demo/image/upload/v1/dental-clinic/medical-imaging/entry_501/pano_xray.jpg",
      "viewUrl": "/api/medical-records/imaging/105/content",
      "resultSummary": "Răng 38 mọc lệch nghiêng gần 45 độ.",
      "createdAt": "2026-10-30T08:30:00Z"
    }
  ]
}
```

### 3.3. GET `/api/medical-records/imaging/{imagingRecordId}`
- **Mô tả:** Lấy chi tiết thông tin một bản ghi hình ảnh.
- **Quyền hạn (Role):** `Dentist`, `Patient` (chính chủ), `Admin`.
- **Mã phản hồi HTTP:** `200 OK`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`.

### 3.4. GET `/api/medical-records/imaging/{imagingRecordId}/content`
- **Mô tả:** Stream nhị phân nội dung tệp hình ảnh trực tiếp cho client đã được xác thực quyền sở hữu.
- **Quyền hạn (Role):** `Dentist` phụ trách, `Patient` chính chủ, `Admin`.
- **Response Headers:**
  - `Content-Type`: `image/jpeg`, `image/png`, hoặc `application/dicom`.
  - `Cache-Control`: `no-store, no-cache, must-revalidate, max-age=0`.
  - `Pragma`: `no-cache`.
- **Mã phản hồi HTTP:** `200 OK`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`.

### 3.5. DELETE `/api/medical-records/imaging/{imagingRecordId}`
- **Mô tả:** Xóa vĩnh viễn một bản ghi hình ảnh khỏi CSDL và thu hồi asset trên Cloudinary.
- **Quyền hạn (Role):** `Dentist` (chỉ Bác sĩ được phân công vào đợt khám tạo ra bản ghi đó).
- **Mã phản hồi HTTP:**
  - `200 OK`: Xóa thành công.
  - `401 Unauthorized`: Chưa đăng nhập.
  - `403 Forbidden`: Bác sĩ khác hoặc Bệnh nhân cố tình xóa.
  - `404 Not Found`: Không tìm thấy bản ghi hình ảnh.

---

## 4. User Guide (Hướng dẫn sử dụng)

### 4.1. Dành cho Bác sĩ (Dentist)
1. **Đăng nhập:** Đăng nhập vào hệ thống bằng tài khoản Bác sĩ được cấp.
2. **Mở Hồ sơ Bệnh án:** Vào mục **Hồ sơ bệnh án** (`/Dentist/MedicalRecords`), chọn Bệnh nhân cần thao tác từ danh sách khám.
3. **Mở Đợt khám:** Chọn đợt khám tương ứng mà bác sĩ được phân công điều trị.
4. **Tải lên phim X-quang:**
   - Nhấn nút **Upload Phim** tại đợt khám.
   - Chọn loại phim phù hợp (`Panorama`, `Periapical`, `Cephalometric`, `Intraoral`).
   - Nhập tóm tắt kết luận đọc phim (`ResultSummary`).
   - Chọn tệp ảnh hợp lệ (`.jpg`, `.jpeg`, `.png`, `.dcm` có kích thước < 15 MB).
   - Nhấn **Tải lên**. Hệ thống sẽ kiểm tra bảo mật, tải lên lưu trữ và tự động cập nhật thư viện ảnh.
5. **Xem phim & Đọc kết quả:** Click vào hình thu nhỏ trong Thư viện ảnh để mở trình xem lớn (Lightbox). Có thể phóng to hoặc tải về máy tính chuyên dụng.
6. **Xóa phim:** Nếu tải nhầm phim, nhấn nút biểu tượng thùng rác màu đỏ tại góc ảnh. Xác nhận xóa tại modal cảnh báo.

### 4.2. Dành cho Bệnh nhân (Patient)
1. **Đăng nhập:** Đăng nhập tài khoản Bệnh nhân cá nhân.
2. **Xem Hồ sơ:** Vào mục **Hồ sơ sức khỏe / Bệnh án** (`/Patient/MedicalRecords`).
3. **Xem Thư viện ảnh:** Chọn từng đợt khám để xem hình ảnh X-quang và kết luận bác sĩ đã ghi nhận.
4. **Phóng to:** Click vào ảnh để mở Lightbox xem chi tiết.
5. **Bảo mật:** Bệnh nhân chỉ có quyền xem phim của chính mình; không có quyền sửa, xóa hoặc tải ảnh của bệnh nhân khác.

---

## 5. Demo Script (Kịch bản kiểm thử Demo)

| Bước | Diễn giải thao tác | Kết quả mong đợi |
| :--- | :--- | :--- |
| **B1** | Đăng nhập tài khoản Bác sĩ phụ trách (`dentist@dentalcare.vn`). | Đăng nhập thành công, điều hướng vào Dashboard Bác sĩ. |
| **B2** | Vào `/Dentist/MedicalRecords`, chọn bệnh nhân đang điều trị. | Hiển thị hồ sơ bệnh án, các đợt khám và nút **Upload Phim**. |
| **B3** | Mở Modal Upload, chọn phim `Panorama` JPEG mẫu và nhập kết luận đọc phim. | Upload thành công, modal đóng, Thư viện hiển thị ngay ảnh mới có badge `Panorama`. |
| **B4** | Click vào ảnh vừa upload trong thư viện ảnh. | Lightbox mở ra hiển thị ảnh kích thước đầy đủ và kết luận đọc phim. |
| **B5** | Đăng xuất, đăng nhập tài khoản Bệnh nhân chính chủ (`patient@dentalcare.vn`). | Vào `/Patient/MedicalRecords`, thấy đúng phim X-quang vừa được bác sĩ tải lên. |
| **B6** | Dùng tài khoản Bệnh nhân truy cập trái phép API xóa hoặc upload. | Backend từ chối với mã lỗi `403 Forbidden`. |
| **B7** | Đăng nhập lại tài khoản Bác sĩ, chọn phim thử nghiệm và nhấn nút **Xóa**. | Hiển thị modal xác nhận; click **Xác nhận xóa**, ảnh biến mất khỏi thư viện và asset đám mây được dọn dẹp. |

---

## 6. Setup & Troubleshooting (Cài đặt & Khắc phục sự cố)

### 6.1. Môi trường yêu cầu
- **.NET SDK:** Phiên bản 8.0 trở lên (`dotnet --version` >= `8.0.x`).
- **Cơ sở dữ liệu:** SQL Server (LocalDB hoặc Express) với chuỗi kết nối cấu hình trong `appsettings.json`.
- **Node.js / Trình duyệt:** Hỗ trợ ES6 và Bootstrap 5.

### 6.2. Cấu hình Dịch vụ Lưu trữ Cloudinary
Sử dụng **.NET Secret Manager** hoặc Biến môi trường để bảo vệ thông tin xác thực (KHÔNG commit API Secret vào Git):

```bash
# Cấu hình Secret cho Backend API
cd BE/DentalClinic.Api
dotnet user-secrets init
dotnet user-secrets set "Cloudinary:CloudName" "your-cloud-name"
dotnet user-secrets set "Cloudinary:ApiKey" "your-api-key"
dotnet user-secrets set "Cloudinary:ApiSecret" "your-api-secret"
```

### 6.3. Khởi chạy ứng dụng
```bash
# Terminal 1 - Backend API (chạy tại https://localhost:7081 hoặc cổng cấu hình)
cd BE/DentalClinic.Api
dotnet run

# Terminal 2 - Frontend Web (chạy tại https://localhost:7025 hoặc cổng cấu hình)
cd FE/DentalClinic.Web
dotnet run
```

### 6.4. Các lỗi thường gặp và cách khắc phục
- **Lỗi `400 Bad Request: Định dạng file không được hỗ trợ`:**
  - *Nguyên nhân:* Tệp không có phần mở rộng `.jpg`, `.jpeg`, `.png`, hoặc `.dcm`, hoặc nội dung tệp không khớp với magic bytes chuẩn.
  - *Khắc phục:* Kiểm tra tệp ảnh gốc, không đổi phần mở rộng tệp thủ công.
- **Lỗi `502 Bad Gateway / Dịch vụ lưu trữ Cloudinary chưa được cấu hình`:**
  - *Nguyên nhân:* `Cloudinary:CloudName` hoặc `Cloudinary:ApiKey` bị trống hoặc mang giá trị placeholder mặc định.
  - *Khắc phục:* Thiết lập secret hợp lệ qua lệnh `dotnet user-secrets` ở mục 6.2.
- **Lỗi `403 Forbidden: Bác sĩ không có quyền thao tác trên hồ sơ này`:**
  - *Nguyên nhân:* Bác sĩ đang đăng nhập không trùng với `DentistUserId` được ghi nhận trong `ClinicalEntry`.
  - *Khắc phục:* Chọn đúng đợt khám mà bác sĩ đó được phân công điều trị.

---

## 7. Limitations & Security Notes (Giới hạn kỹ thuật & Ghi chú bảo mật)

1. **Tính riêng tư của Cloudinary Asset gốc:**
   - Dịch vụ Cloudinary mặc định lưu trữ asset ở chế độ public URL. Mặc dù ứng dụng sử dụng cơ chế **Authenticated Streaming Proxy** và không bao giờ xuất direct Cloudinary URL ra giao diện HTML của client, URL lưu trữ nội bộ vẫn có thể truy cập nếu kẻ tấn công có được URL gốc chính xác và tài khoản Cloudinary chưa bật cấu hình *Authenticated / Private Delivery*.
   - Khuyến nghị môi trường sản xuất (Production): Kích hoạt tính năng *Strict Access Control / Private Storage* trên Cloudinary dashboard để vô hiệu hóa hoàn toàn direct HTTP access.
2. **Tính nguyên tử của Giao dịch Đa dịch vụ (Cross-Service Atomicity):**
   - Cloudinary (dịch vụ bên thứ ba) và SQL Server không chia sẻ cơ chế transaction nguyên tử chung (2-Phase Commit).
   - Cơ chế bồi hoàn (Compensating action / Rollback) đã được triển khai: nếu bước lưu CSDL thất bại, hệ thống lập tức gọi `DeleteDentalImageAsync` để thu hồi asset đã tải lên.
   - Đối với hệ thống quy mô lớn, nên bổ sung mẫu thiết kế Outbox Pattern hoặc Job đối soát định kỳ (Reconciliation Cron).
3. **Phạm vi hỗ trợ tệp chuẩn DICOM:**
   - Hệ thống hiện tại hỗ trợ lưu trữ, xác thực cấu trúc header chuẩn `DICM` (DICOM Part 10), bảo toàn nguyên vẹn dữ liệu nhị phân và cho phép tải về (`Content-Type: application/dicom`).
   - Giao diện người dùng Web hiện tại **chưa tích hợp PACS Web Viewer đa lát cắt (Multi-slice CT/CBCT)**. Với tệp DICOM, Lightbox hiển thị thông báo tải về để bác sĩ đọc trên phần mềm nha khoa chuyên dụng (Romexis, Sidexis, EZDent-i,...).
4. **Bảo vệ dữ liệu y tế (Medical Data Compliance):**
   - Tuyệt đối không sử dụng hình ảnh phim và thông tin bệnh nhân thật trên môi trường phát triển (Development) hoặc trong các kiểm thử tự động.

---

## 8. Acceptance Checklist (Bảng nghiệm thu chất lượng)

| Hạng mục kiểm tra | Trạng thái thực tế | Bằng chứng kỹ thuật / Ghi chú |
| :--- | :---: | :--- |
| **Backend Build** (`BE/DentalClinic.Backend.sln`) | **PASSED** | 0 Warning, 0 Error (`.NET 8.0`). |
| **Frontend Build** (`FE/DentalClinic.Web.sln`) | **PASSED** | 0 Warning, 0 Error (`.NET 8.0`). |
| **Unit & API Tests (Backend)** | **PASSED** | 305/305 tests passed (`DentalClinic.Tests`). |
| **Unit & Integration Tests (Frontend)** | **PASSED** | 234/234 tests passed (`DentalClinic.Web.Tests`). |
| **Tổng số kiểm thử tự động toàn dự án** | **PASSED** | **539/539 tests passed**. |
| **End-to-End Workflow Tests** | **PASSED** | Bộ test E2E mô phỏng đầy đủ chu trình Upload, Persistence, Stream, Delete (`DentalImagingEndToEndWorkflowTests`). |
| **Ma trận phân quyền (Authorization Matrix)** | **PASSED** | Kiểm chứng chặt chẽ cho 5 nhóm quyền: Dentist phụ trách, Dentist khác, Patient chính chủ, Patient khác, Receptionist. |
| **Kiểm tra tệp tin an toàn (File Validation)** | **PASSED** | Xác thực magic bytes (JPEG, PNG, DICOM), giới hạn 15 MB, loại bỏ WebP không an toàn. |
| **Khử khuẩn đường dẫn (Filename Sanitization)** | **PASSED** | Loại bỏ hoàn toàn path traversal (`../../`) qua `Path.GetFileName`. |
| **Phòng vệ SSRF (Storage Security Hardening)** | **PASSED** | Domain allowlist, chặn IP/Localhost, cấm UserInfo, cấm non-443 port, tắt AutoRedirect, giới hạn 20 MB response. |
| **Dọn dẹp tài nguyên giao diện (Memory Leaks)** | **PASSED** | Tự động hủy `src`, `onload`, `onerror` của ảnh độ phân giải cao khi đóng modal Lightbox trên cả Dentist và Patient. |
| **Cấu hình Cloudinary Live Production** | **BLOCKED** | Môi trường CI/Test nội bộ không chứa Production Cloudinary Secret (tuân thủ nguyên tắc không commit secrets). Đã kiểm thử thành công qua Test Harness và Mock Storage. |
| **Tích hợp PACS Web DICOM Viewer** | **NOT RUN** | Nằm ngoài phạm vi của Member 3 (hệ thống xử lý lưu trữ và tải nhị phân an toàn cho phần mềm chuyên dụng). |

---

> **Kết luận:** Chức năng Medical Imaging đã hoàn thiện toàn diện, đáp ứng đầy đủ yêu cầu kỹ thuật, đạt 100% tỷ lệ pass của toàn bộ 539 tests, sẵn sàng cho việc tích hợp vào nhánh `develop`.
