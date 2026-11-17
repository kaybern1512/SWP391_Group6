# DENTALCARE SWP391 — SỔ TAY HƯỚNG DẪN KIỂM THỬ THỦ CÔNG (DEMO MANUAL TEST GUIDE)

**Môi trường:**
- Web App: `https://localhost:7129`
- API Backend: `https://localhost:7350`
- Database: `DentalCare_SWP391_UAT_Fixture_TEST` (SQL Server: `.\SQLEXPRESS`)
- Số bảng chuẩn: 38 bảng vật lý

*(Lưu ý an toàn: Không ghi mật khẩu hoặc token trong tài liệu này; các tài khoản demo sử dụng mật khẩu kiểm thử chuẩn `DentalCare@2026!` đã được cấu hình trong fixture hệ thống).*

---

## 1. DANH SÁCH TÀI KHOẢN DEMO HỢP LỆ THEO CHÍNH XÁC 4 ROLE

Hệ thống DentalCare tuân thủ nghiêm ngặt **chính xác 4 role**, tuyệt đối không có role thứ năm (như kỹ thuật viên X-quang hay dược sĩ):

| Role | Email Đăng Nhập | Họ và Tên | Mục Đích Sử Dụng |
| :--- | :--- | :--- | :--- |
| **SystemAdministrator** | `admin@dentalcare.test` | Hoàng Quản Trị Viên | Phân ca định biên, quản lý khoa, phòng/ghế, danh mục dịch vụ, duyệt bài blog. |
| **Receptionist** | `receptionist@dentalcare.test` | Nguyễn Thị Lễ Tân | Tiếp nhận lịch hẹn, phân phòng/ghế, check-in, cấp số thứ tự, lập hóa đơn và xác nhận thu viện phí. |
| **Dentist (Khám chính & X-quang)** | `dentist.hung@dentalcare.test` | Đặng Văn Hùng (Mã BS: BS-0001) | Bác sĩ khám lâm sàng, lập phác đồ, thực hiện thủ thuật, chỉ định và đọc phim X-quang, kê đơn và cấp thuốc. |
| **Patient** | `patient1@dentalcare.test` | Võ Văn Đông (Mã BN: BN-0001) | Bệnh nhân đặt lịch, xem phác đồ điều trị, theo dõi tái khám, tra cứu hóa đơn viện phí. |

---

## 2. HÀNH TRÌNH DEMO A: QUY TRÌNH KHÁM & ĐIỀU TRỊ TOÀN TRÌNH (COMPLETE EXAM FLOW)

### Bước A1: Bệnh Nhân Đặt Lịch Khám Trong Ngày
- **Role:** Patient (`patient1@dentalcare.test`)
- **URL:** `https://localhost:7129/Patient/Appointment/Book`
- **Menu:** Đặt Lịch Khám Mới
- **Thao tác:**
  1. Chọn chuyên khoa: **Nha Khoa Tổng Quát** (DepartmentId: 1).
  2. Chọn dịch vụ: **Khám & Lấy Cao Răng** (Mã: `SVC-GEN-01`, Đơn giá niêm yết: `200.000 đ`).
  3. Chọn hình thức: **Chọn Bác sĩ** -> Chọn **BS. Đặng Văn Hùng**.
  4. Chọn ngày: Chọn **Hôm nay (17/11/2026)** vào khung giờ ca trực hợp lệ (Ca sáng: `08:00 - 12:00` hoặc Ca chiều: `13:30 - 17:30`).
     *(Lưu ý: Không đặt lịch vào ngày bác sĩ nghỉ phép như 18/11 vì hệ thống sẽ chặn theo quy tắc bảo vệ ngày nghỉ Approved Leave).*
  5. Bấm nút: **"Xác Nhận Đặt Lịch"**.
- **Kết quả mong đợi:** Hệ thống hiển thị thông báo đặt lịch thành công, mã ca khám mới dạng `#APT-...` với trạng thái `Chờ tiếp nhận (PendingReceptionReview)`.
- **Kiểm tra DB:**
  ```sql
  SELECT TOP 1 AppointmentId, PatientUserId, Status, ScheduledStart FROM Appointments ORDER BY AppointmentId DESC;
  ```
  -> `Status = 'PendingReceptionReview'`.

---

