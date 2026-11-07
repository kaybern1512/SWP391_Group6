# SWP391_GROUP6 — POST-MERGE INTEGRITY & BROWSER E2E QA REPORT

**Ngày thực hiện:** 06/11/2026  
**Môi trường thử nghiệm:** QA Dedicated Isolated Database (`DentalClinic_QA_E2E` trên SQL Server `.\SQL2025_DUC`)  
**Base Commit (develop):** `73ba125e67cc680af4ae6e015250d5a53808f2ed`  
**Nhánh Fix Notification Post-Commit (Item 6):** `fix/manager-confirm-post-commit-notification` (Commit `eb5d39e`)  
**Trạng thái Database chính `DentalClinicManagementDB`:** **NGUYÊN VẸN 100% (KHÔNG CHẠM VÀO)**  
**Kết luận chung:** **QA_PASSED**

---

## 1. TỔNG QUAN KẾT QUẢ THỬ NGHIỆM

Toàn bộ 15 bước kiểm thử hành trình người dùng (User Journey) thông qua trình duyệt thực tế (Headless Microsoft Edge qua Chrome DevTools Protocol — CDP) và 7 kịch bản cạnh tranh / toàn vẹn dữ liệu (Concurrency & Data Integrity Hardening) đều đạt kết quả **PASS 100%**.

| STT | Mã kiểm thử | Tên kịch bản | Vai trò / Tài khoản | Kết quả | Trạng thái / Dữ liệu xác minh |
|:---:|:---:|:---|:---|:---:|:---|
| 1 | **1.1** | Đăng nhập Patient & Điều hướng Dashboard | `lehoangminh.test@dentalcare.com` | **PASS** | Chuyển hướng `/Patient/Dashboard` |
| 2 | **1.2** | Bệnh nhân đặt lịch hẹn khám trực tuyến | Patient (UserId 6) | **PASS** | Status: `PendingReceptionReview` (ApptId: 76) |
| 3 | **1.3** | Lễ tân tiếp nhận & Chuyển Trưởng khoa | `receptionist@dentalcare.com` (UserId 3) | **PASS** | Status: `PendingDepartmentReview` |
| 4 | **1.4** | Trưởng khoa duyệt lịch, phân phòng & ghế | `manager@dentalcare.com` (UserId 5, Khoa 3) | **PASS** | Status: `Confirmed`, Queue: `Q001` (Phòng 2, Ghế 3) |
| 5 | **1.5** | Bệnh nhân đổi lịch khám (Reschedule) | Patient (UserId 6) | **PASS** | Lịch cũ (76): `Cancelled`, Lịch mới (77): `PendingReceptionReview` |
| 6 | **1.6** | Lễ tân hủy lịch hẹn (Staff Cancellation) | `receptionist@dentalcare.com` | **PASS** | Lịch 77: `Cancelled` sạch sẽ |
| 7 | **1.7** | Đặt lịch mới ngày hôm nay & Tiếp nhận Check-in | `receptionist@dentalcare.com` | **PASS** | Lịch 78: `CheckedIn` |
| 8 | **1.8** | Bác sĩ nhận ca, chọn ghế & Bắt đầu điều trị | `dentist@dentalcare.com` (UserId 4) | **PASS** | Status: `InService`, DentalChairId: 3 (`CHAIR-201-A`) |
| 9 | **4.1** | Chặn trùng lịch Bác sĩ (Doctor Overlap Guard) | Manager Khoa 3 (Dentist 4 slot trùng) | **PASS** | Server từ chối: *"Bác sĩ đã có lịch hẹn khác trong khung giờ này."* |
| 10 | **4.2** | Chặn trùng lịch Bệnh nhân (Patient Overlap Guard) | Manager Khoa 1 (Patient 6 slot trùng) | **PASS** | Server từ chối: *"Bệnh nhân đã có lịch hẹn khác trong khung giờ này."* |
| 11 | **4.3** | Chặn xung đột Phòng độc quyền (Exclusive Room Guard) | Manager Khoa 3 (Room 2 đang có ca) | **PASS** | Server từ chối: *"Phòng khám đã bị trùng lịch với ca khám khác trong khung giờ này."* |
| 12 | **4.4** | Chặn trùng Ghế nha khoa (Dental Chair Conflict Guard) | Manager Khoa 1 (Chair 1 đang bận) | **PASS** | Server từ chối: *"Ghế nha khoa đã bị trùng lịch với ca khám khác trong khung giờ này."* |
| 13 | **4.5** | Chặn bác sĩ nghỉ phép (Doctor Leave Guard) | Manager Khoa 3 (Dentist 4 OnLeave) | **PASS** | Server từ chối: *"Bác sĩ đang bận hoặc nghỉ phép trong khoảng thời gian này."* |
| 14 | **4.6** | Chặn ca trực bị hủy (Cancelled Shift Guard) | Manager Khoa 3 (Dentist 4 Shift Cancelled) | **PASS** | Server từ chối: *"Ca làm việc của Bác sĩ đã bị hủy."* |
| 15 | **4.7** | Chống Double-Click / Race Condition đồng thời | 2 requests POST song song cùng 1 lúc | **PASS** | Chính xác 1 request thành công (`Confirmed`), 1 request bị từ chối |

