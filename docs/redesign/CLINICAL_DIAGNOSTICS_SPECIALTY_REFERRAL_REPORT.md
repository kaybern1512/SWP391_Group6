# DENTALCARE – CLINICAL DIAGNOSTICS & DEDICATED X-RAY ROOM IMPLEMENTATION REPORT

**Branch:** `feature/clinical-diagnostics-specialty-referral`  
**Base Baseline:** `62e6b17`  
**Date:** 2026-11-10  
**Status:** Hardened, Fully Verified & Ready for Integration  

---

## 1. Executive Summary

Triển khai và hoàn tất toàn diện chức năng **Chẩn Đoán Hình Ảnh, Phòng Chụp X-Quang Độc Lập, Hội Chẩn Chuyên Môn & Chuyển Khoa Nội Bộ** (`Clinical Diagnostics, Dedicated X-Ray Room, Imaging Queue, Clinical Consultation & Specialty Referral Linkage`):

1. **Phòng Chụp X-Quang Riêng Biệt & Quản Lý Thiết Bị Kỹ Thuật Số** (`Rooms`, `ImagingEquipments`):
   - Phòng chụp `XRAY-01` ("Phòng Chụp X-quang 01") với độc quyền vận hành 1 ca (`MaxConcurrentPatients = 1`), giới hạn hàng đợi chờ tối đa 3 bệnh nhân (`MaxWaitingPatients = 3`).
   - Thiết bị kỹ thuật số `XRAY-MACH-01` ("Máy Chụp X-Quang Kỹ Thuật Số Đa Năng Carestream CS 8200 3D") hỗ trợ Intraoral, Panorama, CBCT.
2. **Cấu hình thời lượng & Đơn giá chuẩn kết nối Single Source of Truth**:
   - `ImagingServiceConfigs` liên kết trực tiếp với bảng `DepartmentServices` để đồng bộ giá niêm yết:
     - Intraoral / Periapical: 10 phút (80.000 VNĐ)
     - Panorama OPG: 15 phút (200.000 VNĐ)
     - Cone Beam CT (CBCT 3D): 30 phút (500.000 VNĐ)
3. **Phân định rõ ràng 3 thực thể chẩn đoán**:
   - `DiagnosticOrder`: Chỉ định lâm sàng từ Bác sĩ điều trị.
   - `ImagingSession`: Phiên đặt lịch khung giờ, check-in, vận hành máy chụp nội bộ.
   - `DiagnosticResult`: Lưu trữ metadata tệp ảnh, liên kết tệp vật lý bảo mật, và ý kiến đánh giá chuyên môn của bác sĩ.
4. **Hội chẩn chuyên môn lâm sàng (`ClinicalConsultation`)**:
   - Cho phép Bác sĩ A gửi yêu cầu xin ý kiến chuyên môn từ Bác sĩ B cho một ca bệnh phức tạp.
   - Bác sĩ B xem tóm tắt lâm sàng, hình ảnh X-quang và phản hồi ý kiến chuyên môn (`DentistOpinion`).
5. **Liên kết Chuyển Chuyên Khoa Nội Bộ (`DepartmentReferral`)**:
   - Bổ sung `SourceAppointmentId` (ca khám ban đầu) và `DestinationAppointmentId` (ca khám chuyên khoa đích).
   - Lễ tân điều phối đặt lịch đích tại giao diện `/Receptionist/Referrals` với đầy đủ kiểm tra lịch làm việc bác sĩ, ngày nghỉ phép và tính khả dụng của ghế nha khoa (`DentalChairs`).
   - Tự động đồng bộ trạng thái khi ca khám đích bị hủy hoặc dời lịch.
6. **Hàng đợi độc lập & Thuật toán tính thời gian chờ phi tuyến**:
   - Cấp số thứ tự độc lập `XR-001`, `XR-002`... cho phòng X-quang.
   - Cảnh báo thời gian chờ vượt ngưỡng 20 phút (`TargetMaxWaitMinutes`) và leo thang 30 phút (`EscalationWaitMinutes`).