### Bước A2: Lễ Tân Tiếp Nhận, Phân Phòng/Ghế & Check-In
- **Role:** Receptionist (`receptionist@dentalcare.test`)
- **URL Tiếp nhận:** `https://localhost:7129/Receptionist/Appointments/Requests`
- **URL Check-in:** `https://localhost:7129/Receptionist/Appointments/TodayCheckIn`
- **Thao tác:**
  1. Tại màn hình Tiếp Nhận, tìm ca khám vừa tạo của bệnh nhân Võ Văn Đông.
  2. Bấm nút: **"Duyệt & Phân Phòng"**.
  3. Chọn Phòng: **Phòng Khám Tổng Quát** (Mã: `R101`, RoomId: 1).
  4. Chọn Ghế: **Ghế Khám 101-1** (Mã: `C101-1`, DentalChairId: 1).
     *(Lưu ý: Không chọn Phòng Chụp X-quang R003 vì hệ thống sẽ chặn phân phòng Imaging cho ca điều trị tổng quát).*
  5. Bấm nút: **"Xác Nhận Đặt Hẹn"** (Trạng thái chuyển sang `Confirmed`).
  6. Chuyển sang menu: **Điểm Danh Hôm Nay** (`/Receptionist/Appointments/TodayCheckIn`).
  7. Bấm nút: **"Check-In Tiếp Đón"** tại dòng ca khám của bệnh nhân.
     *(Lưu ý thời gian: Hệ thống chỉ cho phép check-in đối với lịch hẹn diễn ra trong ngày hôm nay. Các lịch hẹn đặt cho ngày tương lai sẽ không hiển thị trên bảng check-in hôm nay).*
- **Kết quả mong đợi:** Ca khám được cấp số thứ tự khám (`QueueNumber`), trạng thái chuyển sang `CheckedIn`.
- **Kiểm tra DB:**
  ```sql
  SELECT AppointmentId, Status, QueueNumber, RoomId, DentalChairId FROM Appointments WHERE AppointmentId = [Id];
  ```
  -> `Status = 'CheckedIn'`, `QueueNumber` có giá trị, `RoomId = 1`, `DentalChairId = 1`.

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
     - Nhập ghi chú lâm sàng: "Đã cạo vôi răng sạch sẽ 2 hàm, hướng dẫn vệ sinh kẽ răng".
     - Bấm **"Lưu & Hoàn Tất Dịch Vụ"**.
- **Kết quả mong đợi:** Dịch vụ chuyển sang trạng thái `Completed`. `PerformedServices` được tạo với đúng đơn giá niêm yết server-authoritative là **200.000 đ**.
- **Kiểm tra DB:**
  ```sql
  SELECT PerformedServiceId, AppointmentId, DepartmentServiceId, Quantity, UnitPrice, Notes 
  FROM PerformedServices WHERE AppointmentId = [Id];
  ```
  -> Có đúng 1 dòng với `DepartmentServiceId = 1`, `UnitPrice = 200000.00`.

---

### Bước A4: Bác Sĩ Kê Đơn Thuốc Phù Hợp & Cấp Phát
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **URL:** Tiếp tục trên màn hình Bệnh Án Lâm Sàng, chuyển sang tab **"Kê Đơn Thuốc"**.
- **Thao tác:**
  1. Bấm nút: **"+ Thêm Thuốc Vào Đơn"**.
  2. Chọn thuốc phù hợp lâm sàng cho ca lấy cao răng (giảm ê buốt / đau nhẹ):
     - Thuốc: **Paracetamol 500mg** (Mã: `MED-PARA-01`, Đơn giá: `2.000 đ/viên`).
     - Số lượng: **10** viên. Liều dùng: "Uống 1 viên khi ê buốt sau ăn".
     *(Lưu ý: Không kê Amoxicillin cho ca cạo vôi răng thông thường vì không đúng chỉ định lâm sàng. Amoxicillin chỉ dùng khi có nhiễm trùng, viêm tủy có mủ hoặc phẫu thuật).*
  3. Bấm **"Lưu Đơn Thuốc"** (Lúc này tồn kho CHƯA bị trừ, hóa đơn CHƯA tính tiền thuốc).
  4. Bác sĩ bấm nút: **"Cấp Phát Thuốc Tại Quầy Dược"**.
  5. Xác nhận hộp thoại cấp phát.
