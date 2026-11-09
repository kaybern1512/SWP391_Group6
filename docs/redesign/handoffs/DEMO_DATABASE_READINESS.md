# DENTALCARE 4-ROLE DEMO DATABASE READINESS & ENVIRONMENT CHECKLIST

> **Tài liệu chuyển giao độc lập từ Anti 1 (SystemAdministrator)**  
> **Dành cho:** Integration Lead & Đội ngũ Demo 4 Role  
> **Workspace kiểm chứng:** `D:\SWP391\DentalCare-Admin`  
> **Database kiểm chứng:** `DentalCare_Admin_Dev` (SQL Server `DESKTOP-1TUBGEU\SQLEXPRESS`)  
> **Branch:** `feature/admin-redesign` (Baseline `5ad6d95`, Commit bàn giao gần nhất: `de5ceeb`)  
> **Thời điểm thẩm định:** 2026-11-09  

---

## 1. TỔNG QUAN VÀ NGUYÊN TẮC BẢO VỆ MÔI TRƯỜNG

Phân hệ Admin đã hoàn thành toàn bộ chức năng và kiểm thử bàn giao. Tài liệu này cung cấp **Checklist điều kiện tiên quyết**, **Ma trận phân quyền RBAC 4 Role**, **Bộ script Seed / Cleanup QA tự động tái lập**, và **Tập lệnh SQL chỉ đọc (Read-Only Queries)** để thẩm định dữ liệu trước buổi trình diễn hệ thống.

### Nguyên tắc an toàn môi trường:
1. **Tuyệt đối không chạy script trên Database chính `DentalClinicManagementDB`**: Mọi script seed/cleanup đều tích hợp chốt chặn an toàn `THROW` nếu phát hiện database name là `DentalClinicManagementDB`.
2. **Không tự ý can thiệp `DentalCare-Integration` hay `DentalCare_Integration_Dev`**: Toàn bộ script và fixtures đã được chạy thử nghiệm thành công 100% trên `DentalCare_Admin_Dev` và bàn giao dưới dạng file `.sql` cho Integration Lead quyết định áp dụng.
3. **Tính tái lập & Idempotency**: Script `20261109_Demo_Readiness_Seed.sql` cho phép chạy lại nhiều lần mà không gây lỗi khóa chính (Primary Key), không trùng lặp dữ liệu và có kèm script hoàn nguyên `20261109_Demo_Cleanup.sql`.

---

## 2. CHECKLIST ĐIỀU KIỆN TIÊN QUYẾT (PRE-DEMO GATES)

Trước khi bắt đầu demo, Integration Lead cần kiểm tra đủ 6 điều kiện tiên quyết sau:

| STT | Điều kiện kiểm tra | Tiêu chuẩn đạt | Cách xác minh |
| :---: | :--- | :--- | :--- |
| **G-01** | **Database Schema Phase 0 & Migration Blog** | Đã chạy migration nới lỏng constraint `CK_BlogPosts_Status` cho phép `'Rejected'` | Chạy Query V-07. Kết quả constraint definition chứa `'Rejected'`. |
| **G-02** | **4-Role User Accounts** | Có đủ 4 role: `SystemAdministrator`, `Receptionist`, `Dentist`, `Patient`. Không còn role `DepartmentManager`. Mật khẩu chuẩn: `Password123@`. | Chạy Query V-01. Tất cả 4 role đều có tài khoản `Active`. |
| **G-03** | **Lịch làm việc bác sĩ theo ngày Demo** | Có 4 bác sĩ thuộc 4 chuyên khoa trực từ `08:00` đến `17:00` vào ngày `@DemoDate` (mặc định: `2026-11-10`). | Chạy Query V-02. Trả về đúng 4 ca trực `Scheduled`. |
| **G-04** | **Phòng & Ghế Hoạt Động (8 Phòng / 16 Ghế)** | 8 phòng thuộc 4 khoa, 16 ghế nha khoa đều ở trạng thái `Active`. Có phòng độc quyền (`IsExclusive=1`) cho Phẫu thuật & Implant. | Chạy Query V-03. Trả về 16 ghế `Active`. |
| **G-05** | **Dịch vụ đa thời lượng (Service-First Booking)** | Có các dịch vụ 20, 30, 45, 60, 75, 90 phút với đơn giá và phòng chuyên biệt. | Chạy Query V-04. Trả về đủ 6 nhóm thời lượng. |
| **G-06** | **Dữ liệu kịch bản Demo xuyên suốt 5 trạng thái hẹn** | Có sẵn 5 lịch hẹn `DEMO-APP-202611-01` -> `05` phủ khắp vòng đời: `PendingReceptionReview` ➔ `Confirmed` ➔ `CheckedIn` ➔ `InService` ➔ `Completed`. | Chạy Query V-05. |

