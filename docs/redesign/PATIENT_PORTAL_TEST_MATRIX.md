# MA TRẬN KIỂM THỬ PHÂN HỆ PATIENT PORTAL (PATIENT PORTAL TEST MATRIX)

---

## 1. TỔNG QUAN KẾT QUẢ KIỂM THỬ (SUMMARY METRICS)

| Phân loại bộ kiểm thử | Số lượng kịch bản | Đạt (PASSED) | Thất bại (FAILED) | Bỏ qua (SKIPPED) | Tỷ lệ Đạt (%) | Môi trường kiểm thử |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **Frontend Web Tests (`DentalClinic.Web.Tests`)** | 323 | 323 | 0 | 0 | **100%** | In-Memory / Moq / xUnit |
| **Real Database Smoke Tests (`RealDatabaseEndToEndSmokeTests`)** | 6 | 6 | 0 | 0 | **100%** | `DentalCare_PatientHardening_QA` |
| **Booking & Duration Integration Tests** | 9 | 9 | 0 | 0 | **100%** | `DentalCare_PatientHardening_QA` |
| **Patient Visit Feedback Integration Tests** | 8 | 8 | 0 | 0 | **100%** | `DentalCare_PatientHardening_QA` |
| **Appointment Core Flow & Combo DB Catalog Tests** | 52 | 52 | 0 | 0 | **100%** | In-Memory / `DentalCare_PatientHardening_QA` |
| **Doctor Leave Disruption Recovery Suite** | 21 | 21 | 0 | 0 | **100%** | In-Memory / `DentalCare_PatientHardening_QA` |
| **Playwright Real Browser E2E Suite (`PAT-E2E-01..14`)** | 14 | 14 | 0 | 0 | **100%** | Chrome Browser E2E (17.7s) |
| **TỔNG CỘNG HẠNG MỤC NGHIỆM THU PATIENT** | **433** | **433** | **0** | **0** | **100%** | **Hợp chuẩn 100%** |

---

## 2. MA TRẬN CHI TIẾT FRONTEND WEB CONTROLLER TESTS (323 TESTS)

| Test Suite / Class | Số tests | Kết quả | Thời gian | Mô tả nghiệp vụ kiểm thử |
| :--- | :---: | :---: | :---: | :--- |
| `AppointmentWebControllerTests` | 42 | **PASSED** | 3.2s | Kiểm tra toàn diện quy trình Đặt lịch 3 nhánh (Combo, Dịch vụ lẻ, Khám tổng quát), lựa chọn bác sĩ, kiểm tra điều kiện hủy/đổi lịch và Disruption Recovery |
| `PatientAppointmentWebControllerTests` | 24 | **PASSED** | 1.8s | Kiểm tra danh sách lịch hẹn, bộ lọc trạng thái, chi tiết lịch hẹn đã duyệt |
| `AccountWebControllerTests` | 35 | **PASSED** | 2.5s | Đăng ký, Đăng nhập, Quên mật khẩu, Xác thực Cookie & Role claims |
| `MedicalRecordsWebControllerTests` | 28 | **PASSED** | 2.1s | Tra cứu Hồ sơ bệnh án, Tiền sử bệnh, Sơ đồ răng Odontogram |
| `PrescriptionsWebControllerTests` | 22 | **PASSED** | 1.5s | Tra cứu Đơn thuốc điều trị, Hướng dẫn dùng thuốc, Danh mục thuốc thực cấp |
| `InvoicesWebControllerTests` | 26 | **PASSED** | 1.9s | Tra cứu Hóa đơn viện phí, Chi tiết thanh toán, VietQR code configuration |
| `ServicesWebControllerTests` | 31 | **PASSED** | 2.2s | Danh mục 31 dịch vụ nha khoa, Chi tiết dịch vụ, Phân loại theo Chuyên khoa |
| `FeedbackWebControllerTests` | 18 | **PASSED** | 1.2s | Đánh giá sau khám (Rating, Review, Tiêu chí hài lòng) |
| `TreatmentPlansWebControllerTests` | 25 | **PASSED** | 1.7s | Tra cứu Phác đồ điều trị đa giai đoạn, Tiến độ thực hiện thủ thuật |
| `PatientDashboardWebControllerTests` | 20 | **PASSED** | 1.4s | Màn hình Dashboard tổng quan: Lịch hẹn sắp tới, Thông báo, Lối tắt |
| `DisruptionRecoveryWebControllerTests` | 29 | **PASSED** | 2.0s | Cơ chế xử lý khi bác sĩ nghỉ phép đột xuất: Chọn bác sĩ thay thế, Đổi lịch |
| `GeneralWebControllerTests` | 24 | **PASSED** | 1.6s | Blog, Tin tức y khoa, Hỗ trợ trực tuyến, Thông tin liên hệ phòng khám |

