# PHÂN TÍCH QUY TRÌNH NGHIỆP VỤ ĐẦU CUỐI (END-TO-END BUSINESS FLOWS)
**Tài liệu:** `04_BUSINESS_FLOWS.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Phương pháp:** Tái hiện từ mã nguồn thực tế (Code-level Reverse Engineering)

---

## 1. QUY TRÌNH XÁC THỰC & QUẢN TRỊ TÀI KHOẢN (AUTHENTICATION & ACCOUNT FLOW)

### 1.1. Đăng ký tài khoản Bệnh nhân mới
- **Trigger:** Người dùng nhấp "Đăng ký" trên trang chủ hoặc thanh điều hướng.
- **Starting Role:** Khách vãng lai (Anonymous User).
- **Preconditions:** Email và Số điện thoại chưa từng tồn tại trong hệ thống.
- **Các bước thực hiện:**
  1. Người dùng điền thông tin vào form tại `/Account/Register` (Họ tên, Email, SĐT, Mật khẩu, Ngày sinh, Giới tính).
  2. Frontend gửi request tới `POST /api/auth/register`.
  3. Backend băm mật khẩu bằng BCrypt (`workFactor = 11`), tạo bản ghi `UserAccounts` với trạng thái `Status = 'Unverified'`, tạo bản ghi `PatientProfiles` với mã tự sinh `PAT-yyyyMMdd-XXXX`.
  4. Backend sinh mã OTP 6 số ngẫu nhiên, lưu vào bộ nhớ cache/bảng xác thực với thời hạn 10 phút, đồng thời gửi email chứa OTP cho người dùng qua `EmailService`.
  5. Người dùng được chuyển hướng tới `/Account/VerifyEmail?email={email}` để nhập mã OTP.
  6. Sau khi nhập đúng mã OTP, tài khoản được kích hoạt sang `Status = 'Active'`.
- **Database Reads/Writes:**
  - READ: Kiểm tra trùng `UserAccounts.Email` và `UserAccounts.PhoneNumber`.
  - WRITE: Thêm mới `UserAccounts`, `PatientProfiles`, `AuditLogs`.
- **Phân loại trạng thái:** `COMPLETE`

### 1.2. Đăng nhập & Điều hướng theo vai trò (Role-based Redirect)
- **Trigger:** Người dùng gửi form tại `/Account/Login`.
- **Starting Role:** Người dùng đã có tài khoản.
- **Các bước thực hiện:**
  1. Frontend gọi `POST /api/auth/login`.
  2. Backend kiểm tra Email, so khớp mật khẩu bằng `BCrypt.Verify`.
  3. Kiểm tra trạng thái tài khoản: Nếu `Locked` hoặc `Inactive` $\rightarrow$ Từ chối; Nếu `Unverified` $\rightarrow$ Yêu cầu xác thực OTP trước.
  4. Backend tạo cặp `AccessToken` (JWT hạn 60 phút) và `RefreshToken` (hạn 7 ngày).
  5. Frontend nhận token, lưu vào Session thông qua `TokenService.SaveTokens`. Đồng thời phát hành Cookie xác thực `DentalCare.AuthCookie` chứa các Claims: `NameIdentifier` (UserId), `Email`, `Name`, và `Role`.
  6. Frontend tự động điều hướng người dùng tới Dashboard tương ứng với vai trò.
- **Phân loại trạng thái:** `COMPLETE`

---

## 2. QUY TRÌNH NGHIỆP VỤ BỆNH NHÂN (PATIENT FLOW)

### 2.1. Đặt lịch khám trực tuyến (Online Appointment Booking)
- **Trigger:** Bệnh nhân nhấp nút "Đặt lịch khám" trên giao diện `/Patient/Appointment/Book`.
- **Starting Role:** `Patient`
- **Preconditions:** Tài khoản ở trạng thái `Active`, không bị khóa quyền đặt lịch theo chính sách phạt Strike Policy.
- **Các bước thực hiện:**
  1. Bệnh nhân chọn Chuyên khoa $\rightarrow$ API nạp danh sách Dịch vụ tương ứng (`GET /api/booking/departments/{id}/services`).
  2. Bệnh nhân có thể chọn Bác sĩ cụ thể hoặc để Trưởng khoa phân bổ linh hoạt.
  3. Bệnh nhân chọn ngày và khung giờ khám (`GET /api/booking/available-slots`).
  4. Bệnh nhân nhập triệu chứng / lý do khám và gửi yêu cầu.
  5. Backend gọi `IAppointmentService.CreatePatientAppointmentAsync`:
     - Kiểm tra Strike Policy: Nếu tài khoản có $\ge 3$ gậy vi phạm trong 30 ngày qua $\rightarrow$ Báo lỗi và chặn đặt lịch.
     - Kiểm tra thời gian: Thời gian khám phải từ hiện tại trở đi (không được trong quá khứ).
     - Tính toán thời gian kết thúc: `ScheduledEnd = ScheduledStart + Service.DurationMinutes`.
     - Sinh mã lịch hẹn duy nhất: `APP-yyyyMMdd-XXXXXX`.
     - Tạo bản ghi `Appointments` với trạng thái ban đầu: `PendingReceptionReview`. Lưu ý: Lúc này `AssignedDentistUserId = NULL`, `RoomId = NULL`, `DentalChairId = NULL` (không gán tài nguyên ở bước này).
     - Ghi nhận lịch sử vào `AppointmentStatusHistories`.
  6. Chuyển hướng bệnh nhân sang màn hình `/Patient/Appointment/Success`.
- **Phân loại trạng thái:** `COMPLETE`

### 2.2. Dời lịch khám (Patient Reschedule Workflow)
- **Trigger:** Bệnh nhân bấm nút "Dời lịch" trên màn hình chi tiết ca khám `/Patient/Appointment/Reschedule/{id}`.
- **Starting Role:** `Patient`
- **Các bước thực hiện:**
  1. Bệnh nhân chỉ được dời lịch khi ca khám ở các trạng thái: `PendingReceptionReview`, `PendingDepartmentReview`, `AwaitingPatientResponse`, `Confirmed`.
  2. Bệnh nhân chọn ngày giờ mới và nhập lý do.
  3. Backend thực hiện nghiệp vụ:
     - Đánh dấu ca khám cũ thành `Cancelled` kèm lý do: `[Dời lịch] Hủy để dời sang lịch khám mới {newCode}`.
     - Giải phóng toàn bộ tài nguyên phòng/ghế của ca cũ (`RoomId = NULL`, `DentalChairId = NULL`, `AssignedDentistUserId = NULL`).
     - Tạo một ca khám mới hoàn toàn với mã `newCode`, trạng thái `PendingReceptionReview`, thời gian theo lịch mới.
     - Tạo bản ghi lịch sử chuyển trạng thái cho cả ca cũ và ca mới.
     - Gửi thông báo In-app cho bệnh nhân.
- **Phân loại trạng thái:** `COMPLETE`

### 2.3. Hủy lịch hẹn & Chính sách phạt vi phạm (Strike Policy)
- **Trigger:** Bệnh nhân bấm "Hủy lịch khám" tại `/Patient/Appointment/Index`.
- **Starting Role:** `Patient`
- **Quy tắc tính vi phạm:**
  - Nếu thời gian thông báo trước khi khám $\ge 12$ giờ: Hủy lịch bình thường, **không bị tính gậy**.
  - Nếu thời gian thông báo trước khi khám $< 12$ giờ: Hệ thống ghi nhận tiền tố `[Hủy sát giờ <12h]` vào `CancellationReason` và **tính 1 gậy vi phạm**.
  - Khi tài khoản tích lũy đủ 3 gậy trong vòng 30 ngày gần nhất: Hệ thống tự động khóa tính năng tự đặt lịch online trong 30 ngày (`IsEligible = false`, `BlockedUntil = now + 30 days`).
- **Phân loại trạng thái:** `COMPLETE`

---

## 3. QUY TRÌNH NGHIỆP VỤ TIẾP TÂN (RECEPTIONIST FLOW)

### 3.1. Tiếp nhận và Xử lý yêu cầu đặt lịch mới
- **Trigger:** Bệnh nhân gửi yêu cầu đặt lịch mới thành công.
- **Starting Role:** `Receptionist`
- **Màn hình:** `/Receptionist/Appointments/Requests`
- **Các bước thực hiện:**
  1. Lễ tân xem danh sách các ca ở trạng thái `PendingReceptionReview`.
  2. Kiểm tra thông tin hành chính của bệnh nhân.
  3. Lựa chọn hành động:
     - **Chuyển tiếp (Forward):** Lễ tân bấm "Chuyển tiếp", nhập ghi chú tiếp nhận $\rightarrow$ Gọi API `POST /api/receptionist/appointments/{id}/forward` $\rightarrow$ Trạng thái chuyển thành `PendingDepartmentReview`.
     - **Từ chối (Reject):** Lễ tân bấm "Từ chối", nhập lý do $\rightarrow$ Gọi API `POST /api/receptionist/appointments/{id}/reject` $\rightarrow$ Trạng thái chuyển thành `Rejected`. Bệnh nhân nhận được thông báo giải thích lý do từ chối.
- **Phân loại trạng thái:** `COMPLETE`

### 3.2. Check-in ca khám hôm nay & Cấp số phòng chờ
- **Trigger:** Bệnh nhân có mặt tại quầy tiếp đón của phòng khám vào ngày hẹn.
- **Starting Role:** `Receptionist`
- **Màn hình:** `/Receptionist/Appointments/TodayCheckIn`
- **Các bước thực hiện:**
  1. Lễ tân tìm kiếm ca khám theo Mã lịch, Tên bệnh nhân hoặc Số điện thoại.
  2. Lễ tân đối chiếu thông tin và bấm nút **[Check-in]**.
  3. Backend gọi `IAppointmentService.ReceptionistCheckInAsync`:
     - Kiểm tra trạng thái ca khám: Bắt buộc phải là `Confirmed`.
     - Cập nhật `Status = 'CheckedIn'`, gán thời gian `CheckedInAt = UtcNow`.
     - Ghi nhận lịch sử chuyển trạng thái.
  4. Ca khám ngay lập tức xuất hiện trên màn hình Bác sĩ trực phòng khám để sẵn sàng tiếp đón.
- **Xử lý bệnh nhân không đến (No-Show):** Nếu quá giờ hẹn mà bệnh nhân không có mặt, Lễ tân bấm nút **[No-Show]** $\rightarrow$ Hệ thống chuyển trạng thái sang `NoShow`, giải phóng lịch hẹn và ghi nhận 1 gậy phạt vi phạm cho bệnh nhân.
- **Phân loại trạng thái:** `COMPLETE`

---

## 4. QUY TRÌNH TRƯỞNG KHOA ĐIỀU PHỐI (DEPARTMENT MANAGER FLOW)

### 4.1. Phê duyệt ca khám & Phân bổ tài nguyên y tế
- **Trigger:** Lễ tân chuyển tiếp yêu cầu đặt lịch sang khoa (`PendingDepartmentReview`).
- **Starting Role:** `DepartmentManager`
- **Màn hình:** `/Department/Appointments/Review/{id}`
- **Các bước thực hiện:**
  1. Trưởng khoa xem thông tin chuyên môn của ca khám (Triệu chứng, Dịch vụ yêu cầu, Bác sĩ bệnh nhân mong muốn).
  2. Hệ thống tự động tính toán và hiển thị danh sách Bác sĩ khả dụng (`GetAvailableDentistsForAppointmentAsync`), phòng khám và ghế trống không bị trùng lịch trong khung giờ đó.
  3. **Ràng buộc nghiệp vụ bắt buộc:** Nếu bệnh nhân đã yêu cầu đích danh một Bác sĩ, Trưởng khoa **không được phép đổi sang bác sĩ khác** trên màn hình Confirm.
  4. Trưởng khoa chọn Bác sĩ phụ trách, Phòng điều trị, Ghế nha khoa và bấm **[Xác nhận]**.
  5. Backend thực thi giao dịch trong Transaction:
     - Kiểm tra xung đột thời gian (Conflict Check): Đảm bảo Bác sĩ, Phòng và Ghế không bị trùng với bất kỳ ca khám nào khác đang ở trạng thái chiếm dụng (`Confirmed`, `CheckedIn`, `Waiting`, `InService`).
     - Sinh số thứ tự khám trong ngày: Đếm số lượng ca khám của khoa trong ngày để cấp số dạng `Q001`, `Q002`,...
     - Cập nhật trạng thái ca khám: `Status = 'Confirmed'`, `ConfirmedAt = UtcNow`.
     - Gửi thông báo thời gian thực và email xác nhận tự động tới bệnh nhân.
- **Phân loại trạng thái:** `COMPLETE`

### 4.2. Đề xuất phương án thay đổi (Propose Change Workflow)
- **Trigger:** Bác sĩ bệnh nhân yêu cầu bị trùng lịch, hoặc phòng khám hết ghế trống trong khung giờ bệnh nhân chọn.
- **Starting Role:** `DepartmentManager`
- **Các bước thực hiện:**
  1. Trưởng khoa chọn khung giờ mới hoặc Bác sĩ thay thế phù hợp kèm lý do giải thích.
  2. Bấm **[Gửi đề xuất thay đổi]** $\rightarrow$ Gọi API `POST /api/department/appointments/{id}/propose-change`.
  3. Backend tạo bản ghi `AppointmentChangeProposals` với trạng thái `Pending`, chuyển trạng thái lịch hẹn sang `AwaitingPatientResponse`.
  4. Gửi thông báo kèm đường link tới bệnh nhân.
  5. Bệnh nhân mở chi tiết lịch hẹn:
     - Nếu bấm **[Đồng ý]**: Hệ thống cập nhật thời gian/bác sĩ mới, chuyển ca về `PendingDepartmentReview` để Trưởng khoa hoàn tất xác nhận.
     - Nếu bấm **[Từ chối]**: Ca khám được chuyển lại cho Trưởng khoa để tìm phương án khác.
- **Phân loại trạng thái:** `COMPLETE`

---

## 5. QUY TRÌNH BÁC SĨ KHÁM & ĐIỀU TRỊ (DENTIST FLOW)

### 5.1. Bắt đầu ca khám & Chọn ghế điều trị
- **Trigger:** Bệnh nhân đã hoàn tất Check-in tại quầy lễ tân.
- **Starting Role:** `Dentist`
- **Màn hình:** `/Dentist/Appointments/Today`
- **Các bước thực hiện:**
  1. Bác sĩ thấy ca khám của bệnh nhân chuyển sang trạng thái xanh dương "Đã Check-in".
  2. Bác sĩ bấm nút **[Vào Khám]** $\rightarrow$ Trạng thái chuyển thành `InService`.
  3. Bác sĩ chọn ghế điều trị thực tế trong phòng khám qua nút **[Chọn/Đổi Ghế]**. Hệ thống kiểm tra ghế không bị bác sĩ khác chiếm dụng và khóa ghế cho ca khám.
  4. Banner hiển thị trạng thái xanh lá đậm: **ĐANG TRONG CA KHÁM**.
- **Phân loại trạng thái:** `COMPLETE`

### 5.2. Chẩn đoán lâm sàng & Cập nhật Sơ đồ răng FDI
- **Trigger:** Bác sĩ tiến hành khám trực tiếp răng miệng cho bệnh nhân.
- **Màn hình:** `/Dentist/MedicalRecords/Index`
- **Các bước thực hiện:**
  1. Bác sĩ mở giao diện Sơ đồ răng FDI 32 răng (hiển thị trên 1 hàng ngang chuẩn công thái học y tế).
  2. Bác sĩ nhấp chuột trực tiếp lên răng bị bệnh lý (ví dụ: Răng R46).
  3. Chọn mặt răng thương tổn (Mặt nhai, Mặt ngoài, Mặt trong, Kẽ gần, Kẽ xa) và tình trạng bệnh lý (Sâu răng, Viêm tủy, Đã hàn trám, Răng sứ, Mất răng).
  4. Tải lên ảnh chụp X-quang hoặc phim chụp cắt lớp CT ConeBeam.
  5. Bấm **[Lưu hồ sơ bệnh án]** $\rightarrow$ Backend lưu vào `ClinicalEntries`, `OdontogramEntries` và `ImagingRecords`.
- **Phân loại trạng thái:** `COMPLETE`

### 5.3. Kê đơn thuốc & Hoàn tất ca khám
- **Trigger:** Bác sĩ hoàn thành thủ thuật điều trị cho bệnh nhân.
- **Các bước thực hiện:**
  1. Bác sĩ vào tab Kê đơn thuốc $\rightarrow$ Chọn danh mục thuốc, liều dùng, số lượng, lời dặn $\rightarrow$ Lưu đơn thuốc vào `Prescriptions` và `PrescriptionItems`.
  2. Bác sĩ bấm nút **[Hoàn Thành Ca Khám]** tại `/Dentist/Appointments/Today`.
  3. Backend cập nhật `Status = 'Completed'`, gán `CompletedAt = UtcNow`.
  4. **Cơ chế khóa an toàn y khoa:** Nút [Chọn/Đổi Ghế] tự động bị vô hiệu hóa và chuyển thành huy hiệu **[Đã Khóa Ghế]** để không ai có thể sửa đổi vị trí ca khám đã hoàn tất.
  5. Ca khám được chuyển ngay lập tức sang danh sách chờ lập hóa đơn của Lễ tân.
- **Phân loại trạng thái:** `COMPLETE`

---

## 6. QUY TRÌNH THU NGÂN & THANH TOÁN (BILLING & PAYMENT FLOW)

### 6.1. Lập hóa đơn viện phí
- **Trigger:** Ca khám của bệnh nhân đạt trạng thái `Completed`.
- **Starting Role:** `Receptionist`
- **Màn hình:** `/Receptionist/Billing/Create`
- **Các bước thực hiện:**
  1. Lễ tân vào danh sách ca hoàn thành chờ thanh toán (`/api/billing/appointments/billable`).
  2. Nhấp "Tạo hóa đơn" cho ca khám $\rightarrow$ Hệ thống tự động nạp các dịch vụ y tế mà Bác sĩ đã thực hiện.
  3. Lễ tân kiểm tra bảng kê chi phí, áp dụng chiết khấu/phụ phí (nếu có) và bấm **[Phát hành hóa đơn]**.
  4. Backend tạo bản ghi `Invoices` với trạng thái ban đầu: `Status = 'Unpaid'`.
- **Phân loại trạng thái:** `COMPLETE`

### 6.2. Thu tiền & Quét mã VietQR động
- **Trigger:** Bệnh nhân thanh toán tại quầy thu ngân hoặc thanh toán qua cổng cá nhân.
- **Màn hình:** `/Receptionist/Billing/Detail/{id}` hoặc `/Patient/Invoices/Detail/{id}`
- **Các bước thực hiện:**
  1. Hệ thống tự động kết xuất mã **VietQR động** chuẩn Napas 247:
     `https://img.vietqr.io/image/MB-039123456789-compact2.png?amount={SốTiền}&addInfo=THANH%20TOAN%20{MãHóaĐơn}`
  2. Bệnh nhân dùng ứng dụng ngân hàng di động quét mã $\rightarrow$ Số tiền và nội dung chuyển khoản được điền chính xác 100%.
  3. Lễ tân nhận thông báo chuyển khoản thành công, bấm **[Xác nhận thanh toán]**:
     - Tạo bản ghi trong bảng `Payments`.
     - Cập nhật số tiền đã thu: Nếu thu đủ $\rightarrow$ `Invoice.Status = 'Paid'`; Nếu thu một phần $\rightarrow$ `Invoice.Status = 'PartiallyPaid'`.
  4. Lễ tân bấm **[In hóa đơn]** $\rightarrow$ Hệ thống mở trang `/Receptionist/Billing/Print/{id}` để in biên lai viện phí cho người bệnh.