---

## 3. BỘ LỆNH SQL KIỂM TRA CHỈ ĐỌC (READ-ONLY VERIFICATION QUERIES)

Integration Lead hoặc QA Engineer chỉ cần mở SQL Server Management Studio (SSMS) hoặc chạy `sqlcmd` với các câu lệnh dưới đây để xác nhận môi trường:

### Query V-01: Kiểm tra tài khoản và vai trò 4 Role
```sql
SELECT u.Role, COUNT(*) AS TotalUsers, 
       SUM(CASE WHEN u.Status = 'Active' THEN 1 ELSE 0 END) AS ActiveUsers,
       STRING_AGG(u.Email, ', ') WITHIN GROUP (ORDER BY u.UserId) AS SampleEmails
FROM UserAccounts u
WHERE u.Role IN ('SystemAdministrator', 'Receptionist', 'Dentist', 'Patient')
GROUP BY u.Role;
```
*Kỳ vọng:* Đủ 4 vai trò, 100% tài khoản demo ở trạng thái `Active`.

### Query V-02: Kiểm tra lịch làm việc của Bác sĩ vào ngày Demo
```sql
DECLARE @DemoDate DATE = '2026-11-10'; -- Cấu hình ngày demo
SELECT s.DentistScheduleId, s.WorkDate, s.StartTime, s.EndTime, s.Status,
       u.Email, sp.FullName AS DentistName, d.Name AS DepartmentName, dp.LicenseNumber
FROM DentistWorkSchedules s
JOIN UserAccounts u ON s.DentistUserId = u.UserId
LEFT JOIN StaffProfiles sp ON u.UserId = sp.UserId
LEFT JOIN DentistProfiles dp ON u.UserId = dp.UserId
LEFT JOIN Departments d ON s.DepartmentId = d.DepartmentId
WHERE s.WorkDate = @DemoDate
ORDER BY s.StartTime, d.DepartmentId;
```
*Kỳ vọng:* Trả về 4 ca trực từ 08:00 - 17:00 thuộc 4 khoa (Khoa Tổng Quát, Khoa Chỉnh Nha, Khoa Phẫu Thuật & Implant, Khoa Nha Nhi).

### Query V-03: Kiểm tra phòng khám và ghế nha khoa
```sql
SELECT r.RoomId, r.RoomCode, r.RoomName, d.Name AS DepartmentName, r.IsExclusive, r.Status AS RoomStatus,
       COUNT(c.DentalChairId) AS TotalChairs,
       SUM(CASE WHEN c.Status = 'Active' THEN 1 ELSE 0 END) AS ActiveChairs
FROM Rooms r
JOIN Departments d ON r.DepartmentId = d.DepartmentId
LEFT JOIN DentalChairs c ON r.RoomId = c.RoomId
GROUP BY r.RoomId, r.RoomCode, r.RoomName, d.Name, r.IsExclusive, r.Status
ORDER BY r.DepartmentId, r.RoomId;
```
*Kỳ vọng:* 8 phòng, 16 ghế `Active`. Các phòng phẫu thuật Implant (ROOM-201, ROOM-202) có `IsExclusive = 1`.

