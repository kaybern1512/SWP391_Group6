# DENTALCARE SWP391 — SỔ TAY HƯỚNG DẪN KIỂM THỬ THỦ CÔNG (DEMO MANUAL TEST GUIDE)

**Môi trường:**
- Web App: `https://localhost:7129`
- API Backend: `https://localhost:7350`
- Database: `DentalCare_SWP391_UAT_Fixture_TEST` (SQL Server: `.\SQLEXPRESS`)
- Số bảng chuẩn: 38 bảng vật lý

*(Lưu ý an toàn: Không ghi mật khẩu hoặc token trong tài liệu này; các tài khoản demo sử dụng mật khẩu kiểm thử chuẩn đã được cấu hình trong fixture hệ thống).*

---

## 1. DANH SÁCH TÀI KHOẢN DEMO HỢP LỆ THEO 4 ROLE

| Role | Email Đăng Nhập | Họ và Tên | Mục Đích Sử Dụng |
| :--- | :--- | :--- | :--- |
| **SystemAdministrator** | `admin@dentalcare.test` | Hoàng Quản Trị Viên | Phân ca định biên, quản lý khoa, phòng/ghế, danh mục dịch vụ, duyệt bài blog. |
| **Receptionist** | `receptionist@dentalcare.test` | Nguyễn Thị Lễ Tân | Tiếp nhận lịch hẹn, phân phòng/ghế, check-in, cấp số thứ tự, lập hóa đơn và thu viện phí. |
| **Dentist (Khám chính)** | `dentist.hung@dentalcare.test` | Đặng Văn Hùng (Mã BS: BS-0001) | Bác sĩ khám lâm sàng, lập phác đồ điều trị, thực hiện thủ thuật, kê đơn và cấp thuốc. |
| **Patient** | `patient1@dentalcare.test` | Võ Văn Đông (Mã BN: BN-0001) | Bệnh nhân đặt lịch, xem phác đồ điều trị, theo dõi tái khám, tra cứu hóa đơn viện phí. |

---

## 2. HÀNH TRÌNH DEMO A: QUY TRÌNH KHÁM & ĐIỀU TRỊ TOÀN TRÌNH (COMPLETE EXAM FLOW)

### Bước A1: Bệnh Nhân Đặt Lịch Khám
- **Role:** Patient (`patient1@dentalcare.test`)
- **URL:** `https://localhost:7129/Patient/Appointment/Book`
- **Menu:** Đặt Lịch Khám Mới
- **Thao tác:**
  1. Chọn chuyên khoa: **Nha Khoa Tổng Quát**.
  2. Chọn dịch vụ: **Khám & Lấy Cao Răng** (Mã: SRV-01, Giá: 200.000 đ).
  3. Chọn hình thức: **Chọn Bác sĩ** -> Chọn **BS. Đặng Văn Hùng**.
  4. Chọn ngày: Ngày hôm nay hoặc ngày mai (chọn khung giờ ca sáng: 08:30).
  5. Bấm nút: **"Xác Nhận Đặt Lịch"**.
- **Kết quả mong đợi:** Hệ thống hiển thị trang thông báo đặt lịch thành công, mã ca khám mới dạng `#APT-...` với trạng thái `Chờ tiếp nhận (PendingReceptionReview)`.
- **Kiểm tra DB:**
  ```sql
  SELECT TOP 1 AppointmentId, PatientUserId, Status, ScheduledStart FROM Appointments ORDER BY AppointmentId DESC;
  ```
  -> `Status = 'PendingReceptionReview'`.
- **Dấu hiệu FAIL:** Báo lỗi slot không khả dụng (xem mục Xử lý sự cố nếu chưa có ca trực).

---

