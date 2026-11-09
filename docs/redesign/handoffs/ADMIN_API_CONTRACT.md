# TÀI LIỆU QUY ƯỚC API HỆ THỐNG QUẢN TRỊ (ADMIN API CONTRACT)
## DentalCare System Administrator Handoff Specification

- **Module:** Anti 1 – SystemAdministrator
- **Target Audience:** Integration Team, Anti 2 (Dentist), Anti 3 (Receptionist), Anti 4 (Patient)
- **Base URL:** `https://localhost:7351` (Backend API)
- **Frontend URL:** `https://localhost:7121` (Web Portal)
- **Authentication:** Bearer JWT Token hoặc Cookie `.DentalCare.Admin.Auth`
- **Specification Version:** 1.1 (Phase 0 Redesign - Final QA Verified)

---

## 1. Nguyên tắc Chung và Phân quyền (RBAC)

1. **Role Targets:**
   - Chỉ gồm 4 roles hợp lệ: `Patient`, `Dentist`, `Receptionist`, `SystemAdministrator`.
   - Các role cũ `DepartmentManager` và `Pharmacist` đã bị loại bỏ hoàn toàn.
2. **Quy tắc phối hợp giữa các Agent:**
   - **Admin:** Sở hữu toàn bộ các endpoint quản trị cấu hình dịch vụ, bác sĩ, phòng/ghế, kho thuốc (độc quyền nhập/kiểm kê), duyệt bài viết/tin tức CMS, kiểm duyệt phản hồi bệnh nhân, và giám sát hệ thống.
   - **Dentist (Anti 2):** Truy vấn danh mục thuốc khả dụng qua endpoint `GET /api/Medicine/available-for-dentist` để kê đơn. Admin không kê đơn thay bác sĩ.
   - **Receptionist (Anti 3):** Viết bài nháp và gửi duyệt bài viết qua endpoint `POST /api/Blog/posts`. Receptionist không có quyền tự duyệt xuất bản (`Published`).
   - **Patient (Anti 4):** Sở hữu việc gửi đánh giá bác sĩ sau khi hoàn tất khám. Admin chỉ kiểm duyệt ẩn/hiện và phản hồi, tuyệt đối không can thiệp số sao đánh giá.

---

## 2. System Diagnostics API

### 2.1. Lấy thông tin kết nối Cơ sở dữ liệu
- **Endpoint:** `GET /api/admin/system/db-info`
- **Role:** `SystemAdministrator`
- **Mục đích:** Xác minh Backend kết nối đúng database cách ly `DentalCare_Admin_Dev` trước khi thực hiện các tác vụ quản trị.
- **Response 200 OK:**
```json
{
  "success": true,
  "message": "Database info retrieved",
  "data": {
    "databaseName": "DentalCare_Admin_Dev",
    "serverName": "DESKTOP-1TUBGEU\\SQLEXPRESS",
    "timestamp": "2026-11-09T16:00:00.000Z"
  }
}
```

---

## 3. User & Staff Management APIs

### 3.1. Danh sách người dùng hệ thống
- **Endpoint:** `GET /api/admin/users?role={role}&searchTerm={term}&isActive={bool}&pageNumber=1&pageSize=20`
- **Role:** `SystemAdministrator`

### 3.2. Tạo tài khoản nhân viên mới (Dentist / Receptionist / Admin)
- **Endpoint:** `POST /api/admin/users/staff`
- **Role:** `SystemAdministrator`
- **Mô tả:** Tạo tài khoản nhân viên. Nghiêm cấm tạo `DepartmentManager` hoặc `Pharmacist`. Mật khẩu được tự động băm qua `IPasswordHasherService`. Khi tạo Bác sĩ, hệ thống tự động tạo đồng bộ: `UserAccount`, `StaffProfile` (mã `DNT-{UserId:D4}`), `DentistProfile` (gồm `Qualification`, `LicenseNumber`, `YearsOfExperience`, `Biography`), và phân công chuyên khoa `DentistDepartment` nếu có `DepartmentId`.
- **Request Body:**
```json
{
  "email": "dr.smith@dentalcare.com",
  "fullName": "Dr. John Smith",
  "phoneNumber": "0912345678",
  "role": "Dentist",
  "specialty": "Implant Phẫu thuật",
  "education": "Thạc sĩ Răng Hàm Mặt",
  "experienceYears": 8,
  "departmentId": 1,
  "password": "SecurePassword123!"
}
```
- **Response 200 OK:**
```json
{
  "success": true,
  "message": "Tạo tài khoản nhân viên thành công",
  "data": {
    "userId": 105,
    "email": "dr.smith@dentalcare.com",
    "fullName": "Dr. John Smith",
    "role": "Dentist",
    "specialty": "Implant Phẫu thuật",
    "isActive": true
  }
}
```

### 3.3. Cập nhật thông tin nhân viên
- **Endpoint:** `PUT /api/admin/users/staff/{userId}`
- **Role:** `SystemAdministrator`

