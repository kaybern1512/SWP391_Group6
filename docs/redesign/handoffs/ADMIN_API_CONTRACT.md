# TÀI LIỆU QUY ƯỚC API HỆ THỐNG QUẢN TRỊ (ADMIN API CONTRACT)
## DentalCare System Administrator Handoff Specification

- **Module:** Anti 1 – SystemAdministrator
- **Target Audience:** Integration Team, Anti 2 (Dentist), Anti 3 (Receptionist), Anti 4 (Patient)
- **Base URL:** `https://localhost:7351` (Backend API)
- **Authentication:** Bearer JWT Token hoặc Cookie `.DentalCare.Admin.Auth`
- **Specification Version:** 1.0 (Phase 0 Redesign)

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
- **Mục đích:** Xác minh Backend kết nối đúng database cách ly trước khi thực hiện các tác vụ quản trị.
- **Response 200 OK:**
```json
{
  "success": true,
  "message": "Database info retrieved",
  "data": {
    "databaseName": "DentalCare_Admin_Dev",
    "serverName": "DESKTOP-1TUBGEU\\SQLEXPRESS",
    "timestamp": "2026-11-09T15:30:00.000Z"
  }
}
```

---

## 3. User & Staff Management APIs

### 3.1. Danh sách người dùng hệ thống
- **Endpoint:** `GET /api/admin/users?role={role}&searchTerm={term}&isActive={bool}&pageNumber=1&pageSize=20`
- **Role:** `SystemAdministrator`
- **Query Params:**
  - `role` (string, optional): `Dentist`, `Receptionist`, `SystemAdministrator`, `Patient`.
  - `searchTerm` (string, optional): Tìm theo tên, email, số điện thoại.
  - `isActive` (bool, optional): `true` / `false`.

### 3.2. Tạo tài khoản nhân viên mới (Dentist / Receptionist / Admin)
- **Endpoint:** `POST /api/admin/users/staff`
- **Role:** `SystemAdministrator`
- **Mô tả:** Tạo tài khoản nhân viên. Nghiêm cấm tạo `DepartmentManager` hoặc `Pharmacist`. Mật khẩu được tự động băm qua `IPasswordHasherService`.
- **Request Body:**
```json
{
  "email": "dr.smith@dentalcare.com",
  "fullName": "Dr. John Smith",
  "phoneNumber": "0912345678",
  "role": "Dentist",
  "specialty": "Implant",
  "password": "SecurePassword123!"
}
```
- **Response 200 OK:**
```json
{
  "success": true,
  "message": "Tạo tài khoản nhân viên thành công",
  "data": {
    "userId": "9f2c1b4d-3e5a-4b6c-8f1a-0e9d8c7b6a5f",
    "email": "dr.smith@dentalcare.com",
    "fullName": "Dr. John Smith",
    "role": "Dentist",
    "specialty": "Implant",
    "isActive": true
  }
}
```

### 3.3. Cập nhật thông tin nhân viên
- **Endpoint:** `PUT /api/admin/users/staff/{userId}`
- **Role:** `SystemAdministrator`
- **Request Body:**
```json
{
  "fullName": "Dr. John Smith, Ph.D",
  "phoneNumber": "0987654321",
  "specialty": "Implant & Oral Surgery"
}
```

### 3.4. Khóa / Mở khóa tài khoản
- **Endpoint:** `POST /api/admin/users/{userId}/toggle-lock`
- **Role:** `SystemAdministrator`

---

## 4. Dental Services & Departments Management APIs

### 4.1. Danh sách Dịch vụ Nha khoa (31 dịch vụ)
- **Endpoint:** `GET /api/admin/services?departmentId={id}&serviceType={type}&isActive={bool}`
- **Role:** `SystemAdministrator`