### Bước A2: Lễ Tân Tiếp Nhận, Phân Phòng/Ghế & Check-In
- **Role:** Receptionist (`receptionist@dentalcare.test`)
- **URL:** `https://localhost:7129/Receptionist/Appointments/Requests`
- **Menu:** Tiếp Nhận Yêu Cầu Hẹn
- **Thao tác:**
  1. Tìm ca khám vừa tạo của bệnh nhân Võ Văn Đông.
  2. Bấm nút: **"Duyệt & Phân Phòng"**.
  3. Chọn Phòng: **Phòng Khám Tổng Quát 101** -> Chọn Ghế: **Ghế Nha Khoa G-01**.
  4. Bấm nút: **"Xác Nhận Đặt Hẹn"** (Trạng thái chuyển sang `Confirmed`).
  5. Chuyển sang menu: **Điểm Danh Hôm Nay** (`/Receptionist/Appointments/TodayCheckIn`).
  6. Tại dòng của bệnh nhân, bấm nút: **"Check-In Tiếp Đón"**.
- **Kết quả mong đợi:** Ca khám được cấp số thứ tự khám (ví dụ: `STT: 01`), trạng thái chuyển sang `CheckedIn`.
- **Kiểm tra DB:**
  ```sql
  SELECT AppointmentId, Status, QueueNumber, RoomId, DentalChairId FROM Appointments WHERE AppointmentId = [Id];
  ```
  -> `Status = 'CheckedIn'`, `QueueNumber` không null, `RoomId` và `DentalChairId` đã được gán.
- **Dấu hiệu FAIL:** Nút Check-in không phản hồi hoặc báo trùng số thứ tự.

---

### Bước A3: Bác Sĩ Tiến Hành Khám & Thực Hiện Dịch Vụ
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **URL:** `https://localhost:7129/Dentist/Appointments/Today`
- **Menu:** Lịch Khám & Điều Trị
- **Thao tác:**
  1. Tìm ca khám của bệnh nhân Võ Văn Đông (trạng thái `CheckedIn`).
  2. Bấm nút: **"Vào Khám (Start Exam)"** -> Mở giao diện Bệnh Án Lâm Sàng (`/Dentist/MedicalRecords/Index?appointmentId=...`).
  3. Tại tab **"Dịch Vụ Điều Trị Tại Ghế"**:
     - Dịch vụ ban đầu "Khám & Lấy Cao Răng" hiển thị trạng thái `InService`.
     - Bác sĩ bấm nút: **"Hoàn Thành Thủ Thuật"**.
     - Nhập ghi chú lâm sàng: "Đã cạo vôi răng sạch sẽ 2 hàm".
     - Bấm **"Lưu & Hoàn Tất Dịch Vụ"**.
- **Kết quả mong đợi:** Dịch vụ chuyển sang trạng thái `Completed`. `PerformedServices` được tạo với đúng đơn giá niêm yết 200.000 đ.
- **Kiểm tra DB:**
  ```sql
  SELECT PerformedServiceId, AppointmentId, DepartmentServiceId, Quantity, UnitPrice FROM PerformedServices WHERE AppointmentId = [Id];
  ```
  -> Có đúng 1 dòng với `UnitPrice = 200000.00`.
- **Dấu hiệu FAIL:** Phát sinh PerformedService trước khi bấm hoàn thành hoặc sai giá.

---

### Bước A4: Bác Sĩ Kê Đơn & Cấp Phát Thuốc
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **URL:** Tiếp tục trên màn hình Bệnh Án Lâm Sàng, chuyển sang tab **"Kê Đơn Thuốc"**.
- **Thao tác:**
  1. Bấm nút: **"+ Thêm Thuốc Vào Đơn"**.
  2. Chọn thuốc: **Amoxicillin 500mg** (Số lượng: 10 viên, Liều dùng: 2 viên/ngày).
  3. Bấm **"Lưu Đơn Thuốc"**. (Lúc này tồn kho CHƯA bị trừ, hóa đơn CHƯA tính tiền thuốc).
  4. Bấm nút: **"Cấp Phát Thuốc Tại Quầy Dược"**.
  5. Xác nhận hộp thoại cấp phát.
- **Kết quả mong đợi:** Đơn thuốc chuyển sang trạng thái `Đã cấp phát (Dispensed)`. Số lượng tồn trong bảng `Medicines` tự động giảm đi 10.
- **Kiểm tra DB:**
  ```sql
  SELECT MedicineName, StockQuantity FROM Medicines WHERE MedicineName LIKE '%Amoxicillin%';
  SELECT PrescriptionId, Status FROM Prescriptions WHERE AppointmentId = [Id];
  ```
  -> Đơn thuốc `Status = 'Dispensed'`, tồn kho đã trừ chính xác.