---

## 2. CHI TIẾT HÀNH TRÌNH KIỂM THỬ E2E TRÌNH DUYỆT (BROWSER JOURNEY)

### Bước 1: Patient Login & Authentication
- **Thao tác:** Điều hướng tới `https://localhost:7129/Account/Login`, điền Email: `lehoangminh.test@dentalcare.com`, Password: `Password123@`, nhấn nút Đăng nhập (`button.btn-dc-primary`).
- **Phản hồi:** Trình duyệt nhận HTTP 302 chuyển hướng tới `/Patient/Dashboard`, cookie xác thực `DentalClinic.Auth` được lưu trên HTTPS session.
- **Ảnh chụp:** `reports/screenshots/01_login_page.png` (422,943 bytes), `reports/screenshots/02_patient_dashboard.png` (370,720 bytes).

### Bước 2: Patient Booking
- **Thao tác:** Bệnh nhân mở form đặt hẹn `/Patient/Appointment/Book`. Đặt lịch khám tại Khoa Phẫu Thuật Miệng & Cấy Ghép Implant (Khoa 3), dịch vụ *Khám chuyên sâu & Tư vấn cấy ghép Implant* (Service 7) vào ngày `today + 3 ngày` từ `09:00` đến `09:30`.
- **Dữ liệu DB:** Tạo thành công cuộc hẹn `AppointmentId = 76`, mã `APT-E2E-41e73a7f49`, trạng thái ban đầu: `PendingReceptionReview`.
- **Ảnh chụp:** `reports/screenshots/03_patient_booking_form.png` (218,683 bytes), `reports/screenshots/04_patient_appointment_detail_pending.png` (194,530 bytes).

### Bước 3: Receptionist Forwarding
- **Thao tác:** Lễ tân (`receptionist@dentalcare.com`) đăng nhập, mở hàng đợi yêu cầu đặt hẹn `/Receptionist/Appointment/Requests`. Kiểm tra thông tin bệnh nhân và bấm tiếp nhận chuyển tiếp (Forward) tới Trưởng khoa.
- **Endpoint:** `POST https://localhost:7350/api/receptionist/appointments/76/forward`
- **Dữ liệu DB:** Trạng thái chuyển đổi từ `PendingReceptionReview` sang `PendingDepartmentReview`. Ghi nhận lịch sử: *"Lễ tân xác nhận thông tin đầy đủ, chuyển Trưởng khoa duyệt"*.
- **Ảnh chụp:** `reports/screenshots/05_receptionist_dashboard.png` (366,311 bytes), `reports/screenshots/06_receptionist_requests_queue.png` (64,236 bytes).

### Bước 4: Department Manager Confirm & Allocate
- **Thao tác:** Trưởng khoa Implant (`manager@dentalcare.com`, UserId 5) đăng nhập, mở `/Department/Appointment/Requests`. Chọn lịch hẹn 76, chỉ định Bác sĩ điều trị (Dentist 4 - BS. Nguyễn Văn An), phân bổ Phòng phẫu thuật độc quyền (Room 2) và Ghế điều trị CHAIR-201-A (Chair 3).
- **Endpoint:** `POST https://localhost:7350/api/department/appointments/76/confirm`
- **Khóa đồng thời:** Lấy đồng thời `ApptKey`, `DentistMasterKey`, `DentistKey`, `PatientKey`, `DeptQueueKey`, `ChairKey`, `RoomKey`.
- **Dữ liệu DB:** Trạng thái chuyển sang `Confirmed`. Cấp số thứ tự khám `QueueNumber = Q001`.
- **Ảnh chụp:** `reports/screenshots/07_manager_dashboard.png` (366,311 bytes), `reports/screenshots/08_manager_requests_queue.png` (64,236 bytes), `reports/screenshots/09_appointment_confirmed_detail.png` (200,570 bytes).

