# TÀI LIỆU HƯỚNG DẪN BẢO VỆ ĐỒ ÁN: CHU TRÌNH BỆNH NHÂN ĐẾN THANH TOÁN
## PATIENT-TO-PAYMENT COMPLETE END-TO-END WORKFLOW GUIDE (SWP391)

Tài liệu này được biên soạn dành riêng cho nhóm sinh viên bảo vệ đồ án tốt nghiệp SWP391 (Hệ thống Quản lý Phòng khám Đa Chuyên khoa DentalCare). Tài liệu giải thích chi tiết từng bước nghiệp vụ, cơ chế lưu trữ dữ liệu, cách thức phân quyền giữa 5 vai trò và các câu hỏi trọng tâm thường gặp từ Hội đồng chấm thi.

---

## 1. KHÁI QUÁT VỀ ĐỊNH DANH BỆNH NHÂN (PATIENT IDENTIFICATION)

Hệ thống DentalCare phân biệt rõ hai luồng tiếp nhận bệnh nhân ban đầu:

### 1.1. Bệnh Nhân Mới (First-Time Patient)
1. **Trực tuyến (Online Self-Registration):**
   - Bệnh nhân đăng ký tài khoản trên cổng thông tin, xác thực qua email/OTP.
   - Tài khoản `UserAccounts` có `Status = AccountStatus.Active`.
   - Hệ thống tự động tạo bản ghi `PatientProfiles` với mã định danh duy nhất định dạng: `PAT-yyyyMM-xxxx` (Ví dụ: `PAT-202610-0001`).
   - Bệnh nhân có đầy đủ quyền tự đặt lịch khám trực tuyến, xem lịch sử khám và hóa đơn.
2. **Khách vãng lai tại quầy (Walk-In Administrative Registration):**
   - Bệnh nhân đến trực tiếp phòng khám mà chưa có tài khoản.
   - Lễ tân nhập họ tên, số điện thoại, ngày sinh, địa chỉ tại giao diện Tiếp nhận vãng lai.
   - Hệ thống tạo bản ghi hành chính: `PasswordHash = NULL`, `Status = AccountStatus.Unverified`, mã bệnh nhân tự tăng.
   - Bệnh nhân được nhân viên Lễ tân phục vụ toàn bộ chu trình khám, điều trị và thanh toán tại quầy. *(Lưu ý bảo vệ: Phiên bản này chưa hỗ trợ luồng bệnh nhân tự kích hoạt trực tuyến qua OTP; tính năng này được định hướng phát triển ở giai đoạn 2).*

### 1.2. Bệnh Nhân Tái Khám (Returning Patient / Follow-Up Visit)
- Bệnh nhân đã từng khám tại cơ sở y tế khi đặt lịch lần 2, lần 3... hệ thống nhận diện qua `UserId` hoặc số điện thoại.
- **Tính toàn vẹn dữ liệu:** Hệ thống tuyệt đối **không tạo bản ghi bệnh nhân trùng lặp**. Mọi ca khám mới (`Appointments`) đều trỏ về cùng một `PatientUserId`.
- Bác sĩ khi tiếp nhận bệnh nhân tái khám sẽ mở toàn bộ lịch sử bệnh án dài hạn: chẩn đoán cũ, sơ đồ răng Odontogram qua các lần khám, hình ảnh X-Quang và đơn thuốc cũ để bảo đảm tính liên tục trong điều trị.

---

## 2. TIẾN TRÌNH 7 BƯỚC TỪ TIẾP NHẬN ĐẾN THANH TOÁN (PATIENT-TO-PAYMENT JOURNEY)

### Bước 1: Đặt Lịch Khám (Appointment Booking)
- **Ai thực hiện:** Bệnh nhân (đặt online) hoặc Lễ tân (đặt tại quầy).
- **Màn hình:** `/Patient/Appointment/Book` hoặc `/Receptionist/Appointments/Create`.
- **Dữ liệu nhập:** Chọn Chuyên khoa (Department), Dịch vụ yêu cầu (Requested Service), Bác sĩ mong muốn (tùy chọn), Ngày và Giờ khám, Lý do khám.
- **Trạng thái khởi tạo:**
  - Nếu bệnh nhân tự đặt: `PendingReceptionReview` (Chờ Lễ tân duyệt).
  - Nếu Lễ tân đặt tại quầy: `PendingDepartmentReview` (Bỏ qua khâu lễ tân, chuyển thẳng sang Trưởng khoa).