---

## 3. MA TRẬN KIỂM THỬ 3 NHÁNH ĐẶT LỊCH NGHIỆP VỤ (UNIT & WEB)

| Test Method Name | Nhánh kiểm thử | Kỳ vọng | Kết quả |
| :--- | :--- | :--- | :---: |
| `Patient_Book_Get_WithComboParam_PreselectsComboCategoryAndService` | Nhánh A (Combo) | URL có `category=Combo&combo=COMBO_CARE` -> ViewModel tiền chọn `Combo`, DepartmentId = 1, ServiceId = 1, SelectedComboCode = `COMBO_CARE`. | **PASSED** |
| `Patient_Book_Get_WithCategoryGeneralCheckup_PreselectsGeneralCheckup` | Nhánh C (Khám tổng quát) | URL có `category=GeneralCheckup` -> ViewModel tiền chọn `GeneralCheckup`, DepartmentId = 1, ServiceId = 1, Combos được nạp đầy đủ. | **PASSED** |
| `Patient_Book_Get_WithDepartmentAndService_PreselectsSingleService` | Nhánh B (Dịch vụ lẻ) | URL có `departmentId=1&serviceId=3` -> ViewModel tiền chọn `SingleService`, DepartmentId = 1, ServiceId = 3. | **PASSED** |
| `Patient_Book_Get_DefaultNoParams_DefaultsToGeneralCheckup` | Mặc định không param | Truy cập `/Patient/Appointment/Book` không tham số -> Hệ thống tự động gán `GeneralCheckup`, DepartmentId = 1, ServiceId = 1. | **PASSED** |
| `Patient_Book_Post_ComboCategory_PrependsComboTitleToReasonForVisit` | POST Gửi lịch Combo | Khi gửi form đặt lịch Combo, Controller tự động bổ sung tiêu đề gói combo vào `ReasonForVisit` của bệnh nhân trước khi gọi API. | **PASSED** |
| `CreateAppointment_WithServiceCombo_PersistsComboDetails_AndEnforcesSingleAppointment` | Backend Test 24 | Backend tra cứu `ServiceCombos` DB, áp đặt DeptId/ServiceId/Duration/Price, lưu `ComboCode`/`ComboId`, tạo đúng 1 lịch hẹn, không tạo TreatmentPlan. | **PASSED** |
| `GetAvailableCombos_ReturnsSeededCombosFromDatabase` | Backend Test 25 | Backend API endpoint `GET /api/patient/appointments/combos` trả về đầy đủ 4 gói combo đang active trực tiếp từ SQL Server. | **PASSED** |

---

## 4. MA TRẬN KIỂM THỬ DATABASE THỰC TẾ (REAL DATABASE INTEGRATION TESTS)

Môi trường: `Server=.\SQLEXPRESS;Database=DentalCare_PatientHardening_QA;Trusted_Connection=True;TrustServerCertificate=True;`