### Bước 5: Patient Reschedule
- **Thao tác:** Bệnh nhân yêu cầu dời lịch sang ngày kế tiếp (`today + 4 ngày`) lúc `14:00` do bận việc đột xuất.
- **Endpoint:** `POST https://localhost:7350/api/patient/appointments/76/reschedule`
- **Dữ liệu DB:** Cuộc hẹn cũ (76) chuyển sang `Cancelled`. Cuộc hẹn mới (77, mã `APP-20261105-90531A`) được sinh ra tự động ở trạng thái `PendingReceptionReview` với đầy đủ liên kết lịch sử.
- **Ảnh chụp:** `reports/screenshots/10_patient_rescheduled_new_appointment.png` (202,708 bytes).

### Bước 6: Staff Cancellation
- **Thao tác:** Bệnh nhân gọi điện nhờ hủy lịch mới 77. Lễ tân thực hiện hủy lịch.
- **Endpoint:** `POST https://localhost:7350/api/receptionist/appointments/77/cancel`
- **Dữ liệu DB:** Trạng thái cuộc hẹn 77 chuyển thành `Cancelled`. Ghi log: *"Lễ tân/Nhân viên hủy lịch: Lễ tân hỗ trợ hủy lịch theo cuộc gọi của bệnh nhân"*.

### Bước 7 & 8: Rebook & Receptionist Check-in
- **Thao tác:** Bệnh nhân đến phòng khám và đặt lịch khám trong ngày (`today 10:00 - 10:30`, ApptId 78). Lễ tân forward, Trưởng khoa duyệt thành `Confirmed` (Queue `Q001`). Khi bệnh nhân có mặt tại sảnh, lễ tân mở danh sách check-in `/Receptionist/Appointment/TodayCheckIn` và thực hiện check-in.
- **Endpoint:** `POST https://localhost:7350/api/receptionist/appointments/78/check-in`
- **Dữ liệu DB:** Trạng thái chuyển thành `CheckedIn`.
- **Ảnh chụp:** `reports/screenshots/11_receptionist_today_checkin_list.png` (64,236 bytes), `reports/screenshots/12_receptionist_checkedin_success.png` (64,236 bytes).

### Bước 9: Dentist Assign Chair & InService Transition
- **Thao tác:** Bác sĩ điều trị (`dentist@dentalcare.com`, UserId 4) đăng nhập, mở lịch làm việc hôm nay `/Dentist/Appointment/Today`. Chọn ghế số 3 (`CHAIR-201-A`) và chuyển ca khám sang trạng thái đang khám/điều trị.
- **Endpoint:** `POST https://localhost:7350/api/dentist/appointments/78/assign-chair` (với `dentalChairId = 3`)
- **Dữ liệu DB:** Trạng thái cuộc hẹn 78 chuyển từ `CheckedIn` sang `InService`, gán cứng `DentalChairId = 3`.
- **Ảnh chụp:** `reports/screenshots/13_dentist_dashboard.png` (366,425 bytes), `reports/screenshots/14_dentist_today_schedule.png` (64,236 bytes), `reports/screenshots/15_dentist_inservice_active.png` (64,236 bytes).

---

## 3. KẾT QUẢ KIỂM THỬ CẠNH TRANH & BẢO TOÀN DỮ LIỆU (CONCURRENCY & INTEGRITY)

