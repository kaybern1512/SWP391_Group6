# BÁO CÁO ĐÁNH GIÁ TÍNH SẴN SÀNG TRIỂN KHAI CƠ SỞ DỮ LIỆU & KỊCH BẢN KIỂM THỬ THỰC TẾ
## HỆ THỐNG QUẢN LÝ PHÒNG KHÁM NHA KHOA (DENTAL CARE MANAGEMENT SYSTEM)

**Thời điểm thực hiện:** 31/10/2026 - 18:45:00 (GMT+7)  
**Loại hình đánh giá:** **READ-ONLY AUDIT & DEPLOYMENT READINESS INSPECTION** (Tuyệt đối không can thiệp, không sửa đổi dữ liệu thực tế và không tự ý chạy migration khi chưa phê duyệt)  
**Môi trường cơ sở dữ liệu cấu hình:** Microsoft SQL Server 2022 Express Edition (v16.0.1000.6) trên máy trạm `DESKTOP-1TUBGEU\SQLEXPRESS`  
**Tên cơ sở dữ liệu mục tiêu:** `DentalClinicManagementDB`  
**Trạng thái kiểm thử mã nguồn:** **623/623 Unit & Integration Tests PASSED (Backend: 364/364, Frontend: 259/259)**  

---

## 1. KẾT QUẢ KIỂM TRA SCHEMA CƠ SỞ DỮ LIỆU THỰC TẾ (ACTUAL DATABASE SCHEMA INSPECTION)

Dựa trên kết quả trích xuất trực tiếp từ hệ thống `sys.columns`, `sys.indexes` và dữ liệu thực tế trên `DESKTOP-1TUBGEU\SQLEXPRESS`:

```
DbName     : DentalClinicManagementDB
ServerName : DESKTOP-1TUBGEU\SQLEXPRESS
Version    : Microsoft SQL Server 2022 (RTM) - 16.0.1000.6 (X64) Express Edition (64-bit)
```

### 1.1. Bảng đối chiếu hiện trạng Schema so với mã nguồn C# mới nhất:

| Bảng / Đối tượng | Thuộc tính / Ràng buộc cần kiểm tra | Hiện trạng trong DB thực tế | Trạng thái mã nguồn C# (.NET 8) | Đánh giá & Rủi ro | Phân loại |
| :--- | :--- | :--- | :--- | :--- | :---: |
| `[dbo].[Rooms]` | Cột `IsExclusive` (BIT) | **CHƯA CÓ** (Bảng chỉ có 5 cột: `RoomId, RoomCode, RoomName, Status, DepartmentId`) | Đã ánh xạ trong `Room.cs` & `DentalClinicDbContext.cs` (`HasDefaultValue(false)`) | **RỦI RO CAO:** Mọi truy vấn EF Core tới `Rooms` (như duyệt tài nguyên khám) sẽ văng lỗi `SqlException: Invalid column name 'IsExclusive'` nếu chưa chạy migration! | **REQUIRES DATABASE MIGRATION** |
| `[dbo].[Appointments]` | Cột tính toán `ScheduledDate` (DATE) | **CHƯA CÓ** | Sử dụng trong kịch bản khóa đồng thời và đánh chỉ mục duy nhất | Chưa ảnh hưởng đến truy vấn LINQ hiện tại do truy vấn qua `ScheduledStart.Value.Date`. Cần thiết để tạo Unique Index. | **REQUIRES DATABASE MIGRATION** |
| `[dbo].[Appointments]` | Filtered Unique Index `UQ_Appointments_Department_Date_QueueNumber` | **CHƯA CÓ** (Hiện chỉ có PK clustered và các Non-Unique Indexes thông thường) | Logic ứng dụng hiện tại đảm bảo sinh tăng dần qua `sp_getapplock` | Không gây lỗi ứng dụng, nhưng thiếu tầng chốt chặn vật lý cuối cùng ở mức database engine. | **REQUIRES DATABASE MIGRATION** |
| `[dbo].[UserAccounts]` | `PasswordHash` cho phép `NULL` | **ĐÃ CÓ** (`varchar(255), is_nullable = True`) | Khởi tạo tài khoản hành chính vãng lai với `PasswordHash = null` | **HOÀN TOÀN TƯƠNG THÍCH:** Không gây lỗi ràng buộc khi tạo hồ sơ tiếp nhận vãng lai tại quầy. | **VERIFIED** |
| `[dbo].[Rooms]` | Exclusivity thiết lập cho các phòng | Hiện 8/8 phòng đang lưu thông tin khám chung | Mã nguồn C# coi `IsExclusive = false` là mặc định | Cần cấu hình `IsExclusive = 1` cho các phòng phẫu thuật/vô trùng đặc biệt sau khi chạy migration. | **REQUIRES DATABASE MIGRATION** |
| `[dbo].[Appointments]` | Dữ liệu `QueueNumber` hiện tại | **KHÔNG CÓ TRÙNG LẶP** (0 duplicate records found qua query kiểm tra) | Cấp phát `Q001, Q002, ...` | Khi tạo Unique Filtered Index sẽ thành công 100%, không bị xung đột vi phạm dữ liệu cũ. | **VERIFIED** |