### 4.2. Tạo mới Dịch vụ Nha khoa (Hỗ trợ đầy đủ 5 trường lâm sàng)
- **Endpoint:** `POST /api/admin/services`
- **Role:** `SystemAdministrator`
- **Request Body:**
```json
{
  "departmentId": "1a2b3c4d-...",
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
- **Request Body:** Tương tự `POST /api/admin/services`.

---

## 5. Doctor Specialization, Schedules & Availability APIs

### 5.1. Quản lý Phân công Chuyên khoa (`DentistAllocations`)
- `GET /api/admin/dentist-allocations`: Lấy danh sách phân công bác sĩ vào khoa.
- `POST /api/admin/dentist-allocations`: Gán bác sĩ vào chuyên khoa:
```json
{
  "dentistUserId": "user-uuid",
  "departmentId": "dept-uuid",
  "notes": "Trưởng kíp cấy ghép Implant"
}
```
- `POST /api/admin/dentist-allocations/{allocationId}/toggle-status`: Bật/tắt trạng thái phân công.

### 5.2. Quản lý Lịch làm việc (`DentistSchedule`)
- `GET /api/DentistSchedule?dentistId={id}&departmentId={id}&workDate={date}`: Xem lịch làm việc.
- `POST /api/DentistSchedule`: Đăng ký ca trực mới:
```json
{
  "dentistId": "user-uuid",
  "departmentId": "dept-uuid",
  "workDate": "2026-11-15",
  "shiftType": "Morning",
  "startTime": "08:00:00",
  "endTime": "12:00:00",
  "roomId": "room-uuid",
  "chairId": "chair-uuid"
}
```
- `PUT /api/DentistSchedule/{scheduleId}/cancel`: Hủy ca trực khi có sự cố.

### 5.3. Quản lý Nghỉ phép & Bận (`DentistAvailability`)
- `GET /api/DentistAvailability?dentistId={id}&fromDate={date}&toDate={date}`: Lấy danh sách đăng ký vắng mặt.
- `POST /api/DentistAvailability`: Đăng ký lịch vắng:
```json
{
  "dentistId": "user-uuid",
  "startDate": "2026-11-20T08:00:00",
  "endDate": "2026-11-22T17:00:00",
  "status": "OnLeave",
  "reason": "Nghỉ phép thường niên"
}
```
- `DELETE /api/DentistAvailability/{availabilityId}`: Xóa bản ghi vắng mặt.

---

## 6. Room & DentalChair Management APIs

### 6.1. Quản lý Phòng khám (`ClinicRoom`)
- `GET /api/admin/rooms`: Lấy danh sách phòng.
- `POST /api/admin/rooms`: Tạo phòng mới (có cờ vô trùng độc quyền):
```json
{
  "roomNumber": "OR-101",
  "roomName": "Phòng Phẫu thuật Vô trùng 1",
  "departmentId": "dept-uuid",
  "capacity": 1,
  "isExclusive": true,
  "notes": "Chuyên dụng cho phẫu thuật hàm mặt và cấy ghép Implant"
}
```
- `PUT /api/admin/rooms/{roomId}`: Sửa cấu hình phòng.
- `POST /api/admin/rooms/{roomId}/status`: Đổi trạng thái (`Active`, `Maintenance`, `Inactive`).

### 6.2. Quản lý Ghế Nha khoa (`DentalChair`)
- `POST /api/admin/chairs`: Thêm ghế nha khoa vào phòng:
```json
{
  "roomId": "room-uuid",
  "chairNumber": "CHAIR-01",
  "chairType": "Advanced Surgical Chair"
}
```
- `PUT /api/admin/chairs/{chairId}`: Cập nhật thông tin ghế.
- `POST /api/admin/chairs/{chairId}/status`: Cập nhật trạng thái ghế (`Active`, `UnderRepair`, `Decommissioned`).

---

## 7. Medicine Inventory APIs (Admin & Dentist Contract)

### 7.1. Danh mục & Tổng quan Tồn kho (Admin)
- `GET /api/Medicine?searchTerm={term}&isActive={bool}&isLowStock={bool}`: Danh mục thuốc kèm cờ tồn kho.
- `GET /api/Medicine/summary`: Thống kê tổng số loại thuốc, số lô hết hạn, số thuốc chạm ngưỡng tối thiểu.
- `POST /api/Medicine`: Thêm thuốc mới vào danh mục:
```json
{
  "medicineCode": "MED-AUG625",
  "medicineName": "Augmentin 625mg",
  "activeIngredient": "Amoxicillin + Clavulanic Acid",
  "strength": "625mg",
  "dosageForm": "Tablet",
  "unit": "Hộp",
  "unitPrice": 185000.0,
  "reorderLevel": 20
}
```

### 7.2. Nhập kho theo Lô (Admin Độc quyền)
- **Endpoint:** `POST /api/Medicine/{medicineId}/batches`
- **Role:** `SystemAdministrator`
- **Request Body:**
```json
{
  "batchNumber": "LOT-20261109-01",
  "expiryDate": "2027-11-09",
  "quantityReceived": 100,
  "unitCost": 150000.0,
  "supplier": "Dược phẩm Trung ương 1"
}
```

### 7.3. Kiểm kê và Điều chỉnh Tồn kho (Admin Độc quyền)
- **Endpoint:** `POST /api/Medicine/batches/{batchId}/adjust`
- **Role:** `SystemAdministrator`
- **Request Body:**
```json
{
  "newQuantity": 95,
  "reason": "Kiểm kê định kỳ tháng 11 phát hiện hao hụt 5 hộp do bể vỡ",
  "adjustmentType": "CycleCount"
}
```

### 7.4. Handoff Contract cho Bác sĩ (Anti 2 - Dentist Query)
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
      "medicineId": "med-uuid",
      "medicineCode": "MED-AUG625",
      "medicineName": "Augmentin 625mg",
      "unit": "Hộp",
      "unitPrice": 185000.0,
      "totalAvailableStock": 95,
      "validBatches": [
        {
          "batchId": "batch-uuid",
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
- **Phân quyền tác giả:**
  - `Receptionist`: Tạo bài viết chia sẻ kinh nghiệm chăm sóc răng miệng, mẹo nha khoa (`PostType = Blog / DentalKnowledge`). Chỉ được phép gửi bài ở trạng thái `Draft` hoặc `PendingReview`. Không được phép tự đặt trạng thái `Published`.
  - `SystemAdministrator`: Được phép tạo Tin tức phòng khám, Thông báo tuyển dụng/lễ tết, Khuyến mãi (`PostType = ClinicNews / Announcement / Promotion`) và xuất bản trực tiếp.
  - `Dentist`: **Bị cấm** gọi endpoint này.
- **Request Body:**
```json
{
  "title": "Hướng dẫn chăm sóc răng miệng sau cấy ghép Implant",
  "summary": "Những lưu ý cốt lõi giúp trụ Implant tích hợp xương nhanh chóng và an toàn.",
  "content": "<p>Nội dung chi tiết...</p>",
  "categoryId": "cat-uuid",
  "postType": "Blog",
  "status": "PendingReview",
  "featuredImageUrl": "https://..."
}
```

### 8.2. Duyệt hoặc Từ chối Bài viết (Admin CMS Moderation)
- **Endpoint:** `PUT /api/Blog/posts/{postId}/review`
- **Role:** `SystemAdministrator`
- **Mô tả:** Admin duyệt bài viết của Lễ tân để xuất bản ra trang chủ (`Published`) hoặc từ chối bài (`Rejected`) kèm lý do giải thích.
- **Request Body (Khi Duyệt):**
```json
{
  "status": "Published",
  "rejectionReason": null
}
```
- **Request Body (Khi Từ chối):**
```json
{
  "status": "Rejected",
  "rejectionReason": "Hình ảnh minh họa mờ và nội dung cần trích dẫn rõ nguồn khuyến nghị của bác sĩ chỉnh nha."
}
```
- **Response 200 OK:**
```json
{
  "success": true,
  "message": "Kiểm duyệt bài viết thành công",
  "data": {
    "postId": "post-uuid",
    "title": "Hướng dẫn chăm sóc răng miệng sau cấy ghép Implant",
    "status": "Rejected",
    "rejectionReason": "Hình ảnh minh họa mờ và nội dung cần trích dẫn rõ nguồn khuyến nghị của bác sĩ chỉnh nha.",
    "approvedByUserId": "admin-uuid",
    "approvedByName": "Admin Quản trị",
    "approvedAt": "2026-11-09T15:35:00.000Z"
  }
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
- **Quy tắc bảo mật y tế:** Chỉ cho phép cập nhật `Status` (`Approved` hoặc `Hidden`) và `AdminNotes`. **Tuyệt đối không thay đổi số sao đánh giá (Rating) của Bệnh nhân.**
- **Request Body:**
```json
{
  "status": "Hidden",
  "adminNotes": "Phản hồi chứa thông tin đời tư cá nhân và từ ngữ không phù hợp quy định kiểm duyệt."
}
```

### 9.3. Gửi Phản hồi Chính thức từ Phòng khám
- **Endpoint:** `POST /api/Feedback/{feedbackId}/respond`
- **Role:** `SystemAdministrator`
- **Request Body:**
```json
{
  "response": "Nha khoa DentalCare trân trọng cảm ơn quý khách đã tin tưởng dịch vụ. Ban Quản trị đã ghi nhận góp ý và tăng cường trang thiết bị tiện nghi tại phòng chờ."
}
```

---

## 10. Bảng Mã Lỗi (Error Codes Summary)

| Mã HTTP | Tình huống | Thông báo mẫu |
|---|---|---|
| `400 Bad Request` | Gán sai role không được phép (ví dụ `DepartmentManager`) | `Vai trò 'DepartmentManager' không còn được hỗ trợ trong mô hình 4 role.` |
| `400 Bad Request` | Từ chối duyệt bài blog mà không có lý do | `Vui lòng nhập lý do từ chối bài viết.` |
| `400 Bad Request` | Điều chỉnh tồn kho thiếu lý do kiểm kê | `Lý do điều chỉnh tồn kho là bắt buộc.` |
| `401 Unauthorized` | Thiếu Auth Cookie hoặc Bearer Token | `Unauthorized.` |
| `403 Forbidden` | Receptionist cố gắng duyệt bài viết hoặc tự publish | `Chỉ SystemAdministrator mới có quyền phê duyệt xuất bản bài viết.` |
| `403 Forbidden` | Bác sĩ cố gắng tạo bài viết blog | `Bác sĩ không có thẩm quyền tạo bài viết blog.` |
| `404 Not Found` | Không tìm thấy thuốc, dịch vụ hoặc bác sĩ tương ứng | `Không tìm thấy đối tượng yêu cầu.` |
| `409 Conflict` | Trùng lịch trực của bác sĩ hoặc trùng ghế nha khoa | `Bác sĩ đã có lịch trực trong khung thời gian này.` |
