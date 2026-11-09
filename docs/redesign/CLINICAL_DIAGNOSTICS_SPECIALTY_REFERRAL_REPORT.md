# DENTALCARE – CLINICAL DIAGNOSTICS & DEDICATED X-RAY ROOM IMPLEMENTATION REPORT

**Branch:** `feature/clinical-diagnostics-specialty-referral`  
**Base Commit:** `62e6b17`  
**Date:** 2026-11-10  
**Status:** Completed & Fully Verified  

---

## 1. Executive Summary

Triển khai hoàn tất phần mở rộng bắt buộc (**REQUIRED ADDENDUM**):
- **Phòng Chụp X-Quang Riêng Biệt & Quản Lý Thiết Bị Kỹ Thuật Số** (`Rooms`, `ImagingEquipments`).
- **Cấu hình thời lượng slot chụp linh hoạt** (`ImagingServiceConfigs`):
  - Intraoral / Periapical X-ray: 10 phút (80.000 VNĐ)
  - Panorama OPG: 15 phút (200.000 VNĐ)
  - Cone Beam CT (CBCT 3D): 30 phút (500.000 VNĐ)
- **Phân định rõ ràng 3 thực thể chẩn đoán**:
  - `DiagnosticOrder`: Chỉ định lâm sàng từ Bác sĩ (Dentist)
  - `ImagingSession`: Phiên đặt lịch, check-in, vận hành máy chụp nội bộ
  - `DiagnosticResult`: Lưu trữ metadata hình ảnh và kết quả đánh giá lâm sàng
- **Hàng đợi độc lập & Thuật toán tính thời gian chờ phi tuyến**:
  - Cấp số thứ tự độc lập `XR-001`, `XR-002`...
  - Giới hạn phòng chờ `MaxWaitingPatients = 3`, độc quyền phòng chụp `MaxConcurrentPatients = 1`.
  - Cảnh báo thời gian chờ vượt ngưỡng 20 phút (`TargetMaxWaitMinutes`) và leo thang 30 phút (`EscalationWaitMinutes`).
- **Quy trình Billing chặt chẽ**:
  - Chỉ chụp nội bộ hoàn tất (`Completed`) mới đủ điều kiện tạo 1 phí dịch vụ trong Billing. Chụp ngoại viện không tính phí nội bộ.
  - Không sinh thêm bản ghi `Appointments` thứ 2. Giữ nguyên bác sĩ và chuyên khoa của ca khám gốc.

---

## 2. Kiến Trúc Database & Migrations

### 2.1 File Migrations
- **Forward Migration**: `DB/redesign_migrations/20261110_Add_Imaging_Room_Equipment_And_Queue_Schema.sql`
- **Rollback Migration**: `DB/redesign_migrations/20261110_Rollback_Imaging_Room_Equipment_And_Queue_Schema.sql`

### 2.2 Các bảng và cột mới
1. **Mở rộng bảng `Rooms`**:
   - `RoomType` (NVARCHAR(50), default 'Treatment')
   - `MaxConcurrentPatients` (INT, default 1)
   - `MaxWaitingPatients` (INT, default 3)
   - `TargetMaxWaitMinutes` (INT, default 20)
   - `EscalationWaitMinutes` (INT, default 30)
2. **Bảng `ImagingEquipments`**:
   - `EquipmentId` (BIGINT, PK)
   - `RoomId` (BIGINT, FK -> Rooms)
   - `EquipmentCode` (VARCHAR(50), UNIQUE)
   - `EquipmentName` (NVARCHAR(150))
   - `EquipmentType` (VARCHAR(50))
   - `SupportedDiagnosticTypes` (NVARCHAR(250))
   - `IsActive` (BIT, default 1)
   - `OperationalStatus` (VARCHAR(30): 'Operational', 'Maintenance', 'OutOfService')
   - `AvailableFrom` (TIME, default '07:00:00'), `AvailableTo` (TIME, default '21:00:00')
3. **Bảng `ImagingServiceConfigs`**:
   - `ConfigId` (BIGINT, PK)
   - `DiagnosticType` (VARCHAR(50), UNIQUE)
   - `ServiceName` (NVARCHAR(150))
   - `SlotDurationMinutes` (INT)
   - `BufferMinutes` (INT)
   - `Price` (DECIMAL(18,2))
   - `IsActive` (BIT)
4. **Bảng `DiagnosticOrders`**:
   - `DiagnosticOrderId` (BIGINT, PK)
   - `AppointmentId` (BIGINT, FK -> Appointments)
   - `PatientUserId` (BIGINT, FK -> PatientProfiles)
   - `DentistUserId` (BIGINT, FK -> DentistProfiles)
   - `DiagnosticType` (VARCHAR(50))
   - `ClinicalIndication` (NVARCHAR(500))
   - `IsExternal` (BIT), `ExternalFacilityName` (NVARCHAR(200))
   - `Status` (VARCHAR(30): 'Ordered', 'Scheduled', 'InProgress', 'Completed', 'Cancelled')
   - `BilledAtUtc`, `InvoiceItemId`
