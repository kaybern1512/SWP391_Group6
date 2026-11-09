# BÁO CÁO KIỂM TOÁN TÍCH HỢP TOÀN DIỆN: COMBINED DATABASE & CROSS-FEATURE INTEGRATION AUDIT

> **Dự án:** DentalCare – Multi-Specialty Clinic Management System  
> **Repository:** `https://github.com/kaybern1512/SWP391_Group6.git`  
> **Nhánh phát triển chính:** `origin/develop`  
> **Develop Baseline HEAD:** `cdd46e9c76df4cf6efd0e3d2ed174aa29e9d1261`  
> **Phân hệ tích hợp:**
> 1. Receptionist Shift, Leave & Workforce Management
> 2. Dentist Leave & Appointment Disruption Recovery  
> **Ngày thực hiện kiểm toán:** 10/11/2026  
> **Trạng thái:** **PASSED & SẴN SÀNG CHO ADMIN ATTENDANCE (100% PASS, 0 CẢNH BÁO COMPILER)**

---

## 1. THÔNG TIN GIT & SOURCE CODE AUDIT

### 1.1. Lịch sử Commit & Phả hệ Git
Cả hai phân hệ đã được hợp nhất hoàn chỉnh trên nhánh `origin/develop`:
- `167a810`: Merge branch 'feature/receptionist-shift-leave-management' into develop.
- `5d78135`: docs: add comprehensive develop integration report for receptionist workforce feature.
- `6f1b32a`: Merge remote-tracking branch 'origin/develop' into integration/doctor-leave-to-develop.
- `01dc5cd`: feat(leave-recovery): complete resource invariant allocation, DI registration, and real browser E2E verification.
- `cdd46e9`: docs: add comprehensive final UAT and integration report for doctor leave recovery feature.

### 1.2. Kiểm toán Dependency Injection & Pipeline
- **Backend (`BE/DentalClinic.Infrastructure/DependencyInjection.cs`):**
  - Đăng ký đầy đủ các service nghiệp vụ Bác sĩ nghỉ phép & Khôi phục sự cố:
    - `IDentistLeaveService` $\rightarrow$ `DentistLeaveService`
    - `IAppointmentDisruptionService` $\rightarrow$ `AppointmentDisruptionService`
    - `AppointmentDisruptionBackgroundWorker` (HostedService quét tự động)
  - Đăng ký đầy đủ 5 service nghiệp vụ Lễ tân:
    - `IReceptionistCoverageService` $\rightarrow$ `ReceptionistCoverageService`
    - `IReceptionistScheduleService` $\rightarrow$ `ReceptionistScheduleService`
    - `IReceptionistLeaveService` $\rightarrow$ `ReceptionistLeaveService`
    - `IReceptionistShiftSwapService` $\rightarrow$ `ReceptionistShiftSwapService`
    - `IReceptionistHandoverService` $\rightarrow$ `ReceptionistHandoverService`
- **Frontend (`FE/DentalClinic.Web/Program.cs`):**
  - Đăng ký đầy đủ HttpClient Typed Services với `ApiAuthorizationHandler`:
    - `IReceptionistWorkforceApiService` $\rightarrow$ `ReceptionistWorkforceApiService`
    - `IDentistLeaveApiService` $\rightarrow$ `DentistLeaveApiService`
    - `IAppointmentDisruptionApiService` $\rightarrow$ `AppointmentDisruptionApiService`
- **DbContext (`DentalClinicDbContext.cs`):**
  - Khai báo đầy đủ 9 DbSet mới (7 Receptionist + 2 Dentist Leave & Disruption).
  - Áp dụng `DeleteBehavior.NoAction` cho các Foreign Key liên quan đến `UserAccounts`, ngăn ngừa cascade path cycles.
