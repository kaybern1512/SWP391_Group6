# BẢNG KIỂM THỬ THỦ CÔNG GIAO DIỆN NGƯỜI DÙNG: CHỨC NĂNG LỊCH TÁI KHÁM (FOLLOW-UP MANUAL UI TEST CHECKLIST)
**Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)  
**Workspace:** `D:\SWP\SWP391_Group6`  
**Môi trường thử nghiệm:**  
- Frontend URL: `http://localhost:5049`  
- Backend API URL: `http://localhost:5350`  
- Database: SQL Server `.\SQL2025_DUC` (`DentalClinic_FollowUp_QA`)  
**Mục đích:** Hướng dẫn kiểm thử viên (Manual QA Tester) xác minh các thao tác giao diện người dùng (GUI), tương tác Modal, Toast notification và kiểm tra hiển thị an toàn không bị lỗi injection trên trình duyệt.

---

## 1. TÀI KHOẢN ĐĂNG NHẬP THỬ NGHIỆM (TEST ACCOUNTS)

| Vai trò | Email đăng nhập | Mật khẩu mặc định | Đường dẫn chính |
|---|---|---|---|
| **Bệnh nhân (Patient)** | `lehoangminh.test@dentalcare.com` | `Password123@` | `/Patient/FollowUps` |
| **Bác sĩ (Dentist)** | `dentist@dentalcare.com` | `Password123@` | `/Dentist/FollowUps` |
| **Lễ tân (Receptionist)** | `receptionist@dentalcare.com` | `Password123@` | `/Receptionist/FollowUps` |
| **Trưởng khoa (Manager)** | `manager@dentalcare.com` | `Password123@` | `/Dentist/Referrals` |

---

## 2. KỊCH BẢN KIỂM THỬ THỦ CÔNG DÀNH CHO BỆNH NHÂN (PATIENT SCENARIOS)

### Kịch bản UI-P01: Xem danh sách lịch tái khám của bệnh nhân
- **Đường dẫn:** `http://localhost:5049/Patient/FollowUps`
- **Các bước thực hiện:**
  1. Đăng nhập tài khoản Bệnh nhân: `lehoangminh.test@dentalcare.com` / `Password123@`.
  2. Truy cập menu **Lịch tái khám của tôi**.
- **Kết quả mong đợi:**
  - [ ] Bảng danh sách hiển thị đầy đủ thông tin: Bác sĩ chỉ định, Chuyên khoa, Ngày hẹn dự kiến, Lý do tái khám.
  - [ ] Badge trạng thái hiển thị màu sắc tương ứng (`Chờ khám`, `Đã xác nhận`, `Hoàn thành`, `Đã hủy`).
  - [ ] Đối với lịch có chỉ định đặc biệt chứa ký tự lạ (như `<`, `>`, `&`, dấu nháy), tên và lý do tái khám được hiển thị an toàn dạng văn bản thuần, không gây vỡ giao diện hay thực thi script.

### Kịch bản UI-P02: Mở Modal đề xuất đổi lịch hẹn (Patient Proposal Modal)
- **Đường dẫn:** `http://localhost:5049/Patient/FollowUps`
- **Các bước thực hiện:**
  1. Tìm một cuộc hẹn đang có trạng thái `Confirmed` hoặc `Pending`.
  2. Bấm nút **"Đề xuất đổi lịch"**.
- **Kết quả mong đợi:**
  - [ ] Modal mở mượt mà, không xuất hiện lỗi JavaScript trong Console (F12).
  - [ ] Tiêu đề Modal hiển thị rõ mã cuộc hẹn và bác sĩ điều trị.
  - [ ] Ô chọn ngày/giờ chỉ cho phép chọn thời gian trong tương lai.
  - [ ] Nhập lý do đổi lịch: `Tôi có chuyến công tác đột xuất vào ngày này`.
  - [ ] Bấm nút **"Gửi yêu cầu"**.
  - [ ] Toast thông báo màu xanh hiển thị: "Gửi đề xuất đổi lịch thành công".
  - [ ] Modal tự động đóng và danh sách cập nhật trạng thái mới.

### Kịch bản UI-P03: Rút lại đề xuất đổi lịch (Withdraw Proposal)
- **Đường dẫn:** `http://localhost:5049/Patient/FollowUps`
- **Các bước thực hiện:**
  1. Chuyển sang tab **"Yêu cầu đổi lịch đang chờ"**.
  2. Bấm nút **"Rút lại yêu cầu"** trên một đề xuất đang chờ duyệt.
  3. Xác nhận trên hộp thoại xác nhận.
- **Kết quả mong đợi:**
  - [ ] Toast thông báo rút yêu cầu thành công.
  - [ ] Trạng thái đề xuất đổi thành `Đã rút lại` hoặc biến mất khỏi danh sách chờ.

---

## 3. KỊCH BẢN KIỂM THỬ THỦ CÔNG DÀNH CHO BÁC SĨ (DENTIST SCENARIOS)

