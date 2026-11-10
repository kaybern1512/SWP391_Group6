# TEST MATRIX – CLINICAL DIAGNOSTICS & IMAGING QUEUE

**Feature:** Dedicated X-ray Room, Equipment Scheduling, Imaging Queue, Clinical Consultation & Specialty Referral Linkage  
**Branch:** `feature/clinical-diagnostics-specialty-referral`  

---

## 1. Danh Sách 32 Kịch Bản Kiểm Thử Unit & Mock (`ImagingRoomAndQueueTests.cs`)

| ID | Tên Kịch Bản | Mục Tiêu Nghiệp Vụ | Kết Quả |
| :---: | :--- | :--- | :---: |
| **TC01** | `CreateDiagnosticOrder_WithValidData_Succeeds` | Bác sĩ tạo chỉ định chẩn đoán hợp lệ cho ca khám | **PASSED** |
| **TC02** | `CreateDiagnosticOrder_InvalidDiagnosticType_ThrowsException` | Từ chối loại chẩn đoán không thuộc Intraoral, Panorama, CBCT | **PASSED** |
| **TC03** | `CreateDiagnosticOrder_EmptyIndication_ThrowsException` | Bắt buộc nhập lý do chỉ định lâm sàng | **PASSED** |
| **TC04** | `CreateDiagnosticOrder_ExternalWithoutFacilityName_ThrowsException` | Chụp ngoại viện bắt buộc nhập tên cơ sở thực hiện | **PASSED** |
| **TC05** | `CreateDiagnosticOrder_ExternalWithFacilityName_Succeeds` | Tạo thành công chỉ định chụp phim ngoại viện | **PASSED** |
| **TC06** | `CreateDiagnosticOrder_CancelledAppointment_ThrowsException` | Từ chối tạo chỉ định cho ca khám đã bị hủy | **PASSED** |
| **TC07** | `GetDiagnosticOrdersByAppointment_ReturnsOrders` | Truy xuất toàn bộ chỉ định của một cuộc hẹn | **PASSED** |
| **TC08** | `GetAvailableSlots_WhenEquipmentOperational_GeneratesSlotsBasedOnConfigDuration` | Quét slot tự động dựa trên thời lượng cấu hình DB (10m, 15m, 30m) | **PASSED** |
| **TC09** | `GetAvailableSlots_WhenEquipmentInMaintenance_ReturnsEmptyList` | Máy bảo trì (`Maintenance`) không trả về slot khả dụng | **PASSED** |
| **TC10** | `GetAvailableSlots_WhenEquipmentOutOfService_ReturnsEmptyList` | Máy hỏng (`OutOfService`) không cho phép đặt lịch | **PASSED** |
| **TC11** | `GetAvailableSlots_FiltersOutBookedIntervals` | Loại bỏ các khung giờ đã có phiên chụp đang hoạt động | **PASSED** |
| **TC12** | `BookImagingSession_WithValidSlot_CreatesSessionAndMarksOrderScheduled` | Đặt lịch phiên chụp nội bộ và cập nhật chỉ định sang Scheduled | **PASSED** |
| **TC13** | `BookImagingSession_WhenEquipmentMaintenance_ThrowsException` | Chặn đặt lịch trên máy đang bảo trì | **PASSED** |
| **TC14** | `BookImagingSession_WhenUnsupportedDiagnosticType_ThrowsException` | Chặn đặt lịch dịch vụ mà máy không hỗ trợ | **PASSED** |
| **TC15** | `BookImagingSession_WhenOrderIsExternal_ThrowsException` | Chặn đặt lịch phòng chụp nội bộ cho chỉ định chụp ngoại viện | **PASSED** |
| **TC16** | `BookImagingSession_WhenOrderAlreadyHasActiveSession_ThrowsException_Idempotency` | Idempotency guard: không tạo 2 phiên chụp cho cùng 1 chỉ định | **PASSED** |
| **TC17** | `BookImagingSession_WhenIntervalOverlaps_ThrowsException_ConcurrencyGuard` | Khóa trùng lặp thời gian trên cùng thiết bị (Concurrency check) | **PASSED** |
| **TC18** | `CheckInImagingSession_GeneratesSequentialQueueNumber_XR001` | Cấp số thứ tự chụp riêng biệt dạng `XR-001`, `XR-002`... | **PASSED** |
| **TC19** | `CheckInImagingSession_WhenNotScheduled_ThrowsException` | Chỉ cho phép check-in phiên chụp ở trạng thái Scheduled | **PASSED** |
| **TC20** | `CheckInImagingSession_WhenQueueExceedsMaxWaiting_ThrowsException` | Bảo vệ công suất phòng: từ chối check-in khi hàng đợi đạt max 3 người | **PASSED** |
| **TC21** | `CheckInImagingSession_CalculatesNonLinearEstimatedWaitMinutes` | Tính thời gian chờ phi tuyến dựa trên tổng thời lượng ca đang đợi | **PASSED** |
| **TC22** | `CheckInImagingSession_WhenWaitExceedsTarget20Minutes_SetsDelayNotification` | Tự động bật cờ cảnh báo vận hành khi thời gian chờ ≥ 20 phút | **PASSED** |
| **TC23** | `CheckInImagingSession_WhenWaitExceedsEscalation30Minutes_SetsEscalationAlert` | Bật cờ cảnh báo leo thang khi thời gian chờ ≥ 30 phút (không tự hủy) | **PASSED** |
| **TC24** | `StartImagingSession_WhenMachineAlreadyInProgress_ThrowsException` | Độc quyền 1 ca chụp: cấm bắt đầu nếu máy đang có ca InProgress | **PASSED** |
| **TC25** | `StartImagingSession_FromWaiting_TransitionsToInProgress` | Chuyển trạng thái từ Waiting sang InProgress và ghi nhận thời gian thực | **PASSED** |
| **TC26** | `AcquireImage_WhenInProgress_TransitionsToAcquired` | Ghi nhận hoàn thành công đoạn chụp phim (Acquired) | **PASSED** |
| **TC27** | `UploadDiagnosticResult_WithValidImage_CreatesResultAndUpdatesSession` | Tải lên ảnh kết quả, gắn metadata và chuyển ResultAvailable | **PASSED** |
| **TC28** | `CompleteImagingSession_WhenAcquiredOrResultAvailable_TransitionsToCompleted` | Hoàn tất phiên chụp, đủ điều kiện đưa vào hóa đơn viện phí | **PASSED** |
| **TC29** | `ReviewDiagnosticResult_DentistAddsFindingsAndNotes_MarksReviewed` | Bác sĩ đánh giá lâm sàng, ghi nhận nhận xét và kết luận | **PASSED** |
| **TC30** | `ReviewDiagnosticResult_CompletedSessionDoesNotAutoReview_RequiresExplicitDentistReview` | Phiên chụp Completed không tự đánh giá lâm sàng; yêu cầu Dentist review | **PASSED** |
| **TC31** | `CancelImagingSession_RevertsOrderToOrderedIfNoActiveSessions` | Hủy ca chụp và hoàn trả trạng thái chỉ định về Ordered | **PASSED** |
| **TC32** | `BillingPreparation_IncludesCompletedInternalImaging_ExcludesExternalImaging` | Billing tích hợp: chỉ tính phí chụp nội bộ, không tính phí chụp ngoại viện | **PASSED** |