7. **Bảo mật RBAC & Tệp ảnh bảo mật**:
   - Ghi nhận ảnh (`AcquireImage`) và tải tệp (`UploadResultFile`): Chỉ Bác sĩ (`Dentist`) hoặc Quản trị viên (`SystemAdministrator`). Lễ tân bị chặn 403 Forbidden.
   - Đánh giá lâm sàng (`ReviewResult`): Chỉ Bác sĩ (`Dentist`) mới có quyền chuyên môn. Quản trị viên bị chặn 403 Forbidden.
   - Stream ảnh an toàn: Endpoint `GET /api/clinical-diagnostics/results/{id}/image` xác thực quyền truy cập trước khi truyền tải byte hình ảnh, không để lộ đường dẫn tệp trực tiếp trên web server.
8. **Quy trình Billing chặt chẽ & Idempotent**:
   - Chỉ các phiên chụp nội bộ hoàn tất (`Completed`) mới được đưa vào hóa đơn viện phí.
   - Sử dụng khóa phân tán `sp_getapplock` ngăn chặn tuyệt đối double-billing khi có nhiều yêu cầu tạo hóa đơn đồng thời.

---

## 2. Kiến Trúc Database & Migrations

### 2.1 File Migrations
- `DB/redesign_migrations/20261110_Add_Imaging_Room_Equipment_And_Queue_Schema.sql`
- `DB/redesign_migrations/20261110_Rollback_Imaging_Room_Equipment_And_Queue_Schema.sql`
- `DB/redesign_migrations/20261110_Add_Clinical_Consultation_And_Referral_Linkage.sql`
- `DB/redesign_migrations/20261110_Rollback_Clinical_Consultation_And_Referral_Linkage.sql`

Đã thực thi thành công trên:
- `DentalCare_Integration_Dev`
- `DentalCare_Leave_Dev`
- `DentalCare_Combined_Leave_QA`

---

## 3. Khóa Phân Tán (Distributed Concurrency Locking)

Tích hợp `SqlAppLockHelper` đa tầng kết hợp giao dịch Relational Transaction (`BeginTransactionAsync`):
1. `IMAGING_EQUIPMENT_{equipmentId}`: Khóa tài nguyên máy chụp, ngăn trùng slot hoặc bắt đầu 2 ca cùng lúc trên 1 máy.
2. `IMAGING_ROOM_{roomId}`: Khóa tài nguyên phòng chụp, bảo đảm giới hạn số bệnh nhân chờ `MaxWaitingPatients = 3`.
3. `Lock_DiagnosticOrder_{orderId}`: Khóa chỉ định, bảo đảm tính Idempotency, không đặt 2 phiên chụp cho cùng 1 chỉ định.
4. `Lock_ImagingQueue_{date}`: Khóa cấp phát số thứ tự `XR-xxx` liên tục, không trùng số giữa các tiến trình đồng thời.
5. `Lock_Referral_{referralId}`: Khóa chuyển khoa, ngăn điều phối 2 lịch đích đồng thời cho 1 phiếu chuyển khoa.
6. `Lock_Appointment_Invoice_{appointmentId}`: Khóa xuất hóa đơn viện phí chống double-billing.

---

## 4. Giao Diện Người Dùng & Điều Phối

### 4.1 Phân hệ Lễ Tân (Receptionist)
- **Hàng Đợi Chụp X-Quang (`/Receptionist/ImagingQueue`)**:
  - Giám sát phòng `XRAY-01`, tình trạng máy `XRAY-MACH-01`.
  - Giám sát ca đang chụp trong phòng và hàng đợi chờ chụp kèm số `XR-xxx`.
  - Cảnh báo thời gian chờ vượt ngưỡng 20 phút / 30 phút.
  - Nút Check-in tiếp nhận bệnh nhân vào hàng đợi.