- **Phân loại trạng thái:** `COMPLETE`

---

## 7. BẢNG TỔNG KẾT TRẠNG THÁI CÁC QUY TRÌNH NGHIỆP VỤ

| Mã quy trình | Tên quy trình nghiệp vụ | Các vai trò tham gia | Độ hoàn thiện | Ghi chú & Bằng chứng |
| :---: | :--- | :--- | :---: | :--- |
| **BF-01** | Đăng ký & Xác thực tài khoản OTP | Patient, System | **COMPLETE** | `AuthController.cs`, `AuthService.cs` |
| **BF-02** | Đăng nhập & Đổi phiên làm việc | All 5 Roles | **COMPLETE** | `AccountController.cs`, `ApiAuthorizationHandler.cs` |
| **BF-03** | Đặt lịch khám trực tuyến | Patient | **COMPLETE** | `AppointmentService.CreatePatientAppointmentAsync` |
| **BF-04** | Tiếp nhận & Chuyển tiếp lịch khám | Receptionist | **COMPLETE** | `AppointmentService.ReceptionistForwardAsync` |
| **BF-05** | Phê duyệt & Gán tài nguyên y tế | DepartmentManager | **COMPLETE** | `AppointmentService.ManagerConfirmAsync` |
| **BF-06** | Đề xuất thay đổi lịch hẹn | Manager $\leftrightarrow$ Patient | **COMPLETE** | `ManagerProposeChangeAsync`, `PatientRespondProposalAsync` |
| **BF-07** | Check-in ca khám & Cấp số thứ tự | Receptionist | **COMPLETE** | `AppointmentService.ReceptionistCheckInAsync` |
| **BF-08** | Khám bệnh & Sơ đồ răng FDI 32 răng | Dentist | **COMPLETE** | `MedicalRecordService.cs`, `Index.cshtml` |
| **BF-09** | Kê đơn thuốc điện tử | Dentist, Patient | **COMPLETE** | `PrescriptionService.cs`, `Print.cshtml` |
| **BF-10** | Lập hóa đơn & Thu tiền VietQR | Receptionist, Patient | **COMPLETE** | `BillingService.cs`, `Detail.cshtml` |
| **BF-11** | Chuyển viện & Chuyển chuyên khoa | Dentist, Manager | **COMPLETE** | `PostCareService.cs`, `Referrals/Index.cshtml` |
| **BF-12** | Hỗ trợ tư vấn trực tuyến (Live Chat) | Patient, Receptionist | **COMPLETE** | `LiveChatHub.cs`, `support-widget.js` |
| **BF-13** | Quản lý chuyên khoa, phòng & ghế | Admin | **COMPLETE** | `AdminService.cs`, `RoomChair/Index.cshtml` |
| **BF-14** | Báo cáo doanh thu & kiểm toán | Admin, Manager | **COMPLETE** | `ReportService.cs`, `AuditLogService.cs` |