---

## 2. Danh Sách 5 Kịch Bản Kiểm Thử SQL Server Concurrency Đa Luồng Thực Tế

**File:** `BE/DentalClinic.Tests/ClinicalDiagnostics/SqlServerImagingAndReferralConcurrencyTests.cs`  
**Database:** `DentalCare_Integration_Dev`

| ID | Tên Kịch Bản | Mục Tiêu Nghiệp Vụ | Kết Quả |
| :---: | :--- | :--- | :---: |
| **SQL01** | `Test1_TwoConcurrentBookings_OnSameMachineAndSlot_OnlyOneSucceeds` | Hai luồng đặt lịch cùng 1 máy và 1 slot thời gian; applock đảm bảo duy nhất 1 thành công | **PASSED** |
| **SQL02** | `Test2_TwoConcurrentCheckIns_NearCapacity_MaxWaitingNotExceeded` | Hai luồng check-in khi phòng chờ còn 1 chỗ trống (2/3); đảm bảo chỉ 1 ca vào hàng đợi | **PASSED** |
| **SQL03** | `Test3_ConcurrentQueueNumbering_YieldsDistinctSequentialNumbers` | Hai luồng check-in đồng thời; transaction lock đảm bảo sinh ra 2 số thứ tự khác nhau | **PASSED** |
| **SQL04** | `Test4_TwoConcurrentReferralScheduling_OnlyOneDestinationApptCreated` | Hai luồng cùng đặt lịch đích cho 1 phiếu chuyển khoa; đảm bảo chỉ 1 lịch đích được tạo | **PASSED** |
| **SQL05** | `Test5_ConcurrentInvoiceCreation_SpGetAppLockPreventsDuplicateBilling` | Hai luồng cùng lập hóa đơn cho ca có chụp X-quang; khóa appointment chống double-billing | **PASSED** |