- **Cơ chế lưu trữ:** Bảng `Appointments`. Cột `QueueNumber`, `RoomId`, `DentalChairId`, `AssignedDentistUserId` lúc này đều là `NULL`.
- *Quy tắc bảo vệ:* Ở bước này **chưa có tài nguyên nào bị chiếm dụng**, đây chỉ là đề xuất nhu cầu (tentative booking) của bệnh nhân.

---

### Bước 2: Lễ Tân Tiếp Nhận & Chuyển Khoa (Receptionist Review & Forward)
- **Ai thực hiện:** Nhân viên Lễ tân (Receptionist).
- **Màn hình:** `/Receptionist/Appointments/Requests`.
- **Thao tác:** Lễ tân kiểm tra thông tin, sự phù hợp của chuyên khoa và bấm nút **"Chuyển khoa"** (`ReceptionistForwardAsync`).
- **Chuyển đổi trạng thái:** `PendingReceptionReview` $\rightarrow$ `PendingDepartmentReview`.
- **Nếu thông tin sai lệch:** Lễ tân bấm **"Từ chối"** (`ReceptionistRejectAsync`), nhập lý do. Trạng thái chuyển sang `Rejected` và hệ thống gửi thông báo cho bệnh nhân.

---

### Bước 3: Trưởng Khoa Phân Bổ Nguồn Lực & Xác Nhận (Manager Assignment & Confirmation)
- **Ai thực hiện:** Trưởng Khoa (DepartmentManager).
- **Màn hình:** `/Department/Appointments/Confirm/{id}`.
- **Thao tác:** Trưởng khoa chỉ định: Bác sĩ điều trị (`AssignedDentistUserId`), Phòng khám (`RoomId`) và Ghế nha khoa (`DentalChairId`).
- **Cơ chế kiểm soát đồng thời cực kỳ chặt chẽ (Điểm sáng kiến trúc):**
  1. Sử dụng ứng dụng khóa **`sp_getapplock`** trên SQL Server theo thứ tự chuẩn hóa (Canonical Ordering) để triệt tiêu hoàn toàn rủi ro Deadlock.
  2. **Kiểm tra trùng lịch Bác sĩ:** Quét toàn bộ các chuyên khoa trên toàn viện, ngăn chặn Bác sĩ bị xếp 2 ca cùng một khung giờ.
  3. **Phân biệt Phòng Độc Quyền và Phòng Đa Ghế:**
     - Phòng Đa Ghế (`IsExclusive = 0`, ví dụ Phòng 101, 102, 301, 401): Cho phép nhiều Bác sĩ cùng khám tại các Ghế khác nhau trong cùng một phòng.
     - Phòng Độc Quyền (`IsExclusive = 1`, ví dụ Phòng phẫu thuật 201, 202, 302, 402): Chỉ cho phép 1 ca khám duy nhất trong phòng tại một thời điểm.
  4. **Cấp số thứ tự khám (`QueueNumber`):** Cấp số tự tăng duy nhất trong ngày theo chuyên khoa (Q001, Q002, Q003...) và được bảo vệ bởi chỉ mục duy nhất SQL Server `UQ_Appointments_Department_Date_QueueNumber`.
- **Chuyển đổi trạng thái:** `PendingDepartmentReview` $\rightarrow$ `Confirmed`.

---

### Bước 4: Đón Tiếp & Check-in (Patient Check-in)
- **Ai thực hiện:** Nhân viên Lễ tân (Receptionist).
- **Màn hình:** `/Receptionist/Appointments/Today`.
- **Thao tác:** Khi bệnh nhân có mặt tại phòng khám vào ngày hẹn, Lễ tân đối chiếu thông tin và bấm **"Xác nhận Check-in"** (`ReceptionistCheckInAsync`).
- **Chuyển đổi trạng thái:** `Confirmed` $\rightarrow$ `CheckedIn`. Hệ thống ghi nhận mốc thời gian bệnh nhân có mặt vào trường `CheckedInAt`.
- *Nếu bệnh nhân không đến:* Sau khi hết ca khám, Lễ tân bấm **"Đánh dấu vắng mặt"** (`ReceptionistMarkNoShowAsync`). Lịch hẹn chuyển sang `NoShow`, bệnh nhân bị tính 1 gậy vi phạm theo Chính sách hủy lịch/vắng mặt (Strike Policy). Tích lũy đủ 3 gậy trong 30 ngày sẽ bị hệ thống tạm khóa quyền đặt lịch trực tuyến 30 ngày.