- **Dấu hiệu FAIL:** Thuốc bị trừ 2 lần hoặc báo lỗi bắt chọn lô thuốc vật lý.

---

### Bước A5: Bác Sĩ Hoàn Tất Ca Khám
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **Thao tác:**
  1. Tại đầu trang Bệnh án lâm sàng, bấm nút: **"Hoàn Tất Buổi Khám (Complete Exam)"**.
  2. Nhập tóm tắt chẩn đoán: "Vệ sinh răng miệng tốt, hẹn tái khám sau 6 tháng".
  3. Bấm **"Xác Nhận Hoàn Tất"**.
- **Kết quả mong đợi:** Ca khám chuyển sang trạng thái `Completed`, thông báo chuyển viện phí sang quầy tiếp tân.

---

### Bước A6: Lễ Tân Lập Hóa Đơn & Thu Viện Phí
- **Role:** Receptionist (`receptionist@dentalcare.test`)
- **URL:** `https://localhost:7129/Receptionist/Billing/Index`
- **Menu:** Thu Viện Phí & Hóa Đơn
- **Thao tác:**
  1. Tại danh sách "Chờ Thanh Toán", tìm bệnh nhân Võ Văn Đông.
  2. Bấm nút: **"Lập Hóa Đơn"** -> Mở trang `/Receptionist/Billing/Create?appointmentId=...`.
  3. Kiểm tra bảng kê chi phí tự động:
     - Dịch vụ: Khám & Lấy Cao Răng (200.000 đ)
     - Thuốc: Amoxicillin 500mg (10 viên x 2.000 đ = 20.000 đ)
     - Tổng cộng: **220.000 đ**.
  4. Bấm nút: **"Tạo Hóa Đơn"**.
  5. Tại màn hình Chi Tiết Hóa Đơn, chọn Phương thức thanh toán: **Tiền Mặt** hoặc **Chuyển Khoản (VietQR)**.
  6. Bấm nút: **"Xác Nhận Đã Thu Đủ Viện Phí"**.
- **Kết quả mong đợi:** Hóa đơn chuyển sang trạng thái `Đã Thanh Toán (Paid)`. Có nút **"In Hóa Đơn Viện Phí"**.
- **Kiểm tra DB:**
  ```sql
  SELECT InvoiceId, AppointmentId, TotalAmount, Status FROM Invoices WHERE AppointmentId = [Id];
  SELECT PaymentId, Amount, PaymentMethod, Status FROM Payments WHERE InvoiceId = [InvoiceId];
  ```
  -> Hóa đơn `Status = 'Paid'`, thanh toán đủ 100%.

---

## 3. HÀNH TRÌNH DEMO B: PHÁC ĐỒ ĐIỀU TRỊ NHIỀU BUỔI (MULTI-VISIT TREATMENT PLAN)

### Bước B1: Bác Sĩ Lập Phác Đồ Từ Ca Khám
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **URL:** Giao diện Bệnh án ca khám hiện tại.
- **Thao tác:**
  1. Bấm nút màu xanh: **"+ Lập Phác Đồ Cho Ca Này"**.
  2. Kiểm tra ô **"Chọn Bệnh Nhân Tiếp Nhận"**: Hiển thị tên Võ Văn Đông và có biểu tượng **Khóa cố định** (`Cố định`). Không thể sửa đổi hoặc chuyển sang bệnh nhân khác.
  3. Chọn Chuyên khoa: **Nha Khoa Tổng Quát**.
  4. Tại mục "Lộ Trình Các Giai Đoạn Điều Trị", bấm nút: **"+ Thêm Giai Đoạn"**:
     - Giai đoạn 1: Chọn **Trám Răng Composite** (Vị trí răng: Răng 16, Chi phí: 350.000 đ).
     - Bấm tiếp **"+ Thêm Giai Đoạn"**:
     - Giai đoạn 2: Chọn **Điều Trị Tủy Răng** (Vị trí răng: Răng 16, Chi phí: 500.000 đ).
  5. Bấm nút: **"Lưu Phác Đồ Điều Trị"**.
