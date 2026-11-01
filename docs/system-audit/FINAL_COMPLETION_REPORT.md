# BÁO CÁO HOÀN THÀNH TOÀN DIỆN VÀ NGHIỆM THU DỰ ÁN DENTALCARE
## FINAL SYSTEM STABILIZATION & E2E BROWSER VALIDATION REPORT

- **Dự án:** Hệ thống Quản lý Phòng khám Đa Chuyên khoa DentalCare (SWP391 - Nhóm 6)
- **Thời điểm nghiệm thu:** 01/11/2026
- **Môi trường Database Demo:** `DESKTOP-1TUBGEU\SQLEXPRESS` / Database: `DentalClinicManagementDB`
- **Môi trường Kiểm thử Cách ly:** `DentalClinic_Disposable_PreDeployClone` (Clone nguyên trạng từ backup)
- **Kiến trúc ứng dụng:** ASP.NET Core 8 Web API + ASP.NET Core 8 MVC + SQL Server EF Core 8
- **Kết quả kiểm thử tự động (Unit & Flow Tests):** **647 / 647 PASSED (100%)** (388 Backend + 259 Frontend)
- **Kết quả kiểm thử trình duyệt thực tế (Puppeteer Real Browser E2E):** **13 / 13 PASSED (100%)**
- **Trạng thái hệ thống:** **SẴN SÀNG TRÌNH DIỄN CÓ KIỂM SOÁT (READY FOR CONTROLLED DEMO)**

---

## 1. BẢNG BẰNG CHỨNG KIỂM THỬ TRÌNH DUYỆT THỰC TẾ (REAL BROWSER POST ACTIONS)

Toàn bộ 13 bước trong 2 quy trình nghiệp vụ cốt lõi đã được thực thi tự động qua trình duyệt Google Chrome (sử dụng Puppeteer), tương tác với giao diện Frontend MVC (`https://localhost:7129`), gửi HTTP POST request thực tế đến Backend API (`https://localhost:7350`) và xác thực trạng thái ghi dữ liệu trực tiếp trong SQL Server.

Mọi dữ liệu kiểm thử ghi nhận thanh toán và bệnh án lâm sàng được thực hiện trên Database Clone độc lập (`DentalClinic_Disposable_PreDeployClone`), bảo đảm Database chính (`DentalClinicManagementDB`) giữ nguyên vẹn 100% dữ liệu sạch (69 appointments, 3 invoices ban đầu).