---

### Bước 5: Bác Sĩ Khám Lâm Sàng, Ghi Bệnh Án & Kê Đơn Thuốc (Clinical Examination)
- **Ai thực hiện:** Bác sĩ điều trị (Dentist).
- **Màn hình:** `/Dentist/Appointments/Today`, `/Dentist/MedicalRecords/CreateEntry`, `/Dentist/Prescriptions/Create`.
- **Thao tác:**
  1. Bác sĩ thấy bệnh nhân đã check-in, bấm **"Tiếp nhận vào ghế"** $\rightarrow$ Trạng thái chuyển sang `InService` (Đang điều trị).
  2. Bác sĩ thực hiện thăm khám và lưu **Bệnh án điện tử (`ClinicalEntries`)**:
     - Ghi nhận triệu chứng, tiền sử dị ứng, chẩn đoán bệnh lý.
     - Cập nhật sơ đồ răng trực quan **Odontogram** (chọn răng số 1 - 32, tình trạng sâu răng, trám răng, mất răng).
     - Đính kèm hình ảnh phim X-Quang / CT (`ImagingRecords`).
  3. Bác sĩ kê **Đơn thuốc (`Prescriptions` & `PrescriptionItems`)**:
     - Nhập tên thuốc, liều lượng, tần suất uống, thời gian điều trị và hướng dẫn sử dụng.
  4. Nếu ca điều trị kéo dài qua nhiều buổi (như niềng răng, cấy ghép Implant):
     - Bác sĩ lập **Kế hoạch điều trị (`TreatmentPlans`)** chia thành nhiều giai đoạn (`TreatmentPlanItems`).
     - Ghi nhận tiến trình buổi khám (`TreatmentSessions`) và các dịch vụ thực tế đã thực hiện (`PerformedServices`).
  5. Bác sĩ hoàn thành thủ thuật và bấm **"Hoàn tất ca khám"** (`DentistCompleteAppointmentAsync`) $\rightarrow$ Trạng thái chuyển sang `Completed`.

---

### Bước 6: Lập Hóa Đơn Viện Phí & Chiết Khấu (Invoice Generation & Discount)
- **Ai thực hiện:** Nhân viên Lễ tân (Receptionist).
- **Màn hình:** `/Receptionist/Billing/Create?appointmentId={id}`.
- **Thao tác:**
  1. Lễ tân mở màn hình lập hóa đơn cho ca khám vừa hoàn thành.
  2. **Tự động nạp chi phí:** Hệ thống tự động kéo các dịch vụ Bác sĩ đã thực hiện trong buổi khám (`PerformedServices`), nếu không có sẽ nạp dịch vụ bệnh nhân đăng ký ban đầu (`RequestedService`).
  3. Lễ tân có thể chọn thêm các dịch vụ bổ sung từ bảng giá phòng khám hoặc thêm dòng chi phí tùy chỉnh.
  4. **Áp dụng Chiết khấu (Discount):**
     - Nhập số tiền chiết khấu trực tiếp vào ô `Giảm giá / Chiết khấu (đ)`.
     - Sử dụng bộ phím tắt gợi ý mã voucher tiện lợi: nhập `GIAM10` (giảm 10%), `GIAM20` (giảm 20%), `GIAM50K`, `GIAM100K`.
     - Nhập thuế VAT (nếu có).
  5. Bấm **"Khởi tạo hóa đơn"** $\rightarrow$ Tạo bản ghi `Invoices` với mã tự sinh `INV-yyyyMMdd-xxxx`, trạng thái ban đầu là `Unpaid`.
- *Nguyên tắc an toàn:* Một ca khám chỉ có thể tạo duy nhất một hóa đơn (ràng buộc Unique Constraint trong CSDL ngăn ngừa bấm trùng lặp).