### Kịch bản UI-D01: Bác sĩ xem danh sách bệnh nhân tái khám và đề xuất đổi lịch
- **Đường dẫn:** `http://localhost:5049/Dentist/FollowUps`
- **Các bước thực hiện:**
  1. Đăng nhập tài khoản Bác sĩ: `dentist@dentalcare.com` / `Password123@`.
  2. Mở trang quản lý tái khám của bác sĩ.
- **Kết quả mong đợi:**
  - [ ] Hiển thị danh sách các bệnh nhân do bác sĩ phụ trách.
  - [ ] Tab **"Yêu cầu đổi lịch của bệnh nhân"** hiển thị badge đếm số lượng yêu cầu đang chờ xử lý.

### Kịch bản UI-D02: Duyệt đề xuất đổi lịch (Approve Proposal Modal)
- **Đường dẫn:** `http://localhost:5049/Dentist/FollowUps`
- **Các bước thực hiện:**
  1. Tại tab yêu cầu đổi lịch, bấm nút **"Duyệt"** (màu xanh lá) trên một yêu cầu.
  2. Quan sát Modal phê duyệt:
- **Kết quả mong đợi:**
  - [ ] Dữ liệu ngày đề xuất mới được điền tự động vào form thông qua `dataset` (không dùng `eval` hay nối chuỗi inline).
  - [ ] Bác sĩ có thể nhập ghi chú duyệt: `Đã thống nhất dời sang tuần sau`.
  - [ ] Bấm **"Xác nhận duyệt"**.
  - [ ] Toast hiển thị "Đã phê duyệt đề xuất dời lịch thành công".
  - [ ] Lịch hẹn trên hệ thống được cập nhật sang giờ mới.

### Kịch bản UI-D03: Từ chối đề xuất đổi lịch (Reject Proposal Modal)
- **Đường dẫn:** `http://localhost:5049/Dentist/FollowUps`
- **Các bước thực hiện:**
  1. Bấm nút **"Từ chối"** (màu đỏ) trên một yêu cầu đổi lịch.
  2. Nhập lý do từ chối: `Bác sĩ bận lịch phẫu thuật trong khung giờ này, vui lòng chọn giờ khác`.
  3. Bấm **"Xác nhận từ chối"**.
- **Kết quả mong đợi:**
  - [ ] Nếu để trống lý do từ chối, form cảnh báo yêu cầu nhập lý do.
  - [ ] Khi nhập đủ lý do và xác nhận: Toast thông báo từ chối thành công, đề xuất chuyển trạng thái `Rejected`.

### Kịch bản UI-D04: Kiểm tra chống XSS trên trang Chuyển khoa / Hội chẩn (Referrals)
- **Đường dẫn:** `http://localhost:5049/Dentist/Referrals`
- **Các bước thực hiện:**
  1. Mở trang Danh sách chuyển khoa / hội chẩn.
  2. Bấm các nút **"Phê duyệt"** và **"Từ chối"** trên các yêu cầu có tên bệnh nhân hoặc ghi chú đặc biệt.
- **Kết quả mong đợi:**
  - [ ] Modal mở ra với nội dung hiển thị qua `textContent`, hoàn toàn không xuất hiện alert box hay lỗi console.
  - [ ] Nút bấm sử dụng `data-*` attributes (`data-referral-id`, `data-patient-name`), không dùng inline `onclick="openModal('...')"` với chuỗi động.

---

## 4. KỊCH BẢN KIỂM THỬ THỦ CÔNG DÀNH CHO LỄ TÂN (RECEPTIONIST SCENARIOS)

### Kịch bản UI-R01: Xem danh sách theo dõi tái khám toàn phòng khám
- **Đường dẫn:** `http://localhost:5049/Receptionist/FollowUps`
- **Các bước thực hiện:**
  1. Đăng nhập Lễ tân: `receptionist@dentalcare.com` / `Password123@`.
  2. Truy cập màn hình **Chăm sóc sau điều trị / Theo dõi tái khám**.
- **Kết quả mong đợi:**
  - [ ] Danh sách hiển thị đầy đủ các cột: Mã BN, Họ tên bệnh nhân, Số điện thoại liên hệ, Bác sĩ phụ trách, Ngày dự kiến, Trạng thái.
  - [ ] Ô tìm kiếm và bộ lọc theo trạng thái (`Tất cả`, `Chờ xác nhận`, `Đã xác nhận`, `Đã hoàn thành`) hoạt động mượt mà.

### Kịch bản UI-R02: Ghi nhận kết quả cuộc gọi chăm sóc / nhắc hẹn (Call Modal)
- **Đường dẫn:** `http://localhost:5049/Receptionist/FollowUps`
- **Các bước thực hiện:**
  1. Bấm nút **"Ghi nhận cuộc gọi"** hoặc biểu tượng điện thoại trên một hàng bệnh nhân.
  2. Quan sát Modal cuộc gọi:
- **Kết quả mong đợi:**
  - [ ] Modal hiển thị chuẩn xác Tên bệnh nhân và Số điện thoại liên hệ được trích xuất từ `data-patient-name` và `data-phone-number`.
  - [ ] Dropdown kết quả cuộc gọi có các tùy chọn: `Đã liên lạc được`, `Máy bận / Không nghe máy`, `Số máy không đúng`.
  - [ ] Nhập nội dung ghi chú cuộc gọi: `Bệnh nhân hẹn chiều thứ 6 sẽ đến đúng giờ`.
  - [ ] Bấm **"Lưu kết quả cuộc gọi"**.
  - [ ] Toast thông báo "Ghi nhận kết quả cuộc gọi thành công".
  - [ ] Lịch sử cuộc gọi được cập nhật vào dòng trạng thái.

### Kịch bản UI-R03: Điều hướng Đổi lịch hẹn sang Quản lý Cuộc hẹn
- **Đường dẫn:** `http://localhost:5049/Receptionist/FollowUps`
- **Các bước thực hiện:**
  1. Trên một lịch tái khám đã có cuộc hẹn chính thức (`AppointmentCode` hiển thị), bấm nút **"Đổi lịch hẹn"** (Reschedule).
- **Kết quả mong đợi:**
  - [ ] Trình duyệt điều hướng chính xác sang URL:  
    `/Receptionist/Appointments?search={AppointmentCode}`
  - [ ] Trang Quản lý Cuộc hẹn tự động điền mã cuộc hẹn vào ô tìm kiếm và lọc ra đúng cuộc hẹn đó.
  - [ ] Lễ tân có thể thao tác chọn lại ghế và khung giờ trên giao diện điều phối cuộc hẹn chính thức mà không gây xung đột ghế.

### Kịch bản UI-R04: Thao tác Xác nhận và Hoàn tất lịch tái khám
- **Đường dẫn:** `http://localhost:5049/Receptionist/FollowUps`
- **Các bước thực hiện:**
  1. Trên một lịch có trạng thái `Pending`, bấm nút **"Xác nhận"**.
  2. Trên một lịch đã hoàn thành điều trị, bấm nút **"Hoàn tất"**.
- **Kết quả mong đợi:**
  - [ ] Nút **"Xác nhận"** gửi request PATCH và cập nhật badge sang `Confirmed` ngay trên UI.
  - [ ] Nếu cuộc hẹn liên kết chưa hoàn thành điều trị, nút **"Hoàn tất"** sẽ hiển thị cảnh báo lỗi hợp lệ, không cho phép đóng lịch trước khi bác sĩ kết thúc phiên khám.

---

## 5. KỊCH BẢN KIỂM THỬ THỦ CÔNG DÀNH CHO TRƯỞNG KHOA (DEPARTMENT MANAGER)

### Kịch bản UI-M01: Duyệt yêu cầu chuyển khoa và hội chẩn chuyên môn
- **Đường dẫn:** `http://localhost:5049/Dentist/Referrals` (hoặc `/Manager/Referrals`)
- **Các bước thực hiện:**
  1. Đăng nhập tài khoản Trưởng khoa: `manager@dentalcare.com` / `Password123@`.
  2. Mở danh sách các ca chuyển tuyến / hội chẩn thuộc Khoa Phẫu thuật miệng & Implant (Khoa 3).
- **Kết quả mong đợi:**
  - [ ] Trưởng khoa nhìn thấy các ca yêu cầu chuyển đến khoa mình quản lý.
  - [ ] Bấm nút **"Tiếp nhận ca"** hoặc **"Chỉ định bác sĩ"**.
  - [ ] Modal chọn bác sĩ điều trị trong khoa mở ra và danh sách bác sĩ chỉ bao gồm các bác sĩ thuộc Khoa 3.
  - [ ] Bấm **"Xác nhận tiếp nhận"** -> Toast hiển thị thành công.

---

## 6. DANH MỤC KIỂM TRA PHÒNG NGỪA HỒI BIẾN (REGRESSION VERIFICATION CHECKLIST)

- [ ] **Modal JS Injection:** Không có bất kỳ đoạn mã inline `<script>` nào được kích hoạt khi mở các modal (Proposal Modal, Call Modal, Approval Modal, Rejection Modal).
- [ ] **Dữ liệu bệnh nhân trong DOM:** Các trường `PatientName`, `PhoneNumber`, `CallNote` hoàn toàn được gán qua `element.textContent = ...` và `input.value = ...` (Tuyệt đối không dùng `innerHTML`).
- [ ] **Bảo vệ quyền sửa ghi chú:** Giao diện Bệnh nhân hoàn toàn không có ô nhập liệu hay nút bấm cho phép chỉnh sửa trường `Reason` y khoa của bác sĩ.
- [ ] **Tính nhất quán khi Hủy:** Bệnh nhân không thể bấm nút hủy trực tiếp trên giao diện nếu lịch tái khám đã được xếp lịch hẹn chính thức mà không qua quy trình hủy cuộc hẹn chuẩn.