- **Kết quả mong đợi:** Đơn thuốc chuyển sang trạng thái `Đã cấp phát (Dispensed)`. Số lượng tồn của Paracetamol 500mg trong bảng `Medicines` tự động giảm đi 10 (từ 100 xuống 90).
- **Kiểm tra DB:**
  ```sql
  SELECT MedicineId, MedicineName, StockQuantity FROM Medicines WHERE MedicineId = 1;
  SELECT PrescriptionId, Status FROM Prescriptions WHERE AppointmentId = [Id];
  ```
  -> `Status = 'Dispensed'`, `StockQuantity` giảm chính xác 10 viên.

---

### Bước A5: Bác Sĩ Hoàn Tất Ca Khám
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **Thao tác:**
  1. Tại đầu trang Bệnh án lâm sàng, bấm nút: **"Hoàn Tất Buổi Khám (Complete Exam)"**.
  2. Nhập tóm tắt chẩn đoán: "Vôi răng độ 1 đã lấy sạch, men răng tốt".
  3. Bấm **"Xác Nhận Hoàn Tất"**.
- **Kết quả mong đợi:** Ca khám chuyển sang trạng thái `Completed`, thông báo chuyển viện phí sang quầy lễ tân.

---

### Bước A6: Lễ Tân Lập Hóa Đơn & Thu Viện Phí
- **Role:** Receptionist (`receptionist@dentalcare.test`)
- **URL:** `https://localhost:7129/Receptionist/Billing/Index`
- **Menu:** Thu Viện Phí & Hóa Đơn
- **Thao tác:**
  1. Tại danh sách "Chờ Thanh Toán", tìm ca khám của bệnh nhân Võ Văn Đông.
  2. Bấm nút: **"Lập Hóa Đơn"** -> Mở trang `/Receptionist/Billing/Create?appointmentId=...`.
  3. Kiểm tra bảng kê chi phí tự động:
     - Dịch vụ: Khám & Lấy Cao Răng: **200.000 đ**
     - Thuốc đã cấp: Paracetamol 500mg (10 viên x 2.000 đ): **20.000 đ**
     - Tổng cộng viện phí: **220.000 đ**.
  4. Bấm nút: **"Tạo Hóa Đơn"**.
  5. Tại màn hình Chi Tiết Hóa Đơn:
     - Lựa chọn phương thức: **Tiền Mặt** hoặc **Chuyển Khoản Ngân Hàng (VietQR)**.
     *(Lưu ý: Mã VietQR hiển thị là phương tiện để bệnh nhân quét chuyển khoản; hệ thống KHÔNG tự động coi là đã thanh toán khi mới chỉ hiển thị mã QR).*
  6. Sau khi kiểm tra tiền đã vào tài khoản / nhận đủ tiền mặt, Lễ tân bấm nút: **"Xác Nhận Đã Thu Đủ Viện Phí"**.
- **Kết quả mong đợi:** Hóa đơn chuyển sang trạng thái `Đã Thanh Toán (Paid)`. Có nút **"In Hóa Đơn Viện Phí"** cho bệnh nhân.
- **Kiểm tra DB:**
  ```sql
  SELECT InvoiceId, AppointmentId, TotalAmount, Status FROM Invoices WHERE AppointmentId = [Id];
  SELECT PaymentId, Amount, PaymentMethod, Status FROM Payments WHERE InvoiceId = [InvoiceId];
  ```
  -> Hóa đơn `Status = 'Paid'`, thanh toán đúng `220000.00`.

---

## 3. HÀNH TRÌNH DEMO B: PHÁC ĐỒ ĐIỀU TRỊ NHIỀU BUỔI (MULTI-VISIT TREATMENT PLAN)

