# DENTALCARE SWP391 — DEMO READINESS GAP VERIFICATION REPORT

**Worktree:** `D:\SWP391\DentalCare-Integration`  
**Branch:** `release/demo-integration`  
**Base Commit:** `34ae9ca`  
**Database:** `DentalCare_SWP391_UAT_Fixture_TEST` (SQL Server Express - 38 physical tables)  
**Ngày Báo Cáo:** 17/11/2026 (Demo: 19/11/2026)  
**Mục Tiêu:** Nhận diện khoảng cách (Gaps), xác minh những gì CHƯA được kiểm thử đầy đủ sau cleanup, đính chính tài liệu và đề xuất kịch bản demo an toàn nhất.

---

## 1. PHÂN LOẠI MỨC ĐỘ XÁC MINH CÁC BƯỚC NGHIỆP VỤ

### 1.1. Các Bước Thực Sự Đã BROWSER E2E PASS (Đã kiểm chứng trực quan & Live HTTP)
1. **Admin Phân Ca & Quản Trị Danh Mục:**
   - Xem tổng quan vận hành (`/Admin/Dashboard` - HTTP 200).
   - Quản lý phân ca định biên bác sĩ (`/Admin/DentistSchedule/Index` - HTTP 200).
   - Bảo vệ ca trực khi có đơn nghỉ phép (`Approved Leave Protection` - ca trực của BS. Hùng ngày 18/11 tự động chuyển `Cancelled`).
2. **Patient Đặt Lịch Trong Ngày:**
   - Mở màn hình đặt lịch (`/Patient/Appointment/Book` - HTTP 200).
   - Chọn chuyên khoa Nha Khoa Tổng Quát, dịch vụ Khám & Lấy Cao Răng (`SVC-GEN-01`), chọn BS. Hùng trong ca trực ngày 17/11 -> Tạo ca khám thành công (`PendingReceptionReview`).
3. **Receptionist Tiếp Nhận & Check-In:**
   - Duyệt lịch hẹn (`/Receptionist/Appointments/Requests` - HTTP 200).
   - Phân phòng `Phòng Khám Tổng Quát (R101)` và ghế `Ghế Khám 101-1 (C101-1)`.
   - Chặn chọn nhầm phòng Imaging (`R003`).
   - Check-in cấp số thứ tự (`/Receptionist/Appointments/TodayCheckIn` - HTTP 200) -> Trạng thái `CheckedIn`.
4. **Dentist Khám Lâm Sàng & Thực Hiện Dịch Vụ:**
   - Mở lịch khám hôm nay (`/Dentist/Appointments/Today` - HTTP 200 sau khi dọn bỏ partial thừa).
   - Mở bệnh án (`/Dentist/MedicalRecords` - HTTP 200).
   - Bấm hoàn thành thủ thuật tại ghế -> `PerformedServices` được tạo với đúng giá niêm yết 200.000 đ.
5. **Dentist Lập Phác Đồ Từ Ca Khám (Hotfix 70694bd):**
   - Mở form "+ Lập Phác Đồ Cho Ca Này" -> Tên bệnh nhân bị khóa cố định (`readonly` kèm badge Khóa).
   - Thêm các giai đoạn điều trị (Trám răng + Điều trị tủy) -> Lưu phác đồ thành công.
   - Ghi nhận buổi điều trị giai đoạn 1 -> `PerformedServices` ghi nhận đúng đơn giá niêm yết 350.000 đ (bỏ qua giá client).
   - Bấm hoàn thành lần 2 -> Hệ thống từ chối submit lặp.
6. **Lập Hóa Đơn & Thu Tiền:**
   - Lễ tân mở danh sách viện phí (`/Receptionist/Billing/Index` - HTTP 200).
   - Lập hóa đơn tự động lấy từ `PerformedServices` -> Xác nhận thanh toán đầy đủ / một phần.

---

### 1.2. Các Bước Chỉ Được UNIT / API TEST (Chưa Browser E2E toàn trình liên phiên)
1. **Patient 1-Click Booking Từ FollowUp:**
   - API link `FollowUp.ResultingAppointmentId = appointment.AppointmentId` và cập nhật `Status = 'Scheduled'` đã được unit test trong `AppointmentServiceTests` và acceptance test.
   - Luồng bấm nút 1-click từ giao diện `/Patient/FollowUps` đã pass HTTP 200, nhưng việc tạo ca khám hẹn tái khám ở ngày tương lai (ví dụ: ngày 24/11) chưa thể thực hiện trọn vẹn bước Check-in trên giao diện ngày hôm nay vì hệ thống tuân thủ quy tắc thời gian thực (`ClinicTimeHelper.Today`).