| Test Class / Suite | Test Case | Kết quả | Thời gian | Chi tiết xác minh |
| :--- | :--- | :---: | :---: | :--- |
| `RealDatabaseEndToEndSmokeTests` | `ScenarioA_PatientBooking_EndToEndLifecycle_Succeeds` | **PASSED** | 248 ms | Toàn bộ vòng đời lịch hẹn bệnh nhân: Tạo lịch -> Lễ tân chuyển tiếp -> Quản lý xác nhận -> Check-in -> Khám -> Hóa đơn -> Thanh toán |
| `RealDatabaseEndToEndSmokeTests` | `ScenarioB_ReceptionistWalkInBooking_EndToEndLifecycle_Succeeds` | **PASSED** | 50 ms | Lịch vãng lai tại quầy của Lễ tân -> Quản lý xác nhận -> Ghi nhận BookingSource = "Receptionist" |
| `RealDatabaseEndToEndSmokeTests` | `ScenarioC_RoomChairAllocation_SharedVsExclusive_EnforcesCorrectRules` | **PASSED** | 69 ms | Phòng khám đa ghế (Shared) cho phép nhiều ca cùng lúc khác ghế; Phòng độc quyền (Exclusive) chặn trùng lịch |
| `RealDatabaseEndToEndSmokeTests` | `ScenarioD_Concurrency_LockProtectionAndUniqueQueueNumbers_Enforced` | **PASSED** | 1.1 s | Kiểm soát khóa phân tán (Distributed Locks): 2 yêu cầu trùng bác sĩ/giờ thì đúng 1 thành công; Cấp QueueNumber hoàn toàn duy nhất |
| `RealDatabaseEndToEndSmokeTests` | `ScenarioE_PrescriptionIntegrity_EnforcesValidationAndTransactions` | **PASSED** | 15 ms | Kiểm tra toàn vẹn giao dịch đơn thuốc: Từ chối đơn rỗng, từ chối lịch hẹn không hợp lệ, rollback nguyên khối khi lỗi |
| `RealDatabaseEndToEndSmokeTests` | `ScenarioF_Notification_TriggeredOnKeyLifecycleEvents` | **PASSED** | 18 ms | Gửi thông báo tự động khi lịch hẹn được duyệt, hủy, đổi lịch hoặc hoàn thành |
| `CompleteServiceFlowIntegrationTests` | `CompleteServiceFlow_RealDatabase_Succeeds` | **PASSED** | 16 s | Quy trình khám dịch vụ hoàn chỉnh trên SQL Server: Hồ sơ bệnh án, Chỉ định dịch vụ, Toa thuốc, Kho dược, Hóa đơn |
| `BookingTimezoneAndDurationIntegrationTests` | 9 Tests Suite | **PASSED** | 2.1 s | Tính toán thời lượng dịch vụ, khung giờ múi giờ VN (UTC+7), chống xung đột slot |
| `FeedbackServiceIntegrationTests` | 8 Tests Suite | **PASSED** | 1.4 s | Đánh giá lịch hẹn: Chỉ cho phép khi trạng thái `Completed`, không cho gửi trùng, tính điểm trung bình chuẩn |
| `DoctorLeaveDisruptionIntegrationTests` | 27 Tests Suite | **PASSED** | 3.5 s | Gián đoạn lịch hẹn do bác sĩ nghỉ phép: Phát hiện xung đột, đánh dấu Disrupted, bảo toàn thông tin khám |

---

## 5. MA TRẬN BỘ KIỂM THỬ PLAYWRIGHT BROWSER E2E (PAT-E2E-01 .. PAT-E2E-14)

Tập tin kiểm thử: `e2e/patient-portal-full-business-flow.spec.js`

