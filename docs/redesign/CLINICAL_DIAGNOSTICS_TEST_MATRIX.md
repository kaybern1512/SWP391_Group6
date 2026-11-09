# TEST MATRIX – CLINICAL DIAGNOSTICS & IMAGING QUEUE

**Feature:** Dedicated X-ray Room, Equipment Scheduling & Imaging Queue  
**Branch:** `feature/clinical-diagnostics-specialty-referral`  
**Test File:** `BE/DentalClinic.Tests/ClinicalDiagnostics/ImagingRoomAndQueueTests.cs`  

---

## Danh Sách 32 Kịch Bản Kiểm Thử Backend & Concurrency

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

## Danh Sách 5 Kịch Bản Kiểm Thử Frontend Controller

**File:** `FE/DentalClinic.Web.Tests/Controllers/ReceptionistImagingQueueControllerTests.cs`

| ID | Tên Kịch Bản | Kết Quả |
| :---: | :--- | :---: |
| **FE01** | `Index_ReturnsViewWithQueueAndEquipments` | **PASSED** |
| **FE02** | `CheckIn_WhenSuccess_SetsTempDataAndRedirects` | **PASSED** |
| **FE03** | `Start_WhenSuccess_SetsTempDataAndRedirects` | **PASSED** |
| **FE04** | `Complete_WhenSuccess_SetsTempDataAndRedirects` | **PASSED** |
| **FE05** | `Cancel_WhenSuccess_SetsTempDataAndRedirects` | **PASSED** |