### Bước B1: Bác Sĩ Lập Phác Đồ Từ Ca Khám
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **URL:** Mở ca khám đang diễn ra của bệnh nhân Võ Văn Đông tại `/Dentist/MedicalRecords/Index?appointmentId=...`.
- **Thao tác:**
  1. Bấm nút màu xanh: **"+ Lập Phác Đồ Cho Ca Này"**.
  2. Kiểm tra ô **"Chọn Bệnh Nhân Tiếp Nhận"**:
     - Hiển thị tên bệnh nhân: `Võ Văn Đông - Mã: BN-0001`.
     - Có huy hiệu **Cố định** kèm biểu tượng khóa (`<i class="bi bi-lock-fill"></i> Cố định`).
     - Input ở chế độ readonly, không thể can thiệp chọn sang bệnh nhân khác (Bảo vệ toàn vẹn Task 1).
  3. Chọn Chuyên khoa điều trị: **Nha Khoa Tổng Quát**.
  4. Tại mục "Lộ Trình Các Giai Đoạn Điều Trị", bấm nút: **"+ Thêm Giai Đoạn"**:
     - **Giai đoạn 1:** Chọn dịch vụ **Trám Răng Composite** (Mã: `SVC-FILL-01`, Chi phí: `350.000 đ`, Vị trí: `Răng 16`).
     - Bấm tiếp **"+ Thêm Giai Đoạn"**:
     - **Giai đoạn 2:** Chọn dịch vụ **Điều Trị Tủy Răng (Nội Nha)** (Mã: `SVC-ENDO-01`, Chi phí: `1.200.000 đ`, Vị trí: `Răng 16`).
     - Tổng chi phí dự kiến tự động tính: **1.550.000 đ**.
  5. Bấm nút: **"Lưu Phác Đồ Điều Trị"**.
- **Kết quả mong đợi:** Phác đồ được tạo thành công với mã `#TP-...`, tiến độ 0%, tự động điều hướng quay về tab `#tab-plans` của ca khám hiện tại.
- **Kiểm tra DB:**
  ```sql
  SELECT TreatmentPlanId, PatientUserId, Status, EstimatedTotal FROM TreatmentPlans ORDER BY TreatmentPlanId DESC;
  SELECT PlanItemId, SequenceNo, DepartmentServiceId, ToothNumber, EstimatedCost, Status 
  FROM TreatmentPlanItems WHERE TreatmentPlanId = [PlanId] ORDER BY SequenceNo ASC;
  ```
  -> Cột **`SequenceNo`** lưu thứ tự giai đoạn (1 và 2), trạng thái `Planned`, chi phí tương ứng `350000.00` và `1200000.00`.

---

### Bước B2: Ghi Nhận Thực Hiện Giai Đoạn 1 Tại Ca Khám Hiện Tại
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **Thao tác:**
  1. Tại chi tiết phác đồ trong ca khám, bấm nút: **"Ghi Nhận Thực Hiện Buổi Điều Trị (Record Session)"**.
  2. Chọn giai đoạn: **Giai đoạn 1 (Trám Răng Composite)**.
  3. Chọn trạng thái cập nhật: **Hoàn thành giai đoạn (Completed)**.
  4. Nhập ghi chú lâm sàng: "Đã làm sạch xoang sâu mặt nhai R16, trám composite thẩm mỹ".
  5. Bấm **"Lưu Buổi Điều Trị"**.
- **Kết quả mong đợi:** Giai đoạn 1 chuyển sang `Completed`. Tiến độ phác đồ cập nhật lên **50%**. Hệ thống ghi nhận 1 dòng `PerformedService` với đúng giá niêm yết **350.000 đ** (server-authoritative).
- **Kiểm tra DB:**
  ```sql
  SELECT SessionId, TreatmentPlanId, AppointmentId, SessionDate FROM TreatmentSessions WHERE TreatmentPlanId = [PlanId];
  SELECT PerformedServiceId, DepartmentServiceId, UnitPrice, Notes FROM PerformedServices WHERE TreatmentSessionId = [SessionId];
  ```
  -> `TreatmentSession` liên kết cả `TreatmentPlanId` và `AppointmentId`, `UnitPrice = 350000.00`.

---

### Bước B3: Bác Sĩ Đề Xuất Hẹn Tái Khám Cho Giai Đoạn 2
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **Thao tác:**
  1. Tại màn hình Bệnh án lâm sàng, bấm nút: **"Đề Xuất Hẹn Tái Khám (Follow-Up)"**.
  2. Chọn Ngày khuyến nghị: Sau 7 ngày.
  3. Dịch vụ tái khám: **Điều Trị Tủy Răng (Nội Nha)** (Giai đoạn 2 của phác đồ).
  4. Bấm **"Gửi Đề Xuất Tái Khám"**.