2. **Kê Đơn & Cấp Phát Thuốc:**
   - Backend logic (kê đơn chưa trừ kho, chỉ trừ kho khi bấm Cấp phát, kiểm tra tồn kho tối thiểu, không đòi batch vật lý) đã pass 100% trong `DentalClinic.Tests`.
   - Đã verify trên database: `Medicines.StockQuantity` lưu trữ trực tiếp.
3. **Chống Tráo Bệnh Nhân & Anti-Tamper Pricing:**
   - Đã chạy E2E Script trực tiếp bằng `curl/REST`: Chặn `PatientUserId = 999` với HTTP 400; chặn giá `1.00 VNĐ` ghi nhận `350.000 VNĐ`.

---

### 1.3. Các Bước Chưa Triển Khai Hoặc Bị Giới Hạn Nghiệp Vụ
1. **Luồng X-Quang Hai Bác Sĩ Độc Lập Chuyển Giao Liên Khoa:**
   - **Tình trạng:** **IMPLEMENTED AT API BUT NOT FULLY VERIFIED IN BROWSER AS 2-SEPARATE ROSTERS**.
   - **Phân tích kỹ thuật:**
     - Mã nguồn Backend có đủ các endpoint: tạo order, start session, acquire, upload file, review.
     - Tuy nhiên, trong `DentistXrayQueueController.cs` (dòng 28), mã phòng mặc định đang đặt là `roomId ?? 9`, trong khi phòng X-quang thực tế trong database fixture là `RoomId = 5` (`Phòng Chụp X-quang`, RoomCode `R003`). Nếu truy cập `/Dentist/XrayQueue` không kèm param, hệ thống sẽ báo lỗi *"Không tìm thấy phòng chụp"*.
     - Để demo X-quang an toàn, bác sĩ khám chính thực hiện chỉ định tại ca khám (`OrderXray`) và tải ảnh phim trực tiếp tại tab **"Chẩn Đoán Hình Ảnh & X-Quang"** của ca khám. Luồng này hoạt động mượt mà và trực quan nhất.
2. **Check-In Ca Khám Tái Khám Tương Lai:**
   - **Tình trạng:** **INTENTIONALLY BLOCKED BY TIME INTEGRITY RULE**.
   - Ca khám tạo cho ngày tái khám (ví dụ: sau 7 ngày) không thể check-in trên bảng "Điểm Danh Hôm Nay". Đây là tính năng bảo vệ toàn vẹn lịch khám, KHÔNG phải lỗi hệ thống.

---

## 2. NHỮNG LỖI TÀI LIỆU ĐÃ SỬA TRONG `FINAL_DEMO_MANUAL_TEST_GUIDE.md`

| Nội dung cũ | Vấn đề phát hiện | Đã sửa lại thành |
| :--- | :--- | :--- |
| `TreatmentPlanItems.PhaseOrder` | Lỗi SQL `Invalid column name 'PhaseOrder'`. | Sửa thành `TreatmentPlanItems.SequenceNo` (cột chuẩn trong EF Core & DB). |
| `https://localhost:7129/Patient/FollowUps/Index` | Route trả về HTTP 404 vì Controller chỉ map `[HttpGet("")]`. | Sửa thành `https://localhost:7129/Patient/FollowUps` (HTTP 200 OK). |
| `https://localhost:7129/Admin/Services/Index` | Route trả về HTTP 404 vì Controller chỉ map `[HttpGet("")]`. | Sửa thành `https://localhost:7129/Admin/Services` (HTTP 200 OK). |
| Tên phòng: "Phòng Khám Tổng Quát 101" | Sai tên so với Database fixture. | Sửa thành "Phòng Khám Tổng Quát" (RoomCode: `R101`, RoomId: 1). |
| Tên ghế: "Ghế Nha Khoa G-01" | Ghế không tồn tại trong DB. | Sửa thành "Ghế Khám 101-1" (ChairCode: `C101-1`, DentalChairId: 1). |
| Kê Amoxicillin cho ca cạo vôi răng | Sai chỉ định lâm sàng nha khoa thông thường. | Đổi sang Paracetamol 500mg (2.000 đ/viên) giảm ê buốt nhẹ, phù hợp thực tế. |
| Giá thuốc Amoxicillin: "2.000 đ" | Sai giá so với bảng `Medicines` (giá thật là 5.500 đ). | Sử dụng Paracetamol 500mg giá 2.000 đ, khớp 100% với đơn giá DB. |
| "Kỹ thuật viên X-quang" | Trái với quy định chỉ có đúng 4 role. | Loại bỏ hoàn toàn; X-quang do Bác sĩ (Dentist) phụ trách. |
| Coi VietQR hiển thị là đã thanh toán | VietQR chỉ là phương thức chuyển tiền. | Làm rõ Lễ tân phải xác nhận đã nhận tiền trước khi hóa đơn thành `Paid`. |