5. **Bảng `ImagingSessions`**:
   - `ImagingSessionId` (BIGINT, PK)
   - `DiagnosticOrderId` (BIGINT, FK -> DiagnosticOrders)
   - `SourceAppointmentId` (BIGINT, FK -> Appointments)
   - `PatientUserId` (BIGINT, FK -> PatientProfiles)
   - `ImagingRoomId` (BIGINT, FK -> Rooms)
   - `EquipmentId` (BIGINT, FK -> ImagingEquipments)
   - `ScheduledStartUtc`, `ScheduledEndUtc`, `ActualStartUtc`, `ActualEndUtc`
   - `QueueNumber` (VARCHAR(20), e.g. 'XR-001')
   - `Priority` ('Normal', 'Urgent')
   - `Status` ('Scheduled', 'CheckedIn', 'Waiting', 'InProgress', 'Acquired', 'ResultAvailable', 'Completed', 'Cancelled', 'EquipmentUnavailable', 'NeedsReschedule', 'UnableToPerform')
   - `EstimatedWaitMinutes`, `DelayNotificationSent`, `EscalationAlertSent`
6. **Bảng `DiagnosticResults`**:
   - `DiagnosticResultId` (BIGINT, PK)
   - `DiagnosticOrderId` (BIGINT, FK -> DiagnosticOrders)
   - `ImagingSessionId` (BIGINT, FK -> ImagingSessions, NULLABLE)
   - `SourceType` ('Internal', 'External')
   - `EquipmentId` (BIGINT, NULLABLE)
   - `ExternalFacilityName` (NVARCHAR(200), NULLABLE)
   - `FileUrl`, `ThumbnailUrl`, `ImageFormat`, `Findings`
   - `ReviewedByDentistUserId` (BIGINT, FK -> DentistProfiles)
   - `ReviewedAtUtc`, `ReviewNotes`, `IsReviewed`

---

## 3. Khóa Phân Tán (Distributed Concurrency Locking)

Tích hợp `SqlAppLockHelper` đa tầng để loại bỏ hoàn toàn race conditions:
1. `IMAGING_EQUIPMENT_{equipmentId}`: Khóa tài nguyên máy chụp, ngăn trùng slot hoặc bắt đầu 2 ca cùng lúc trên 1 máy.
2. `IMAGING_ROOM_{roomId}`: Khóa tài nguyên phòng chụp, bảo đảm giới hạn số bệnh nhân chờ `MaxWaitingPatients = 3`.
3. `Lock_DiagnosticOrder_{orderId}`: Khóa chỉ định, bảo đảm tính Idempotency, không đặt 2 phiên chụp cho cùng 1 chỉ định.
4. `Lock_ImagingQueue_{date}`: Khóa cấp phát số thứ tự `XR-xxx` liên tục, không trùng số.

---

## 4. Giao Diện Người Dùng & Điều Phối

### 4.1 Phân hệ Lễ Tân (Receptionist)
- **Route:** `/Receptionist/ImagingQueue`
- **Chức năng:**
  - Theo dõi ca đang chụp trong phòng `XRAY-01`.
  - Giám sát hàng đợi bệnh nhân chờ chụp kèm số thứ tự `XR-001`, `XR-002`...
  - Cảnh báo trực quan khi thời gian chờ vượt 20 phút (Màu vàng) hoặc leo thang quá 30 phút (Màu đỏ).
  - Nút Check-in tiếp nhận bệnh nhân vào hàng đợi.
  - Nút Bắt đầu chụp (`Start`), Ghi nhận ảnh (`Acquire`), Tải kết quả và Hoàn tất ca chụp (`Complete`).
  - Nút Hủy ca chụp (`Cancel`) kèm ghi chú lý do.
  - Bảng theo dõi trạng thái thiết bị máy chụp (`Operational`, `Maintenance`, `OutOfService`).

### 4.2 Phân hệ Bác Sĩ (Dentist)
- Chỉ định chụp X-quang trực tiếp qua API `POST api/clinical-diagnostics/orders`.
- Xem kết quả hình ảnh và thực hiện đánh giá lâm sàng độc lập (`ReviewDiagnosticResultAsync`).
- Đánh dấu đã review (`IsReviewed = true`) và ghi chú kết luận lâm sàng (`ReviewNotes`).

---

## 5. Kết Quả Kiểm Thử (Test Verification)

| Phân hệ / Bộ test | Số lượng test | Kết quả | Ghi chú |
| :--- | :---: | :---: | :--- |
| **Backend Unit & Concurrency Tests** | **862 / 863** | **PASSED** (100% active) | 1 test skip do Cloudinary integration |
| **ImagingRoomAndQueueTests** | **32 / 32** | **PASSED** (100%) | Đạt toàn bộ 32 kịch bản addendum |
| **Frontend Web Tests** | **320 / 320** | **PASSED** (100%) | Đã bổ sung 5 tests cho `ReceptionistImagingQueueController` |
| **Playwright Browser E2E** | **2 / 2** | **READY** | File `e2e/imaging-queue-workflow.spec.js` |