- **Kết quả mong đợi:** Phác đồ được tạo với mã `#TP-...`, tiến độ 0%, tự động quay trở lại tab Phác Đồ của ca khám hiện tại.
- **Kiểm tra DB:**
  ```sql
  SELECT TreatmentPlanId, PatientUserId, Status, EstimatedTotal FROM TreatmentPlans ORDER BY TreatmentPlanId DESC;
  SELECT PlanItemId, PhaseOrder, Status FROM TreatmentPlanItems WHERE TreatmentPlanId = [PlanId];
  ```
  -> 2 PlanItems được lưu với trạng thái `Planned`.

---

### Bước B2: Ghi Nhận Thực Hiện Giai Đoạn 1 Tại Ca Khám
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **Thao tác:**
  1. Tại chi tiết phác đồ, bấm nút: **"Ghi Nhận Thực Hiện Buổi Điều Trị (Record Session)"**.
  2. Chọn giai đoạn: **Giai đoạn 1 (Trám Răng Composite)**.
  3. Chọn trạng thái cập nhật: **Hoàn thành giai đoạn (Completed)**.
  4. Nhập nhận xét lâm sàng: "Đã trám xoang sâu mặt nhai R16".
  5. Bấm **"Lưu Buổi Điều Trị"**.
- **Kết quả mong đợi:** Giai đoạn 1 chuyển sang `Completed`. Tiến độ phác đồ tăng lên **50%**. Bảng `PerformedServices` được ghi nhận với đúng giá niêm yết 350.000 đ.
- **Kiểm tra DB:**
  ```sql
  SELECT SessionId, TreatmentPlanId, AppointmentId FROM TreatmentSessions WHERE TreatmentPlanId = [PlanId];
  SELECT PerformedServiceId, UnitPrice FROM PerformedServices WHERE TreatmentSessionId = [SessionId];
  ```
  -> Có 1 `TreatmentSession` liên kết cả `TreatmentPlanId` và `AppointmentId`, `PerformedService` ghi nhận 350.000 đ.

---

### Bước B3: Bác Sĩ Đề Xuất Hẹn Tái Khám Cho Giai Đoạn 2
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **Thao tác:**
  1. Tại màn hình Bệnh án lâm sàng, bấm nút: **"Đề Xuất Hẹn Tái Khám (Follow-Up)"**.
  2. Chọn Ngày khuyến nghị: Sau 7 ngày.
  3. Dịch vụ tái khám: **Điều Trị Tủy Răng** (Giai đoạn 2 của phác đồ).
  4. Bấm **"Gửi Đề Xuất Tái Khám"**.
- **Kết quả mong đợi:** Bản ghi `FollowUpSchedules` được tạo với trạng thái `Proposed`. Hệ thống **KHÔNG tự động tạo ca khám Confirmed** (chờ bệnh nhân xác nhận).
- **Kiểm tra DB:**
  ```sql
  SELECT FollowUpId, RecommendedDate, Status, ResultingAppointmentId FROM FollowUpSchedules ORDER BY FollowUpId DESC;
  ```
  -> `Status = 'Proposed'`, `ResultingAppointmentId IS NULL`.

---

### Bước B4: Bệnh Nhân Đặt Lịch Tái Khám Bằng 1-Click
- **Role:** Patient (`patient1@dentalcare.test`)
- **URL:** `https://localhost:7129/Patient/FollowUps/Index`
- **Menu:** Lịch Tái Khám Khuyến Nghị
- **Thao tác:**
  1. Nhìn thấy lời nhắc tái khám: "Điều Trị Tủy Răng - BS Đặng Văn Hùng".
  2. Bấm nút: **"Đặt Hẹn Theo Khuyến Nghị (1-Click Booking)"**.
  3. Chọn giờ hẹn phù hợp và bấm **"Xác Nhận Hẹn"**.
- **Kết quả mong đợi:** Ca khám mới được tạo, bản ghi FollowUp được cập nhật `ResultingAppointmentId`. Bác sĩ tiếp tục phác đồ cũ tại ca khám mới này.

---

## 4. HÀNH TRÌNH DEMO C: CHỈ ĐỊNH & XEM PHIM X-QUANG (IMAGING FLOW)

