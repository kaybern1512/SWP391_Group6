# DENTALCARE – ADMIN MONTHLY DENTIST WORK SCHEDULE API CONTRACT

**Branch:** `feature/admin-monthly-scheduling`  
**Base:** `0294de5` (feature/admin-redesign)  
**Role:** `SystemAdministrator`  
**Backend Port:** `https://localhost:7351`  
**Frontend Port:** `https://localhost:7121`  
**Database:** `DentalCare_Admin_Dev` (SQL Server)  
**Date:** 2026-11-09  

---

## 1. Overview & Architecture Principles

Hệ thống quản lý lịch trực theo tháng (Monthly Dentist Work Scheduling) được xây dựng trên nguyên tắc **Single Source of Truth**:
1. **Database Schema:** Tái sử dụng 100% bảng `DentistWorkSchedules` (`WorkDate` kiểu `date`, `StartTime`, `EndTime`, `Status`). Không tạo bảng lưu trữ song song.
2. **Booking Integration:** `BookingService` đọc trực tiếp từ `DentistWorkSchedules` (`ws.WorkDate == targetDate && ws.Status == "Scheduled"`). Bất kỳ ca trực nào được tạo hàng loạt hoặc chỉnh sửa theo ngày sẽ lập tức phản ánh slot đặt lịch của bệnh nhân.
3. **Appointment Guard:** Không cho phép hủy hoặc gỡ ca trực nếu làm mồ côi các cuộc hẹn của bệnh nhân đã xác nhận (`Confirmed`, `CheckedIn`, `Waiting`, `Called`, `InService`).
4. **Concurrency & Idempotency:** Sử dụng `SqlAppLockHelper.DentistMasterKey(dentistUserId)` và kiểm tra `alreadyExists` bên trong transaction để chống xung đột phân công đồng thời.

---

## 2. API Endpoints

### 2.1. Lấy Lịch Trực Theo Tháng & KPIs
- **Method:** `GET`
- **Route:** `/api/DentistSchedule/monthly`
- **Authorization:** `[Authorize(Roles = "SystemAdministrator,Dentist")]` (Dentist chỉ được xem lịch của chính mình)
- **Query Parameters:**
  - `year` (int, default: current year)
  - `month` (int, 1-12, default: current month)
  - `departmentId` (long, optional)
  - `dentistUserId` (long, optional)
- **Response Format:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": null,
  "data": {
    "year": 2026,
    "month": 11,
    "departmentId": 1,
    "departmentName": "Khoa Răng Tổng Quát",
    "dentistUserId": 10,
    "dentistName": "Bác Sĩ Nguyễn Văn A",
    "totalWorkingDays": 22,
    "totalWorkingHours": 88.0,
    "totalOffDays": 2,
    "totalConfirmedAppointments": 14,
    "days": [
      {
        "date": "2026-11-01",
        "dayOfWeek": 7,
        "dayOfWeekName": "Chủ Nhật",
        "isCurrentMonth": true,
        "isWeekend": true,
        "isOnLeave": false,
        "leaveReason": null,
        "shifts": [
          {
            "dentistScheduleId": 101,
            "dentistUserId": 10,
            "dentistName": "Bác Sĩ Nguyễn Văn A",
            "departmentId": 1,
            "departmentName": "Khoa Răng Tổng Quát",
            "workDate": "2026-11-01",
            "startTime": "08:00:00",
            "endTime": "12:00:00",
            "status": "Scheduled",
            "note": "Ca Sáng"
          }
        ],
        "shiftType": "Morning",
        "confirmedAppointmentsCount": 2,
        "totalAppointmentsCount": 2
      }
    ]
  }
}
```

---

### 2.2. Kiểm Tra Trước Phân Công Hàng Loạt (Bulk Preview)
- **Method:** `POST`
- **Route:** `/api/DentistSchedule/bulk-preview`
- **Authorization:** `[Authorize(Roles = "SystemAdministrator")]`
- **Request Body:**
```json
{
  "departmentId": 1,
  "dentistUserIds": [10, 20],
  "fromDate": "2026-11-01",
  "toDate": "2026-11-30",
  "daysOfWeek": [1, 2, 3, 4, 5],
  "shifts": [
    { "startTime": "08:00", "endTime": "12:00", "note": "Ca Sáng" },
    { "startTime": "13:30", "endTime": "17:30", "note": "Ca Chiều" }
  ],
  "overwriteExisting": false
}
```
- **Response Format:**
```json
{
  "success": true,
  "statusCode": 200,
  "data": {
    "totalProposedShifts": 88,
    "validShiftsCount": 82,
    "duplicateShiftsCount": 4,
    "overlapShiftsCount": 2,
    "leaveConflictCount": 0,
    "appointmentConflictCount": 0,
    "departmentMismatchCount": 0,
    "items": [
      {
        "dentistUserId": 10,
        "dentistName": "Bác Sĩ Nguyễn Văn A",
        "departmentId": 1,
        "departmentName": "Khoa Răng Tổng Quát",
        "workDate": "2026-11-02",
        "startTime": "08:00:00",
        "endTime": "12:00:00",
        "status": "Valid",
        "message": "Hợp lệ",
        "canApply": true,
        "affectedAppointmentsCount": 0
      }
    ]
  }
}
```

---

### 2.3. Áp Dụng Phân Công Hàng Loạt (Bulk Apply)
- **Method:** `POST`
- **Route:** `/api/DentistSchedule/bulk-apply`
- **Authorization:** `[Authorize(Roles = "SystemAdministrator")]`
- **Request Body:**
```json
{
  "departmentId": 1,
  "dentistUserIds": [10, 20],
  "fromDate": "2026-11-01",
  "toDate": "2026-11-30",
  "daysOfWeek": [1, 2, 3, 4, 5],
  "shifts": [
    { "startTime": "08:00", "endTime": "12:00", "note": "Ca Sáng" }
  ],
  "overwriteExisting": false,
  "skipConflicts": true
}
```
- **Response Format:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Áp dụng phân công thành công (44 ca mới).",
  "data": {
    "createdCount": 44,
    "skippedCount": 0,
    "failedCount": 0,
    "messages": [
      "Đã tạo thành công 44 ca trực làm việc mới cho bác sĩ."
    ]
  }
}
```