| STT | Vai trò (Role) | URL Thực Hiện Thao Tác | Nút / Thao Tác UI | HTTP Result | Trạng Thái DB Mong Đợi | Trạng Thái DB Thực Tế | Kết Quả | Tệp Ảnh Minh Chứng (Screenshot) |
|:---:|:---|:---|:---|:---:|:---|:---|:---:|:---|
| **01** | **Patient** | `https://localhost:7129/Patient/Appointment/Book` | Xác Nhận Đặt Lịch Khám (`#btnSubmitBooking`) | `200` (Redirect) | `Status=PendingReceptionReview`, `BookingSource=Patient` | `Status=PendingReceptionReview, BookingSource=Patient` (Appt #119) | **PASS** | `e2e_01_patient_booked.png` |
| **02** | **Receptionist** | `https://localhost:7129/Receptionist/Appointments/Forward/119` | Xác Nhận Chuyển Khoa (`.btn-success`) | `200` (Redirect) | `Status=PendingDepartmentReview` | `Status=PendingDepartmentReview` | **PASS** | `e2e_02_receptionist_forwarded.png` |
| **03** | **DepartmentManager** | `https://localhost:7129/Department/Appointments/Confirm/119` | Xác Nhận & Phân Bổ Nguồn Lực (`.btn-success`) | `200` (Redirect) | `Status=Confirmed`, Cấp `QueueNumber` tự động | `Status=Confirmed, QueueNumber=Q002` (Dentist=3, Room=1, Chair=1) | **PASS** | `e2e_03_manager_confirmed.png` |
| **04** | **Receptionist** | `https://localhost:7129/Receptionist/Appointments/CheckIn/119` | Xác Nhận Check-in (`.btn-primary`) | `200` (Redirect) | `Status=CheckedIn`, `CheckedInAt NOT NULL` | `Status=CheckedIn, CheckedInAt=2026-10-31 14:09:13` | **PASS** | `e2e_04_receptionist_checkedin.png` |
| **05** | **Dentist** | `https://localhost:7129/Dentist/Appointments/119/Start` | Bắt Đầu Khám (`.btn-success`) | `200` (Redirect) | `Status=InService` | `Status=InService` | **PASS** | `e2e_05_dentist_started.png` |
| **06** | **Dentist** | `https://localhost:7129/Dentist/MedicalRecords/CreateEntry` | Lưu Bệnh Án Lâm Sàng (`.btn-primary`) | `200` (Redirect) | Bản ghi `ClinicalEntries` mới (`Count >= 1`) | `ClinicalEntry count=1` (Chẩn đoán viêm lợi cấp) | **PASS** | `e2e_06_dentist_clinical_entry.png` |
| **07** | **Dentist** | `https://localhost:7129/Dentist/Prescriptions/Create` | Lưu Đơn Thuốc (`.btn-success`) | `200` (Redirect) | Bản ghi `Prescriptions` mới (`Count >= 1`) | `Prescription count=1` (Amoxicillin 500mg, Paracetamol 500mg) | **PASS** | `e2e_07_dentist_prescription.png` |
| **08** | **Dentist** | `https://localhost:7129/Dentist/Appointments/119/Complete` | Hoàn Thành Ca Khám (`.btn-outline-success`) | `200` (Redirect) | `Status=Completed` | `Status=Completed` | **PASS** | `e2e_08_dentist_completed.png` |
| **09** | **Receptionist** | `/Receptionist/Billing/Create` | Tạo Hóa Đơn (`.btn-primary`) | `200` (Redirect) | Hóa đơn mới với `Status=Unpaid` | `Invoice #INV-20261031-0003, Status=Unpaid, TotalAmount=500,000đ` | **PASS** | `e2e_09_receptionist_invoice_created.png` |
| **10** | **Receptionist** | `/Receptionist/Billing/Pay` | Xác Nhận Thu Tiền Mặt (`.btn-success`) | `200` (Redirect) | Hóa đơn `Status=Paid`, Thêm bản ghi `Payments` | `Status=Paid, PaymentCount=1` (Method: Cash) | **PASS** | `e2e_10_receptionist_payment_recorded.png` |
| **11** | **Patient** | `https://localhost:7129/Patient/Invoices` | Xem Chi Tiết Lịch Hẹn & Hóa Đơn | `200` (Page Load) | Lịch hẹn `Completed`, Hóa đơn `Paid` hiển thị rõ | `Appt=Completed, Invoice=Paid` (Dữ liệu hiển thị đồng bộ) | **PASS** | `e2e_11_patient_invoice_viewed.png` |
| **12** | **Receptionist** | `/Receptionist/Patients/RegisterWalkIn` | Lưu Hồ Sơ Khách Vãng Lai (`.btn-primary`) | `200` (Redirect) | `UserAccounts.Status=Unverified`, `PasswordHash=NULL` | `UserId=55, Status=Unverified, PasswordHash=NULL, Code=PAT-202610-0019` | **PASS** | `e2e_12_walkin_patient_registered.png` |
| **13** | **Receptionist** | `/Receptionist/Appointments/Create` | Tạo Lịch Hẹn Tại Quầy (`.btn-primary`) | `200` (Redirect) | `Status=PendingDepartmentReview`, `BookingSource=Receptionist` | `Appt #APP-20261031-01E03A, Status=PendingDepartmentReview, BookingSource=Receptionist` | **PASS** | `e2e_13_walkin_appointment_submitted.png` |

---

## 2. HIỆU CHỈNH CÁC ĐIỂM BẤT CẬP VỀ TÀI LIỆU & KIẾN TRÚC NGHIỆP VỤ

Nhằm đảm bảo tính chính xác tuyệt đối và nhất quán với mã nguồn thực tế:

### 2.1. Cấu hình Phòng Khám Thực Tế (`Rooms.IsExclusive`)
Dựa trên truy vấn trực tiếp bảng `Rooms` từ cơ sở dữ liệu `DentalClinicManagementDB`:
- **Phòng Chia Sẻ Đa Ghế (`IsExclusive = 0`):**
  - `ROOM-101`: Phòng Khám Răng Tổng Quát 101 (3 ghế nha khoa)
  - `ROOM-102`: Phòng Điều Trị Răng Tổng Quát 102 (2 ghế nha khoa)
  - `ROOM-301`: Phòng Chỉnh Nha & Thẩm Mỹ 301 (2 ghế nha khoa)
  - `ROOM-401`: Phòng Khám Răng Trẻ Em Vui Nhộn 401 (2 ghế nha khoa)
- **Phòng Sử Dụng Độc Quyền (`IsExclusive = 1`):**
  - `ROOM-201`: Phòng Phẫu Thuật & Implant 201 (Chỉ 1 ca phẫu thuật tại một thời điểm)
  - `ROOM-202`: Phòng Cấy Ghép Implant Kỹ Thuật Cao 202
  - `ROOM-302`: Phòng Thẩm Mỹ Nụ Cười & Laser 302
  - `ROOM-402`: Phòng Điều Trị Nha Nhi Hoàng Gia 402

### 2.2. Tên và Định Nghĩa Chỉ Mục SQL Server Duy Nhất Đã Triển Khai
Truy vấn từ `sys.indexes` trên `DentalClinicManagementDB`:
- **Tên chỉ mục:** `[dbo].[Appointments].[UQ_Appointments_Department_Date_QueueNumber]`
- **Cột được lập chỉ mục:** `(DepartmentId, ScheduledDate, QueueNumber)`
- **Bộ lọc thực tế (Filter Definition):**
  ```sql
  ([DepartmentId] IS NOT NULL AND [ScheduledStart] IS NOT NULL AND [QueueNumber] IS NOT NULL AND [QueueNumber]<>'')
  ```
- **Lưu ý nghiệp vụ:** Chỉ mục **KHÔNG** chứa điều kiện `Status <> 'Cancelled'`. Các lịch hẹn đã bị hủy (Cancelled) vẫn giữ nguyên tính duy nhất của số thứ tự đã cấp, đảm bảo nguyên tắc an toàn: số thứ tự khám một khi đã phát hành trong ngày sẽ không bao giờ bị cấp đè hay tái sử dụng cho bệnh nhân khác.

### 2.3. Danh Mục Hằng Số Trạng Thái Lịch Hẹn (`AppointmentStatus`)
Mã nguồn thực tế (`BE/DentalClinic.Domain/Enums/AppointmentStatus.cs`):
1. `PendingReceptionReview`: Chờ Lễ tân tiếp nhận (đặt lịch online).
2. `PendingDepartmentReview`: Chờ Trưởng khoa duyệt & xếp phòng/bác sĩ/ghế (Lễ tân forward hoặc tạo lịch tại quầy).
3. `AwaitingPatientResponse`: Chờ bệnh nhân phản hồi (đổi giờ khám).
4. `Confirmed`: Đã xác nhận & phân bổ tài nguyên thành công.
5. `CheckedIn`: Bệnh nhân đã có mặt và check-in tại quầy lễ tân.
6. `Waiting`: Chờ bác sĩ gọi vào phòng khám.
7. `Called`: Bác sĩ đã gọi tên vào phòng khám.
8. `InService`: Đang trong ca khám điều trị.
9. `Completed`: Đã hoàn thành quy trình lâm sàng.
10. `Rejected`: Trưởng khoa hoặc Lễ tân từ chối yêu cầu.
11. `Withdrawn`: Bệnh nhân chủ động rút yêu cầu trước khi xác nhận.
12. `Expired`: Đề xuất hết hạn do quá thời gian phản hồi.
13. `Cancelled`: Ca khám bị hủy bỏ.
14. `NoShow`: Bệnh nhân không đến khám đúng hẹn sau khi check-in/xác nhận.

### 2.4. Điểm Cấp Số Thứ Tự Khám (`QueueNumber`)
- `QueueNumber` (`Q001`, `Q002`, `Q003`...) được cấp phát tuần tự tăng dần **duy nhất tại bước Trưởng khoa xác nhận (`ManagerConfirmAsync`)**, được bảo vệ bằng ứng dụng khóa `sp_getapplock` và chỉ mục duy nhất SQL Server.
- Thao tác **Check-in (`ReceptionistCheckInAsync`)** của Lễ tân chỉ ghi nhận mốc thời gian bệnh nhân có mặt thực tế (`CheckedInAt = DateTime.UtcNow`), kích hoạt ca khám vào hàng đợi phục vụ của Bác sĩ chứ không tạo mới `QueueNumber`.

### 2.5. Tách Biệt Vòng Đời Lâm Sàng và Tài Chính
- **Vòng đời Lâm sàng:** Thực hiện trên thực thể `Appointments`: `InService` $\rightarrow$ `Completed`. Do Bác sĩ trực tiếp hoàn tất, đi kèm Bệnh án điện tử (`ClinicalEntries`) và Đơn thuốc (`Prescriptions`).
- **Vòng đời Tài chính:** Thực hiện độc lập trên thực thể `Invoices` và `Payments`. Hóa đơn do Lễ tân tạo lập sau khi ca khám hoàn thành (`Status = Unpaid`), khi thu tiền chuyển thành `Status = Paid`. Hai thực thể và vòng đời độc lập hoàn toàn.

### 2.6. Phạm Vi Tính Năng Thanh Toán
- Hệ thống hỗ trợ kế toán thu ngân nội bộ phòng khám toàn diện: **Tiền mặt (Cash)**, **Chuyển khoản (BankTransfer)**, **Thẻ tín dụng / POS (CreditCard)**.
- Hệ thống **không** tích hợp cổng thanh toán trực tuyến bên thứ ba VNPay Gateway (không có API callback).

---

## 3. TRẠNG THÁI TÍNH NĂNG KÍCH HOẠT TÀI KHOẢN BỆNH NHÂN VÃNG LAI

- **Trạng thái thực tế:** **CHƯA ĐƯỢC CÀI ĐẶT / CHƯA XÁC THỰC (NOT IMPLEMENTED / NOT VERIFIED)**.
- **Hiện trạng mã nguồn:**
  - Khi tiếp nhận bệnh nhân vãng lai tại quầy, hệ thống tạo bản ghi hành chính: `PasswordHash = NULL`, `Status = AccountStatus.Unverified` và `PatientProfile` với mã bệnh nhân tự tăng (`PAT-yyyyMM-xxxx`).
  - Toàn bộ chu trình nghiệp vụ khám, điều trị, kê đơn và thu ngân cho bệnh nhân vãng lai được thực hiện đầy đủ, an toàn thông qua sự hỗ trợ trực tiếp của Lễ tân và Nhân viên y tế tại cơ sở phòng khám.
  - Quy trình tự phục vụ trực tuyến (bệnh nhân tự nhập OTP để kích hoạt tài khoản ban đầu và tự tạo mật khẩu) hiện **chưa có luồng API riêng** trong phiên bản này.
  - Phương thức `ForgotPasswordAsync` trong `AuthService.cs` kiểm tra nghiêm ngặt `user.Status == AccountStatus.Active`, từ chối tạo OTP đối với tài khoản `Unverified` nhằm bảo vệ chống hành vi chiếm quyền tài khoản hành chính.

---

## 4. KHẮC PHỤC TRIỆT ĐỂ LỖI BẢO MẬT PHÂN QUYỀN LỄ TÂN (P0 SECURITY FIX)

1. **Phát hiện lỗ hổng tiềm ẩn:**
   - Việc bổ sung vai trò `Receptionist` vào thuộc tính `[Authorize]` ở cấp độ class của `AdminController.cs` trước đó đã vô tình mở đường cho Lễ tân tiếp cận các API quản trị nội bộ nếu không có attribute ghi đè ở cấp method.
2. **Khắc phục dứt điểm:**
   - **Cách ly hoàn toàn AdminController:** Thuộc tính class-level của `AdminController.cs` được thu hẹp nghiêm ngặt về chỉ dành cho:
     `[Authorize(Roles = $"{UserRole.SystemAdministrator},{UserRole.DepartmentManager}")]`. Lễ tân bị cấm 100% (HTTP 403 Forbidden) đối với toàn bộ các endpoint thuộc `AdminController`.
   - **Khóa chặt các thao tác sửa đổi nhạy cảm:** Bổ sung `[Authorize(Roles = UserRole.SystemAdministrator)]` rõ ràng cho các endpoint cấu hình dịch vụ (`POST /api/admin/services`, `PUT /api/admin/services/{id}`, `POST /api/admin/services/{id}/toggle-status`).
   - **Xây dựng Endpoint Tra cứu Bệnh nhân Thu hẹp:**
     Tạo endpoint chuyên biệt `GET /api/receptionist/patients` trên `AppointmentsController.cs`, phân quyền `[Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]`.
     Phương thức `AppointmentService.GetPatientsAsync` chỉ truy vấn duy nhất các tài khoản có `Role == UserRole.Patient`, không bao giờ lộ tài khoản Quản trị viên, Bác sĩ hay Trưởng khoa.
   - **Cập nhật Frontend:** `ReceptionistPatientController` và `ReceptionistAppointmentController` chuyển sang sử dụng `_appointmentApi.GetPatientsAsync()`, giữ nguyên vẹn trải nghiệm tìm kiếm và đặt lịch tại quầy.
3. **Bộ kiểm thử hồi quy bảo mật:**
   - Đã tạo tệp kiểm thử `BE/DentalClinic.Tests/Admin/AdminControllerAuthorizationRegressionTests.cs` (19 test cases), chứng minh bằng mã nguồn và phản xạ (reflection):
     - Lễ tân bị chặn 403 đối với `AdminController`.
     - `GetReceptionistPatients` chỉ cho phép `Receptionist` và `SystemAdministrator`.
     - `GetPatientsAsync` chỉ trả về người dùng vai trò `Patient`.

---

## 5. TỔNG KẾT KẾT QUẢ KIỂM THỬ TỰ ĐỘNG

- **Backend Tests (`DentalClinic.Tests.csproj`):** **388 / 388 PASSED** (Bao gồm 19 tests bảo mật mới)
- **Frontend MVC Tests (`DentalClinic.Web.Tests.csproj`):** **259 / 259 PASSED**
- **Tổng cộng:** **647 / 647 PASSED (100% tỷ lệ thành công, 0 lỗi, 0 cảnh báo gián đoạn)**

---

## 6. KẾT LUẬN NGHIỆM THU

Hệ thống DentalCare đã giải quyết triệt để các rủi ro bảo mật phân quyền, bảo vệ trọn vẹn tính toàn vẹn của dữ liệu và quy trình nghiệp vụ. Hệ thống đạt trạng thái **SẴN SÀNG TRÌNH DIỄN CÓ KIỂM SOÁT (READY FOR CONTROLLED DEMO)** phục vụ Hội đồng chấm đồ án tốt nghiệp.