- **Kết quả mong đợi:** Bản ghi `FollowUpSchedules` được tạo với trạng thái `Proposed`. Hệ thống **KHÔNG tự động tạo ca khám Confirmed** mà chờ bệnh nhân chủ động đặt lịch.
- **Kiểm tra DB:**
  ```sql
  SELECT FollowUpId, RecommendedDate, RecommendedServiceId, Status, ResultingAppointmentId 
  FROM FollowUpSchedules WHERE PatientUserId = [PatientId] ORDER BY FollowUpId DESC;
  ```
  -> `Status = 'Proposed'`, `ResultingAppointmentId IS NULL`.

---

### Bước B4: Bệnh Nhân Đặt Lịch Tái Khám (1-Click Booking)
- **Role:** Patient (`patient1@dentalcare.test`)
- **URL:** `https://localhost:7129/Patient/FollowUps`
- **Menu:** Lịch Tái Khám Khuyến Nghị
- **Thao tác:**
  1. Mở danh sách khuyến nghị tái khám, nhìn thấy mục: "Điều Trị Tủy Răng (Nội Nha) - BS Đặng Văn Hùng".
  2. Bấm nút: **"Đặt Hẹn Theo Khuyến Nghị (1-Click Booking)"**.
  3. Hệ thống tự điền dịch vụ và bác sĩ. Chọn ngày và khung giờ có ca trực khả dụng của bác sĩ.
  4. Bấm **"Xác Nhận Đặt Hẹn"**.
- **Kết quả mong đợi:** Ca khám mới (Appointment B) được tạo. Bản ghi `FollowUpSchedules` tự động cập nhật `ResultingAppointmentId = AppointmentB` và `Status = 'Scheduled'`.
- **Kiểm tra DB:**
  ```sql
  SELECT FollowUpId, Status, ResultingAppointmentId FROM FollowUpSchedules WHERE FollowUpId = [FollowUpId];
  ```
  -> `Status = 'Scheduled'`, `ResultingAppointmentId` lưu đúng mã ca khám mới.

---

### Bước B5: Bác Sĩ Tiếp Tục Phác Đồ Cũ Tại Ca Khám Mới
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **Thao tác:**
  1. Khi ca khám Appointment B diễn ra và được check-in, Bác sĩ mở Bệnh án ca Appointment B.
  2. Tại tab `#tab-plans`, phác đồ điều trị cũ tự động hiển thị đầy đủ lịch sử (Giai đoạn 1 đã xong, Giai đoạn 2 đang chờ).
  3. Bác sĩ thực hiện Giai đoạn 2 và bấm **"Ghi Nhận Thực Hiện Buổi Điều Trị"** cho Giai đoạn 2.
- **Kết quả mong đợi:** Phác đồ đạt tiến độ **100% (Hoàn thành)**.
- **Toàn vẹn Viện phí (Task 3):**
  - Hóa đơn ca A chỉ tính Giai đoạn 1 (350.000 đ).
  - Hóa đơn ca B chỉ tính Giai đoạn 2 (1.200.000 đ).
  - Tuyệt đối không bị tính trùng lặp.

---

## 4. HÀNH TRÌNH DEMO C: CHỈ ĐỊNH & XEM PHIM X-QUANG (IMAGING WORKFLOW)

### Bước C1: Bác Sĩ Khám Chính Chỉ Định Chụp X-Quang
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **URL:** `https://localhost:7129/Dentist/Appointments/Today`
- **Thao tác:**
  1. Tại hàng ca khám của bệnh nhân, bấm nút: **"Chỉ Định X-Quang"**.
  2. Chọn Loại phim: **Panorama (Toàn cảnh hàm răng)**.
  3. Nhập lý do chỉ định: "Chụp khảo sát cuống răng và xương hàm trước khi điều trị tủy".
  4. Bấm **"Xác Nhận Chỉ Định"**.
- **Kết quả mong đợi:** Ca khám hiển thị huy hiệu `Chỉ định X-Quang: Panorama`. Bản ghi `Diagnostics` được tạo trong cơ sở dữ liệu với trạng thái chờ thực hiện.

---