---

## 3. CÁC BLOCKER CÒN LẠI TRƯỚC DEMO 19/11/2026

- **Lỗi P0 (Nghiêm trọng chặn demo):** **KHÔNG CÓ (0 BLOCKER)**.
- **Rủi ro vận hành khi demo:**
  1. Nếu mở `/Dentist/XrayQueue` mà không truyền `?roomId=5`, màn hình sẽ lỗi vì roomId mặc định 9 không có trong DB. *(Khắc phục: Demo X-quang trực tiếp tại tab Bệnh án của ca khám).*
  2. Bệnh nhân đặt lịch cho bác sĩ có ngày nghỉ (như BS. Hùng ngày 18/11) sẽ bị từ chối. *(Khắc phục: Luôn chọn ngày 17/11 khi demo đặt lịch).*

---

## 4. ĐỀ XUẤT KỊCH BẢN DEMO AN TOÀN & ẤN TƯỢNG NHẤT (RECOMMENDED DEMO SCRIPT)

Để buổi bảo vệ ngày 19/11 diễn ra hoàn hảo, không gặp sự cố về dữ liệu hoặc giới hạn thời gian:

### Kịch Bản 1: Luồng Khám & Điều Trị Tại Ghế Toàn Trình (Thời lượng: 5 phút)
1. **Patient:** Đăng nhập `patient1@dentalcare.test`, đặt lịch khám "Khám & Lấy Cao Răng" với BS. Đặng Văn Hùng trong ca sáng hôm nay.
2. **Receptionist:** Đăng nhập `receptionist@dentalcare.test`, duyệt phân phòng `Phòng Khám Tổng Quát (R101)` và ghế `Ghế Khám 101-1 (C101-1)`. Sau đó sang bảng điểm danh bấm **Check-in** cấp số thứ tự.
3. **Dentist:** Đăng nhập `dentist.hung@dentalcare.test`, vào ca khám:
   - Chỉ định X-quang Panorama.
   - Mở tab X-quang tải ảnh phim và kết luận -> Phim hiển thị to rõ tại ghế.
   - Bấm hoàn thành dịch vụ lấy cao răng.
   - Kê đơn 10 viên Paracetamol 500mg và bấm **Cấp phát thuốc**.
   - Bấm **Hoàn tất buổi khám**.
4. **Receptionist:** Mở bảng thu viện phí, lập hóa đơn tự động (Dịch vụ 200.000 đ + Thuốc 20.000 đ = 220.000 đ), xác nhận thu tiền và in phiếu thu.

### Kịch Bản 2: Lập Phác Đồ Điều Trị Dài Hạn & Ghi Nhận Buổi Điều Trị (Thời lượng: 3 phút)
1. **Dentist:** Tại ca khám, bấm "+ Lập Phác Đồ Cho Ca Này".
2. **Chứng minh an toàn lâm sàng:** Chỉ cho người chấm điểm thấy ô Tên Bệnh Nhân đã được **Khóa cố định kèm biểu tượng ổ khóa**, không thể đổi sang bệnh nhân khác.
3. **Thêm giai đoạn:**
   - Giai đoạn 1: Trám Răng Composite (350.000 đ - R16).
   - Giai đoạn 2: Điều Trị Tủy Răng Nội Nha (1.200.000 đ - R16).
4. **Thực hiện giai đoạn 1:** Bấm ghi nhận buổi điều trị, hệ thống tự động cập nhật tiến độ lên 50% và đưa 350.000 đ vào danh mục thanh toán viện phí của ca khám mà không bị tính thừa tiền của Giai đoạn 2.
5. **Đề xuất tái khám:** Bác sĩ bấm Đề xuất hẹn tái khám cho Giai đoạn 2 sau 7 ngày.