### Query V-04: Thẩm định cấu hình thời lượng Service-First Booking
```sql
SELECT DurationMinutes, COUNT(*) AS ServiceCount, 
       STRING_AGG(ServiceName, ' | ') WITHIN GROUP (ORDER BY DepartmentServiceId) AS ServicesList
FROM DepartmentServices
WHERE Status = 'Active'
GROUP BY DurationMinutes
ORDER BY DurationMinutes;
```
*Kỳ vọng:* Phân bổ thời lượng dịch vụ đa dạng:
- **20 phút:** Nhổ răng sữa lung lay
- **30 phút:** Khám tổng quát & tư vấn, Trám răng sữa
- **45 phút:** Lấy cao răng & đánh bóng, Trám răng Composite
- **60 phút:** Tiểu phẫu nhổ răng khôn mọc lệch, Niềng răng mắc cài sứ
- **75 phút:** Điều trị tủy răng hàm nhiều chân, Cấy ghép Implant Straumann
- **90 phút:** Cấy ghép trụ Implant chính hãng

### Query V-05: Kiểm tra 5 Lịch hẹn Demo vòng đời 4 Role
```sql
SELECT a.AppointmentId, a.AppointmentCode, a.Status, 
       a.ScheduledStart, a.ScheduledEnd, 
       p.FullName AS PatientName,
       d.Name AS DepartmentName,
       ds.ServiceName, ds.DurationMinutes,
       den.FullName AS DentistName,
       r.RoomCode, c.ChairCode, a.QueueNumber
FROM Appointments a
JOIN UserAccounts up ON a.PatientUserId = up.UserId
LEFT JOIN PatientProfiles p ON up.UserId = p.UserId
JOIN Departments d ON a.DepartmentId = d.DepartmentId
JOIN DepartmentServices ds ON a.RequestedServiceId = ds.DepartmentServiceId
LEFT JOIN StaffProfiles den ON a.AssignedDentistUserId = den.UserId
LEFT JOIN Rooms r ON a.RoomId = r.RoomId
LEFT JOIN DentalChairs c ON a.DentalChairId = c.DentalChairId
WHERE a.AppointmentCode LIKE 'DEMO-APP%'
ORDER BY a.ScheduledStart;
```
*Kỳ vọng:* 5 lịch hẹn có mã `DEMO-APP-202611-01` -> `05` phủ kín 5 trạng thái cần diễn tập.

### Query V-06: Thẩm định Hồ sơ bệnh án & Đơn thuốc đã cấp
```sql
SELECT a.AppointmentCode, ce.Diagnosis, ce.ClinicalNotes, 
       p.GeneralInstructions, pi.MedicationName, pi.Dosage, pi.Frequency, pi.Duration, pi.Instructions
FROM Appointments a
JOIN ClinicalEntries ce ON a.AppointmentId = ce.AppointmentId
JOIN TreatmentSessions ts ON a.AppointmentId = ts.AppointmentId
JOIN Prescriptions p ON ts.TreatmentSessionId = p.TreatmentSessionId
JOIN PrescriptionItems pi ON p.PrescriptionId = pi.PrescriptionId
WHERE a.AppointmentCode = 'DEMO-APP-202611-05';
```
*Kỳ vọng:* Trả về 3 dòng thuốc (Amoxicillin 500mg, Paracetamol 500mg, Chlorhexidine 0.12%) liên kết chuẩn với chẩn đoán nhổ răng khôn của BS. Hùng.

### Query V-07: Kiểm tra bài viết Blog theo 4 trạng thái và 3 PostType
```sql
SELECT b.BlogPostId, b.Title, b.Status, b.PostType, u.Email AS Author, b.RejectionReason, b.PublishedAt
FROM BlogPosts b
JOIN UserAccounts u ON b.AuthorUserId = u.UserId
WHERE b.Slug LIKE 'demo-blog%'
ORDER BY b.BlogPostId;
```
*Kỳ vọng:* Có đủ 4 trạng thái:
1. `Draft` (PostType: `Blog`)
2. `PendingReview` (PostType: `ClinicNews`)
3. `Published` (PostType: `Announcement`)
4. `Rejected` (PostType: `Blog`, có đầy đủ `RejectionReason`)