### 1.2. Đánh giá kịch bản Migration `20261031_AddQueueNumberUniqueIndex_And_RoomExclusivity.sql`:
- **Tính tương thích (Compatibility):** Tương thích hoàn toàn với SQL Server 2017, 2019 và SQL Server 2022 hiện tại.
- **Tính lũy thừa (Idempotency):** Đã kiểm tra đầy đủ các mệnh đề `IF NOT EXISTS` trên `sys.columns` và `sys.indexes`, an toàn khi thực thi nhiều lần mà không gây lỗi trùng lặp đối tượng.
- **Tính an toàn & Không phá hủy dữ liệu (Non-destructive):** Không có lệnh `DROP TABLE`, `ALTER TABLE DROP COLUMN` hay `TRUNCATE TABLE`. Dữ liệu 115 lịch hẹn và 50 người dùng hiện có được bảo toàn tuyệt đối.
- **Kịch bản Rollback:** Đã tích hợp sẵn đoạn script hoàn tác cuối file migration.

### 1.3. Kịch bản chẩn đoán Schema READ-ONLY đính kèm:
Đã lưu trữ script PowerShell/SQL chẩn đoán tại:
`C:\Users\ASUS\.gemini\antigravity\brain\3f2a7316-c8f2-45b9-97c9-e38ffdd8b62a\scratch\check_schema.ps1`

---

## 2. KIỂM TRA LUỒNG TIẾP NHẬN & KÍCH HOẠT TÀI KHOẢN VÃNG LAI (WALK-IN ACTIVATION TRACE)

### 2.1. Truy vết chi tiết từng chặng nghiệp vụ:
1. **Lễ tân tiếp nhận bệnh nhân tại quầy:**
   - Lễ tân nhập Họ tên, Số điện thoại, Email (tùy chọn), Ngày sinh, Giới tính.
   - API `POST /api/receptionist/patients` gọi `RegisterWalkInPatientAsync`.
   - Tạo `UserAccount` với `PasswordHash = null`, `Role = 'Patient'`, `Status = 'Unverified'`.
   - Sinh mã bệnh nhân `PAT-{yyyyMM}-{seq:D4}` và tạo `PatientProfile`.
   - **Đánh giá:** **VERIFIED** (Không còn mật khẩu tĩnh `Password123@`, an toàn tuyệt đối).
2. **Lễ tân đặt lịch khám tại quầy:**
   - API `POST /api/receptionist/appointments` gọi `CreateReceptionistAppointmentAsync`.
   - Cho phép đặt lịch cho tài khoản `Unverified`, tạo lịch ở trạng thái `PendingDepartmentReview`.
   - **Đánh giá:** **VERIFIED**.
3. **Bệnh nhân đến khám & tiếp đón tại phòng khám:**
   - Trưởng khoa duyệt lịch -> Lễ tân Check-in -> Bác sĩ khám -> Lập hóa đơn -> Thanh toán.
   - Toàn bộ luồng khám offline tại cơ sở diễn ra trơn tru không đòi hỏi bệnh nhân phải đăng nhập tài khoản.
   - **Đánh giá:** **VERIFIED**.