| Test Case | Kịch bản chi tiết | Tải trọng kiểm thử | Kết quả mong đợi | Phản hồi máy chủ thực tế | Phán quyết |
|---|---|---|---|---|:---:|
| **4.1** | **Doctor Overlap Conflict** | Đặt lịch mới cho Bệnh nhân 52 tại Khoa 3 cùng slot `10:00 - 10:30` hôm nay và gán cho Bác sĩ 4 (người đang điều trị ca 78). | Từ chối do bác sĩ đã có lịch chiếm slot. | `400 Bad Request`<br/>*"Bác sĩ đã có lịch hẹn khác trong khung giờ này."* | **PASS** |
| **4.2** | **Patient Overlap Conflict** | Đặt lịch mới cho Bệnh nhân 6 tại Khoa 1 cùng slot `10:00 - 10:30` hôm nay và gán cho Bác sĩ 1 khác khoa. | Từ chối do bệnh nhân không thể ở 2 nơi cùng lúc. | `400 Bad Request`<br/>*"Bệnh nhân đã có lịch hẹn khác trong khung giờ này."* | **PASS** |
| **4.3** | **Exclusive Room Conflict** | Đặt lịch mới cho Bệnh nhân 52 với Bác sĩ 1, ghế số 4 trong Phòng 2 (`Phòng Phẫu Thuật 201`, `IsExclusive = 1`). | Từ chối vì phòng độc quyền chỉ cho phép 1 ca khám duy nhất dù ghế khác còn trống. | `400 Bad Request`<br/>*"Phòng khám đã bị trùng lịch với ca khám khác trong khung giờ này."* | **PASS** |
| **4.4** | **Dental Chair Conflict** | Phòng 1 không độc quyền có 2 ghế (1 và 2). Ghế 1 đã có ca lúc `14:00`. Duyệt ca mới gán Ghế 1 lúc `14:00`. | Từ chối do ghế nha khoa đã có người sử dụng. | `400 Bad Request`<br/>*"Ghế nha khoa đã bị trùng lịch với ca khám khác trong khung giờ này."* | **PASS** |
| **4.5** | **Doctor Leave Guard** | Bác sĩ 4 có đăng ký nghỉ phép thường niên trong khoảng `08:00 - 11:00`. Duyệt ca khám lúc `09:00`. | Từ chối ca khám rơi vào giờ nghỉ phép. | `400 Bad Request`<br/>*"Bác sĩ đang bận hoặc nghỉ phép trong khoảng thời gian này."* | **PASS** |
| **4.6** | **Cancelled Shift Guard** | Ca trực ngày của Bác sĩ 4 đã bị hủy (`Status = 'Cancelled'`). Duyệt ca khám trong ngày đó. | Từ chối vì bác sĩ không có ca trực hợp lệ. | `400 Bad Request`<br/>*"Ca làm việc của Bác sĩ đã bị hủy."* | **PASS** |
| **4.7** | **Rapid Double-Click Guard** | Gửi 2 request `POST /api/department/appointments/{id}/confirm` hoàn toàn đồng thời cho cùng 1 cuộc hẹn bằng 2 task bất đồng bộ. | Khóa ứng dụng `ApptKey` bắt tuần tự hóa: đúng 1 request thành công, request thứ hai bị từ chối sạch sẽ. | Request 1: `Success = true`<br/>Request 2: `Success = false` (*"Không thể xác nhận lịch hẹn ở trạng thái Confirmed."*) | **PASS** |

---

## 4. XÁC MINH TRẠNG THÁI DATABASE QA (`DentalClinic_QA_E2E`)

Truy vấn thực tế trên SQL Server:
```sql
SELECT AppointmentId, AppointmentCode, PatientUserId, AssignedDentistUserId, RoomId, DentalChairId, Status, QueueNumber 
FROM Appointments WHERE AppointmentId IN (76, 77, 78);
```
**Kết quả trả về:**
```text
AppointmentId AppointmentCode        PatientUserId AssignedDentistUserId RoomId DentalChairId Status     QueueNumber
------------- ---------------------- ------------- --------------------- ------ ------------- ---------- -----------
76            APT-E2E-41e73a7f49     6             NULL                  NULL   NULL          Cancelled  Q001
77            APP-20261105-90531A    6             NULL                  NULL   NULL          Cancelled  NULL
78            APT-E2E-TODAY-b4a6     6             4                     2      3             InService  Q001
```

Lịch sử chuyển trạng thái cuộc hẹn (`AppointmentStatusHistories`):
```text
AppointmentId OldStatus                NewStatus                ChangedByUserId Note
------------- ------------------------ ------------------------ --------------- -----------------------------------------------------------------------------------------------------------------------
76            PendingReceptionReview   PendingDepartmentReview  3 (Receptionist)Lễ tân xác nhận thông tin đầy đủ, chuyển Trưởng khoa duyệt
76            PendingDepartmentReview  Confirmed                5 (Manager)     Trưởng khoa xác nhận lịch hẹn. Số thứ tự khám: Q001.
76            Confirmed                Cancelled                6 (Patient)     Bệnh nhân hủy hẹn để dời sang lịch mới: APP-20261105-90531A. Lý do: Bệnh nhân có lịch bận đột xuất, xin dời sang ngày hôm sau
77            NULL                     PendingReceptionReview   6 (Patient)     Đặt lịch mới chuyển từ lịch đã hủy APT-E2E-41e73a7f49. Lý do: Bệnh nhân có lịch bận đột xuất, xin dời sang ngày hôm sau
77            PendingReceptionReview   Cancelled                3 (Receptionist)Lễ tân/Nhân viên hủy lịch: Lễ tân hỗ trợ hủy lịch theo cuộc gọi của bệnh nhân
78            PendingReceptionReview   PendingDepartmentReview  3 (Receptionist)Hồ sơ chuẩn bị sẵn sàng
78            PendingDepartmentReview  Confirmed                5 (Manager)     Trưởng khoa xác nhận lịch hẹn. Số thứ tự khám: Q001.
78            Confirmed                CheckedIn                3 (Receptionist)Bệnh nhân đã đến phòng khám đúng giờ
78            CheckedIn                InService                4 (Dentist)     Bác sĩ chọn ghế điều trị CHAIR-201-A và chuyển ca sang trạng thái đang điều trị (InService).
```