### 3.4. Khóa / Mở khóa tài khoản
- **Endpoint:** `POST /api/admin/users/{userId}/toggle-lock`
- **Role:** `SystemAdministrator`

---

## 4. Dental Services Management APIs (Tương tác Booking)

### 4.1. Danh sách Dịch vụ Nha khoa (31 dịch vụ)
- **Endpoint:** `GET /api/admin/services?departmentId={id}&serviceType={type}&isActive={bool}`
- **Role:** `SystemAdministrator`

### 4.2. Tạo mới Dịch vụ Nha khoa
- **Endpoint:** `POST /api/admin/services`
- **Role:** `SystemAdministrator`
- **Mô tả:** Thiết lập thời lượng và đặc tả lâm sàng riêng biệt, không ép về 30 phút. Booking API đọc trực tiếp từ bảng `DepartmentServices`.
- **Request Body:**
```json
{
  "departmentId": 1,
  "serviceName": "Cấy ghép Implant Straumann",
  "description": "Cấy ghép trụ Implant Thụy Sĩ vô trùng tuyệt đối",
  "price": 25000000.0,
  "durationMinutes": 90,
  "serviceType": "Implant",
  "requiresExclusiveRoom": true,
  "requiresConsultationFirst": true,
  "minLeadTimeHours": 24,
  "bookingInstructions": "Bệnh nhân cần nhịn ăn 4 giờ trước phẫu thuật và mang theo kết quả chụp phim CT Cone Beam."
}
```

### 4.3. Cập nhật Dịch vụ Nha khoa
- **Endpoint:** `PUT /api/admin/services/{serviceId}`
- **Role:** `SystemAdministrator`

---

## 5. Doctor Specialization, Schedules & Availability APIs

### 5.1. Phân công Chuyên khoa (`DentistAllocations`)
- `GET /api/admin/dentist-allocations`: Lấy danh sách phân công bác sĩ vào khoa.
- `POST /api/admin/dentist-allocations`: Gán bác sĩ vào chuyên khoa:
```json
{
  "dentistUserId": 105,
  "departmentId": 1,
  "notes": "Trưởng kíp cấy ghép Implant"
}
```
- `POST /api/admin/dentist-allocations/{allocationId}/toggle-status`: Bật/tắt trạng thái phân công.

### 5.2. Lịch làm việc (`DentistSchedule`)
- `GET /api/DentistSchedule?dentistId={id}&departmentId={id}&workDate={date}`: Xem lịch làm việc.
- `POST /api/DentistSchedule`: Đăng ký ca trực (có kiểm tra chống trùng giờ giao thoa).
- `PUT /api/DentistSchedule/{scheduleId}/cancel`: Hủy ca trực khi có sự cố.

### 5.3. Nghỉ phép & Bận (`DentistAvailability`)
- `GET /api/DentistAvailability?dentistId={id}&fromDate={date}&toDate={date}`: Lấy danh sách vắng mặt.
- `POST /api/DentistAvailability`: Đăng ký lịch vắng (`OnLeave`, `Busy`, `Unavailable`).
- `DELETE /api/DentistAvailability/{availabilityId}`: Xóa bản ghi vắng mặt.

---

## 6. Room & DentalChair Management APIs

### 6.1. Phòng khám (`ClinicRoom`)
- `GET /api/admin/rooms`: Lấy danh sách phòng.
- `POST /api/admin/rooms`: Tạo phòng mới (hỗ trợ cờ vô trùng độc quyền `IsExclusive`).
- `PUT /api/admin/rooms/{roomId}`: Sửa cấu hình phòng.
- `POST /api/admin/rooms/{roomId}/status`: Đổi trạng thái (`Active`, `Maintenance`, `Inactive`).

### 6.2. Ghế Nha khoa (`DentalChair`)
- `POST /api/admin/chairs`: Thêm ghế nha khoa vào phòng.
- `PUT /api/admin/chairs/{chairId}`: Cập nhật thông tin ghế.
- `POST /api/admin/chairs/{chairId}/status`: Cập nhật trạng thái ghế (`Active`, `UnderRepair`, `Decommissioned`).

---

## 7. Medicine Inventory APIs (Admin & Dentist Single Source of Truth)

### 7.1. Danh mục & Tổng quan Tồn kho (Admin)
- `GET /api/Medicine`: Danh mục thuốc kèm cờ cảnh báo `IsLowStock`.
- `GET /api/Medicine/summary`: Thống kê tổng số loại thuốc, số lô hết hạn, số thuốc chạm ngưỡng tối thiểu.
- `POST /api/Medicine`: Thêm thuốc mới vào danh mục.

### 7.2. Nhập kho theo Lô (Admin Độc quyền)
- **Endpoint:** `POST /api/Medicine/{medicineId}/batches`
- **Role:** `SystemAdministrator`
- **Mô tả:** Nhập lô thuốc mới, cộng trực tiếp vào `MedicineBatches.QuantityAvailable` và tạo transaction loại `Import`.

