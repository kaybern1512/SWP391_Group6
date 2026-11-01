# KỊCH BẢN KIỂM THỬ ĐẦU CUỐI THỰC TẾ (REAL-WORLD E2E TEST SCENARIOS)
**Tài liệu:** `12_END_TO_END_TEST_SCENARIOS.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Mục đích:** Hướng dẫn kiểm thử thực tế và nghiệm thu phần mềm (Manual & Automated E2E Testing)

---

## BẢNG TÀI KHOẢN DÙNG CHO CÁC KỊCH BẢN TEST
*(Mật khẩu chung cho toàn bộ tài khoản thử nghiệm: `Password123@`)*

- **Quản trị viên (Admin):** `admin@dentalcare.com`
- **Trưởng khoa Răng Tổng Quát (Manager):** `manager@dentalcare.com` (hoặc `manager.general@dentalcare.com`)
- **Bác sĩ phụ trách chính (Dentist 1):** `dentist2@dentalcare.com` (BS. Nguyễn Thị Phương Thảo)
- **Bác sĩ cộng tác (Dentist 2):** `dentist@dentalcare.com` (BS. Trần Văn Hùng)
- **Tiếp tân / Thu ngân (Receptionist):** `receptionist@dentalcare.com`
- **Bệnh nhân mẫu 1 (Patient 1):** `vovandong1512@gmail.com` (Võ Văn Đông)
- **Bệnh nhân mẫu 2 (Patient 2):** `lehoangminh.test@dentalcare.com` (Lê Hoàng Minh)

---

## 13 KỊCH BẢN KIỂM THỬ ĐẦU CUỐI CHI TIẾT

### Kịch bản 1: Đăng ký tài khoản, Xác thực OTP Email và Đăng nhập
- **Mục tiêu:** Kiểm tra luồng cấp tài khoản bệnh nhân mới và bảo mật 2 lớp qua email.
- **Dữ liệu đầu vào:**
  - Họ tên: `Nguyễn Văn Thử Nghiệm`
  - Email: `testuser.dentalcare@gmail.com`
  - Số điện thoại: `0987654321`
  - Mật khẩu: `Password123@`
- **Các bước thực hiện:**
  1. Truy cập `/Account/Register`, điền form và bấm **[Đăng ký]**.
  2. Trình duyệt tự động chuyển hướng đến `/Account/VerifyEmail?email=testuser.dentalcare@gmail.com`.
  3. Lấy mã OTP 6 số từ log hoặc hòm thư, nhập vào ô xác minh và bấm **[Xác nhận]**.
  4. Hệ thống thông báo kích hoạt thành công, tự chuyển sang `/Account/Login`.
  5. Đăng nhập với tài khoản vừa tạo $\rightarrow$ Điều hướng thành công vào `/Patient/Dashboard`.
- **Điểm xác nhận thành công (Success Point):** Bản ghi `UserAccounts` chuyển từ `Unverified` sang `Active`, bản ghi `PatientProfiles` được tạo với mã bệnh nhân tự sinh `PAT-yyyyMMdd-XXXX`.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 2: Bệnh nhân đặt lịch khám trực tuyến với Bác sĩ mong muốn
- **Mục tiêu:** Kiểm tra quy trình đặt hẹn có chỉ định bác sĩ chuyên khoa.
- **Tài khoản thực hiện:** `lehoangminh.test@dentalcare.com`
- **Các bước thực hiện:**
  1. Vào `/Patient/Appointment/Book`.
  2. Chọn Chuyên khoa: `Khoa Răng Tổng Quát` (DepartmentId = 1).
  3. Chọn Dịch vụ: `Khám tổng quát & tư vấn chuyên khoa` (ServiceId = 1).
  4. Chọn Bác sĩ mong muốn: `BS. Nguyễn Thị Phương Thảo` (DentistUserId = 6).
  5. Chọn Ngày khám: Ngày hôm nay hoặc ngày mai; Khung giờ: `09:00`.
  6. Nhập lý do khám: `Khám răng định kỳ & kiểm tra răng hàm dưới`.
  7. Bấm **[Xác nhận đặt lịch]**.
- **Điểm xác nhận thành công:** Màn hình `/Patient/Appointment/Success` hiển thị mã lịch hẹn (dạng `APP-yyyyMMdd-XXXXXX`), trạng thái trong database là `PendingReceptionReview`, `RequestedDentistUserId = 6`, `AssignedDentistUserId = NULL`.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 3: Lễ tân tiếp nhận sơ bộ và chuyển tiếp cho Trưởng khoa
- **Mục tiêu:** Kiểm tra quyền tiếp nhận của nhân viên lễ tân.
- **Tài khoản thực hiện:** `receptionist@dentalcare.com`
- **Các bước thực hiện:**
  1. Đăng nhập lễ tân, vào menu `Lịch hẹn` $\rightarrow$ `Yêu cầu đặt hẹn mới` (`/Receptionist/Appointments/Requests`).
  2. Tìm lịch hẹn vừa tạo ở Kịch bản 2.
  3. Nhập ghi chú: `Đã đối chiếu thông tin hành chính của bệnh nhân hợp lệ.`
  4. Bấm nút **[Chuyển tiếp]**.
- **Điểm xác nhận thành công:** Banner xanh lá thông báo *"Đã chuyển tiếp yêu cầu sang Trưởng khoa thành công"*. Trạng thái ca khám chuyển thành `PendingDepartmentReview`, ghi 1 bản ghi vào `AppointmentStatusHistories`.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 4: Trưởng khoa phê duyệt, gán phòng ghế và cấp số thứ tự
- **Mục tiêu:** Kiểm tra thuật toán phát hiện xung đột và phân bổ tài nguyên của Trưởng khoa.
- **Tài khoản thực hiện:** `manager@dentalcare.com`
- **Các bước thực hiện:**
  1. Vào `/Department/Appointments/Requests`.
  2. Bấm nút **[Xem xét & Phân bổ]** (`/Department/Appointments/Review/{id}`).
  3. Hệ thống hiển thị Bác sĩ Nguyễn Thị Phương Thảo (khả dụng), Phòng 101, Ghế Chair-01.
  4. Bấm nút **[Xác Nhận Lịch Hẹn]**.
- **Điểm xác nhận thành công:**
  - Ca khám chuyển sang trạng thái `Confirmed`.
  - Cấp số thứ tự khám `QueueNumber` (ví dụ: `Q001`).
  - Gán chính thức `AssignedDentistUserId = 6`, `RoomId = 1`, `DentalChairId = 1`.
  - Bệnh nhân nhận được thông báo In-app và email xác nhận.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 5: Trưởng khoa đề xuất thay đổi lịch hẹn & Bệnh nhân phản hồi
- **Mục tiêu:** Kiểm tra luồng thương lượng lịch khám giữa phòng khám và khách hàng.
- **Tài khoản thực hiện:** `manager@dentalcare.com` $\leftrightarrow$ `vovandong1512@gmail.com`
- **Các bước thực hiện:**
  1. Trưởng khoa mở xem xét ca khám, thay vì bấm Confirm, Trưởng khoa chọn khung giờ mới: `14:00` và lý do: `Bác sĩ Thảo có ca phẫu thuật đột xuất buổi sáng, đề xuất chuyển sang buổi chiều`.
  2. Bấm **[Gửi Đề Xuất Thay Đổi]**.
  3. Trạng thái ca khám chuyển thành `AwaitingPatientResponse`.
  4. Đăng nhập tài khoản bệnh nhân `vovandong1512@gmail.com`, vào chi tiết lịch hẹn `/Patient/Appointment/Detail/{id}`.
  5. Hộp thông báo màu vàng hiện ra chi tiết đề xuất. Bệnh nhân bấm nút **[Đồng ý thay đổi]**.
- **Điểm xác nhận thành công:** Trạng thái ca khám chuyển về `PendingDepartmentReview` với giờ mới là 14:00 để Trưởng khoa xác nhận chính thức. Bản ghi `AppointmentChangeProposals` chuyển thành `Accepted`.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 6: Bệnh nhân đến phòng khám & Lễ tân thực hiện Check-in
- **Mục tiêu:** Kiểm tra quy trình đón tiếp tại quầy và đưa bệnh nhân vào phòng chờ.
- **Tài khoản thực hiện:** `receptionist@dentalcare.com`
- **Các bước thực hiện:**
  1. Vào menu `Bàn tiếp đón` $\rightarrow$ `Check-in ca khám hôm nay` (`/Receptionist/Appointments/TodayCheckIn`).
  2. Tìm kiếm bệnh nhân theo tên "Lê Hoàng Minh" hoặc số thứ tự `Q001`.
  3. Bấm nút **[Check-in]**.
- **Điểm xác nhận thành công:**
  - Huy hiệu trạng thái chuyển từ xanh lục `Confirmed` sang xanh dương `CheckedIn`.
  - Cập nhật thời gian `CheckedInAt = UtcNow`.
  - Ca khám xuất hiện ngay trên màn hình ca trực hôm nay của Bác sĩ Phương Thảo.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 7: Bác sĩ bắt đầu khám, cập nhật sơ đồ răng FDI và tải ảnh X-quang
- **Mục tiêu:** Kiểm tra các tính năng chuyên môn lâm sàng và công thái học sơ đồ răng.
- **Tài khoản thực hiện:** `dentist2@dentalcare.com` (BS. Phương Thảo)
- **Các bước thực hiện:**
  1. Vào `/Dentist/Appointments/Today`.
  2. Thấy bệnh nhân Lê Hoàng Minh đã Check-in, bấm nút **[Vào Khám]** $\rightarrow$ Banner đổi thành màu xanh lá đậm: **ĐANG TRONG CA KHÁM**, trạng thái là `InService`.
  3. Bấm **[Chọn Ghế]**, chọn Ghế 01 $\rightarrow$ Ghế được khóa cho ca khám.
  4. Bấm nút **[Mở Hồ Sơ Bệnh Án]** $\rightarrow$ Mở trang `/Dentist/MedicalRecords/Index?appointmentId={id}&isExamining=true`.
  5. Trên sơ đồ 32 răng (1 hàng 16 răng), nhấp chọn răng **R46** $\rightarrow$ Chọn mặt nhai $\rightarrow$ Đánh dấu tình trạng: `Sâu răng (Caries)` (màu đỏ).
  6. Nhấp nút **[Tải Phim X-quang]**, chọn tệp tin ảnh chụp X-quang răng cận chóp, nhập ghi chú: `Sâu men ngà mặt nhai sát tủy` $\rightarrow$ Bấm Tải lên.
  7. Bấm **[Lưu Hồ Sơ Khám Lâm Sàng]**.
- **Điểm xác nhận thành công:** Bản ghi `ClinicalEntries`, `OdontogramEntries` và `ImagingRecords` được lưu vào database. Sơ đồ răng cập nhật trực quan thời gian thực.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 8: Bác sĩ kê đơn thuốc, hoàn tất ca khám và kiểm tra khóa ghế
- **Mục tiêu:** Kiểm tra khâu kết thúc điều trị và cơ chế bảo vệ khóa ghế y khoa.
- **Tài khoản thực hiện:** `dentist2@dentalcare.com`
- **Các bước thực hiện:**
  1. Tại hồ sơ bệnh án, chuyển sang tab Kê đơn thuốc $\rightarrow$ Chọn thuốc `Amoxicillin 500mg` (Số lượng: 10 viên), `Paracetamol 500mg` (Số lượng: 10 viên) $\rightarrow$ Bấm **[Lưu Đơn Thuốc]**.
  2. Quay lại `/Dentist/Appointments/Today`, bấm nút **[Hoàn Thành Ca Khám]**.
- **Điểm xác nhận thành công:**
  - Ca khám chuyển sang trạng thái `Completed`, cập nhật `CompletedAt = UtcNow`.
  - Banner chuyển sang màu sáng: **CA KHÁM ĐÃ HOÀN THÀNH**.
  - Nút `[Chọn/Đổi Ghế]` tự động biến mất và thay thế bằng huy hiệu xám: **`[Đã Khóa Ghế]`**.
  - Nút hoàn thành biến thành huy hiệu xanh: **`Ca khám đã hoàn tất`**.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 9: Lễ tân lập hóa đơn viện phí và Thu tiền qua mã VietQR động
- **Mục tiêu:** Kiểm tra tính chính xác của hóa đơn và tích hợp VietQR Napas 247.
- **Tài khoản thực hiện:** `receptionist@dentalcare.com`
- **Các bước thực hiện:**
  1. Vào `/Receptionist/Billing/Index` $\rightarrow$ Bấm nút **[Tạo hóa đơn]** cho ca khám vừa hoàn thành.
  2. Hệ thống tự nạp các dịch vụ Bác sĩ đã thực hiện kèm đơn giá chuẩn. Tổng tiền hiển thị: `400,000 VNĐ`.
  3. Bấm **[Phát hành hóa đơn]** $\rightarrow$ Hóa đơn `INV-yyyyMMdd-XXX` được tạo ở trạng thái `Unpaid`.
  4. Mở chi tiết hóa đơn `/Receptionist/Billing/Detail/{id}` $\rightarrow$ Bấm tab "Chuyển khoản VietQR".
  5. Quét mã VietQR trên màn hình: Ứng dụng ngân hàng tự động điền Số tài khoản `039123456789`, Ngân hàng `MBBank`, Số tiền `400,000`, Nội dung `THANH TOAN INV-...`.
  6. Lễ tân bấm nút **[Xác nhận thanh toán]** $\rightarrow$ Nhập số tiền thu: `400,000` $\rightarrow$ Bấm Lưu.
  7. Bấm nút **[In Hóa Đơn]** $\rightarrow$ Trang `/Receptionist/Billing/Print/{id}` mở ra chuẩn in A5.
- **Điểm xác nhận thành công:** Hóa đơn chuyển trạng thái sang `Paid`, số dư còn nợ bằng 0.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 10: Bệnh nhân hủy lịch hẹn sát giờ (<12h) và nhận 1 gậy Strike Policy
- **Mục tiêu:** Kiểm tra thuật toán phát hiện vi phạm và kích hoạt chế độ phạt.
- **Tài khoản thực hiện:** `vovandong1512@gmail.com`
- **Các bước thực hiện:**
  1. Bệnh nhân có lịch hẹn đã Confirmed vào lúc `10:00 Hôm nay`.
  2. Bệnh nhân vào `/Patient/Appointment/Detail/{id}`, bấm **[Hủy lịch hẹn]**.
  3. Nhập lý do: `Bận việc đột xuất không thể đến`.
  4. Bấm Xác nhận hủy.
- **Điểm xác nhận thành công:**
  - Hệ thống tính toán thời gian thông báo trước $< 12$ tiếng.
  - Ghi tiền tố `[Hủy sát giờ <12h]` vào `CancellationReason`.
  - Thông báo hiển thị cảnh báo: *"Hủy lịch khám thành công. Lưu ý: Do hủy trong vòng 12h trước giờ hẹn, bạn đã bị tính 1 lần vi phạm (1/3 lần trong 30 ngày)"*.
  - Bảng điều khiển `/Patient/Dashboard` hiển thị thẻ cảnh báo vi phạm.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 11: Ngăn chặn xung đột gán trùng Bác sĩ / Ghế (Conflict Prevention)
- **Mục tiêu:** Kiểm tra tính toàn vẹn khi xếp lịch.
- **Tài khoản thực hiện:** `manager@dentalcare.com`
- **Các bước thực hiện:**
  1. Trưởng khoa mở duyệt ca khám A từ 09:00 đến 10:00 ngày hôm nay, gán cho BS. Phương Thảo tại Ghế 1.
  2. Trưởng khoa mở tiếp ca khám B cũng từ 09:00 đến 10:00 ngày hôm nay, cố tình chọn gán cho BS. Phương Thảo.
  3. Bấm **[Xác nhận]**.
- **Điểm xác nhận thành công:** Backend chặn đứng request, trả về mã lỗi 400 kèm thông điệp: *"Bác sĩ đã có lịch hẹn khác trong khung giờ này."*. Trạng thái ca B vẫn giữ nguyên là `PendingDepartmentReview`.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 12: Thử nghiệm ngăn chặn truy cập trái phép chéo hồ sơ (IDOR Prevention)
- **Mục tiêu:** Kiểm tra bảo mật phân quyền ở tầng Service.
- **Tài khoản thực hiện:** Đăng nhập bệnh nhân Lê Hoàng Minh (`lehoangminh.test@dentalcare.com`).
- **Các bước thực hiện:**
  1. Giả lập kẻ tấn công đổi URL trên thanh địa chỉ sang ID lịch hẹn của bệnh nhân Võ Văn Đông: `/Patient/Appointment/Detail/111`.
  2. Hoặc gửi request HTTP trực tiếp: `POST /api/patient/appointments/111/cancel`.
- **Điểm xác nhận thành công:** Backend so khớp `appointment.PatientUserId != currentUserId` và lập tức trả về mã lỗi `403 Forbidden` kèm thông điệp: *"Bạn không có quyền truy cập hoặc thao tác trên lịch hẹn này."*.
- **Trạng thái:** `COMPLETE`

---

### Kịch bản 13: Bác sĩ tạo giấy chuyển chuyên khoa (Referral) và Trưởng khoa tiếp nhận
- **Mục tiêu:** Kiểm tra quy trình hội chẩn liên khoa.
- **Tài khoản thực hiện:** `dentist2@dentalcare.com` $\rightarrow$ `manager@dentalcare.com`
- **Các bước thực hiện:**
  1. Bác sĩ vào `/Dentist/Referrals/Index`, chọn bệnh nhân, chọn chuyển từ `Khoa Răng Tổng Quát` sang `Khoa Chỉnh Nha`.
  2. Chọn mức độ khẩn cấp: `Urgent`, nhập lý do: `Khớp cắn ngược độ 3 cần chỉnh nha can thiệp sớm`.
  3. Bấm Gửi giấy chuyển khoa.
  4. Đăng nhập Trưởng khoa Chỉnh nha, vào `/Department/Referrals/Index`.
  5. Thấy giấy chuyển khoa ở trạng thái `Pending`, bấm **[Tiếp nhận]**.
- **Điểm xác nhận thành công:** Bản ghi `DepartmentReferrals` chuyển sang `Accepted`, hệ thống ghi nhận hồ sơ chuyển viện hoàn chỉnh.
- **Trạng thái:** `COMPLETE`