- **Điều Phối Chuyển Chuyên Khoa (`/Receptionist/Referrals`)**:
  - Bộ lọc trạng thái: Tất Cả, Chờ Điều Phối, Đã Đặt Lịch Đích, Đã Từ Chối.
  - Xem tóm tắt ca gốc (chẩn đoán, kế hoạch điều trị, ảnh X-quang liên quan).
  - Modal điều phối đặt lịch đích: chọn bác sĩ chuyên khoa đích, chọn ghế nha khoa, chọn khung giờ.

### 4.2 Phân hệ Bác Sĩ (Dentist)
- **Hội Chẩn & Chuyển Khoa (`/Dentist/Referrals`)**:
  - Tab "Hội Chẩn Lâm Sàng": Xem danh sách yêu cầu hội chẩn chuyên môn gửi đi và nhận về.
  - Modal tạo yêu cầu hội chẩn chuyên môn (chọn ca khám, chọn bác sĩ hội chẩn, nhập câu hỏi lâm sàng).
  - Modal phản hồi ý kiến chuyên môn (nhập `DentistOpinion` và khuyến nghị điều trị).

---

## 5. Kết Quả Kiểm Thử (Verification Matrix)

### 5.1 Backend Unit & Integration Tests (.NET 8)
- **Tổng số test:** **868**
- **Passed:** **867** (100% active tests)
- **Skipped:** **1** (`UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully` do không gắn secret Cloudinary)
- **Failed:** **0**

### 5.2 Real SQL Server Concurrency Tests (`SqlServerImagingAndReferralConcurrencyTests.cs`)
Chạy trực tiếp trên SQL Server `DentalCare_Integration_Dev`:
- **Test 1:** `Test1_TwoConcurrentBookings_OnSameMachineAndSlot_OnlyOneSucceeds` -> **PASSED**
- **Test 2:** `Test2_TwoConcurrentCheckIns_NearCapacity_MaxWaitingNotExceeded` -> **PASSED**
- **Test 3:** `Test3_ConcurrentQueueNumbering_YieldsDistinctSequentialNumbers` -> **PASSED**
- **Test 4:** `Test4_TwoConcurrentReferralScheduling_OnlyOneDestinationApptCreated` -> **PASSED**
- **Test 5:** `Test5_ConcurrentInvoiceCreation_SpGetAppLockPreventsDuplicateBilling` -> **PASSED**

### 5.3 Playwright Browser E2E Tests (`e2e/imaging-queue-workflow.spec.js`)
Chạy thực tế trên Chromium headless với backend `https://localhost:7350` và frontend `https://localhost:7129`:
- **E2E-01:** Receptionist navigates to Imaging Queue and verifies room, equipment status & KPIs -> **PASSED**
- **E2E-02:** Receptionist verifies queue sections, capacity indicators, and interactive controls -> **PASSED**
- **E2E-03:** RBAC Guard: Receptionist is blocked (HTTP 403) from acquiring images or uploading technical results -> **PASSED**
- **E2E-04:** RBAC Guard: SystemAdministrator is blocked (HTTP 403) from clinical review (Dentist strictly required) -> **PASSED**
- **E2E-05:** Receptionist navigates to Referrals coordination workspace and verifies filter pills and table -> **PASSED**
- **E2E-06:** Dentist navigates to Dentist Referrals, switches to Consultations tab, and inspects request modal -> **PASSED**
- **E2E-07:** Full Diagnostic Imaging Workflow: Order, Slot Booking, Check-in, Acquisition & Real File Upload -> **PASSED**
- **E2E-08:** Secure Image Streaming & Clinical Dentist Result Review -> **PASSED**
- **E2E-09:** Clinical Consultation: Dentist A creates consultation, Dentist B responds with clinical opinion -> **PASSED**
- **E2E-10:** Specialty Referral Scheduling & DepartmentServices Pricing Verification -> **PASSED**

**Kết quả E2E:** **10/10 PASSED (100%)**.