### 7.3. Kiểm kê và Điều chỉnh Tồn kho (Admin Độc quyền)
- **Endpoint:** `POST /api/Medicine/batches/{batchId}/adjust`
- **Role:** `SystemAdministrator`
- **Mô tả:** Điều chỉnh tồn kho có lý do kiểm kê bắt buộc, ghi nhận vào `MedicineStockTransactions` loại `Adjustment`.

### 7.4. Handoff Contract cho Bác sĩ (Anti 2 - Dentist Kê đơn)
- **Endpoint:** `GET /api/Medicine/available-for-dentist?searchTerm={term}`
- **Role:** `Dentist`, `SystemAdministrator`
- **Mục đích:** Cung cấp danh mục thuốc, các lô còn hạn dùng, giá bán và số lượng thực tế có thể kê đơn.
- **Quy tắc an toàn lâm sàng:** Hệ thống tự động loại bỏ các lô đã hết hạn (`ExpiryDate <= Today`) hoặc trạng thái khác `Available`.
- **Response 200 OK:**
```json
{
  "success": true,
  "data": [
    {
      "medicineId": 12,
      "medicineCode": "MED-AUG625",
      "medicineName": "Augmentin 625mg",
      "unit": "Hộp",
      "unitPrice": 185000.0,
      "totalAvailableStock": 95,
      "validBatches": [
        {
          "batchId": 35,
          "batchNumber": "LOT-20261109-01",
          "expiryDate": "2027-11-09T00:00:00",
          "availableQuantity": 95
        }
      ]
    }
  ]
}
```

---

## 8. Blog Moderation & News CMS APIs

### 8.1. Đăng bài viết / Tin tức
- **Endpoint:** `POST /api/Blog/posts`
- **Role:** `Receptionist`, `SystemAdministrator`
- **Tác quyền:**
  - `Receptionist`: Tạo bài chia sẻ kiến thức nha khoa (`PostType = Blog`). Bắt buộc ở trạng thái `Draft` hoặc `PendingReview`.
  - `SystemAdministrator`: Tạo tin tức phòng khám, thông báo tuyển dụng/lễ tết, ưu đãi (`PostType = ClinicNews / Announcement`) và xuất bản trực tiếp.
  - `Dentist`: **Bị cấm hoàn toàn (403 Forbidden)**.
- **Lưu ý tương thích `PostType`:** Schema SQL Server giới hạn `('Announcement', 'ClinicNews', 'Blog')`. Nếu phía Patient truyền `KnowledgeBlog`, Backend tự động map về `Blog` để không vi phạm ràng buộc CHECK.

### 8.2. Duyệt hoặc Từ chối Bài viết (Admin CMS Moderation)
- **Endpoint:** `PUT /api/Blog/posts/{postId}/review`
- **Role:** `SystemAdministrator`
- **Duyệt bài (`Published`):** Ghi nhận `ApprovedByUserId` và `ApprovedAt`, bài viết lập tức hiển thị công khai.
- **Từ chối bài (`Rejected`):** Bắt buộc nhập `RejectionReason` để tác giả Lễ tân xem và chỉnh sửa lại bản nháp.
- **Request Body (Khi Từ chối):**
```json
{
  "status": "Rejected",
  "rejectionReason": "Cần trích dẫn hướng dẫn chi tiết từ hiệp hội nha khoa."
}
```

---

## 9. Doctor Feedback Moderation APIs

### 9.1. Lấy danh sách Đánh giá Bác sĩ
- **Endpoint:** `GET /api/Feedback?status={Approved|Hidden}&dentistId={id}&minRating={int}&searchTerm={term}&pageNumber=1&pageSize=20`
- **Role:** `SystemAdministrator`

### 9.2. Kiểm duyệt Ẩn / Hiện Đánh giá
- **Endpoint:** `PUT /api/Feedback/{feedbackId}/moderate`
- **Role:** `SystemAdministrator`
- **Bảo mật rating:** Chỉ cho phép đổi `Status` và `AdminNotes`. **Tuyệt đối không can thiệp số sao đánh giá.**
- **Request Body:**
```json
{
  "status": "Hidden",
  "adminNotes": "Phản hồi chứa thông tin đời tư cá nhân và từ ngữ không phù hợp quy định kiểm duyệt."
}
```

### 9.3. Phản hồi Chính thức từ Phòng khám
- **Endpoint:** `POST /api/Feedback/{feedbackId}/respond`
- **Role:** `SystemAdministrator`

---

## 10. Database Migrations Bổ sung cho Integration Team

Để hỗ trợ tính năng Admin từ chối duyệt bài viết (`Rejected`), Integration Team cần thực thi lệnh sau khi cập nhật cơ sở dữ liệu chính:

```sql
-- Cập nhật ràng buộc CK_BlogPosts_Status hỗ trợ trạng thái Rejected
ALTER TABLE BlogPosts DROP CONSTRAINT CK_BlogPosts_Status;
ALTER TABLE BlogPosts ADD CONSTRAINT CK_BlogPosts_Status 
    CHECK ([Status]='Archived' OR [Status]='Published' OR [Status]='PendingReview' OR [Status]='Draft' OR [Status]='Rejected');
```