---

## 3. Danh Sách 10 Kịch Bản Kiểm Thử Real Playwright E2E Browser / API

**File:** `e2e/imaging-queue-workflow.spec.js`  
**Browser:** Chromium Headless  
**Environment:** BE `https://localhost:7350` / FE `https://localhost:7129`

| ID | Tên Kịch Bản | Mục Tiêu Nghiệp Vụ | Kết Quả |
| :---: | :--- | :--- | :---: |
| **E2E-01** | `Receptionist navigates to Imaging Queue and verifies room, equipment status & KPIs` | Lễ tân giám sát phòng XRAY-01, thiết bị XRAY-MACH-01, KPI ca chụp và hàng đợi | **PASSED** |
| **E2E-02** | `Receptionist verifies queue sections, capacity indicators, and interactive controls` | Kiểm tra giao diện hàng đợi, các thẻ điều phối, chỉ số công suất phòng | **PASSED** |
| **E2E-03** | `RBAC Guard: Receptionist is blocked (HTTP 403) from acquiring images or uploading technical results` | Phân quyền: Lễ tân gọi Acquire/Upload bị trả về HTTP 403 Forbidden | **PASSED** |
| **E2E-04** | `RBAC Guard: SystemAdministrator is blocked (HTTP 403) from clinical review` | Phân quyền: Admin gọi ReviewResult bị chặn HTTP 403 (chỉ Bác sĩ mới được review) | **PASSED** |
| **E2E-05** | `Receptionist navigates to Referrals coordination workspace and verifies filter pills and table` | Lễ tân mở không gian điều phối chuyển khoa, kiểm tra bộ lọc trạng thái và danh sách | **PASSED** |
| **E2E-06** | `Dentist navigates to Dentist Referrals, switches to Consultations tab, and inspects request modal` | Bác sĩ vào tab Hội Chẩn Lâm Sàng, mở modal tạo yêu cầu hội chẩn chuyên môn | **PASSED** |
| **E2E-07** | `Full Diagnostic Imaging Workflow: Order, Slot Booking, Check-in, Acquisition & Real File Upload` | Toàn trình: Tạo chỉ định -> Đặt slot -> Check-in hàng đợi -> Chụp -> Upload file ảnh thật | **PASSED** |
| **E2E-08** | `Secure Image Streaming & Clinical Dentist Result Review` | Stream tệp ảnh bảo mật qua API (`image/png`) và Bác sĩ đánh giá lâm sàng | **PASSED** |
| **E2E-09** | `Clinical Consultation: Dentist A creates consultation, Dentist B responds with clinical opinion` | Bác sĩ A gửi câu hỏi hội chẩn, Bác sĩ B phản hồi ý kiến chuyên môn | **PASSED** |
| **E2E-10** | `Specialty Referral Scheduling & DepartmentServices Pricing Verification` | Điều phối chuyển khoa đích và xác minh đơn giá dịch vụ lấy từ DepartmentServices | **PASSED** |

---

## 4. Danh Sách 5 Kịch Bản Kiểm Thử Frontend Controller

**File:** `FE/DentalClinic.Web.Tests/Controllers/ReceptionistImagingQueueControllerTests.cs`

| ID | Tên Kịch Bản | Kết Quả |
| :---: | :--- | :---: |
| **FE01** | `Index_ReturnsViewWithQueueAndEquipments` | **PASSED** |
| **FE02** | `CheckIn_WhenSuccess_SetsTempDataAndRedirects` | **PASSED** |
| **FE03** | `Start_WhenSuccess_SetsTempDataAndRedirects` | **PASSED** |
| **FE04** | `Complete_WhenSuccess_SetsTempDataAndRedirects` | **PASSED** |
| **FE05** | `Cancel_WhenSuccess_SetsTempDataAndRedirects` | **PASSED** |