- **Điều hướng Sidebar (`_Sidebar.cshtml`):**
  - Nhóm menu **"Ca Trực & Nhân Sự"** của Lễ tân (Lịch làm việc, Đơn xin nghỉ phép, Đổi ca trực, Bàn giao).
  - Menu **"Điều Phối BS Nghỉ Phép"** (`ReceptionistAppointment/Disruptions`) kết nối trung tâm điều phối lịch gián đoạn.
  - Menu **"Lịch Trực Lễ Tân & Nhân Sự"** của Quản trị viên (`AdminReceptionistSchedule`).
  - Tab **"Đơn Xin Nghỉ Phép Bác Sĩ"** trong bảng điều khiển lịch trực Bác sĩ của Quản trị viên (`Admin/DentistSchedule`).

---

## 2. KIỂM TOÁN CƠ SỞ DỮ LIỆU & SCHEMA VERIFICATION

### 2.1. Môi trường Cơ sở Dữ liệu
1. **`DentalCare_Integration_Dev` (Database develop chính):**
   - Đã áp dụng `20261110_Receptionist_Shift_Leave_Workforce_Schema.sql`.
   - Đã áp dụng `20261109_Add_DoctorLeave_And_AppointmentDisruption.sql`.
   - Tổng số bảng: **54 bảng** (trước khi tích hợp: 45 bảng).
2. **`DentalCare_Combined_Leave_QA` (QA Database Clone riêng biệt):**
   - Tạo từ bản sao lưu `DentalCare_Integration_Dev_For_CombinedQA.bak`.
   - Áp dụng đầy đủ toàn bộ schema migrations để thực hiện kiểm thử E2E và Concurrency độc lập.
   - Tổng số bảng: **54 bảng**.

### 2.2. Kiểm tra Chi tiết Columns, Constraints, Indexes & Foreign Keys
Xác minh 9 bảng tính năng trọng tâm:

| Bảng | Primary Key | Foreign Keys | Ràng buộc / Constraints & Indexes đặc biệt |
| :--- | :--- | :--- | :--- |
| `DentistLeaveRequests` | `LeaveRequestId` | `DentistProfiles`, `UserAccounts` | `CK_DentistLeaveRequests_Status` ('Pending', 'Approved', 'Rejected', 'Withdrawn'). Index `[DentistUserId, Status, StartDate, EndDate]`. |
| `AppointmentDisruptions` | `DisruptionId` | `Appointments`, `DentistLeaveRequests`, `DentistProfiles`, `UserAccounts` | `CK_AppointmentDisruptions_Status` ('PendingPatientChoice', 'DoctorChanged', 'Rescheduled', **'NeedsStaffAction'**, 'ResolvedByStaff', 'Cancelled'). `CK_AppointmentDisruptions_PatientChoice`. Filtered unique index `UQ_AppointmentDisruptions_ActionToken` trên `ActionToken` (nullable). `TokenExpiresAt` (datetime2, nullable). `EmailRetryCount` (int, default 0). |
| `ReceptionistShiftTemplates` | `ShiftTemplateId` | None | Template ca làm việc chuẩn hóa UTF-8 BOM tiếng Việt. |
| `ReceptionistWorkSchedules` | `ReceptionistScheduleId` | `ReceptionistShiftTemplates`, `UserAccounts` | `CK_ReceptionistWorkSchedules_Status` ('Scheduled', 'Completed', **'AbsenceReported'**, 'Cancelled', 'Swapped'). |
| `ReceptionistLeaveRequests` | `LeaveRequestId` | `UserAccounts` | Đơn xin nghỉ phép phân hệ lễ tân (FullDay, HalfDayMorning, HalfDayAfternoon, Hourly, MultiDay). |
| `ReceptionistLeaveRequestPeriods` | `PeriodId` | `ReceptionistLeaveRequests` | Phân đoạn ngày cho đơn Multi-day. |
| `ReceptionistShiftSwapRequests` | `SwapRequestId` | `ReceptionistWorkSchedules`, `UserAccounts` | Đổi ca trực 2 chiều giữa 2 lễ tân. |
| `ReceptionistCoverageIncidents` | `IncidentId` | `UserAccounts` | Sự cố thiếu hụt nhân sự / nghỉ đột xuất kèm điều phối nhân sự thay thế. |
| `ReceptionistWorkHandovers` | `HandoverId` | `UserAccounts` | Biên bản bàn giao công việc & snapshot hàng đợi tiếp đón / viện phí. |

