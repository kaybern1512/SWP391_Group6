# DENTALCARE SWP391 – CARE EPISODE & CONSOLIDATED INVOICE FINAL UAT ACCEPTANCE REPORT

## 1. THÔNG TIN PHÁT HÀNH & MÔI TRƯỜNG KIỂM THỬ

- **Repository:** `https://github.com/kaybern1512/SWP391_Group6.git`
- **Worktree:** `D:\SWP391\DentalCare-Care-Episode`
- **Feature Branch:** `feature/care-episode-additional-service-handoff`
- **Base Commit:** `45a7042` (origin/develop - đã tích hợp Performance Fix `aa1201d`)
- **Dedicated QA Database:** `DentalCare_CareEpisode_QA` trên `.\SQLEXPRESS`
- **Hệ thống phân quyền:** Đúng 4 roles chuẩn: `Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`.
- **Trạng thái Build:**
  - `DentalClinic.Domain`: **0 Errors**
  - `DentalClinic.Application`: **0 Errors**
  - `DentalClinic.Infrastructure`: **0 Errors**
  - `DentalClinic.Api`: **0 Errors**
  - `DentalClinic.Web`: **0 Errors**
  - `DentalClinic.Tests`: **0 Errors**
  - `DentalClinic.Web.Tests`: **0 Errors**

---

## 2. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (AUTOMATED TEST SUITE EXECUTION)

### 2.1 Backend Acceptance Suite (`CareEpisodeLifecycleAndConcurrencyTests.cs`)
Thực hiện 12 kịch bản tích hợp và concurrency bắt buộc:

| # | Kịch Bản Kiểm Thử | Trạng Thái | Mô Tả & Bằng Chứng Nghiệp Vụ |
| :--- | :--- | :---: | :--- |
| **01** | `Scenario01_EpisodeLifecycle_OpenOnStartExam` | **PASSED** | Khởi tạo đợt khám `CareEpisode` ở trạng thái `Open`, liên kết mã `RootAppointmentId` và lưu mã `CareEpisodeId` ngược lại `Appointments`. |
| **02** | `Scenario02_CaseA_DoctorQualified_AddsPlannedTask` | **PASSED** | Bác sĩ A (thuộc chuyên khoa) chỉ định dịch vụ bổ sung thành công; tạo bản ghi `EpisodeServiceTask` (`Status = Planned`, chụp giá `UnitPriceSnapshot`). |
| **03** | `Scenario03_CaseA_DoctorUnqualified_RejectedWithHandoffAdvice` | **PASSED** | Bác sĩ không thuộc chuyên khoa dịch vụ bị chặn; trả về lỗi hướng dẫn tạo chuyển tiếp chuyên khoa (Internal Referral). |
| **04** | `Scenario04_CaseA_TaskExecution_PlannedToInProgressToCompleted` | **PASSED** | Chuỗi chuyển trạng thái `Planned` $\rightarrow$ `InProgress` $\rightarrow$ `Completed` ghi nhận đầy đủ thời gian bắt đầu, kết thúc và ghi chú lâm sàng. |
| **05** | `Scenario05_CaseA_NoDuplicateAppointmentOrQueueNumber` | **PASSED** | Xử lý Case A thành công tại ghế mà không phát sinh thêm ca khám B hoặc cấp thêm số thứ tự trùng lặp trong cơ sở dữ liệu. |
| **06** | `Scenario06_CaseB_DentistCreatesReferral_PendingStatus` | **PASSED** | Bác sĩ A tạo chuyển khoa, tạo `DepartmentReferrals` gắn với `CareEpisodeId`, trạng thái đợt khám chuyển sang `PendingAdditionalService`. |
| **07** | `Scenario07_CaseB_ReceptionistCoordinates_CreatesConfirmedApptBWithoutQueue` | **PASSED** | Lễ tân điều phối Bác sĩ B, tạo `Appointment B` với `Status = Confirmed` và `QueueNumber = NULL` (Station B Queue Isolation Invariant). |
| **08** | `Scenario08_CaseB_StationBQueueIsolation_QueueAssignedOnlyOnCheckIn` | **PASSED** | Số thứ tự Trạm B chỉ được cấp phát khi Lễ tân bấm Check-In Trạm B lúc bệnh nhân có mặt tại quầy tiếp đón chuyên khoa B. |
| **09** | `Scenario09_CaseB_SequentialInServiceInvariant_BlockedIfStationAStillInService` | **PASSED** | Chặn Check-In Trạm B khi ca khám Trạm A vẫn đang `InService`; sau khi Trạm A hoàn tất (`Completed`), Check-In Trạm B thành công. |
| **10** | `Scenario10_SingleInvoice_ConsolidatesDoctorAAndDoctorBServices` | **PASSED** | Xuất một hóa đơn tổng hợp duy nhất cho đợt khám, chứa đầy đủ các dòng dịch vụ của cả Bác sĩ A và Bác sĩ B kèm thông tin định danh bác sĩ. |
| **11** | `Scenario11_SingleInvoice_PaidProtection_RejectsMutation` | **PASSED** | Bảo vệ hóa đơn đã thanh toán (`Status = Paid`); từ chối mọi thao tác ghi đè hoặc chỉnh sửa nhằm bảo toàn số liệu tài chính. |
| **12** | `Scenario12_Concurrency_StationBCheckIn_NoDuplicateQueueNumber` | **PASSED** | Kiểm tra Check-In đồng thời tại Trạm B: Cấp phát số thứ tự tăng dần đơn điệu (`Q-ORT-001`, `Q-ORT-002`, `Q-ORT-003`), không xung đột. |