---

### Bước 7: Thu Ngân & Ghi Nhận Thanh Toán (Payment Processing)
- **Ai thực hiện:** Nhân viên Lễ tân (Receptionist).
- **Màn hình:** `/Receptionist/Billing/Detail/{invoiceId}`.
- **Thao tác:**
  1. Lễ tân chọn hình thức thu tiền: **Tiền mặt (Cash)**, **Chuyển khoản (BankTransfer)** hoặc **Thẻ / POS (CreditCard)**.
  2. Nhập số tiền thu và mã giao dịch đối chiếu (nếu chuyển khoản).
  3. Bấm **"Xác nhận thanh toán"** (`BillingService.ProcessPaymentAsync`).
  4. **Xử lý trạng thái tài chính:**
     - Nếu thanh toán đủ 100%: Hóa đơn chuyển sang `Paid`.
     - Nếu thanh toán một phần (đặt cọc): Hóa đơn chuyển sang `PartiallyPaid`, hiển thị dư nợ còn lại và cho phép thu tiếp các đợt sau.
     - Hệ thống ngăn chặn thu vượt quá số tiền còn lại phải thanh toán (Overpayment check).
  5. **Bệnh nhân:** Đăng nhập vào cổng bệnh nhân `/Patient/Invoices`, xem và tải phiếu thu/hóa đơn điện tử có mã xác nhận chi tiết.

---

## 3. CƠ CHẾ DỜI LỊCH (RESCHEDULING) & HỦY LỊCH (CANCELLATION)

### 3.1. Dời Lịch Khám (Rescheduling)
- Khi Bệnh nhân yêu cầu dời lịch: Hệ thống áp dụng cơ chế **Hủy lịch cũ + Tạo lịch mới thay thế**.
- Lịch cũ chuyển trạng thái `Cancelled`, giải phóng Bác sĩ và ghế khám để người khác có thể sử dụng.
- Lịch mới được tạo ở trạng thái `PendingReceptionReview` với mã hẹn mới, đồng thời ghi chú truy vết chéo mã hẹn cũ trong lịch sử trạng thái (`AppointmentStatusHistory`), đảm bảo không thất lạc thông tin bệnh nhân.

### 3.2. Hủy Lịch Khám (Cancellation)
- Bệnh nhân chỉ được phép hủy lịch khi ca khám ở trạng thái chờ duyệt hoặc đã xác nhận (`Confirmed`).
- **Tuyệt đối cấm hủy** khi ca khám đã bắt đầu khám (`InService`) hoặc đã kết thúc (`Completed`).
- Nếu hủy trước 12 tiếng: Hủy bình thường, không phạt.
- Nếu hủy sát giờ (<12 tiếng): Tính 1 gậy vi phạm (Strike) cảnh cáo.

---

## 4. MA TRẬN PHÂN QUYỀN TRUY CẬP GIỮA 5 VAI TRÒ (RBAC MATRIX)

| Nghiệp Vụ / Tài Nguyên | Bệnh Nhân (Patient) | Lễ Tân (Receptionist) | Trưởng Khoa (DeptManager) | Bác Sĩ (Dentist) | Quản Trị Viên (Admin) |
|:---|:---:|:---:|:---:|:---:|:---:|
| **Đặt lịch trực tuyến** | Toàn quyền (chính mình) | Không (Dùng màn hình quầy) | Xem danh sách | Không | Xem toàn bộ |
| **Tiếp nhận & Check-in tại quầy** | Không có quyền | Toàn quyền thao tác | Xem | Xem ca của mình | Xem toàn bộ |
| **Duyệt phòng, ghế, phân bác sĩ** | Không có quyền | Không có quyền | Toàn quyền trong khoa | Không có quyền | Toàn quyền |
| **Ghi bệnh án & Đơn thuốc** | Không có quyền | Không có quyền | Xem | Toàn quyền điều trị | Xem báo cáo |
| **Lập hóa đơn & Thu tiền** | Chỉ xem hóa đơn của mình | Toàn quyền lập & thu tiền | Xem báo cáo tài chính | Không có quyền | Toàn quyền quản trị |
| **Quản trị người dùng & Dịch vụ** | Không có quyền | Bị chặn 403 Forbidden | Xem danh mục | Không có quyền | Toàn quyền thêm/sửa/xóa |