### Bước C2: Chụp & Tải Lên Phim X-Quang Tại Ghế Điều Trị
- **Role:** Dentist (`dentist.hung@dentalcare.test`)
- **URL:** Mở giao diện Bệnh Án Lâm Sàng của ca khám -> Tab **"Chẩn Đoán Hình Ảnh & X-Quang"**.
- **Thao tác:**
  1. Tại khu vực tải lên ảnh phim, chọn tệp ảnh phim X-quang (.jpg, .png dung lượng < 10MB).
  2. Nhập kết luận phim: "Mào xương ổ răng bình thường, chân răng R16 cong nhẹ ở 1/3 chóp".
  3. Bấm nút: **"Tải Lên & Lưu Kết Quả"**.
- **Kết quả mong đợi:**
  - Ảnh phim X-quang hiển thị trực quan ngay trên giao diện bệnh án, có công cụ phóng to để bác sĩ phân tích.
  - Ca khám KHÔNG bị tự động kết thúc. Bác sĩ tiếp tục các thủ thuật điều trị dựa trên kết quả phim.
  - Dịch vụ chụp X-quang được ghi nhận vào danh mục viện phí của ca khám.

---

## 5. HƯỚNG DẪN KIỂM TRA CÁC TÌNH HUỐNG NGOẠI LỆ (EDGE CASES)

### Tình huống 1: Bác Sĩ Có Đơn Nghỉ Phép Được Duyệt (Approved Leave Protection)
- **Kịch bản:** Bác sĩ Đặng Văn Hùng có đơn nghỉ phép được duyệt vào ngày **18/11/2026**.
- **Thao tác kiểm tra:**
  1. Đăng nhập Admin (`admin@dentalcare.test`), truy cập `https://localhost:7129/Admin/DentistSchedule/Index`.
  2. Thử phân ca cho BS. Đặng Văn Hùng vào ngày 18/11/2026.
  3. Hoặc đăng nhập Patient (`patient1@dentalcare.test`), thử đặt lịch hẹn với BS. Đặng Văn Hùng vào ngày 18/11/2026.
- **Kết quả mong đợi:** Hệ thống hiển thị ca trực ngày 18/11 ở trạng thái `Cancelled` và chặn đặt hẹn với thông báo: *"Bác sĩ đã có đơn nghỉ phép được duyệt trong ngày này."*

### Tình huống 2: Chặn Phân Phòng Không Đúng Chức Năng (Imaging Room Guard)
- **Kịch bản:** Lễ tân duyệt lịch hẹn điều trị thông thường nhưng cố tình chọn phòng Chụp X-quang.
- **Thao tác kiểm tra:** Tại màn hình Duyệt Lịch Hẹn, chọn phòng `Phòng Chụp X-quang (R003)` cho dịch vụ Trám Răng hoặc Lấy Cao Răng.
- **Kết quả mong đợi:** Hệ thống từ chối phân công với thông báo: *"Phòng X-quang chỉ dành riêng cho các thủ thuật chẩn đoán hình ảnh."*

### Tình huống 3: Cấp Phát Thuốc Vượt Quá Tồn Kho Khả Dụng
- **Kịch bản:** Bác sĩ kê đơn thuốc với số lượng vượt quá số lượng trong kho.
- **Thao tác kiểm tra:** Thuốc Paracetamol 500mg trong kho còn 90 viên, bác sĩ kê số lượng 150 viên và bấm "Cấp Phát Thuốc".
- **Kết quả mong đợi:** Hệ thống từ chối cấp phát với thông báo lỗi: *"Số lượng thuốc yêu cầu (150) vượt quá số lượng tồn kho khả dụng (90)."*

### Tình huống 4: Thanh Toán Viện Phí Một Phần (Partial Payment)
- **Kịch bản:** Bệnh nhân chưa đủ tiền mặt và xin trả trước một phần viện phí.
- **Thao tác kiểm tra:** Tại màn hình thu viện phí của Lễ tân, nhập số tiền thanh toán thực tế nhỏ hơn tổng tiền hóa đơn (ví dụ: trả 100.000 đ trên hóa đơn 220.000 đ).
- **Kết quả mong đợi:** Hóa đơn ghi nhận trạng thái `PartiallyPaid`, hiển thị số tiền còn nợ (`RemainingAmount: 120.000 đ`). Ca khám chưa được đóng hoàn toàn cho đến khi thu đủ.