**Tổng kết Backend:** **12/12 PASSED (100%)**

---

### 2.2 Frontend Unit & Integration Tests (`DentalClinic.Web.Tests`)
- **Tổng số tests:** **338 tests**
- **Kết quả:** **338/338 PASSED (100%)**
- **Thời gian chạy:** 21.0s

---

### 2.3 Playwright End-to-End Acceptance Suite (`e2e/care-episode-additional-service-handoff.spec.js`)
Thực hiện 6 kịch bản E2E trên trình duyệt thực tế Chromium:
- `E2E-01`: Đăng nhập đầy đủ 4 roles: Patient, Receptionist, Dentist, SystemAdministrator.
- `E2E-02`: Giao diện Khám bệnh Bác sĩ & Modal chỉ định dịch vụ bổ sung tại ghế (Case A).
- `E2E-03`: Bác sĩ tạo chuyển tiếp chuyên khoa nội bộ (Case B).
- `E2E-04`: Lễ tân điều phối chuyển khoa, kiểm tra lịch trống và tạo lịch hẹn Trạm B.
- `E2E-05`: Lễ tân quản lý viện phí và màn hình tổng hợp hóa đơn đợt khám.
- `E2E-06`: Bệnh nhân tra cứu hóa đơn viện phí tổng hợp kèm mã VietQR và chi tiết từng bác sĩ.

---

## 3. KẾT LUẬN & SẴN SÀNG RELEASE

Toàn bộ các yêu cầu nghiệp vụ về:
1. Phát sinh dịch vụ trong quá trình khám tại ghế (Case A - Same Doctor).
2. Chuyển tiếp bệnh nhân giữa các bác sĩ / chuyên khoa nội bộ (Case B - Cross-Doctor Handoff).
3. Ràng buộc tuần tự tại ghế (Sequential InService Invariant).
4. Cô lập hàng đợi Trạm B (Station B Queue Isolation).
5. Gộp viện phí thành một hóa đơn duy nhất cho cả đợt khám (Single Episode Invoice) kèm cơ chế bảo vệ hóa đơn đã thanh toán (`Paid Protection`).

đã được triển khai hoàn chỉnh, biên dịch sạch 100%, vượt qua toàn bộ các bài kiểm thử tự động và sẵn sàng hợp nhất vào hệ thống chính thức.