---

## 5. BỘ CÂU HỎI PHẢN BIỆN PHỤC VỤ BẢO VỆ ĐỒ ÁN (DEFENSE FAQ)

### Câu hỏi 1: Hệ thống giải quyết bài toán chống trùng lịch Bác sĩ và Ghế nha khoa như thế nào khi có nhiều người cùng thao tác một lúc?
> **Trả lời:** Hệ thống sử dụng cơ chế khóa tầng sâu **`sp_getapplock`** trực tiếp trên SQL Server với phạm vi Transaction. Các khóa tài nguyên (`Lock_Dentist`, `Lock_Chair`, `Lock_Room`, `Lock_DeptQueue`) được sắp xếp theo thứ tự bảng chữ cái chuẩn hóa (Canonical Ordering) trước khi yêu cầu khóa, loại trừ triệt để nguy cơ Deadlock. Trong vùng an toàn của khóa, hệ thống kiểm tra trùng lịch Bác sĩ trên phạm vi toàn viện và kiểm tra ghế/phòng độc quyền trước khi commit dữ liệu.

### Câu hỏi 2: Tại sao số thứ tự khám `QueueNumber` không được cấp lúc bệnh nhân check-in tại quầy lễ tân mà lại cấp lúc Trưởng khoa duyệt?
> **Trả lời:** Trong mô hình phòng khám nha khoa đa chuyên khoa, việc phân bổ nguồn lực (Bác sĩ, Phòng tiểu phẫu, Ghế máy) quyết định trực tiếp đến thứ tự phục vụ trong ngày. Khi Trưởng khoa xác nhận ca khám, số thứ tự `QueueNumber` được cấp theo định dạng `Q001`, `Q002`... gắn liền với khung giờ và chuyên khoa khám. Thao tác Check-in của Lễ tân chỉ nhằm xác thực bệnh nhân đã có mặt thực tế tại phòng khám (`CheckedInAt`), kích hoạt ca khám vào danh sách sẵn sàng của Bác sĩ chứ không làm xáo trộn số thứ tự đã ấn định.

### Câu hỏi 3: Nếu một bệnh nhân thực hiện thủ thuật nha khoa kéo dài nhiều tháng (như chỉnh nha), hệ thống quản lý chi phí và buổi khám ra sao?
> **Trả lời:** Bác sĩ sẽ khởi tạo một **Kế hoạch điều trị dài hạn (`TreatmentPlans`)** gồm nhiều giai đoạn (`TreatmentPlanItems`). Mỗi lần bệnh nhân đến phòng khám theo lịch hẹn, Bác sĩ ghi nhận buổi điều trị (`TreatmentSessions`) và các dịch vụ thực tế đã thực hiện (`PerformedServices`). Khi ca khám hoàn tất, Lễ tân chỉ cần mở màn hình Billing, hệ thống sẽ tự động tổng hợp các dịch vụ đã thực hiện trong buổi khám đó sang hóa đơn viện phí để thu tiền, hỗ trợ cả hình thức thanh toán từng phần (`PartiallyPaid`) theo tiến độ điều trị.

### Câu hỏi 4: Tại sao hệ thống không lưu mã Coupon vào một bảng riêng trong CSDL?
> **Trả lời:** Trong phạm vi nghiệp vụ cốt lõi của đồ án phòng khám nha khoa, ưu tiên hàng đầu là tính toàn vẹn của dữ liệu lâm sàng và thanh toán chính xác. Hệ thống hỗ trợ nghiệp vụ chiết khấu thực tế bằng trường `DiscountAmount` trên giao diện lập hóa đơn (do Lễ tân nhập theo chính sách ưu đãi của phòng khám hoặc nhập các mã giảm giá tiện ích được định nghĩa sẵn). Hệ thống quản trị chiến dịch khuyến mại chuyên sâu (Coupon Management System với bảng lưu trữ, thời hạn và số lượt dùng toàn viện) là một phân hệ mở rộng độc lập, được định hướng phát triển ở phiên bản thương mại tiếp theo.