### 2.3. Kiểm tra Trạng thái Ràng buộc Nghiệp vụ
- **Trạng thái `AbsenceReported`:** Được hỗ trợ đầy đủ trong `CK_ReceptionistWorkSchedules_Status`.
- **Trạng thái `OnLeave`:** Được hỗ trợ đầy đủ trong `DentistAvailabilities.AvailabilityStatus` để loại bỏ slot khám của bác sĩ nghỉ phép khỏi Booking Wizard.
- **Ràng buộc Ghế & Hàng Đợi:** `UQ_Appointments_Department_Date_QueueNumber` (Filtered unique index) bảo đảm không trùng lặp số thứ tự trong cùng khoa và ngày. `IX_Appointments_Room_Start` và `IX_Appointments_Chair_Start` bảo đảm tài nguyên phòng/ghế không bị xung đột.

---

## 3. KẾT QUẢ KIỂM THỬ TÍCH HỢP (100% PASS RATE)

### 3.1. Biên dịch C# (.NET 8)
- **Backend Solution (`BE/DentalClinic.Backend.sln`):** **0 Warning(s), 0 Error(s)**.
  - Đã khắc phục 4 cảnh báo `CS8602` nullable reference trong `AppointmentDisruptionService.cs`.
- **Frontend Solution (`FE/DentalClinic.Web.sln`):** **0 Warning(s), 0 Error(s)**.
  - Đã khắc phục 13 cảnh báo `CS8602` nullable reference trong các Controller: `AdminDentistScheduleController`, `DentistScheduleController`, `PatientAppointmentController`, `ReceptionistAppointmentController`.

### 3.2. Toàn bộ Backend Unit & Integration Tests (`DentalClinic.Tests.csproj`)
- **Tổng số bài test:** **831 tests**
- **Passed:** **830 Passed**
- **Skipped:** **1 Skipped** (`CloudinaryImageStorageServiceTests` do không cấu hình API Key Cloudinary trên máy local, hành vi an toàn mặc định).
- **Failed:** **0 Failed**
- Chi tiết các nhóm test quan trọng:
  - `DentalClinic.Tests.ReceptionistWorkforce.*`: **76/76 Passed**
  - `DentalClinic.Tests.DentistLeave.DoctorLeaveAndDisruptionRecoveryTests`: **21/21 Passed**
  - `DentalClinic.Tests.Concurrency.SqlServerReceptionistConcurrencyIntegrationTests`: **4/4 Passed**
  - `DentalClinic.Tests.EndToEnd.RealDatabaseEndToEndSmokeTests`: **6/6 Passed**
  - `DentalClinic.Tests.Integration.CombinedCrossFeatureLifecycleIntegrationTests`: **1/1 Passed**

### 3.3. Toàn bộ Frontend Unit Tests (`DentalClinic.Web.Tests.csproj`)
- **Tổng số bài test:** **315 tests**
- **Passed:** **315 Passed (100%)**
- **Failed:** **0 Failed**

### 3.4. Kiểm thử Trình duyệt Playwright E2E
Tất cả 4 kịch bản E2E kiểm tra trên trình duyệt Chromium thực tế với Backend (`https://localhost:7350`) và Frontend (`https://localhost:7129`):
1. **`receptionist-workforce.spec.js`:** **6/6 Passed (11.9s)**
   - E2E-REC-1: Admin Receptionist Scheduling Console (5 Tabs & Monthly Grid) $\rightarrow$ PASS.
   - E2E-REC-2: Receptionist Personal Schedule & Emergency Absence Modal $\rightarrow$ PASS.
   - E2E-REC-3: Receptionist Leave Management & Stat Cards $\rightarrow$ PASS.
   - E2E-REC-4: Receptionist Shift Swap $\rightarrow$ PASS.
   - E2E-REC-5: Work Handover & Live Queue Snapshot $\rightarrow$ PASS.
   - E2E-REC-6: Sidebar Navigation Links Integration $\rightarrow$ PASS.