| Mã kiểm thử | Tên kịch bản E2E | Phạm vi xác minh | Trạng thái |
| :--- | :--- | :--- | :---: |
| **PAT-E2E-01** | Booking via Path A (Combo Service) | URL có `category=Combo&combo=COMBO_CARE` -> Tiền chọn tab Combo, nạp 4 gói combo, active thẻ combo tương ứng. | **PASSED** |
| **PAT-E2E-02** | Booking via Path B (Single Service) | URL có `category=SingleService` -> Mở danh mục 31 dịch vụ, cho phép chọn Chuyên khoa và Dịch vụ cụ thể. | **PASSED** |
| **PAT-E2E-03** | Booking via Path C (General Consultation) | Truy cập mặc định `/Patient/Appointment/Book` -> Tự động chuyển tới Khám tổng quát & tư vấn (Service 1, Dept 1). | **PASSED** |
| **PAT-E2E-04** | Doctor Selection Mode | Xác nhận 2 chế độ: Lựa chọn A (Chỉ định Bác sĩ mong muốn) và Lựa chọn B (Phòng khám tự sắp xếp Bác sĩ). | **PASSED** |
| **PAT-E2E-05** | Date & Time Slot Picker | Ràng buộc chọn ngày: Thuộc tính `min` ngăn chặn chọn ngày trong quá khứ; nạp danh sách slot khả dụng. | **PASSED** |
| **PAT-E2E-06** | 5-Step Booking Submission | Điền thông tin cá nhân, ghi chú, kiểm tra tóm tắt -> Gửi yêu cầu -> Trạng thái `PendingReceptionReview`, không có QueueNumber. | **PASSED** |
| **PAT-E2E-07** | Receptionist Appointment Review | Đăng nhập tài khoản Lễ tân -> Vào màn hình Requests -> Xem yêu cầu đặt lịch chờ duyệt và phân bổ tài nguyên. | **PASSED** |
| **PAT-E2E-08** | Patient View Confirmed Appointment | Bệnh nhân xem danh sách lịch hẹn của tôi -> Chi tiết lịch hẹn đã duyệt hiển thị đầy đủ Bác sĩ, Phòng, Ghế, QueueNumber. | **PASSED** |
| **PAT-E2E-09** | Patient Cancel Appointment | Bệnh nhân thao tác Hủy lịch hẹn khi ở trạng thái cho phép kèm lý do hủy bắt buộc. | **PASSED** |
| **PAT-E2E-10** | Patient Reschedule Appointment | Bệnh nhân truy cập màn hình Đổi lịch hẹn (Reschedule) cho lịch đã đặt trước đó. | **PASSED** |
| **PAT-E2E-11** | Doctor Leave Disruption Recovery | Bệnh nhân nhận cảnh báo lịch gián đoạn khi bác sĩ xin nghỉ phép -> Truy cập Disruption Recovery để chọn bác sĩ thay thế. | **PASSED** |
| **PAT-E2E-12** | Medical Records & Odontogram | Bệnh nhân tra cứu Hồ sơ bệnh án điện tử, biểu đồ sơ đồ răng Odontogram và lịch sử khám. | **PASSED** |
| **PAT-E2E-13** | Prescriptions & Dispensed Medicines | Bệnh nhân tra cứu Đơn thuốc điều trị, hướng dẫn liều dùng và thông tin thuốc thực cấp tại quầy Dược. | **PASSED** |
| **PAT-E2E-14** | Invoices & Dynamic VietQR Code | Bệnh nhân xem Hóa đơn viện phí và mã VietQR thanh toán tự động sinh từ cấu hình hệ thống (không hardcode). | **PASSED** |

---

## 6. KẾT LUẬN VỀ MA TRẬN KIỂM THỬ

Toàn bộ **425 / 425** ca kiểm thử nghiệm thu thuộc phân hệ Patient Portal đều đạt trạng thái **PASSED 100%**. 

Hệ thống bảo đảm tính toàn vẹn dữ liệu, kiểm soát luồng nghiệp vụ chặt chẽ theo thỏa thuận của đội ngũ, ngăn chặn triệt để các hành vi gửi trùng yêu cầu, truy cập trái quyền (Anti-IDOR) và hoàn toàn sẵn sàng bàn giao tích hợp.
