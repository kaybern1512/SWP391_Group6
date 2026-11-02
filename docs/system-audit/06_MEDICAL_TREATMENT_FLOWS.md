# QUY TRÌNH HỒ SƠ BỆNH ÁN, PHÁC ĐỒ ĐIỀU TRỊ & ĐƠN THUỐC
**Tài liệu:** `06_MEDICAL_TREATMENT_FLOWS.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Phân hệ chuyên môn:** Clinical & Dental Examination Engine

---

## 1. CẤU TRÚC HỒ SƠ BỆNH ÁN ĐIỆN TỬ (DENTAL EMR ARCHITECTURE)

Hồ sơ bệnh án điện tử của phòng khám được cấu trúc thành hệ thống thực thể phân tầng:

```
[DentalMedicalRecords] (1 Hồ sơ duy nhất cho mỗi Bệnh nhân / PatientProfile)
         │
         ├── [ClinicalEntries] (Mỗi lần khám tương ứng 1 bản ghi lâm sàng)
         │        ├── Khám thực thể: Lý do khám, Huyết áp, Nhịp tim, Chẩn đoán
         │        ├── [OdontogramEntries] (Tình trạng chi tiết từng mặt răng theo chuẩn FDI)
         │        └── [ImagingRecords] (Phim X-quang, Chụp cận chóp, Panorama, CBCT)
         │
         └── [TreatmentPlans] (Kế hoạch điều trị dài hạn)
                  │
                  └── [TreatmentPlanItems] (Từng giai đoạn / thủ thuật điều trị)
                           │
                           └── [TreatmentSessions] (Từng buổi điều trị thực tế tại ghế)
                                    ├── [PerformedServices] (Dịch vụ y tế đã thực hiện)
                                    └── [Prescriptions] (Đơn thuốc cấp cho buổi điều trị)
                                             └── [PrescriptionItems] (Chi tiết các loại thuốc)
```

---

## 2. HIỆN THỰC SƠ ĐỒ RĂNG FDI 32 RĂNG (ODONTOGRAM ENGINE)

### 2.1. Chuẩn đánh số răng FDI Quốc tế
Hệ thống sử dụng hệ thống đánh số răng FDI 2 chữ số chuẩn thế giới:
- **Hàm trên bên Phải (Phần 1):** Răng 18, 17, 16, 15, 14, 13, 12, 11 (Răng khôn đến răng cửa giữa)
- **Hàm trên bên Trái (Phần 2):** Răng 21, 22, 23, 24, 25, 26, 27, 28 (Răng cửa giữa đến răng khôn)
- **Hàm dưới bên Trái (Phần 3):** Răng 38, 37, 36, 35, 34, 33, 32, 31
- **Hàm dưới bên Phải (Phần 4):** Răng 41, 42, 43, 44, 45, 46, 47, 48

### 2.2. Giao diện Công thái học (Ergonomics Layout)
- File Razor View: `FE/DentalClinic.Web/Views/Dentist/MedicalRecords/Index.cshtml`
- **Thiết kế 1 hàng ngang duy nhất:**
  - Hàng hàm trên: Hiển thị liên tục **16 răng trên 1 hàng** (từ 18 đến 28) theo thứ tự cung hàm trực quan.
  - Hàng hàm dưới: Hiển thị liên tục **16 răng trên 1 hàng** (từ 48 đến 38).
- **Mô phỏng 5 mặt giải phẫu của mỗi răng:**
  - Mặt nhai (`Occlusal`)
  - Mặt kẽ gần (`Mesial`)
  - Mặt kẽ xa (`Distal`)
  - Mặt ngoài / má (`Buccal` / `Facial`)
  - Mặt trong / lưỡi (`Lingual` / `Palatal`)
- **Bộ mã màu chuẩn bệnh lý nha khoa:**
  - `Normal` (Bình thường): Màu trắng / viền xám
  - `Caries` (Sâu răng): Màu đỏ rực (`#dc3545`)
  - `Filled` (Đã hàn trám composite/amalgam): Màu xanh lam (`#0d6efd`)
  - `Crown` (Bọc răng sứ): Màu vàng hoàng yến (`#ffc107`)
  - `RootCanal` (Đã điều trị tủy): Màu tím (`#6f42c1`)
  - `Missing` / `Extracted` (Mất răng / Đã nhổ): Dấu gạch chéo xám (`#6c757d`)
  - `Bridge` (Cầu răng): Viền liên kết nối liền các thân răng

### 2.3. Bộ lọc xem lịch sử theo từng đợt khám (Per-Visit History Switcher)
- Bác sĩ có thể bấm chuyển đổi giữa:
  - **Tất cả các lần khám (`all`):** Hiển thị tình trạng răng lũy kế mới nhất của bệnh nhân.
  - **Từng đợt khám riêng biệt (`visit-{id}`):** Hiển thị chính xác trạng thái răng tại thời điểm buổi khám đó diễn ra, giúp bác sĩ so sánh tiến triển phục hồi của bệnh nhân.

---

## 3. CHẨN ĐOÁN HÌNH ẢNH & TẢI PHIM X-QUANG (IMAGING UPLOAD)