### Bước C1: Bác Sĩ Chỉ Định Chụp X-Quang
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **URL:** `https://localhost:7129/Dentist/Appointments/Today`
- **Thao tác:**
  1. Tại hàng ca khám của bệnh nhân, bấm nút: **"Chỉ Định X-Quang"**.
  2. Chọn Loại phim: **Panorama (Toàn cảnh hàm răng)**.
  3. Nhập lý do: "Chụp khảo sát cuống răng và xương hàm trước khi điều trị tủy".
  4. Bấm **"Xác Nhận Chỉ Định"**.
- **Kết quả mong đợi:** Ca khám hiển thị huy hiệu `Chỉ định X-Quang: Panorama`. Bản ghi `Diagnostics` được tạo với trạng thái chờ xử lý.

---

### Bước C2: Tiếp Nhận & Upload Phim X-Quang
- **Role:** Dentist / Kỹ thuật viên chẩn đoán hình ảnh (`dentist.hung@dentalcare.test` hoặc Lễ tân hỗ trợ)
- **URL:** Giao diện Bệnh Án Lâm Sàng -> Tab **"Chẩn Đoán Hình Ảnh & X-Quang"**.
- **Thao tác:**
  1. Chọn tệp ảnh phim X-quang (hỗ trợ .jpg, .png dung lượng < 10MB).
  2. Nhập kết luận phim: "Tiêu xương vùng cuống răng 16 nhẹ, không phát hiện nang xương".
  3. Bấm **"Tải Lên & Lưu Kết Quả"**.
- **Kết quả mong đợi:** Ảnh phim hiển thị trực quan, có chế độ phóng to xem chi tiết ngay tại ghế điều trị. Ca khám KHÔNG bị tự động kết thúc. Bác sĩ tiếp tục điều trị dựa trên kết quả phim.

---

## 5. HƯỚNG DẪN KIỂM TRA CÁC TÌNH HUỐNG NGOẠI LỆ (EDGE CASES)

### Tình huống 1: Bác Sĩ Nghỉ Phép (Approved Leave)
- **Kịch bản:** Admin phân ca trực cho bác sĩ vào ngày mà bác sĩ đã có đơn nghỉ phép được duyệt.
- **Thao tác:** Admin vào `Admin/DentistSchedule`, chọn ngày bác sĩ nghỉ phép và thử gán ca.
- **Kết quả mong đợi:** Hệ thống hiển thị cảnh báo đỏ từ chối: *"Bác sĩ đã có đơn nghỉ phép được duyệt trong ngày này, không thể phân ca trực."*

### Tình huống 2: Chọn Phòng Không Phù Hợp (Imaging Room Guard)
- **Kịch bản:** Lễ tân duyệt ca khám trám răng thường nhưng chọn vào phòng Chẩn Đoán Hình Ảnh (Phòng X-Quang).
- **Thao tác:** Tại màn hình duyệt lịch hẹn, chọn phòng Imaging cho dịch vụ nha khoa tổng quát.
- **Kết quả mong đợi:** Hệ thống cảnh báo: *"Phòng X-quang chỉ dành riêng cho các thủ thuật chẩn đoán hình ảnh."*

### Tình huống 3: Cấp Phát Thuốc Vượt Tồn Kho
- **Kịch bản:** Bác sĩ kê 100 viên nhưng tồn kho chỉ còn 5 viên.
- **Thao tác:** Nhập số lượng 100 và bấm cấp phát thuốc.
- **Kết quả mong đợi:** Hệ thống chặn lại với thông báo: *"Số lượng thuốc yêu cầu (100) vượt quá số lượng tồn kho khả dụng (5)."*

### Tình huống 4: Thanh Toán Viện Phí Một Phần (Partial Payment)
- **Kịch bản:** Bệnh nhân chỉ thanh toán trước một nửa tiền viện phí.
- **Thao tác:** Tại màn hình thu tiền, nhập số tiền trả một phần và chọn phương thức.
- **Kết quả mong đợi:** Hóa đơn ghi nhận trạng thái `PartiallyPaid`, hiển thị rõ số tiền còn nợ (`RemainingAmount`).