### Query V-08: Kiểm tra đánh giá đang chờ duyệt (Pending Feedback Moderation)
```sql
SELECT f.FeedbackId, a.AppointmentCode, p.FullName AS PatientName, s.FullName AS DentistName,
       f.DentistRating, f.ClinicRating, f.Status, f.Comment
FROM VisitFeedback f
JOIN Appointments a ON f.AppointmentId = a.AppointmentId
LEFT JOIN PatientProfiles p ON f.PatientUserId = p.UserId
LEFT JOIN StaffProfiles s ON f.DentistUserId = s.UserId
WHERE f.Status = 'Pending';
```
*Kỳ vọng:* Có ít nhất 1-2 đánh giá trạng thái `Pending` để Admin trình diễn tính năng phê duyệt / phản hồi đánh giá.

### Query V-09: Kiểm tra Kho thuốc và cảnh báo Hạn dùng / Tồn kho
```sql
SELECT m.MedicineCode, m.MedicineName, m.Unit, m.MinimumStockAlert,
       b.BatchNumber, b.ExpiryDate, b.QuantityAvailable, b.Status AS BatchStatus,
       CASE 
           WHEN b.ExpiryDate <= DATEADD(DAY, 45, '2026-11-09') THEN N'Sắp hết hạn (<45 ngày)'
           WHEN b.QuantityAvailable <= m.MinimumStockAlert THEN N'Sắp hết hàng (Low Stock)'
           ELSE N'Bình thường'
       END AS DemoAlertCategory
FROM Medicines m
JOIN MedicineBatches b ON m.MedicineId = b.MedicineId
WHERE m.MedicineCode IN ('MED-AMOX-500', 'MED-EFFE-500', 'MED-PARA-500')
ORDER BY m.MedicineId, b.MedicineBatchId;
```
*Kỳ vọng:* Có đủ 3 kịch bản: Tồn kho dồi dào để kê đơn, Lô sắp hết hạn (`AMX-2025-NEAR`), và Thuốc dưới ngưỡng tồn kho cảnh báo (`EFF-2026-LOW`).

---

## 4. HƯỚNG DẪN THỰC THI SEED VÀ CLEANUP