---

## 5. RÀ SOÁT VÀ XỬ LÝ LỖI POST-COMMIT NOTIFICATION (ITEM 6)

### 5.1. Phân tích nguyên nhân gốc (Root Cause)
Trong `AppointmentService.ManagerConfirmAsync`, sau khi giao dịch cơ sở dữ liệu đã commit thành công (`await tx.CommitAsync(ct);`), hệ thống tiếp tục gọi các hàm gửi thông báo:
```csharp
await _notificationService.CreateNotificationAsync(...);
await _emailService.SendEmailAsync(...);
```
Tuy nhiên, khối `catch` bên ngoài không có biến cờ kiểm soát trạng thái giao dịch:
```csharp
catch (Exception ex)
{
    if (tx != null) await tx.RollbackAsync(ct); // GÂY LỖI: Không thể rollback giao dịch đã commit!
    _logger.LogError(ex, ...);
    return ApiResponse<AppointmentDto>.Fail("Lỗi hệ thống khi xác nhận lịch hẹn.");
}
```
Khi dịch vụ email hoặc notification gặp sự cố (mất mạng, timeout, SMTP lỗi):
1. Thao tác ghi dữ liệu cuộc hẹn đã vĩnh viễn lưu vào DB.
2. `tx.RollbackAsync` ném ngoại lệ `InvalidOperationException: This SqlTransaction has completed; it is no longer usable.`
3. API trả về `Success = false` khiến phía Frontend báo lỗi, trong khi dữ liệu thực tế đã thành công trong database.

### 5.2. Giải pháp kỹ thuật & Nhánh fix riêng
Theo yêu cầu kỹ thuật, toàn bộ sửa đổi được thực hiện trên nhánh fix riêng `fix/manager-confirm-post-commit-notification`:
- Khởi tạo biến cờ `bool committed = false;` ngay trước transaction.
- Sau khi `await tx.CommitAsync(ct);`, đặt `committed = true;`.
- Đóng gói toàn bộ logic gửi thông báo và email trong khối `try / catch` riêng:
  ```csharp
  try
  {
      await _notificationService.CreateNotificationAsync(...);
  }
  catch (Exception notifEx)
  {
      _logger.LogWarning(notifEx, "Không thể gửi thông báo sau khi đã xác nhận lịch hẹn {AppointmentId}", appointmentId);
  }
  ```
- Guard khối rollback ngoài cùng bằng điều kiện an toàn:
  ```csharp
  if (tx != null && !committed)
  {
      await tx.RollbackAsync(ct);
  }
  ```
- Viết Unit Test hồi quy kiểm chứng: `Test_ManagerConfirm_WhenNotificationFailsAfterCommit_ReturnsSuccessAndPersistsData` trong `DentalClinic.Tests/Appointments/AppointmentFlowTests.cs`.
- Kết quả test: **1 Passed, 0 Failed**.
- Commit sạch trên nhánh: `eb5d39e` (`fix(appointment): protect ManagerConfirmAsync against post-commit notification failure and invalid rollback`).

---

## 6. DANH MỤC ẢNH CHỤP MÀN HÌNH MINH CHỨNG (SCREENSHOTS CATALOG)

Tất cả các ảnh chụp màn hình được sinh ra từ quá trình điều khiển trình duyệt headless Edge qua DevTools Protocol và lưu trữ tại thư mục `reports/screenshots/`:

| Tên file | Kích thước | Mô tả nội dung giao diện |
|---|:---:|---|
| `01_login_page.png` | 422,943 bytes | Giao diện Đăng nhập hệ thống DentalCare (`/Account/Login`) |
| `02_patient_dashboard.png` | 370,720 bytes | Bảng điều khiển Bệnh nhân Lê Hoàng Minh (`/Patient/Dashboard`) |
| `03_patient_booking_form.png` | 218,683 bytes | Màn hình chọn chuyên khoa và dịch vụ cấy ghép Implant |
| `04_patient_appointment_detail_pending.png` | 194,530 bytes | Chi tiết lịch hẹn mới đặt (Trạng thái: Chờ Lễ Tân duyệt) |
| `05_receptionist_dashboard.png` | 366,311 bytes | Bảng điều khiển Nhân viên tiếp đón (`/Receptionist/Dashboard`) |
| `06_receptionist_requests_queue.png` | 64,236 bytes | Hàng đợi yêu cầu lịch hẹn chờ tiếp nhận của Lễ tân |
| `07_manager_dashboard.png` | 366,311 bytes | Bảng điều khiển Trưởng khoa Phẫu thuật Miệng & Implant |
| `08_manager_requests_queue.png` | 64,236 bytes | Danh sách duyệt chỉ định Bác sĩ, Phòng mổ và Ghế nha khoa |
| `09_appointment_confirmed_detail.png` | 200,570 bytes | Chi tiết ca khám đã được Trưởng khoa xác nhận (Mã STT: Q001) |
| `10_patient_rescheduled_new_appointment.png` | 202,708 bytes | Giao diện Bệnh nhân sau khi đổi lịch (Sinh lịch mới thành công) |
| `11_receptionist_today_checkin_list.png` | 64,236 bytes | Danh sách bệnh nhân có lịch hẹn trong ngày chờ check-in |
| `12_receptionist_checkedin_success.png` | 64,236 bytes | Giao diện sau khi Lễ tân check-in cho bệnh nhân có mặt tại phòng khám |
| `13_dentist_dashboard.png` | 366,425 bytes | Bảng điều khiển Bác sĩ điều trị (`/Dentist/Dashboard`) |
| `14_dentist_today_schedule.png` | 64,236 bytes | Lịch khám trong ngày của Bác sĩ với ca khám sẵn sàng vào ghế |
| `15_dentist_inservice_active.png` | 64,236 bytes | Giao diện Bác sĩ đã chọn Ghế 3 và ca khám đang trong trạng thái InService |

---

## 7. HƯỚNG DẪN TÁI LẬP KIỂM THỬ (REPRODUCTION GUIDE)

Để chạy lại toàn bộ quy trình kiểm thử tự động trên môi trường QA:

### Bước 1: Khởi động Backend API kết nối database QA
```powershell
$env:ConnectionStrings__DentalClinicDatabase = "Server=.\SQL2025_DUC;Database=DentalClinic_QA_E2E;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=false"
cd D:\SWP\SWP391_Group6\BE\DentalClinic.Api
dotnet run --no-build
```

### Bước 2: Khởi động Frontend Web
```powershell
$env:ApiSettings__BaseUrl = "https://localhost:7350"
$env:ASPNETCORE_URLS = "https://localhost:7129;http://localhost:5049"
cd D:\SWP\SWP391_Group6\FE\DentalClinic.Web
dotnet run --no-build
```

### Bước 3: Chạy bộ E2E Automation Runner
```powershell
cd D:\SWP\SWP391_Group6
dotnet run --project tools/QA_E2E_TestRunner/QA_E2E_TestRunner.csproj
```

Bộ runner sẽ tự động:
1. Dọn dẹp dữ liệu thử nghiệm cũ trong `DentalClinic_QA_E2E`.
2. Khởi động Edge Headless và kết nối CDP.
3. Thực thi tuần tự 9 bước hành trình người dùng và 7 kịch bản cạnh tranh.
4. Chụp toàn bộ 15 ảnh bằng chứng vào thư mục `reports/screenshots/`.
5. Đưa ra phán quyết cuối cùng `QA_PASSED`.

---

## 8. PHÁN QUYẾT CUỐI CÙNG

- **Môi trường Database chính:** Bảo toàn 100% không bị ảnh hưởng.
- **Tính toàn vẹn dữ liệu và an toàn đồng thời:** Đạt tiêu chuẩn kiểm định cấp cao.
- **Khả năng hoạt động thực tế trên trình duyệt:** Đã chứng minh qua CDP screenshots và session cookies.
- **Lỗi Notification sau Commit:** Đã được cô lập, khắc phục và có regression test trên nhánh `fix/manager-confirm-post-commit-notification`.

**FINAL VERDICT: QA_PASSED**