- Triển khai tại: `BE/DentalClinic.Api/Controllers/MedicalRecordController.cs` và `FE/DentalClinic.Web/Views/Dentist/MedicalRecords/Index.cshtml`
- **Các loại phim hỗ trợ (`DentalImagingType`):**
  - `Periapical` (Phim chụp cận chóp)
  - `Panoramic` (Phim chụp toàn cảnh Panorama)
  - `Cephalometric` (Phim sọ nghiêng chỉnh nha)
  - `Bitewing` (Phim cánh cắn phát hiện sâu kẽ)
  - `CBCT` (Phim chụp cắt lớp vi tính hình nón 3D ConeBeam CT)
  - `IntraoralPhoto` (Ảnh chụp trong miệng)
- **Kiểm thực file an toàn (Security File Validation):**
  - File validator: `DentalImageFileValidator.cs`
  - Giới hạn kích thước: Tối đa 50MB cho mỗi tệp tin.
  - Định dạng cho phép: `.jpg`, `.jpeg`, `.png`, `.dcm` (chuẩn ảnh y khoa DICOM), `.zip` (gói lát cắt CT).
  - Kiểm tra chữ ký nhị phân (Magic Bytes) để ngăn chặn kẻ tấn công đổi đuôi file mã độc.
  - Xem trước ảnh phóng to (Modal Image Viewer) kèm các công cụ xoay ảnh, đảo màu phục vụ đọc phim.

---

## 4. KẾ HOẠCH ĐIỀU TRỊ & PHÁC ĐỒ THEO GIAI ĐOẠN (TREATMENT PLANS)

- Triển khai tại: `BE/DentalClinic.Infrastructure/Services/TreatmentService.cs`
- **Cấu trúc kế hoạch điều trị:**
  - `TreatmentPlan`: Đại diện cho toàn bộ lộ trình điều trị (ví dụ: "Lộ trình Chỉnh nha mắc cài sứ 18 tháng" hoặc "Phục hình Implant toàn hàm").
  - `TreatmentPlanItem`: Các giai đoạn tuần tự (Phases):
    - Giai đoạn 1: Điều trị nha chu & Lấy cao răng
    - Giai đoạn 2: Nhổ răng tiền cối tạo khoảng
    - Giai đoạn 3: Gắn mắc cài & đi dây cung
    - Giai đoạn 4: Tinh chỉnh khớp cắn & đeo hàm duy trì
- **Trạng thái từng giai đoạn:** `Proposed` $\rightarrow$ `Accepted` $\rightarrow$ `InProgress` $\rightarrow$ `Completed`.
- **Ghi nhận buổi điều trị thực tế (`TreatmentSessions`):**
  Mỗi lần bệnh nhân đến phòng khám để thực hiện một bước trong kế hoạch, Bác sĩ tạo một `TreatmentSession` liên kết trực tiếp với lịch hẹn `AppointmentId` và lưu danh sách dịch vụ đã làm vào `PerformedServices`.

---

## 5. KÊ ĐƠN THUỐC ĐIỆN TỬ (PRESCRIPTION ENGINE)

- Triển khai tại: `BE/DentalClinic.Infrastructure/Services/PrescriptionService.cs`
- **Mẫu đơn thuốc:** Tuân thủ quy chuẩn đơn thuốc ngoại trú của Bộ Y tế:
  - Mã đơn thuốc: `RX-yyyyMMdd-XXXX`
  - Thông tin bệnh nhân: Họ tên, Năm sinh, Giới tính, Địa chỉ, Chẩn đoán lâm sàng
  - Danh mục thuốc: Tên biệt dược / hoạt chất, Dạng bào chế (Viên nén, Hỗn dịch, Viên nang), Hàm lượng, Số lượng cấp, Hướng dẫn sử dụng chi tiết (Sáng, Trưa, Chiều, Tối, Uống trước hay sau ăn).
  - Lời dặn của bác sĩ điều trị và thời gian hẹn tái khám.
- **In ấn đơn thuốc:** Trang `/Dentist/Prescriptions/Print/{id}` được thiết kế chuyên biệt cho máy in nhiệt hoặc máy in laser A5, tự động căn lề chuẩn y tế.

---

## 6. MỐI LIÊN KẾT GIỮA NGHIỆP VỤ Y KHOA VÀ VÒNG ĐỜI LỊCH KHÁM

1. **Khởi tạo:** Bác sĩ chỉ có thể tạo `ClinicalEntry` hoặc `TreatmentSession` khi lịch khám đang ở trạng thái `InService`.
2. **Kế thừa dịch vụ thanh toán:** Khi Bác sĩ ghi nhận các thủ thuật đã hoàn thành vào `PerformedServices`, toàn bộ danh mục dịch vụ này sẽ tự động chuyển sang phân hệ Hóa đơn của Tiếp tân (`BillingPreparationDtos`). Tiếp tân không phải nhập tay lại dịch vụ, tránh sai sót thất thoát viện phí.
3. **Khóa dữ liệu an toàn:** Sau khi Bác sĩ bấm **[Hoàn Thành Ca Khám]**, hồ sơ lâm sàng được đóng lại, vị trí ghế điều trị bị khóa, sẵn sàng cho khâu thu ngân và đón tiếp bệnh nhân tiếp theo.