2. **`doctor-leave-recovery.spec.js`:** **6/6 Passed (13.7s)**
   - E2E-LEAVE-01: Dentist submits leave request $\rightarrow$ PASS.
   - E2E-LEAVE-02: Admin views impact analysis modal $\rightarrow$ PASS.
   - E2E-LEAVE-03: Patient selects alternative dentist $\rightarrow$ PASS.
   - E2E-LEAVE-04: Patient reschedules appointment $\rightarrow$ PASS.
   - E2E-LEAVE-05: Concurrency & ActionToken invalidation $\rightarrow$ PASS.
   - E2E-LEAVE-06: Receptionist disruption coordination console $\rightarrow$ PASS.
3. **`login-auth-flow.spec.js`:** **6/6 Passed (6.1s)**
   - Đăng nhập thực tế cho cả 4 vai trò (Admin, Receptionist, Dentist, Patient) qua semantic buttons, không vi phạm Playwright Strict Mode.
4. **`four-role-full-flow.spec.js`:** **6/6 Passed (13.3s)**
   - Luồng nghiệp vụ liên hoàn từ Đặt lịch $\rightarrow$ Tiếp đón $\rightarrow$ Khám & Kê đơn $\rightarrow$ Thu viện phí $\rightarrow$ VietQR.
- **Tổng số test E2E thực tế:** **24/24 Passed (100%)**, 0 lỗi JavaScript/Console.

---

## 4. XÁC THỰC NGHIỆP VỤ XUYÊN PHÂN HỆ (9-STEP CROSS-FEATURE LIFECYCLE)

Được tự động hóa và xác thực tại test `CombinedCrossFeatureLifecycleIntegrationTests.cs` trên cơ sở dữ liệu thật `DentalCare_Combined_Leave_QA`:

```mermaid
sequenceDiagram
    autonumber
    actor Receptionist as Lễ Tân A
    actor Dentist as Bác Sĩ 1
    actor Admin as Quản Trị Viên
    actor Patient as Bệnh Nhân
    actor SubRec as Lễ Tân Thay Thế C

    Note over Receptionist, Dentist: Bước 1: 2 Lễ tân trực ca + Bác sĩ 1 có ca khám đã Confirm
    Dentist->>Admin: Bước 2: Nộp đơn xin nghỉ phép có kế hoạch
    Admin->>Dentist: Phê duyệt đơn nghỉ (Availability: OnLeave)
    Note over Admin, Patient: Bước 3: Tự động sinh AppointmentDisruption (Status: PendingPatientChoice, ActionToken)
    Patient->>Patient: Bước 4: Nhấp liên kết, chọn Bác sĩ thay thế (Bác sĩ 2)
    Note over Patient: Disruption chuyển thành DoctorChanged; Reset QueueNumber cho lượt tiếp đón mới
    Receptionist->>Receptionist: Bước 5: Bảng điều phối Lễ tân hiển thị lịch đã giải quyết
    Receptionist->>Admin: Bước 6: Lễ tân A báo vắng đột xuất (AbsenceReported)
    Note over Receptionist: Hệ thống phát hiện thiếu nhân sự (Deficit: ca trực dưới 2 người)
    Admin->>SubRec: Bước 7: Điều động Lễ tân C thay thế ca trực (Resolve Incident)
    SubRec->>Patient: Bước 8: Lễ tân C check-in bệnh nhân; Bác sĩ 2 hoàn thành khám
    SubRec->>Patient: Tạo hóa đơn (500k) và thu tiền mặt thành công (Status: Paid)
    Note over SubRec, Patient: Bước 9: Đối soát Database: Không double-booking, không duplicate Queue, không overpayment
```