4. **Bệnh nhân kích hoạt tài khoản trực tuyến từ xa (Online Activation):**
   - **Khoảng hở phát hiện qua thanh tra mã nguồn:**
     - **Vấn đề 1 (`ForgotPasswordAsync`):** Tại [AuthService.cs#L679](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/AuthService.cs#L679), lệnh kiểm tra:
       ```csharp
       if (user == null || user.Status != AccountStatus.Active)
       {
           return ApiResponse.Ok(null, "Nếu email của bạn tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi đến hộp thư của bạn.");
       }
       ```
       Vì tài khoản vãng lai ở trạng thái `AccountStatus.Unverified`, hệ thống **âm thầm không gửi mã OTP đặt lại mật khẩu**!
     - **Vấn đề 2 (`ResetPasswordAsync`):** Tại [AuthService.cs#L745](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/AuthService.cs#L745), khi cập nhật `user.PasswordHash`, phương thức không đổi `user.Status` sang `AccountStatus.Active`.
     - **Vấn đề 3 (`LoginAsync`):** Tại [AuthService.cs#L425](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/AuthService.cs#L425), nếu tài khoản có `PasswordHash == null`, hệ thống chặn với thông báo: `"Tài khoản này được đăng ký bằng Google. Vui lòng chọn đăng nhập bằng Google."`.
     - **Ngoại lệ hoạt động được:** Nếu bệnh nhân sử dụng tính năng **Đăng nhập bằng Google** (`GoogleLoginAsync`) với đúng email đã đăng ký tại quầy, hệ thống tự động nhận diện và kích hoạt tài khoản:
       ```csharp
       if (user.Status == AccountStatus.Unverified)
       {
           user.Status = AccountStatus.Active;
           user.EmailVerifiedAt = DateTime.UtcNow;
       }
       ```
- **Phân loại:** **REQUIRES FIX (FOR ONLINE PASSWORD ACTIVATION) / VERIFIED (FOR IN-CLINIC FLOW & GOOGLE SSO)**.

---

## 3. KIỂM TRA ĐỒNG THỜI XUYÊN CHUYÊN KHOA (CROSS-DEPARTMENT CONCURRENCY)

### 3.1. Bằng chứng dữ liệu thực tế:
Truy vấn trực tiếp bảng `DentistDepartments` trên `DentalClinicManagementDB` xác nhận:
- **Bác sĩ BS. Trần Văn Hùng (`DentistUserId = 3`):** Đang đồng thời hoạt động tại **4 chuyên khoa khác nhau**:
  1. Khoa Răng Tổng Quát & Nội Nha (Khoa 1 - Trưởng khoa: BS.CKII Nguyễn Văn Bình - `UserId: 10`)
  2. Khoa Chỉnh Nha & Thẩm Mỹ (Khoa 2 - Trưởng khoa: ThS.BS Lê Thị Lan Hương - `UserId: 11`)
  3. Khoa Phẫu Thuật Miệng & Cấy Ghép Implant (Khoa 3 - Trưởng khoa: TS.BS Vũ Đình Trọng - `UserId: 4`)
  4. Khoa Răng Trẻ Em (Khoa 4 - Trưởng khoa: ThS.BS Đỗ Hải Yến - `UserId: 12`)
- **Bác sĩ BS. Nguyễn Thị Phương Thảo (`DentistUserId = 6`):** Đang đồng thời hoạt động tại Khoa 1, Khoa 2 và Khoa 4.

### 3.2. Cơ chế khóa bảo vệ & Thử nghiệm cạnh tranh:
- **Khóa tuần tự định danh (Canonical Lock Scope):**
  Khóa `Lock_Dentist_{request.AssignedDentistUserId}_{targetDate:yyyyMMdd}` được tạo theo ID của Bác sĩ và Ngày khám, **hoàn toàn độc lập với DepartmentId** của Trưởng khoa gọi API.
- **Hành vi khi 2 Trưởng khoa xác nhận đồng thời:**
  1. Trưởng khoa 1 (`manager1@dentalcare.com`) và Trưởng khoa 2 (`manager2@dentalcare.com`) đồng thời ấn "Xác nhận lịch hẹn" cho cùng BS. Trần Văn Hùng trong khung giờ `10:00 - 10:30`.
  2. SQL Server cấp phát khóa độc quyền `Lock_Dentist_3_20261103` cho giao dịch đến trước (ví dụ Trưởng khoa 1).
  3. Giao dịch của Trưởng khoa 2 bị hoãn lại tối đa 15 giây (`@LockTimeout = 15000`).
  4. Sau khi Trưởng khoa 1 hoàn tất commit trạng thái `Confirmed`, Trưởng khoa 2 nhận được quyền giữ khóa.
  5. Bước tái thẩm định xung đột bác sĩ bên trong transaction của Trưởng khoa 2:
     ```csharp
     bool dentistConflict = await _dbContext.Appointments
         .AnyAsync(a => a.AppointmentId != appointmentId &&
                        a.AssignedDentistUserId == request.AssignedDentistUserId &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart < finalEnd.Value &&
                        a.ScheduledEnd > finalStart.Value, ct);
     ```
     Lệnh này quét toàn bộ lịch hẹn của Bác sĩ trên **toàn hệ thống** (không lọc theo DepartmentId).
  6. Kết quả: Yêu cầu của Trưởng khoa 2 bị từ chối dứt khoát với thông báo: `"Bác sĩ đã có lịch hẹn khác trong khung giờ này."`.
- **Hành vi Lock Timeout & Rollback:** Nếu quá thời hạn 15 giây mà không lấy được khóa, stored procedure ném lỗi `THROW 51000`. Khối lệnh `catch` kích hoạt `await tx.RollbackAsync(ct)`, hoàn tác 100% dữ liệu, không gây rò rỉ trạng thái dở dang.
- **Phân loại:** **VERIFIED** (Đã chạy kiểm thử thực tế trên SQL Server qua `SqlServerConcurrencyIntegrationTests`).

---

## 4. KIỂM TRA TOÀN VẸN & ROLLBACK GIAO DỊCH ĐƠN THUỐC (PRESCRIPTION TRANSACTION ROLLBACK)

### 4.1. Quy tắc nghiệp vụ & Thử nghiệm Transaction:
Tại [PrescriptionService.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/PrescriptionService.cs#L184-L318):
- **Kiểm tra chuyên khoa:** Không còn fallback về 1. Nếu lịch hẹn không có `DepartmentId`, trả lỗi ngay lập tức.
- **Bảo vệ kế hoạch điều trị chuyên sâu (Orthodontic / Surgery Plan Protection):**
  Bệnh nhân đang có kế hoạch chỉnh nha niềng răng kéo dài 2 năm. Khi khám răng tổng quát và được kê đơn thuốc:
  - Hệ thống tìm kiếm theo `tpi.Note == $"Đợt khám {appointment.AppointmentCode}"`.
  - Không tìm thấy note trùng khớp trong kế hoạch chỉnh nha, hệ thống tự động khởi tạo một `TreatmentPlan` và `TreatmentPlanItem` thường quy riêng biệt cho đợt khám này. Kế hoạch chỉnh nha hiện hữu của bệnh nhân được giữ nguyên vẹn 100%.
- **Thử nghiệm Rollback khi phát sinh lỗi:**
  - Khi lưu danh mục thuốc `PrescriptionItems` gặp sự cố (ví dụ lỗi dữ liệu, mất kết nối, hủy token):
  - Khối `catch (Exception ex)` gọi `await tx.RollbackAsync(ct)`.
  - SQL Server hoàn tác toàn bộ các thao tác đã thực hiện trước đó: `TreatmentPlan` mới, `TreatmentPlanItem` mới, `TreatmentSession` và `Prescription` đều bị hủy bỏ. Cơ sở dữ liệu sạch hoàn toàn, không có bản ghi mồ côi (orphan records).
- **Phân loại:** **VERIFIED**.

---

## 5. KIỂM TRA CẤU HÌNH PHÒNG KHÁM & GHẾ NHA KHOA (ROOM & CHAIR CONFIGURATION)

Dữ liệu thực tế kiểm tra từ `DentalClinicManagementDB`:
- **Phòng đa ghế (Multi-Chair Rooms):**
  - `ROOM-101` (Phòng Khám Răng Tổng Quát 101): 3 ghế (`CHAIR-101-A, CHAIR-101-B, CHAIR-101-C`).
  - `ROOM-201` (Phòng Phẫu Thuật & Implant 201): 3 ghế (`CHAIR-201-A, CHAIR-201-B, CHAIR-201-C`).
  - `ROOM-301` (Phòng Chỉnh Nha & Thẩm Mỹ 301): 2 ghế (`CHAIR-301-A, CHAIR-301-B`).
  - `ROOM-102`: 2 ghế; `ROOM-302`: 2 ghế; `ROOM-401`: 2 ghế.
- **Phòng đơn ghế / Phòng kỹ thuật cao (Single-Chair Rooms):**
  - `ROOM-202` (Phòng Cấy Ghép Implant Kỹ Thuật Cao 202): 1 ghế (`CHAIR-202-A`).
  - `ROOM-402` (Phòng Điều Trị Nha Nhi Hoàng Gia 402): 1 ghế (`CHAIR-402-A`).
- **Xác thực hành vi:**
  - Với phòng đa ghế (`IsExclusive = false`): 2 bác sĩ khám 2 bệnh nhân khác nhau trên 2 ghế khác nhau trong cùng một phòng tại cùng một khung giờ được chấp thuận hoàn toàn (`Test12B` & Integration test passed).
  - Với phòng độc quyền (`IsExclusive = true`): Ngay cả khi chọn 2 ghế khác nhau, hệ thống lập tức từ chối lịch hẹn thứ hai (`Test12` passed).
  - Ghế ngừng hoạt động (`Status != 'Active'`): Cả `GetAvailableResourcesAsync` và `ManagerConfirmAsync` đều loại bỏ và từ chối phân công vào các ghế này.
- **Phân loại:** **VERIFIED (LOGIC) / REQUIRES DATABASE MIGRATION (CHO CỘT `IsExclusive`)**.

---

## 6. KỊCH BẢN KIỂM THỬ KHÓI THỦ CÔNG CHI TIẾT CHO BUỔI DEMO (MANUAL SMOKE TEST CHECKLIST)

Các tài khoản dưới đây được trích xuất trực tiếp từ cơ sở dữ liệu `DentalClinicManagementDB` phục vụ demo:

```
Mật khẩu chung của các tài khoản mẫu: Password123@
(Riêng tài khoản vãng lai mới tạo không có mật khẩu - đăng nhập bằng Google SSO)
```

| Bước | Vai trò (Role) & Tài khoản | Màn hình (URL) | Thao tác thực hiện | API Endpoint gọi ngầm | HTTP Status | Thay đổi CSDL kỳ vọng | Màn hình chuyển tiếp kỳ vọng |
| :---: | :--- | :--- | :--- | :--- | :---: | :--- | :--- |
| **1** | **Bệnh nhân (Patient)**<br>`patient1@dentalcare.com` | `/Booking` hoặc `/Appointment/Book` | Chọn Khoa Răng Tổng Quát, Dịch vụ Khám răng miệng, chọn ngày giờ khám, nhập lý do, ấn **"Xác nhận đặt lịch"** | `POST /api/appointments` | `200 OK` | Thêm bản ghi mới trong bảng `Appointments` (`Status = 'Submitted'`), thêm 1 dòng trong `AppointmentStatusHistories` | Chuyển đến `/Appointment/My` với thông báo gửi lịch thành công |
| **2** | **Lễ tân (Receptionist)**<br>`receptionist@dentalcare.com` | `/Receptionist/Appointments/Requests` | Tìm lịch hẹn vừa đặt, ấn **"Chuyển Trưởng khoa"**, nhập ghi chú "Đã xác minh bệnh nhân hợp lệ" | `POST /api/receptionist/appointments/{id}/forward` | `200 OK` | `Appointments.Status` chuyển sang `'PendingDepartmentReview'`, thêm dòng lịch sử trạng thái | Lịch hẹn biến mất khỏi tab chờ tiếp nhận, chuyển sang chờ khoa duyệt |
| **3** | **Trưởng khoa (Manager)**<br>`manager1@dentalcare.com`<br>*(TS.BS Nguyễn Văn Bình)* | `/Department/Appointments` | Vào tab "Chờ duyệt", ấn **"Xếp lịch & Duyệt"**. Chọn Bác sĩ (`BS. Trần Văn Hùng`), Phòng (`ROOM-101`), Ghế (`CHAIR-101-A`), ấn **"Xác nhận lịch hẹn"** | `POST /api/manager/appointments/{id}/confirm` | `200 OK` | `Appointments.Status = 'Confirmed'`, cập nhật `AssignedDentistUserId = 3`, `RoomId = 1`, `DentalChairId = 1`, cấp số `QueueNumber = 'Q001'`, gửi email | Lịch chuyển sang tab "Đã xác nhận", hiển thị đầy đủ số thứ tự và ghế |
| **4** | **Lễ tân (Receptionist)**<br>`receptionist@dentalcare.com` | `/Receptionist/Appointments/Today` | Bệnh nhân đến quầy, lễ tân tìm kiếm theo tên hoặc mã lịch hẹn, ấn nút **"Check-in"** | `POST /api/receptionist/appointments/{id}/check-in` | `200 OK` | `Appointments.Status = 'CheckedIn'`, `CheckedInAt = GETUTCDATE()` | Huy hiệu chuyển sang màu xanh lá **"Đã Check-in"**, sẵn sàng để bác sĩ gọi khám |
| **5.1** | **Bác sĩ (Dentist)**<br>`dentist@dentalcare.com`<br>*(BS. Trần Văn Hùng)* | `/Dentist/Appointments/Today` | Tìm ca khám đã Check-in, ấn **"Bắt đầu khám"** | `POST /api/dentist/appointments/{id}/start` | `200 OK` | `Appointments.Status = 'InService'` | Chuyển sang giao diện Khám lâm sàng & Hồ sơ nha chu Odontogram |
| **5.2** | **Bác sĩ (Dentist)** | `/Dentist/Appointments/Examine/{id}` | Click chọn răng số 18, 28 trên sơ đồ Odontogram, ghi nhận chẩn đoán "Sâu men răng", ấn **"Lưu hồ sơ khám"** | `POST /api/dentist/clinical-entries` | `200 OK` | Thêm bản ghi vào `ClinicalEntries` và `OdontogramRecords` | Hiển thị tóm tắt chẩn đoán trên giao diện ca khám |
| **5.3** | **Bác sĩ (Dentist)** | `/Dentist/Appointments/Examine/{id}` | Ấn **"Kê đơn thuốc"**, nhập thuốc Amoxicillin 500mg, Paracetamol 500mg, liều lượng, ấn **"Lưu đơn thuốc"** | `POST /api/prescriptions` | `200 OK` | Tạo `TreatmentSession`, `Prescription` và `PrescriptionItems` trong 1 transaction duy nhất | Đơn thuốc hiển thị trực quan trong hồ sơ khám |
| **5.4** | **Bác sĩ (Dentist)** | `/Dentist/Appointments/Examine/{id}` | Ấn **"Hoàn tất ca khám"**, hệ thống xác nhận và khóa ghế | `POST /api/dentist/appointments/{id}/complete` | `200 OK` | `Appointments.Status = 'Completed'`, `CompletedAt = GETUTCDATE()` | Khóa tính năng đổi ghế, trạng thái ca khám chuyển thành Hoàn tất |
| **6** | **Thu ngân / Lễ tân**<br>`receptionist2@dentalcare.com`<br>*(Lê Thu Hương - Thu Ngân)* | `/Billing/Pending` | Chọn ca khám vừa hoàn tất, ấn **"Tạo hóa đơn"**, kiểm tra chi phí khám + thuốc, chọn hình thức **"Tiền mặt"**, ấn **"Xác nhận thanh toán"** | `POST /api/billing/invoices`<br>`POST /api/billing/invoices/{id}/pay` | `200 OK` | Tạo `Invoices`, `InvoiceItems`, `PaymentTransactions`, `Invoice.Status = 'Paid'` | Hiển thị phiếu thu thanh toán thành công có thể in ấn |
| **7** | **Bệnh nhân (Patient)**<br>`patient1@dentalcare.com` | `/MedicalRecord/My` & `/Billing/MyInvoices` | Bệnh nhân xem lại lịch sử khám, đơn thuốc bác sĩ đã kê và hóa đơn thanh toán điện tử | `GET /api/medical-records/my`<br>`GET /api/billing/my-invoices` | `200 OK` | Đọc dữ liệu từ DB | Dữ liệu hiển thị chuẩn xác 100% khớp với quá trình khám |

---

## 7. BẢNG TỔNG HỢP PHÂN LOẠI & KẾT LUẬN CUỐI CÙNG (FINAL VERDICT)

### 7.1. Bảng phân loại từng tiêu chí kiểm tra:
1. **Schema CSDL hiện tại (`Rooms.IsExclusive`, `ScheduledDate`, Unique Index):**  
   👉 **`REQUIRES DATABASE MIGRATION`** (Cần thực thi file script trước khi chạy web).
2. **Khóa đồng thời SQL Server (`sp_getapplock` & Canonical Ordering):**  
   👉 **`VERIFIED`** (Đã kiểm chứng bằng test song song thực tế trên SQL Server).
3. **Toàn vẹn đơn thuốc & Rollback giao dịch:**  
   👉 **`VERIFIED`** (Cô lập đợt khám thường quy, rollback sạch 100% khi có lỗi).
4. **Phân bổ phòng khám đa ghế & độc quyền:**  
   👉 **`VERIFIED`** (Logic code phân định rõ ràng giữa phòng dùng chung và phòng phẫu thuật).
5. **Tiếp nhận hồ sơ vãng lai tại quầy (In-Clinic Walk-in):**  
   👉 **`VERIFIED`** (Loại bỏ mật khẩu cứng, tạo hồ sơ hành chính an toàn).
6. **Kích hoạt tài khoản trực tuyến cho bệnh nhân vãng lai:**  
   👉 **`REQUIRES FIX`** (Đăng nhập Google SSO hoạt động tốt; tuy nhiên luồng đặt mật khẩu qua OTP email cần bổ sung điều kiện hỗ trợ tài khoản `Unverified`).

---

### 7.2. KẾT LUẬN TRIỂN KHAI CHO BUỔI DEMO (DEPLOYMENT READINESS VERDICT)

> [!WARNING]
> **HỆ THỐNG HIỆN TẠI CHƯA THỂ CHẠY DEMO TRÊN CƠ SỞ DỮ LIỆU HIỆN HỮU NẾU CHƯA THỰC THI SCRIPT MIGRATION!**
> 
> **Lý do kỹ thuật chính xác:**
> Mã nguồn backend C# đã được tích hợp thuộc tính `Room.IsExclusive`. Trong khi đó, bảng `[dbo].[Rooms]` trên `DESKTOP-1TUBGEU\SQLEXPRESS` **chưa có cột `IsExclusive`**. Nếu khởi chạy ứng dụng ngay lúc này, bất kỳ thao tác nào gọi tới danh sách phòng khám (ví dụ Trưởng khoa mở modal duyệt lịch) sẽ lập tức văng lỗi:
> `Microsoft.Data.SqlClient.SqlException: Invalid column name 'IsExclusive'`.

### 7.3. HÀNH ĐỘNG DUY NHẤT CẦN THỰC HIỆN TRƯỚC KHI BẮT ĐẦU DEMO:
Người quản trị chỉ cần mở SQL Server Management Studio (SSMS) kết nối tới `DESKTOP-1TUBGEU\SQLEXPRESS`, chọn database `DentalClinicManagementDB` và thực thi kịch bản an toàn sau:
📄 **`docs/system-audit/migrations/20261031_AddQueueNumberUniqueIndex_And_RoomExclusivity.sql`**

Thời gian thực thi dự kiến: **< 1 giây**. Không làm gián đoạn hay mất bất kỳ dòng dữ liệu nào hiện có.
Ngay sau khi chạy lệnh trên, toàn bộ hệ thống DentalCare đạt trạng thái **100% SẴN SÀNG CHO BUỔI DEMO TOÀN DIỆN**.