---

### 2.4. Xem Trước Sao Chép Lịch Tháng (Copy Month Preview)
- **Method:** `POST`
- **Route:** `/api/DentistSchedule/copy-month-preview`
- **Authorization:** `[Authorize(Roles = "SystemAdministrator")]`
- **Request Body:**
```json
{
  "sourceYear": 2026,
  "sourceMonth": 10,
  "targetYear": 2026,
  "targetMonth": 11,
  "departmentId": 1,
  "dentistUserId": null,
  "mode": "DayOfWeekPattern",
  "excludeLeaves": true,
  "overwriteExisting": false,
  "skipConflicts": true
}
```
- **Response Format:**
```json
{
  "success": true,
  "statusCode": 200,
  "data": {
    "sourceShiftsCount": 40,
    "proposedShiftsCount": 42,
    "validCount": 40,
    "conflictCount": 2,
    "items": []
  }
}
```

---

### 2.5. Áp Dụng Sao Chép Lịch Tháng (Copy Month Apply)
- **Method:** `POST`
- **Route:** `/api/DentistSchedule/copy-month-apply`
- **Authorization:** `[Authorize(Roles = "SystemAdministrator")]`
- **Response Format:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Sao chép hoàn tất (40 ca mới).",
  "data": {
    "createdCount": 40,
    "skippedCount": 2,
    "failedCount": 0,
    "messages": [
      "Sao chép thành công 40 ca trực sang tháng 11/2026."
    ]
  }
}
```

---

### 2.6. Kiểm Tra Lịch Hẹn Bị Ảnh Hưởng (Affected Appointments Guard)
- **Method:** `GET`
- **Route:** `/api/DentistSchedule/affected-appointments`
- **Authorization:** `[Authorize(Roles = "SystemAdministrator")]`
- **Query Parameters:**
  - `dentistUserId` (long)
  - `workDate` (date: yyyy-MM-dd)
  - `startTime` (time: HH:mm, optional)
  - `endTime` (time: HH:mm, optional)
- **Response Format:**
```json
{
  "success": true,
  "statusCode": 200,
  "data": [
    {
      "appointmentId": 501,
      "appointmentCode": "APT-20261105-01",
      "patientName": "Nguyễn Văn Bệnh Nhân",
      "patientPhone": "0912345678",
      "serviceName": "Khám Răng Tổng Quát",
      "scheduledStart": "2026-11-05T09:00:00",
      "scheduledEnd": "2026-11-05T09:30:00",
      "status": "Confirmed"
    }
  ]
}
```

---

### 2.7. Chỉnh Sửa Ca Trực Riêng Lẻ Trong Ngày (Daily Override)
- **Method:** `PUT`
- **Route:** `/api/DentistSchedule/daily-override`
- **Authorization:** `[Authorize(Roles = "SystemAdministrator")]`
- **Request Body:**
```json
{
  "dentistUserId": 10,
  "departmentId": 1,
  "workDate": "2026-11-05",
  "isDayOff": false,
  "leaveReason": null,
  "shifts": [
    { "startTime": "08:00", "endTime": "11:30", "note": "Ca sáng điều chỉnh" },
    { "startTime": "13:30", "endTime": "17:00", "note": "Ca chiều điều chỉnh" }
  ]
}
```
- **Guard Validation Failure Response (khi có Appointment Confirmed bị ảnh hưởng):**
```json
{
  "success": false,
  "statusCode": 400,
  "message": "Không thể điều chỉnh ca trực vì có lịch hẹn đã xác nhận nằm ngoài khung giờ mới. Vui lòng kiểm tra danh sách lịch hẹn bị ảnh hưởng."
}
```