Tất cả các tệp SQL đã được lưu tại thư mục: [`DB/demo_seed/`](file:///D:/SWP391/DentalCare-Admin/DB/demo_seed).

### Cách 1: Nạp dữ liệu Demo (Forward Seed)
```powershell
sqlcmd -S "DESKTOP-1TUBGEU\SQLEXPRESS" -d "DentalCare_Admin_Dev" -E -i "D:\SWP391\DentalCare-Admin\DB\demo_seed\20261109_Demo_Readiness_Seed.sql"
```

### Cách 2: Hoàn nguyên / Xóa sạch dữ liệu Demo (Rollback / Cleanup)
```powershell
sqlcmd -S "DESKTOP-1TUBGEU\SQLEXPRESS" -d "DentalCare_Admin_Dev" -E -i "D:\SWP391\DentalCare-Admin\DB\demo_seed\20261109_Demo_Cleanup.sql"
```

---

## 5. MA TRẬN PHÂN QUYỀN RBAC 4 ROLE & KẾT QUẢ AUTOMATED TESTS

Đội ngũ Anti 1 đã xây dựng bộ kiểm thử tự động chuyên biệt [`FourRoleRbacAuthorizationRegressionTests.cs`](file:///D:/SWP391/DentalCare-Admin/BE/DentalClinic.Tests/Admin/FourRoleRbacAuthorizationRegressionTests.cs) gồm **21 test cases** kiểm tra nghiêm ngặt qua Reflection trên toàn bộ Controller:

| Nghiệp vụ / Phân hệ | SystemAdministrator | Receptionist | Dentist | Patient | Cơ chế bảo vệ & Kết quả Test |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **Quản trị người dùng & Phân quyền** | **Toàn quyền** | Không | Không | Không | Chặn ở `AdminController` (`Authorize(Roles = SystemAdministrator)`). **PASSED**. |
| **Cấu hình Khoa, Dịch vụ, Phòng, Ghế** | **Toàn quyền** | Không | Không | Không | Chặn ở `AdminController`. **PASSED**. |
| **Quản lý Kho thuốc (Nhập/Xuất/Tồn/Kiểm kê)** | **Toàn quyền** | **Bị cấm (403)** | Xem tồn để kê đơn | **Bị cấm (403)** | `MedicineController` chỉ cho phép `SystemAdministrator`. Receptionist không thể truy cập. **PASSED**. |
| **Tạo Lịch hẹn (Booking)** | Xem báo cáo | **Tạo tại quầy** | Không | **Đặt trực tuyến** | `BookingController` phân quyền theo kênh tiếp nhận. **PASSED**. |
| **Tiếp đón, Phân bổ Ghế & Check-in** | Không can thiệp | **Toàn quyền** | Xem hàng đợi | Xem trạng thái | `AppointmentsController` phân bổ tài nguyên thuộc Receptionist. **PASSED**. |
| **Khám lâm sàng, Odontogram & Bệnh án** | **Bị cấm ghi bệnh án** | **Bị cấm ghi bệnh án** | **Toàn quyền lâm sàng** | Xem bệnh án của chính mình | `MedicalRecordController.CreateClinicalEntry` chỉ cho phép `Dentist`. Admin không thể ghi đè. **PASSED**. |
| **Kê đơn thuốc y khoa** | **Bị cấm kê đơn** | **Bị cấm kê đơn** | **Toàn quyền** | Xem đơn thuốc của mình | `PrescriptionController` chỉ cho phép `Dentist`. **PASSED**. |
| **Bảo vệ Bệnh án Bệnh nhân (Chống IDOR)** | Tra cứu theo mã | Tra cứu tiếp đón | Tra cứu ca khám | **Chỉ xem được của chính mình** | `GetMyMedicalRecord` đọc User ID từ JWT token claim, chặn truy cập chéo. **PASSED**. |
| **Viết bài Blog / Tin tức phòng khám** | Duyệt & Quản lý | **Tạo bài & Gửi duyệt** | **Bị cấm viết blog** | Chỉ đọc bài Published | `BlogController.CreatePost` chỉ cho phép Receptionist/Admin. Dentist gọi sẽ trả về 403. **PASSED**. |
| **Kiểm duyệt bài viết Blog (Duyệt/Từ chối)** | **Toàn quyền duyệt/từ chối** | Không | Không | Không | `BlogController.ReviewPost` chỉ cho phép `SystemAdministrator`. **PASSED**. |
| **Kiểm duyệt đánh giá (Feedback Moderation)** | **Toàn quyền ẩn/duyệt/phản hồi** | Không | Xem đánh giá về mình | Tạo đánh giá ca khám của mình | `FeedbackController.ModerateFeedback` chỉ cho phép `SystemAdministrator`. Không cho sửa số sao. **PASSED**. |

### Thống kê kiểm thử RBAC & Phân hệ Admin:
- **FourRoleRbacAuthorizationRegressionTests:** `21/21 Passed (100%)`
- **Toàn bộ Admin Tests (`FullyQualifiedName~Admin`):** `69/69 Passed (100%)`
- **Web Frontend Tests (`DentalClinic.Web.Tests`):** `289/289 Passed (100%)`

---

## 6. THÔNG BÁO DÀNH CHO INTEGRATION LEAD

1. **Phân hệ Anti 1 SystemAdministrator hoàn toàn sạch lỗi:** Không có bất kỳ lỗi P0 nào trên mã nguồn Admin.
2. **Kịch bản Demo đã sẵn sàng:** Có thể chạy script `20261109_Demo_Readiness_Seed.sql` trên bất kỳ database clone nào của Phase 0 để ngay lập tức có đầy đủ dữ liệu cho cả 4 role trình diễn mà không cần tạo tay từ đầu.
3. **Liên kết tài liệu chi tiết:**
   - Xem bảng tra cứu tài khoản và danh sách fixtures chi tiết tại [`DEMO_ACCOUNTS_AND_FIXTURES.md`](file:///D:/SWP391/DentalCare-Admin/docs/redesign/handoffs/DEMO_ACCOUNTS_AND_FIXTURES.md).
   - Xem hợp đồng API tại [`ADMIN_API_CONTRACT.md`](file:///D:/SWP391/DentalCare-Admin/docs/redesign/handoffs/ADMIN_API_CONTRACT.md).
