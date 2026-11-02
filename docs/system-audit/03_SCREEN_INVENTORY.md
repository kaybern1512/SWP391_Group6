# DANH MỤC MÀN HÌNH HỆ THỐNG TOÀN DIỆN (COMPLETE SCREEN INVENTORY)
**Tài liệu:** `03_SCREEN_INVENTORY.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Phạm vi quét:** Toàn bộ Controller, Action, Razor View (.cshtml), ViewModel và JavaScript Client

---

## 1. TỔNG HỢP MÀN HÌNH THEO PHÂN QUYỀN

Hệ thống đã hiện thực tổng cộng **73 màn hình giao diện người dùng** phục vụ công chúng và 5 nhóm người dùng, không có trang rỗng hoặc đường dẫn chết (broken link):

- **Xác thực & Chung (Common & Auth):** 11 màn hình
- **Phân hệ Bệnh nhân (Patient):** 14 màn hình
- **Phân hệ Tiếp tân (Receptionist):** 10 màn hình
- **Phân hệ Bác sĩ (Dentist):** 15 màn hình
- **Phân hệ Trưởng khoa (Department Manager):** 11 màn hình
- **Phân hệ Quản trị viên (System Administrator):** 12 màn hình

---

## 2. BẢNG KIỂM TOÀN BỘ 73 MÀN HÌNH GIAO DIỆN

### PHẦN I: XÁC THỰC, HỒ SƠ & TRANG CHỦ DÙNG CHUNG (11 Màn hình)

#### 1. Trang chủ (Home)
- **Role:** Khách vãng lai & Tất cả vai trò
- **Tên màn hình:** Trang chủ phòng khám DentalCare
- **Route / URL:** `/` hoặc `/Home/Index`
- **Controller + Action:** `HomeController.Index`
- **Razor View:** `FE/DentalClinic.Web/Views/Home/Index.cshtml`
- **ViewModel / DTO:** Không yêu cầu (Static Landing Page)
- **APIs gọi:** Không gọi API trực tiếp
- **Dữ liệu hiển thị:** Giới thiệu chuyên khoa Răng Hàm Mặt, đội ngũ bác sĩ, danh mục dịch vụ, quy trình đặt lịch, form tư vấn nhanh, widget hỗ trợ live chat.
- **Thao tác người dùng:** Nhấp "Đặt lịch ngay" $\rightarrow$ điều hướng `/Patient/Appointment/Book`; Đăng nhập/Đăng ký $\rightarrow$ `/Account/Login`.
- **Trạng thái:** Thành công render HTML.
- **Thực thể liên quan:** `Departments`, `DepartmentServices`.

#### 2. Trang Chính sách bảo mật (Privacy Policy)
- **Role:** Công chúng
- **Route / URL:** `/Home/Privacy`
- **Controller + Action:** `HomeController.Privacy`
- **Razor View:** `FE/DentalClinic.Web/Views/Home/Privacy.cshtml`
- **Thực thể liên quan:** Không có.

#### 3. Trang Báo lỗi Toàn cục (Error)
- **Role:** Công chúng
- **Route / URL:** `/Home/Error?statusCode={0}`
- **Controller + Action:** `HomeController.Error`
- **Razor View:** `FE/DentalClinic.Web/Views/Shared/Error.cshtml`
- **ViewModel:** `ErrorViewModel`
- **Dữ liệu hiển thị:** Mã trạng thái HTTP (400, 401, 403, 404, 500) kèm mô tả thân thiện bằng tiếng Việt.

#### 4. Đăng nhập (Login)
- **Role:** Người dùng chưa đăng nhập
- **Route / URL:** `/Account/Login` (Alias: `/Auth/Login`)
- **Controller + Action:** `AccountController.Login` (GET/POST)
- **Razor View:** `FE/DentalClinic.Web/Views/Account/Login.cshtml`
- **ViewModel:** `LoginViewModel` (Email, Password, RememberMe)
- **APIs gọi:** `POST /api/auth/login` (qua `AuthApiService`)
- **Validation:** Bắt buộc nhập Email đúng định dạng, Mật khẩu không để trống.
- **Điều hướng sau khi thành công:** Dựa vào `UserRole` trong Token:
  - `Patient` $\rightarrow$ `/Patient/Dashboard`
  - `Receptionist` $\rightarrow$ `/Receptionist/Dashboard`
  - `Dentist` $\rightarrow$ `/Dentist/Dashboard`
  - `DepartmentManager` $\rightarrow$ `/Department/Dashboard`
  - `SystemAdministrator` $\rightarrow$ `/Admin/Dashboard`
- **Trạng thái lỗi:** Báo sai mật khẩu, tài khoản chưa xác minh (`Unverified` $\rightarrow$ chuyển sang `/Account/VerifyEmail`), hoặc tài khoản bị khóa (`Locked`).
- **Thực thể liên quan:** `UserAccounts`.

#### 5. Đăng ký tài khoản (Register)
- **Role:** Khách hàng mới
- **Route / URL:** `/Account/Register`
- **Controller + Action:** `AccountController.Register` (GET/POST)
- **Razor View:** `FE/DentalClinic.Web/Views/Account/Register.cshtml`
- **ViewModel:** `RegisterViewModel` (FullName, Email, PhoneNumber, Password, ConfirmPassword, DateOfBirth, Gender)
- **APIs gọi:** `POST /api/auth/register`
- **Validation:** Mật khẩu tối thiểu 8 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt. Số điện thoại chuẩn Việt Nam (10 chữ số).
- **Điều hướng sau khi thành công:** Chuyển sang `/Account/VerifyEmail?email={email}` để nhập mã OTP 6 số.

#### 6. Xác minh Email (Verify Email OTP)
- **Role:** Bệnh nhân vừa đăng ký
- **Route / URL:** `/Account/VerifyEmail`
- **Controller + Action:** `AccountController.VerifyEmail` (GET/POST)
- **Razor View:** `FE/DentalClinic.Web/Views/Account/VerifyEmail.cshtml`
- **ViewModel:** `VerifyEmailViewModel` (Email, OtpCode)
- **APIs gọi:** `POST /api/auth/verify-email`, `POST /api/auth/resend-verification`
- **Điều hướng:** Thành công $\rightarrow$ `/Account/Login` kèm thông báo tài khoản đã kích hoạt.

#### 7. Quên mật khẩu (Forgot Password)
- **Route / URL:** `/Account/ForgotPassword`
- **Razor View:** `FE/DentalClinic.Web/Views/Account/ForgotPassword.cshtml`
- **APIs gọi:** `POST /api/auth/forgot-password` $\rightarrow$ Gửi mã OTP khôi phục qua email.

#### 8. Đặt lại mật khẩu (Reset Password)
- **Route / URL:** `/Account/ResetPassword`
- **Razor View:** `FE/DentalClinic.Web/Views/Account/ResetPassword.cshtml`
- **APIs gọi:** `POST /api/auth/reset-password`

#### 9. Đổi mật khẩu (Change Password)
- **Role:** Người dùng đã đăng nhập (Tất cả vai trò)
- **Route / URL:** `/Account/ChangePassword`
- **Razor View:** `FE/DentalClinic.Web/Views/Account/ChangePassword.cshtml`
- **APIs gọi:** `POST /api/auth/change-password`

#### 10. Từ chối truy cập (Access Denied)
- **Route / URL:** `/Account/AccessDenied`
- **Razor View:** `FE/DentalClinic.Web/Views/Account/AccessDenied.cshtml`
- **Mô tả:** Hiển thị khi người dùng truy cập route không đúng vai trò cho phép.

#### 11. Hồ sơ cá nhân (Profile View & Edit)
- **Route / URL:** `/Profile/Index` và `/Profile/Edit`
- **Controller + Action:** `ProfileController.Index`, `ProfileController.Edit`
- **Razor View:** `FE/DentalClinic.Web/Views/Profile/Index.cshtml`, `Edit.cshtml`
- **APIs gọi:** `GET /api/profile/me`, `PUT /api/profile/me`, `POST /api/profile/avatar`

---

### PHẦN II: PHÂN HỆ BỆNH NHÂN (Patient - 14 Màn hình)

1. **Bảng điều khiển bệnh nhân:** `/Patient/Dashboard` (`PatientDashboardController.Index`)  
   *Hiển thị:* Lịch hẹn sắp tới, tiến độ điều trị, hóa đơn chưa trả, thẻ gậy cảnh báo Strike Policy.
2. **Đặt lịch khám online:** `/Patient/Appointment/Book` (`PatientAppointmentController.Book`)  
   *Thao tác:* Chọn Chuyên khoa $\rightarrow$ Chọn Dịch vụ $\rightarrow$ Chọn Bác sĩ (tùy chọn) $\rightarrow$ Chọn Ngày giờ $\rightarrow$ Nhập lý do khám $\rightarrow$ Đặt hẹn.
3. **Đặt lịch thành công:** `/Patient/Appointment/Success` (`PatientAppointmentController.Success`)  
   *Hiển thị:* Mã lịch hẹn dạng `APP-yyyyMMdd-XXXXXX`, hướng dẫn chuẩn bị trước khi đến phòng khám.
4. **Danh sách lịch khám cá nhân:** `/Patient/Appointment/Index` (`PatientAppointmentController.Index`)  
   *Lọc theo trạng thái:* Chờ tiếp nhận, Đã xác nhận, Đã check-in, Đã hoàn thành, Đã hủy.
5. **Chi tiết lịch khám:** `/Patient/Appointment/Detail/{id}` (`PatientAppointmentController.Detail`)  
   *Hiển thị:* Chi tiết phòng khám, ghế, bác sĩ, lịch sử chuyển trạng thái, hộp phản hồi đề xuất đổi lịch (nếu có).
6. **Dời lịch khám:** `/Patient/Appointment/Reschedule/{id}` (`PatientAppointmentController.Reschedule`)  
   *Thao tác:* Chọn ngày giờ mới và lý do dời lịch. Hệ thống tự hủy ca cũ và tạo ca mới kế thừa.
7. **Đánh giá dịch vụ khám:** `/Patient/Appointment/Feedback/{id}` (`PatientAppointmentController.Feedback`)  
   *Thao tác:* Đánh giá số sao (1-5 sao) và để lại nhận xét sau khi ca khám hoàn tất.
8. **Hồ sơ bệnh án điện tử:** `/Patient/MedicalRecords/Index` (`PatientMedicalRecordController.Index`)  
   *Hiển thị:* Tiền sử bệnh, dị ứng thuốc, các đợt khám lâm sàng, hình ảnh phim X-quang, sơ đồ răng.
9. **Kế hoạch điều trị cá nhân:** `/Patient/TreatmentPlans/Index` (`PatientTreatmentPlanController.Index`)  
   *Hiển thị:* Danh sách các phác đồ điều trị đang thực hiện và đã hoàn tất.
10. **Chi tiết kế hoạch điều trị:** `/Patient/TreatmentPlans/Detail/{id}` (`PatientTreatmentPlanController.Detail`)  
    *Hiển thị:* Các giai đoạn (phases) điều trị, chi phí ước tính, bác sĩ phụ trách từng giai đoạn.
11. **Đơn thuốc cá nhân:** `/Patient/Prescriptions/Index` (`PatientPrescriptionController.Index`)  
    *Hiển thị:* Danh sách đơn thuốc, danh mục thuốc, liều lượng, cách uống, lời dặn của bác sĩ.
12. **Danh sách hóa đơn:** `/Patient/Invoices/Index` (`PatientInvoiceController.Index`)  
    *Hiển thị:* Danh sách hóa đơn khám bệnh và các khoản đã thanh toán/còn nợ.
13. **Chi tiết hóa đơn & VietQR:** `/Patient/Invoices/Detail/{id}` (`PatientInvoiceController.Detail`)  
    *Điểm nhấn:* Hiển thị mã VietQR động chuẩn MBBank, tự động sinh số tiền và nội dung chuyển khoản.
14. **Lịch hẹn tái khám:** `/Patient/FollowUps/Index` (`PatientFollowUpController.Index`)  
    *Hiển thị:* Các lịch hẹn tái khám được bác sĩ chỉ định kèm ngày hẹn và nội dung kiểm tra.

---

### PHẦN III: PHÂN HỆ TIẾP TÂN (Receptionist - 10 Màn hình)

1. **Bảng điều khiển lễ tân:** `/Receptionist/Dashboard` (`ReceptionistDashboardController.Index`)  
   *Thống kê:* Số ca chờ tiếp nhận, số ca đã xác nhận hôm nay, số ca đã check-in, doanh thu trong ca trực.
2. **Tiếp nhận yêu cầu đặt hẹn mới:** `/Receptionist/Appointments/Requests` (`ReceptionistAppointmentController.Requests`)  
   *Thao tác:* Bấm **[Chuyển tiếp]** sang Trưởng khoa xem xét, hoặc bấm **[Từ chối]** kèm lý do.
3. **Check-in ca hôm nay & Gọi số:** `/Receptionist/Appointments/TodayCheckIn` (`ReceptionistAppointmentController.TodayCheckIn`)  
   *Thao tác:* Tìm kiếm theo mã lịch hoặc SĐT $\rightarrow$ Bấm **[Check-in]** (cấp số phòng chờ `Q-xxx`) $\rightarrow$ Bấm **[No-Show]** nếu bệnh nhân không đến.
4. **Tra cứu bệnh nhân:** `/Receptionist/Patients/Index` (`ReceptionistPatientController.Index`)  
   *Thao tác:* Tra cứu hồ sơ theo Tên, Mã bệnh nhân (`PAT-xxx`), SĐT, xem lịch sử các lần khám.
5. **Danh sách hóa đơn viện phí:** `/Receptionist/Billing/Index` (`ReceptionistBillingController.Index`)  
   *Lọc:* Hóa đơn chờ thu (`Unpaid`), thu một phần (`PartiallyPaid`), đã thu (`Paid`).
6. **Lập hóa đơn thanh toán:** `/Receptionist/Billing/Create` (`ReceptionistBillingController.Create`)  
   *Thao tác:* Chọn ca khám vừa hoàn thành (`Completed`) $\rightarrow$ Hệ thống tự load danh mục dịch vụ đã thực hiện $\rightarrow$ Thêm phụ phí $\rightarrow$ Phát hành hóa đơn.
7. **Chi tiết hóa đơn & Thu tiền:** `/Receptionist/Billing/Detail/{id}` (`ReceptionistBillingController.Detail`)  
   *Thao tác:* Chọn phương thức (Tiền mặt / Thẻ / Chuyển khoản VietQR) $\rightarrow$ Nhập số tiền thu $\rightarrow$ Ghi nhận thanh toán.
8. **In biên lai thu tiền:** `/Receptionist/Billing/Print/{id}` (`ReceptionistBillingController.Print`)  
   *Giao diện:* Chuẩn mẫu in hóa đơn y tế khổ A4/A5, tự động bật hộp thoại in của trình duyệt (`window.print()`).
9. **Bàn trực Chat hỗ trợ:** `/Receptionist/Chat/Index` (`ReceptionistChatController.Index`)  
   *Tính năng:* Quản lý các cuộc trò chuyện trực tuyến thời gian thực với bệnh nhân qua SignalR.
10. **Quản lý tái khám:** `/Receptionist/FollowUps/Index` (`ReceptionistFollowUpController.Index`)  
    *Thao tác:* Theo dõi danh sách bệnh nhân cần gọi điện nhắc lịch tái khám trong tuần.

---

### PHẦN IV: PHÂN HỆ BÁC SĨ (Dentist - 15 Màn hình)

1. **Bảng điều khiển bác sĩ:** `/Dentist/Dashboard` (`DentistDashboardController.Index`)
2. **Hàng đợi ca khám hôm nay:** `/Dentist/Appointments/Today` (`DentistAppointmentController.Today`)  
   *Thao tác:* Xem danh sách bệnh nhân đã Check-in $\rightarrow$ Bấm **[Vào Khám]** $\rightarrow$ Bấm **[Chọn/Đổi Ghế]** $\rightarrow$ Bấm **[Hoàn Thành Ca Khám]**.
3. **Hồ sơ bệnh án & Sơ đồ răng FDI:** `/Dentist/MedicalRecords/Index` (`DentistMedicalRecordController.Index`)  
   *Tính năng cao cấp:* Sơ đồ 32 răng chuẩn FDI (hiển thị trên 1 hàng ngang 16 răng hàm trên / 16 răng hàm dưới), click chọn từng mặt răng để đánh dấu sâu răng, trám răng, bọc sứ; tải ảnh chụp X-quang.
4. **Danh sách phác đồ điều trị:** `/Dentist/TreatmentPlans/Index` (`DentistTreatmentPlanController.Index`)
5. **Lập phác đồ điều trị mới:** `/Dentist/TreatmentPlans/Create` (`DentistTreatmentPlanController.Create`)
6. **Chi tiết phác đồ điều trị:** `/Dentist/TreatmentPlans/Detail/{id}` (`DentistTreatmentPlanController.Detail`)
7. **Ghi nhận buổi điều trị thực tế:** `/Dentist/TreatmentPlans/Sessions` (`DentistTreatmentPlanController.Sessions`)
8. **Danh sách đơn thuốc đã kê:** `/Dentist/Prescriptions/Index` (`DentistPrescriptionController.Index`)
9. **In đơn thuốc y tế:** `/Dentist/Prescriptions/Print/{id}` (`DentistPrescriptionController.Print`)
10. **Lịch làm việc cá nhân:** `/Dentist/Schedule/Index` (`DentistScheduleController.Index`)
11. **Đăng ký ngày nghỉ / Bận:** `/Dentist/Availability/Index` (`DentistScheduleController.Availability`)
12. **Chuyển khoa chuyên môn (Referral):** `/Dentist/Referrals/Index` (`DentistReferralController.Index`)  
    *Thao tác:* Chuyển bệnh nhân sang chuyên khoa khác (ví dụ: từ Tổng quát sang Chỉnh nha) kèm mức độ khẩn cấp (`Routine`, `Urgent`, `Emergency`).
13. **Xem phản hồi đánh giá:** `/Dentist/Feedback/Index` (`DentistAppointmentController.Feedback`)
14. **Danh sách bài viết Blog cá nhân:** `/Dentist/Blog/Index` (`DentistBlogController.Index`)
15. **Soạn thảo bài viết Blog:** `/Dentist/Blog/Edit` (`DentistBlogController.Edit`)

---

### PHẦN V: PHÂN HỆ TRƯỞNG KHOA (Department Manager - 11 Màn hình)

1. **Bảng điều khiển trưởng khoa:** `/Department/Dashboard` (`DepartmentDashboardController.Index`)
2. **Danh sách ca chờ duyệt:** `/Department/Appointments/Requests` (`DepartmentAppointmentController.Requests`)
3. **Màn hình duyệt ca & Phân bổ:** `/Department/Appointments/Review/{id}` (`DepartmentAppointmentController.Review`)  
   *Thao tác:* Kiểm tra xung đột $\rightarrow$ Gán Bác sĩ, Phòng khám, Ghế nha khoa $\rightarrow$ Bấm **[Xác Nhận]** hoặc **[Đề Xuất Thay Đổi]**.
4. **Quản lý bác sĩ trong khoa:** `/Department/Dentists/Index` (`DepartmentDentistController.Index`)
5. **Lập lịch phân ca trực khoa:** `/Department/Schedule/Index` (`DepartmentScheduleController.Index`)  
   *Tính năng:* Xếp lịch làm việc theo tuần cho từng bác sĩ trong khoa.
6. **Danh mục dịch vụ khoa:** `/Department/Services/Index` (`DepartmentServiceController.Index`)
7. **Theo dõi tiến độ điều trị của khoa:** `/Department/Treatment/Index` (`DepartmentTreatmentController.Index`)
8. **Chi tiết ca điều trị liên khoa:** `/Department/Treatment/Detail/{id}` (`DepartmentTreatmentController.Detail`)
9. **Duyệt giấy chuyển khoa đến:** `/Department/Referrals/Index` (`DepartmentReferralController.Index`)
10. **Báo cáo hiệu suất chuyên khoa:** `/Department/Reports/Performance` (`DepartmentReportController.Performance`)
11. **Xuất báo cáo CSV:** `/Department/Reports/ExportPerformanceCsv`

---

### PHẦN VI: PHÂN HỆ QUẢN TRỊ VIÊN (System Administrator - 12 Màn hình)

1. **Bảng điều khiển quản trị:** `/Admin/Dashboard` (`AdminDashboardController.Index`)
2. **Quản lý người dùng & phân quyền:** `/Admin/Users/Index` (`AdminUserController.Index`)  
   *Thao tác:* Tìm kiếm tài khoản, xem vai trò, bấm **[Khóa tài khoản]** / **[Mở khóa]**.
3. **Quản lý chuyên khoa:** `/Admin/Departments/Index` (`AdminDepartmentController.Index`)  
   *Thao tác:* Thêm mới khoa, cập nhật trưởng khoa, kích hoạt/tạm dừng hoạt động khoa.
4. **Quản lý danh mục dịch vụ:** `/Admin/Services/Index` (`AdminServiceController.Index`)  
   *Thao tác:* Thêm dịch vụ, cập nhật đơn giá, quy định thời lượng khám (`DurationMinutes`).
5. **Quản lý phòng khám & Ghế nha khoa:** `/Admin/RoomChair/Index` (`AdminRoomChairController.Index`)  
   *Thao tác:* Thiết lập phòng bệnh, thêm ghế điều trị, cập nhật trạng thái bảo trì ghế.
6. **Báo cáo tổng quan:** `/Admin/Reports/Index` (`AdminReportController.Index`)
7. **Báo cáo tài chính doanh thu:** `/Admin/Reports/Revenue` (`AdminReportController.Revenue`)  
   *Hiển thị:* Biểu đồ doanh thu theo tháng, theo chuyên khoa, theo phương thức thanh toán.
8. **Xuất dữ liệu doanh thu CSV:** `/Admin/Reports/ExportRevenueCsv`
9. **Nhật ký kiểm toán hệ thống:** `/Admin/AuditLogs/Index` (`AdminAuditLogController.Index`)  
   *Hiển thị:* Toàn bộ dấu vết lịch sử thay đổi: Ai thay đổi, bảng nào, hành động (INSERT, UPDATE, DELETE), IP và thời gian.
10. **Kiểm duyệt Blog nha khoa:** `/Admin/Blog/Index` (`AdminBlogController.Index`)  
    *Thao tác:* Duyệt xuất bản bài viết của bác sĩ ra trang chủ, đặt bài viết nổi bật (`Featured`).
11. **Quản lý chuyên mục Blog:** `/Admin/Blog/Categories`
12. **Báo cáo công suất lịch khám:** `/Admin/Reports/Appointments`

---

## 3. RÀ SOÁT TÍNH ĐIỀU HƯỚNG & TRẠNG THÁI LIÊN KẾT

- **Khả năng tiếp cận từ Menu (Menu Accessible):** 100% các màn hình quản lý chính đều có liên kết trực tiếp trên Sidebar tương ứng với từng vai trò đăng nhập.
- **Màn hình truy cập qua ngữ cảnh (Contextual Links):** Các màn hình chi tiết như `/Patient/Appointment/Detail/{id}`, `/Department/Appointments/Review/{id}`, `/Receptionist/Billing/Detail/{id}`, `/Dentist/Prescriptions/Print/{id}` được điều hướng mượt mà từ các bảng danh sách thông qua nút bấm hành động (Action Buttons).
- **Trang chết / Liên kết gãy (Dead Pages & Broken Links):** **KHÔNG CÓ**. Tất cả các link trên Sidebar (`_Sidebar.cshtml`) đều đã được kiểm tra và liên kết chính xác tới Controller/Action thực tế.
- **Trạng thái giao diện đầy đủ:** Mỗi màn hình đều có đủ 4 trạng thái:
  - *Loading State:* Hiển thị spinner Bootstrap khi đang tải dữ liệu.
  - *Empty State:* Hiển thị banner thân thiện (`_EmptyState.cshtml`) khi danh sách trống.
  - *Success State:* Banner xanh lá `_AlertMessage.cshtml` từ `TempData["SuccessMessage"]`.
  - *Error State:* Banner đỏ `_AlertMessage.cshtml` từ `TempData["ErrorMessage"]`.