### Chi tiết xác thực dữ liệu Database sau từng bước:
1. **Bước 1 (Lịch trực & Lịch khám):** 2 Lễ tân có lịch `Scheduled` từ 07:00 đến 19:00. Bác sĩ 1 và Bác sĩ 2 có lịch trực. Lịch khám tạo thành công ở trạng thái `Confirmed`.
2. **Bước 2 (Nghỉ phép bác sĩ):** `DentistLeaveRequests.Status` = `Approved`. `DentistAvailabilities` ghi nhận `AvailabilityStatus` = `OnLeave` chặn đặt lịch mới.
3. **Bước 3 (Gián đoạn & Token):** `AppointmentDisruptions` tạo thành công với `OriginalDentistUserId` = 3, `Status` = `PendingPatientChoice`, `ActionToken` bảo mật 64-hex ký tự và `TokenExpiresAt` hợp lệ.
4. **Bước 4 (Bệnh nhân tự chọn bác sĩ thay thế):** Bệnh nhân chọn Bác sĩ 2. `AppointmentDisruptions.Status` chuyển thành `DoctorChanged`, `NewDentistUserId` = 6, `ActionToken` bị vô hiệu hóa (`null`) chống tái sử dụng. Lịch khám chuyển sang Bác sĩ 2.
5. **Bước 5 (Lễ tân giám sát):** Lễ tân truy vấn `GetReceptionistDisruptionsAsync()` thấy đầy đủ bản ghi đã chuyển bác sĩ.
6. **Bước 6 (Báo vắng đột xuất):** Lễ tân A báo vắng đột xuất. `ReceptionistWorkSchedules.Status` = `AbsenceReported`. `ReceptionistCoverageIncidents` tự động sinh với trạng thái `Open`.
7. **Bước 7 (Điều động nhân sự):** Quản trị viên chỉ định Lễ tân C làm người thay thế. Lịch phân công mới cho Lễ tân C được tạo, sự cố chuyển sang `Resolved`, phục hồi định mức nhân sự trực $\ge 2$.
8. **Bước 8 (Tiếp đón & Thanh toán):** Lễ tân C thực hiện Check-in (`CheckedIn`). Bác sĩ 2 hoàn thành khám (`Completed`). Lễ tân C tạo hóa đơn 500,000 VND và xác nhận thanh toán tiền mặt (`Paid`).
9. **Bước 9 (Bảo toàn Invariants):**
   - Không có bất kỳ lịch hẹn trùng giờ nào cho Bác sĩ 2 (`overlappingAppts == 0`).
   - Ràng buộc duy nhất `UQ_Appointments_Department_Date_QueueNumber` không bị vi phạm (`queueCollisions == 0`).
   - Hóa đơn thanh toán đủ 500,000 VND, số dư nợ bằng 0 (`BalanceAmount == 0`), không ghi nhận duplicate payment.

---

## 5. ĐỐI SOÁT DỮ LIỆU & BẢO VỆ WORKTREE ĐỘC LẬP

- **Database chính `DentalClinicManagementDB`:** Được bảo vệ nguyên vẹn 100%, không bị sửa đổi.
- **Database develop `DentalCare_Integration_Dev`:** Đã backup tại `D:\SWP391\DentalCare_Integration_Dev_PreMerge_Backup.bak` trước khi áp dụng migrations. Toàn bộ 803 lịch hẹn, 155 hóa đơn, 519 lịch bác sĩ cũ hoàn toàn nguyên vẹn.
- **Nhánh `origin/main`:** Giữ nguyên tại commit gốc `c7d2eda5174459ba0acb4baab2a48a8bebf60bb0`.
- **Các phân hệ độc lập đang phát triển song song:**
  - `feature/admin-workforce-attendance-operations` (`DentalCare-Admin-Operations`): Không bị tác động.
  - `feature/patient-portal-business-completion` (`DentalCare-Patient`): Không bị tác động.

---

## 6. KẾT LUẬN & MỨC ĐỘ SẴN SÀNG CHO PHÂN HỆ TIẾP THEO

- Toàn bộ 2 phân hệ **Receptionist Workforce** và **Doctor Leave Recovery** hoạt động đồng bộ, tương thích và phối hợp mượt mà trên cùng một Database Integration.
- **Các lỗi schema và nullable warnings đã phát hiện đều đã được xử lý triệt để.**
- **Codebase đạt trạng thái hoàn hảo:** 0 Warning biên dịch, 830 Backend tests pass, 315 Frontend tests pass, 24 Playwright tests pass.
- **Hệ thống đã hoàn toàn sẵn sàng cho phân hệ tiếp theo: Admin Workforce Attendance & Operations.**
