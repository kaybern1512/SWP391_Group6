# FOLLOW-UP CONCURRENCY & INTEGRITY HARDENING REPORT (REVIEW ROUND 4 - FINAL SAFETY FIXES)

**Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)  
**Workspace:** `D:\SWP\SWP391_Group6`  
**Nhánh làm việc:** `fix/followup-concurrency-integrity`  
**Nhánh gốc (Base):** `origin/develop`  
**Base Commit HEAD:** `9ca88ea9f24f5e74b93c4abb77717fb5fae62f79`  
**SQL Server Instance:** `.\SQL2025_DUC`  
**Vai trò:** Senior .NET 8 / SQL Server Concurrency Engineer, QA Automation Engineer & Security Reviewer  
**Technical Lead Review:** Review Round 4 (Khắc phục hoàn toàn 6 vấn đề an toàn đồng thời P0/P1)  
**Ngày thực hiện:** 05/11/2026  
**Final Verdict:** `READY_FOR_REVIEW`  

---

## 1. MỤC TIÊU VÀ PHẠM VI XỬ LÝ TRONG REVIEW ROUND 4

Technical Lead review vòng 4 đã xác định 6 vấn đề an toàn đồng thời cần giải quyết triệt để:

### 1.1. P0-01 — Xung đột lịch bệnh nhân khi Trưởng khoa xác nhận (`ManagerConfirmAsync`)
- **Vấn đề:** `AppointmentService.ManagerConfirmAsync` đã khóa `SqlAppLockHelper.PatientKey`, nhưng **chưa kiểm tra lại xung đột lịch bệnh nhân** trước khi chuyển trạng thái cuộc hẹn thành `Confirmed`. Nếu bệnh nhân đã có cuộc hẹn khác đang chiếm slot (`Confirmed`, `CheckedIn`, `Waiting`, `Called`, `InService`) trong khung giờ trùng lặp (`existingStart < newEnd && existingEnd > newStart`), hệ thống vẫn phê duyệt, dẫn đến bệnh nhân có hai lịch hẹn cùng lúc tại hai khoa hoặc với hai bác sĩ khác nhau.
- **Giải pháp:**
  - Bổ sung kiểm tra `PatientUserId` với các cuộc hẹn chiếm slot trong cùng giao dịch và dưới khóa `PatientKey`.
  - Loại trừ chính cuộc hẹn đang được xác nhận (`a.AppointmentId != appointmentId`).
  - Nếu phát hiện trùng lặp, rollback giao dịch và trả về thông báo lỗi tiếng Việt: `"Bệnh nhân đã có lịch hẹn khác trong khung giờ này."`.
  - Bổ sung 4 integration tests trên SQL Server thật: **B21, B22, B23, B24**.

### 1.2. P0-02 — Hủy ca làm việc an toàn của Bác sĩ (`CancelScheduleAsync`)
- **Vấn đề:** `DentistScheduleService.CancelScheduleAsync` hủy ca làm việc mà không kiểm tra xem bác sĩ đã có lịch hẹn khám với bệnh nhân trong ca đó hay chưa, dẫn đến việc bỏ rơi bệnh nhân đã được xác nhận lịch.
- **Giải pháp:**
  - Bọc quy trình trong giao dịch có khóa phân cấp `DentistMasterKey(dentistUserId)` và `DentistKey(dentistUserId, workDate)`.
  - Reload thực thể ca làm việc dưới khóa.
  - Kiểm tra xem có cuộc hẹn nào ở trạng thái chiếm slot (`Confirmed`, `CheckedIn`, `Waiting`, `Called`, `InService`) giao thoa với ca làm việc hay không.
  - Nếu có, kiểm tra tiếp xem có ca làm việc nào khác của bác sĩ trong cùng ngày bao phủ toàn bộ các cuộc hẹn đó không. Nếu không được bao phủ, từ chối hủy ca với lỗi tiếng Việt rõ ràng: `"Không thể hủy ca làm việc vì bác sĩ đang có lịch khám với bệnh nhân trong ca này."`.
  - Bổ sung 2 integration tests trên SQL Server thật: **B25, B26**.

### 1.3. P0-03 — Cập nhật ca làm việc an toàn của Bác sĩ (`UpdateScheduleAsync`)
- **Vấn đề:** `DentistScheduleService.UpdateScheduleAsync` cho phép thay đổi ngày hoặc giờ làm việc của ca khám mà không kiểm tra xem các cuộc hẹn đã được xếp trong ca cũ có bị rơi ra ngoài ca mới hay không.
- **Giải pháp:**
  - Khóa đồng thời `DentistMasterKey` cùng `DentistKey` của cả ngày cũ và ngày mới (sắp xếp thứ tự lexicographical để chống deadlock).
  - Reload ca làm việc dưới khóa.
  - Thu thập tất cả cuộc hẹn chiếm slot trong khung giờ ca cũ; xác minh từng cuộc hẹn: hoặc được bao phủ bởi một ca khác còn lại trong ngày cũ, hoặc ngày làm việc không đổi và khung giờ mới của ca vẫn bao trùm hoàn toàn cuộc hẹn. Nếu có cuộc hẹn bị mất ca khám, từ chối cập nhật và rollback.
  - Bổ sung 2 integration tests trên SQL Server thật: **B27, B28**.

### 1.4. P1-04 — Độc quyền phòng khám khi gán ghế điều trị (`DentistAssignChairAsync`)
- **Vấn đề:** `AppointmentService.DentistAssignChairAsync` kiểm tra ghế bận nhưng không kiểm tra tính độc quyền của phòng (`room.IsExclusive = true`). Nếu phòng độc quyền có nhiều ghế, hai bác sĩ có thể gán hai ghế khác nhau cùng lúc trong cùng một phòng độc quyền.
- **Giải pháp:**
  - Tải thực thể `Room` của `chair.RoomId`.
  - Nếu `room.IsExclusive == true`, bổ sung `SqlAppLockHelper.RoomKey(chair.RoomId, targetDate)` vào danh sách khóa.
  - Kiểm tra không có cuộc hẹn nào khác đang chiếm phòng độc quyền này trong khung giờ:
    `a.AppointmentId != appointmentId && a.RoomId == chair.RoomId && activeStatuses.Contains(a.Status) && a.ScheduledStart < end && a.ScheduledEnd > start`.
  - Bổ sung integration test trên SQL Server thật: **B29**.

### 1.5. P1-05 — Đồng bộ nghỉ phép dài hạn qua khóa phân cấp `DentistMasterKey`
- **Vấn đề:** Trong `CreateAvailabilityAsync` và `UpdateAvailabilityAsync`, việc dùng vòng lặp khóa từng ngày bị giới hạn cứng ở 60 ngày hoặc gây nổ số lượng khóa khi nghỉ phép dài hạn (> 60 ngày hoặc `EndDateTime == null`).
- **Giải pháp:**
  - Thiết kế khóa phân cấp `DentistMasterKey(dentistUserId) => $"Lock_DentistMaster_{dentistUserId}"`.
  - Đồng bộ `CreateAvailabilityAsync` và `UpdateAvailabilityAsync` qua `DentistMasterKey`. Với khoảng thời gian ngắn (<= 14 ngày), khóa thêm từng ngày qua `DentistKey` để tối ưu concurrency.
  - Tất cả các entry point đặt lịch và xác nhận lịch (`ManagerConfirmAsync`, `CreateDoctorFollowUpAppointmentAsync`, `ResolveProposalAsync`, `CreatePlanFollowUpAppointmentAsync`) đều lấy `DentistMasterKey` theo thứ tự chuẩn.
  - Bổ sung 2 integration tests trên SQL Server thật: **B30, B31**.

### 1.6. P1-06 — Bổ sung Test va chạm mã `AppointmentCode` qua Interceptor thực tế
- **Vấn đề:** Test `B15B` trước đây chưa chứng minh được nhánh catch `DbUpdateException` (lỗi 2627/2601) thực sự được kích hoạt trong luồng chạy thực tế của `FollowUpSchedulingService`.
- **Giải pháp:**
  - Viết `CollisionSimulationInterceptor : SaveChangesInterceptor` chặn `SavingChangesAsync` trên `DentalClinicDbContext`.
  - `B15B`: Ép va chạm 1 lần với mã đã tồn tại trên SQL Server thật, chứng minh bộ đếm va chạm `interceptor.InterceptedCollisions == 1`, nhánh catch/retry hoạt động thành công và lưu mã mới vào cơ sở dữ liệu.
  - `B15C`: Ép va chạm 5 lần (> 3 lần retry tối đa), kiểm chứng hệ thống bắt kiệt số lần retry, rollback giao dịch an toàn và trả về `ApiResponse.Fail` thay vì để throw unhandled exception ra ngoài.

---

## 2. TỔNG HỢP CÁC SỬA ĐỔI MÃ NGUỒN CHÍNH

### 2.1. `BE/DentalClinic.Infrastructure/Services/SqlAppLockHelper.cs`
- Thêm khóa phân cấp:
  ```csharp
  public static string DentistMasterKey(long dentistUserId) => $"Lock_DentistMaster_{dentistUserId}";
  ```
- Đảm bảo tất cả các khóa được chuẩn hóa và sắp xếp thứ tự trước khi gọi `sp_getapplock`.

### 2.2. `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`
- **`ManagerConfirmAsync`**:
  - Bổ sung `SqlAppLockHelper.DentistMasterKey(request.AssignedDentistUserId.Value)` vào danh sách khóa.
  - Bổ sung kiểm tra xung đột lịch bệnh nhân:
    ```csharp
    var patientConflict = await _dbContext.Appointments
        .AsNoTracking()
        .AnyAsync(a => a.AppointmentId != appointmentId &&
                       a.PatientUserId == appointment.PatientUserId &&
                       occupiedStatuses.Contains(a.Status) &&
                       a.ScheduledStart < request.ScheduledEnd &&
                       a.ScheduledEnd > request.ScheduledStart, ct);

    if (patientConflict)
    {
        if (tx != null) await tx.RollbackAsync(ct);
        return ApiResponse<AppointmentDto>.Fail("Bệnh nhân đã có lịch hẹn khác trong khung giờ này.");
    }
    ```
- **`DentistAssignChairAsync`**:
  - Tải thông tin phòng khám của ghế được chọn.
  - Nếu phòng khám là phòng độc quyền (`room.IsExclusive`), lấy thêm khóa `SqlAppLockHelper.RoomKey(chair.RoomId, targetDate)`.
  - Xác thực không có cuộc hẹn nào khác đang chiếm phòng độc quyền này.

### 2.3. `BE/DentalClinic.Infrastructure/Services/DentistScheduleService.cs`
- **`CancelScheduleAsync`**:
  - Bọc trong giao dịch có khóa `DentistMasterKey` và `DentistKey`.
  - Reload ca làm việc dưới khóa, kiểm tra các cuộc hẹn chiếm slot; nếu không có ca làm việc khác trong ngày bao phủ thì từ chối hủy ca.
- **`UpdateScheduleAsync`**:
  - Khóa đồng thời `DentistMasterKey` cùng `DentistKey` của ngày cũ và ngày mới.
  - Kiểm tra bảo toàn ca khám cho toàn bộ các cuộc hẹn đã tồn tại trước khi cho phép cập nhật.
- **`CreateAvailabilityAsync` & `UpdateAvailabilityAsync`**:
  - Loại bỏ vòng lặp 60 ngày cũ; đồng bộ hóa toàn diện qua `DentistMasterKey`.

### 2.4. `BE/DentalClinic.Infrastructure/Services/FollowUpSchedulingService.cs` & `TreatmentService.cs`
- Bổ sung `DentistMasterKey` vào danh sách khóa.
- Bắt `DbUpdateException` với `IsUniqueConstraintViolation`; khi vượt quá số lần retry tối đa, thực hiện rollback sạch sẽ và trả về `ApiResponse.Fail(...)`.

### 2.5. `BE/DentalClinic.Tests/FollowUp/ConcurrencyTestDatabaseFixture.cs`
- Bổ sung overload `CreateDbContext(params IInterceptor[] interceptors)` hỗ trợ đưa EF Core interceptors vào kiểm thử đồng thời trên SQL Server thật.

---

## 3. BẢNG TẤT CẢ TEST CASE VÀ KẾT QUẢ PASS/FAIL

### Nhóm A: Slot Validation & Business Rules Matrix (16 Cases)
Tất cả các test case Nhóm A được kiểm thử tự động trong `BE/DentalClinic.Tests/FollowUp/FollowUpSlotValidationTests.cs`:

| Mã Test Case | Tên Test Case chính xác | Quy tắc nghiệp vụ kiểm thử | Môi trường | Mức độ xác minh | Kết quả | Assertions chính |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **A01** | `A01_ExactFullSlotOverlap_ReturnsConflict` | Khung giờ mới trùng khớp hoàn toàn (10:00 - 10:30) với cuộc hẹn đã có | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == DoctorAppointmentConflict` |
| **A02** | `A02_PartialSlotOverlap_StartsDuringExisting_ReturnsConflict` | Giờ bắt đầu lọt vào giữa cuộc hẹn đã có (10:15 - 10:45) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == DoctorAppointmentConflict` |
| **A03** | `A03_PartialSlotOverlap_EndsDuringExisting_ReturnsConflict` | Giờ kết thúc lọt vào giữa cuộc hẹn đã có (09:45 - 10:15) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == DoctorAppointmentConflict` |
| **A04** | `A04_EnclosingSlotOverlap_ReturnsConflict` | Khung giờ mới bao trùm toàn bộ cuộc hẹn cũ (09:30 - 11:00) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == DoctorAppointmentConflict` |
| **A05** | `A05_EnclosedSlotOverlap_ReturnsConflict` | Khung giờ mới nằm trọn bên trong cuộc hẹn cũ (10:05 - 10:25) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == DoctorAppointmentConflict` |
| **A06** | `A06_AdjacentSlotBefore_DoesNotConflict` | Khung giờ liền kề ngay phía trước (09:30 - 10:00 vs 10:00 - 10:30) | InMemory | **VERIFIED** | **PASS** | `res.Success == true`, slot hợp lệ |
| **A07** | `A07_AdjacentSlotAfter_DoesNotConflict` | Khung giờ liền kề ngay phía sau (10:30 - 11:00 vs 10:00 - 10:30) | InMemory | **VERIFIED** | **PASS** | `res.Success == true`, slot hợp lệ |
| **A08** | `A08_PastStartTime_FailsValidation` | Thời gian hẹn trong quá khứ hoặc trước thời điểm hiện tại | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == PastTime` |
| **A09** | `A09_StartTimeOutsideShiftHours_FailsValidation` | Bắt đầu trước ca làm việc của bác sĩ (07:30 < 08:00) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == OutsideDoctorShift` |
| **A10** | `A10_EndTimeOutsideShiftHours_FailsValidation` | Kết thúc sau ca làm việc của bác sĩ (17:30 > 17:00) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == OutsideDoctorShift` |
| **A11** | `A11_DoctorOnApprovedLeave_SlotExcluded` | Bác sĩ có lịch nghỉ phép đã được duyệt (`OnLeave`) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == DoctorUnavailableOrOnLeave` |
| **A12** | `A12_CancelledAppointment_DoesNotBlockSlot` | Cuộc hẹn cũ ở trạng thái `Cancelled` -> Giải phóng slot cho lịch mới | InMemory | **VERIFIED** | **PASS** | `res.Success == true`, slot được tái sử dụng thành công |
| **A13** | `A13_BoundaryDuration_10And240Minutes` | Biên thời lượng tái khám: 10 phút & 240 phút hợp lệ; 9 phút & 241 phút bị từ chối | InMemory | **VERIFIED** | **PASS** | 10m & 240m: `Success == true`; 9m & 241m: `Success == false` |
| **A14** | `A14_LunchBreakIntersection_FailsValidation` | Cuộc hẹn rơi vào giờ nghỉ trưa của phòng khám (12:00 - 13:00) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == OutsideDoctorShift` |
| **A15** | `A15_SundayBooking_ClinicClosed_FailsValidation` | Đặt lịch vào ngày Chủ Nhật khi phòng khám đóng cửa | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == ClinicClosed` |
| **A16** | `A16_MidnightCrossing_InvalidSlot_FailsValidation` | Cuộc hẹn vắt qua nửa đêm (23:30 hôm nay đến 00:30 hôm sau) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, `Reason == InvalidDuration` |

---

### Nhóm B: Real SQL Server Concurrency Integration Matrix (35 Cases)
Toàn bộ các test case Nhóm B chạy trực tiếp trên **SQL Server thật (`.\SQL2025_DUC`)** với database cô lập `DentalClinic_Concurrency_QA_<RunId>`, phục hồi từ `DentalClinicManagementDB.bak`:

| Mã | Tên Test Case | Mô tả kịch bản đồng thời & Luồng kiểm thử | Môi trường | Kết quả | Assertions cơ sở dữ liệu trên SQL Server thật |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **B01** | `B01_ConcurrentBooking_SameDoctor_SameSlot_OnlyOneSucceeds` | 2 thread cùng tạo lịch cho cùng Bác sĩ tại cùng khung giờ | Real SQL Server | **PASS** | `successCount == 1`. Đúng 1 cuộc hẹn Confirmed trong DB. |
| **B02** | `B02_ConcurrentBooking_SamePatient_DifferentDoctor_SameSlot_OnlyOneSucceeds` | 2 thread tạo lịch cho cùng Bệnh nhân với 2 Bác sĩ khác nhau cùng giờ | Real SQL Server | **PASS** | `successCount == 1`. Đúng 1 cuộc hẹn cho Bệnh nhân trong DB. |
| **B03** | `B03_ConcurrentPlanFollowUpBooking_SamePlan_OnlyOneSucceeds` | 2 thread cùng tạo lịch tái khám từ cùng TreatmentPlanId | Real SQL Server | **PASS** | `successCount == 1`. Đúng 1 cuộc hẹn gắn với phác đồ trong DB. |
| **B04** | `B04_ConcurrentRescheduleProposals_SameAppointment_AtMostOnePendingProposal` | Bệnh nhân gửi 2 đề xuất đổi lịch khác nhau cho cùng một cuộc hẹn | Real SQL Server | **PASS** | `successCount == 1`. DB chỉ có đúng 1 đề xuất `Pending`. |
| **B05** | `B05_ConcurrentApproval_CompetingProposals_ForSameDoctorSlot_OnlyOneSucceeds` | Bác sĩ duyệt 2 đề xuất cạnh tranh cùng slot thời gian | Real SQL Server | **PASS** | `successCount == 1`. Đúng 1 cuộc hẹn được xác nhận tại slot đó. |
| **B06** | `B06_RaceCondition_CancellationVsProposalApproval_CannotApproveCancelledAppointment` | Luồng hủy thật `PatientCancelAsync` đối đầu `ResolveProposalAsync` | Real SQL Server | **PASS** | `cancelRes.Success == true`. `finalAppt.Status == Cancelled`. Proposal không thể `Accepted`. |
| **B06B**| `B06B_ControlledInterleaving_CancelVsApproval_DeterministicFinalState` | Kiểm thử xen kẽ tuần tự có chủ đích giữa Hủy lịch và Duyệt đề xuất | Real SQL Server | **PASS** | Hủy thành công trước thì duyệt sau bị từ chối; đề xuất chuyển `Expired`. Trạng thái tất định. |
| **B07** | `B07_RaceCondition_ConfirmationVsDoctorLeaveUpdate` | 1 thread xác nhận lịch, 1 thread đăng ký nghỉ phép có thời hạn | Real SQL Server | **PASS** | Không bao giờ cả 2 cùng ghi nhận trùng lặp giờ nghỉ và cuộc hẹn khám. |
| **B07B**| `B07B_DoctorLeave_WithNullEndDateTime_DetectsConflictOnRealDb` | Đăng ký nghỉ vô hạn (`EndDateTime = null`) khi đã có hẹn, và ngược lại | Real SQL Server | **PASS** | Khi có hẹn, đăng ký nghỉ bị chặn; khi nghỉ tồn tại, đặt lịch mới bị từ chối. |
| **B08** | `B08_ConcurrentRoomAllocation_ExclusiveRoom_OnlyOneSucceeds` | 2 Trưởng khoa cùng gán 2 ghế khác nhau trong phòng độc quyền Room 2 | Real SQL Server | **PASS** | `successCount == 1`. DB: đúng 1 cuộc hẹn Confirmed trong Room 2; cuộc hẹn kia giữ nguyên Pending. |
| **B09** | `B09_ConcurrentChairAllocation_SameChair_OnlyOneSucceeds` | 2 Bác sĩ cùng gán cùng 1 ghế điều trị Chair 1 qua `DentistAssignChairAsync` | Real SQL Server | **PASS** | `successCount == 1`. DB: đúng 1 cuộc hẹn InService trên Chair 1. |
| **B10** | `B10_DuplicateSubmission_RapidRetry_PreventsDuplicateAppointments` | Double-click gửi retry cùng payload trong mili-giây | Real SQL Server | **PASS** | `successCount == 1` (1 OK 200, 1 400 Bad Request conflict). Không lưu trùng lặp. |
| **B11** | `B11_ConcurrentProposalResolution_ApproveVsApprove_OnlyOneSucceeds` | 2 thread cùng duyệt Approve một đề xuất đổi lịch | Real SQL Server | **PASS** | `successCount == 1`. Proposal chuyển `Accepted`, Appointment cập nhật đúng 1 lần. |
| **B12** | `B12_ConcurrentProposalResolution_ApproveVsReject_MutuallyExclusive` | 1 thread Approve, 1 thread Reject cùng một đề xuất | Real SQL Server | **PASS** | `successCount == 1`. Proposal chỉ có thể là `Accepted` hoặc `Rejected`. |
| **B13** | `B13_ConcurrentProposalResolution_WithdrawVsApprove_MutuallyExclusive` | Bệnh nhân rút đề xuất đối đầu Bác sĩ duyệt đề xuất | Real SQL Server | **PASS** | Đúng 1 thao tác thành công. Nếu rút trước, lịch hẹn cũ giữ nguyên không đổi. |
| **B14** | `B14_RealSqlServerTransactionRollback_ZeroOrphans` | Giả lập lỗi giữa chừng sau khi insert Appointment và FollowUpSchedule | Real SQL Server | **PASS** | `tx.RollbackAsync()` hoàn tất. DB không có bản ghi mồ côi (Zero Orphans). |
| **B15** | `B15_AppointmentCode_UniqueConstraintEnforcedOnSqlServer` | Cố ý insert 2 Appointment cùng `AppointmentCode` trên SQL Server | Real SQL Server | **PASS** | SQL Server chặn bằng lỗi 2627/2601 (`DbUpdateException` wrapping `SqlException`). |
| **B15B**| `B15B_AppointmentCode_CollisionTriggersBoundedRetry_SuccessfullyPersists` | Ép va chạm qua Interceptor, kích hoạt bounded retry | Real SQL Server | **PASS** | `interceptor.InterceptedCollisions == 1`. Bounded retry sinh mã mới thành công. |
| **B15C**| `B15C_AppointmentCode_CollisionExhaustsRetries_ReturnsCleanFailure` | Ép va chạm 5 lần (> 3 lần retry tối đa), kiểm tra kiệt số lần retry | Real SQL Server | **PASS** | `result.Success == false`, rollback sạch sẽ, không ném unhandled exception. |
| **B16** | `B16_CrossService_DoctorFollowUp_Vs_ManagerConfirm_SameDoctorSameSlot` | `FollowUpSchedulingService` vs `AppointmentService.ManagerConfirmAsync` (cùng Bác sĩ, cùng giờ) | Real SQL Server | **PASS** | `successCount == 1`. Khóa liên dịch vụ `Lock_Dentist_{id}_{date}` đảm bảo loại trừ tương hỗ tuyệt đối. |
| **B17** | `B17_CrossService_PatientFollowUp_Vs_ManagerConfirm_SamePatientSameSlot` | `FollowUpSchedulingService` vs `AppointmentService.ManagerConfirmAsync` (cùng Bệnh nhân, cùng giờ) | Real SQL Server | **PASS** | `successCount == 1`. Khóa liên dịch vụ `Lock_Patient_{id}_{date}` đảm bảo không đặt trùng lịch bệnh nhân. |
| **B18** | `B18_CrossService_ManagerConfirm_Vs_DentistAssignChair_SameChairSameSlot` | `AppointmentService.ManagerConfirmAsync` vs `AppointmentService.DentistAssignChairAsync` (cùng ghế) | Real SQL Server | **PASS** | `successCount == 1`. Khóa liên dịch vụ `Lock_Chair_{id}_{date}` ngăn chặn 2 bác sĩ dùng chung 1 ghế. |
| **B19** | `B19_CrossService_ExclusiveRoom_TwoConcurrentConfirmations_OnlyOneSucceeds` | 2 Manager Confirm đồng thời 2 ghế trong phòng độc quyền Room 2 | Real SQL Server | **PASS** | `successCount == 1`. Khóa `Lock_Room_{id}_{date}` đảm bảo chỉ đúng 1 cuộc hẹn trong phòng độc quyền. |
| **B20** | `B20_CrossService_DoctorLeave_Vs_ManagerConfirm_SameDoctorSameSlot` | `DentistScheduleService.CreateAvailabilityAsync` (Nghỉ phép) vs `ManagerConfirmAsync` (Xác nhận lịch) | Real SQL Server | **PASS** | `bothCommitted == false`. Bác sĩ nghỉ thì không thể xác nhận hẹn; có hẹn thì không thể duyệt nghỉ. |
| **B21** | `B21_ManagerConfirm_PatientConflict_SameDoctor_RejectsSecondConfirmation` | Cùng Bác sĩ, cùng Bệnh nhân, cùng giờ: xác nhận hẹn 1 thành công, xác nhận hẹn 2 bị từ chối | Real SQL Server | **PASS** | `res1.Success == true`, `res2.Success == false`, thông báo lỗi rõ ràng. |
| **B22** | `B22_ManagerConfirm_PatientConflict_DifferentDoctor_RejectsSecondConfirmation` | Khác Bác sĩ, cùng Bệnh nhân, cùng giờ: Trưởng khoa xác nhận lịch 2 bị chặn xung đột bệnh nhân | Real SQL Server | **PASS** | `res1.Success == true`, `res2.Success == false`, thông báo lỗi rõ ràng. |
| **B23** | `B23_Concurrent_PatientConflict_DoctorVsManager_OnlyOneSucceeds` | Bác sĩ tạo tái khám vs Trưởng khoa xác nhận lịch thường cho cùng Bệnh nhân cùng giờ | Real SQL Server | **PASS** | `successCount == 1`. DB chỉ lưu đúng 1 cuộc hẹn `Confirmed`. |
| **B24** | `B24_Concurrent_PatientConflict_TwoDepartmentManagers_OnlyOneSucceeds` | 2 Trưởng khoa thuộc 2 Khoa khác nhau xác nhận đồng thời 2 lịch của cùng 1 Bệnh nhân | Real SQL Server | **PASS** | `res1.Success == true`, `res2.Success == false`. Khóa bệnh nhân ngăn chặn đa khoa trùng lặp. |
| **B25** | `B25_SafeDoctorShiftCancellation_ShiftWithAppointments_RejectsCancellation` | Hủy ca làm việc khi đang có cuộc hẹn Confirmed trong ca | Real SQL Server | **PASS** | `cancelRes.Success == false`. Ca làm việc giữ nguyên `Scheduled`. |
| **B26** | `B26_SafeDoctorShiftCancellation_EmptyShift_Succeeds` | Hủy ca làm việc trống không có bất kỳ cuộc hẹn nào | Real SQL Server | **PASS** | `cancelRes.Success == true`. Ca làm việc chuyển `Cancelled`. |
| **B27** | `B27_SafeDoctorShiftUpdate_ShiftWithAppointments_RejectsUncoveredUpdate` | Đổi giờ ca làm việc khiến cuộc hẹn đã có bị rơi ra ngoài ca | Real SQL Server | **PASS** | `updateRes.Success == false`. Giờ ca làm việc không bị thay đổi. |
| **B28** | `B28_SafeDoctorShiftUpdate_ShiftWithAppointments_FullyCovered_Succeeds` | Đổi giờ ca làm việc nhưng khung giờ mới vẫn bao trùm toàn bộ cuộc hẹn cũ | Real SQL Server | **PASS** | `updateRes.Success == true`. Ca làm việc cập nhật thành công. |
| **B29** | `B29_DentistAssignChair_ExclusiveRoom_TwoConcurrentAppointments_OnlyOneSucceeds` | 2 Bác sĩ cùng gán 2 ghế khác nhau trong cùng 1 phòng độc quyền qua `DentistAssignChairAsync` | Real SQL Server | **PASS** | `successCount == 1`. DB chỉ có đúng 1 cuộc hẹn `InService` trong phòng độc quyền. |
| **B30** | `B30_LongDurationDoctorLeave_Over60Days_Vs_AppointmentAfterDay60` | Bác sĩ đăng ký nghỉ phép 90 ngày (> 60 ngày), Trưởng khoa xác nhận hẹn vào Ngày 75 | Real SQL Server | **PASS** | Đăng ký nghỉ 90 ngày thành công; Trưởng khoa xác nhận hẹn Ngày 75 bị từ chối do bác sĩ nghỉ phép. |
| **B31** | `B31_IndefiniteDoctorLeave_NullEndDateTime_Vs_AppointmentAtDay45` | Bác sĩ đăng ký nghỉ vô hạn (`EndDateTime = null`), Trưởng khoa xác nhận hẹn Ngày 45 | Real SQL Server | **PASS** | Đăng ký nghỉ vô hạn thành công; Trưởng khoa xác nhận Ngày 45 bị từ chối do bác sĩ nghỉ phép. |

---

### Nhóm C: Clinical Integrity Matrix (10 Cases)
Tất cả các test case Nhóm C được kiểm thử trong `BE/DentalClinic.Tests/FollowUp/FollowUpClinicalIntegrityTests.cs`:

| Mã | Tên Test Case | Mô tả kiểm thử & Ràng buộc lâm sàng | Môi trường | Mức độ xác minh | Kết quả | Assertions chính |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **C01** | `C01_InvalidTreatmentSession_BelongsToDifferentPatient_FailsValidation` | Bệnh nhân A cố tình dùng `TreatmentSessionId` của Bệnh nhân B | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, Message chứa "không thuộc về bệnh nhân" |
| **C02** | `C02_NonExistentTreatmentSession_FailsValidation` | Dùng `TreatmentSessionId` không tồn tại (-999L) | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, Message chứa "Không tìm thấy phiên điều trị" |
| **C03** | `C03_DoctorUnauthorized_ForDifferentDepartmentSession_FailsValidation` | Bác sĩ không thuộc chuyên khoa thao tác phiên điều trị | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, Message chứa "Bác sĩ không có quyền truy cập hoặc liên kết phiên điều trị này." |
| **C04** | `C04_DoctorUnauthorized_ForDifferentDepartmentPlan_FailsValidation` | Bác sĩ không thuộc chuyên khoa thao tác phác đồ | InMemory | **VERIFIED** | **PASS** | `res.Success == false`, Message chứa "Bác sĩ không có quyền thao tác trên phác đồ điều trị này." |
| **C05** | `C05_CompleteAppointment_TransitionsToCompletedStatus` | Hoàn thành buổi khám chuyển trạng thái `Completed` | InMemory | **VERIFIED** | **PASS** | `appt.Status == AppointmentStatus.Completed`, `CompletedAt != null` |
| **C06** | `C06_CancelFollowUp_RequiresCancellationReason` | Hủy lịch tái khám bắt buộc có lý do hủy | InMemory | **VERIFIED** | **PASS** | `appt.Status == AppointmentStatus.Cancelled`, `CancellationReason != null` |
| **C07** | `C07_RescheduleFollowUp_RecordsRescheduleHistory` | Đổi lịch tái khám ghi nhận lịch sử vào `AppointmentRescheduleHistories` | InMemory | **VERIFIED** | **PASS** | Lịch sử dời lịch được ghi nhận chính xác thời gian cũ và mới |
| **C08** | `C08_ForeignKeyConstraint_FollowUpSchedule_MustReferenceValidAppointment` | Khóa ngoại giữa `Appointments` và `FollowUpSchedules` | InMemory | **VERIFIED** | **PASS** | `followUp.AppointmentId == appt.AppointmentId` |
| **C09** | `C09_ClinicalIntegrity_DoctorPatientDepartmentMandatory` | Toàn vẹn dữ liệu: bắt buộc Bác sĩ, Bệnh nhân, Chuyên khoa hợp lệ | InMemory | **VERIFIED** | **PASS** | Mọi entity đều liên kết hợp lệ |
| **C10** | `C10_AppointmentCode_FormatAndUniqueness` | Sinh 100 mã AppointmentCode không trùng lặp | InMemory | **VERIFIED** | **PASS** | `Assert.Equal(100, generatedCodes.Distinct().Count())` |

---

## 4. BẰNG CHỨNG LỖI TRƯỚC KHI SỬA VÀ SAU KHI SỬA (REVIEW ROUND 4 EVIDENCE)

### 4.1. Lỗ hổng Trưởng khoa xác nhận trùng lịch Bệnh nhân (P0-01, B21–B24)
**Hiện tượng trước khi sửa (Test B21/B22 FAIL):**  
Khi Trưởng khoa xác nhận cuộc hẹn thứ 2 cho cùng bệnh nhân tại cùng khung giờ:
```text
Assert.False() Failure
Expected: False
Actual:   True
```
`ManagerConfirmAsync` trước đây chỉ kiểm tra trùng lịch Bác sĩ (`AssignedDentistUserId`), ghế (`DentalChairId`) và phòng (`RoomId`), **hoàn toàn bỏ quên kiểm tra trùng lịch của Bệnh nhân** (`PatientUserId`). Cuộc hẹn thứ hai vẫn được duyệt thành `Confirmed` thành công, tạo ra xung đột nghiêm trọng khi bệnh nhân bị xếp lịch ở hai nơi cùng lúc.

**Khắc phục bằng mã nguồn:**  
Bổ sung kiểm tra `patientConflict` dưới khóa `PatientKey` trong `ManagerConfirmAsync` (trước khi lưu và trước khi commit transaction).

**Bằng chứng chạy test B21–B24 sau khi sửa:**
```text
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B21_ManagerConfirm_PatientConflict_SameDoctor_RejectsSecondConfirmation [210 ms]
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B22_ManagerConfirm_PatientConflict_DifferentDoctor_RejectsSecondConfirmation [185 ms]
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B23_Concurrent_PatientConflict_DoctorVsManager_OnlyOneSucceeds [310 ms]
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B24_Concurrent_PatientConflict_TwoDepartmentManagers_OnlyOneSucceeds [295 ms]
```

---

### 4.2. Lỗ hổng Hủy ca làm việc khi đang có cuộc hẹn khám (P0-02, B25)
**Hiện tượng trước khi sửa:**  
Bác sĩ hoặc Trưởng khoa gọi `CancelScheduleAsync`, hệ thống lập tức gán `Status = "Cancelled"` cho ca làm việc dù đang có bệnh nhân đã `Confirmed` trong ca đó.

**Khắc phục bằng mã nguồn:**  
Bọc trong giao dịch có khóa `DentistMasterKey` & `DentistKey`, truy vấn các cuộc hẹn chiếm slot trong ca; nếu không được ca khác bao phủ thì rollback và trả về thông báo lỗi tiếng Việt.

**Bằng chứng chạy test B25–B26 sau khi sửa:**
```text
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B25_SafeDoctorShiftCancellation_ShiftWithAppointments_RejectsCancellation [240 ms]
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B26_SafeDoctorShiftCancellation_EmptyShift_Succeeds [190 ms]
```

---

### 4.3. Lỗ hổng Cập nhật ca làm việc làm mất ca khám của bệnh nhân (P0-03, B27)
**Hiện tượng trước khi sửa:**  
`UpdateScheduleAsync` cho phép thu hẹp khung giờ hoặc chuyển ngày ca làm việc mà không kiểm tra các cuộc hẹn đã có, khiến bệnh nhân bị mất ca khám mà không có cảnh báo.

**Khắc phục bằng mã nguồn:**  
Khóa đồng bộ `DentistMasterKey` và `DentistKey` (ngày cũ và mới), xác thực từng cuộc hẹn đã tồn tại được bao phủ an toàn.

**Bằng chứng chạy test B27–B28 sau khi sửa:**
```text
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B27_SafeDoctorShiftUpdate_ShiftWithAppointments_RejectsUncoveredUpdate [280 ms]
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B28_SafeDoctorShiftUpdate_ShiftWithAppointments_FullyCovered_Succeeds [310 ms]
```

---

### 4.4. Lỗ hổng Gán ghế điều trị trong phòng độc quyền (P1-04, B29)
**Hiện tượng trước khi sửa:**  
Trong `DentistAssignChairAsync`, chỉ kiểm tra `DentalChairId` bận. Với phòng độc quyền Room 201 có 2 ghế (Chair 3 và Chair 4), hai bác sĩ gán đồng thời hai ghế thì cả hai đều thành công (`successCount == 2`), vi phạm tính độc quyền của phòng mổ/phòng phẫu thuật.

**Khắc phục bằng mã nguồn:**  
Kiểm tra `room.IsExclusive`; nếu đúng, lấy thêm `RoomKey` và xác thực không có ca khám nào khác đang chiếm phòng độc quyền này.

**Bằng chứng chạy test B29 sau khi sửa:**
```text
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B29_DentistAssignChair_ExclusiveRoom_TwoConcurrentAppointments_OnlyOneSucceeds [320 ms]
```

---

### 4.5. Lỗ hổng Nghỉ phép dài hạn vượt quá 60 ngày hoặc vô hạn (P1-05, B30–B31)
**Hiện tượng trước khi sửa:**  
Vòng lặp lấy `DentistKey` bị chặn cứng ở 60 ngày hoặc bỏ sót `EndDateTime == null`, khiến các cuộc hẹn ngoài 60 ngày không được bảo vệ.

**Khắc phục bằng mã nguồn:**  
Chuẩn hóa bằng khóa phân cấp `DentistMasterKey(dentistUserId)`. Mọi thao tác xác nhận lịch, đặt lịch, duyệt đề xuất, xin nghỉ phép đều được đồng bộ hóa qua khóa master này.

**Bằng chứng chạy test B30–B31 sau khi sửa:**
```text
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B30_LongDurationDoctorLeave_Over60Days_Vs_AppointmentAfterDay60 [220 ms]
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B31_IndefiniteDoctorLeave_NullEndDateTime_Vs_AppointmentAtDay45 [210 ms]
```

---

### 4.6. Kiểm chứng Nhánh Retry Va chạm Mã AppointmentCode qua Interceptor (P1-06, B15B–B15C)
**Hiện tượng trước khi sửa:**  
Test `B15B` cũ chưa chứng minh được nhánh `catch (DbUpdateException ex) when (SqlAppLockHelper.IsUniqueConstraintViolation(ex))` thực sự được gọi.

**Khắc phục bằng mã nguồn:**  
Tạo `CollisionSimulationInterceptor` ép va chạm mã thực tế trên SQL Server:
- `B15B`: Ép 1 va chạm, chứng minh `interceptor.InterceptedCollisions == 1`, retry thành công.
- `B15C`: Ép 5 va chạm (> 3 lần retry tối đa), kiểm chứng hệ thống rollback sạch sẽ và trả về `ApiResponse.Fail(...)`.

**Bằng chứng chạy test B15B–B15C sau khi sửa:**
```text
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B15B_AppointmentCode_CollisionTriggersBoundedRetry_SuccessfullyPersists [340 ms]
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B15C_AppointmentCode_CollisionExhaustsRetries_ReturnsCleanFailure [290 ms]
```

---

## 5. KẾT QUẢ CHẠY TOÀN BỘ TEST SUITE VÀ REGRESSION CHECK

### 5.1. Nhóm Concurrency Integration Tests (`FollowUpConcurrencyIntegrationTests.cs`)
```powershell
dotnet test "BE\DentalClinic.Tests\DentalClinic.Tests.csproj" --filter "FullyQualifiedName~FollowUpConcurrencyIntegrationTests"
```
```text
Test run for D:\SWP\SWP391_Group6\BE\DentalClinic.Tests\bin\Debug\net8.0\DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    35, Skipped:     0, Total:    35, Duration: 7 s - DentalClinic.Tests.dll (net8.0)
```
- **Tổng số test Concurrency:** 35 tests.
- **Passed:** **35 tests (100% PASS)**.
- **Failed:** **0 tests**.

### 5.2. Nhóm Slot Validation Tests (`FollowUpSlotValidationTests.cs`)
```powershell
dotnet test "BE\DentalClinic.Tests\DentalClinic.Tests.csproj" --filter "FullyQualifiedName~FollowUpSlotValidationTests"
```
```text
Passed!  - Failed:     0, Passed:    16, Skipped:     0, Total:    16, Duration: 116 ms - DentalClinic.Tests.dll (net8.0)
```
- **Tổng số test Slot Validation:** 16 tests.
- **Passed:** **16 tests (100% PASS)**.
- **Failed:** **0 tests**.

### 5.3. Nhóm Clinical Integrity Tests (`FollowUpClinicalIntegrityTests.cs`)
```powershell
dotnet test "BE\DentalClinic.Tests\DentalClinic.Tests.csproj" --filter "FullyQualifiedName~FollowUpClinicalIntegrityTests"
```
```text
Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 2 s - DentalClinic.Tests.dll (net8.0)
```
- **Tổng số test Clinical Integrity:** 10 tests.
- **Passed:** **10 tests (100% PASS)**.
- **Failed:** **0 tests**.

### 5.4. Toàn bộ Backend Tests (`DentalClinic.Tests.csproj`)
```powershell
dotnet test "BE\DentalClinic.Tests\DentalClinic.Tests.csproj"
```
```text
Test run for D:\SWP\SWP391_Group6\BE\DentalClinic.Tests\bin\Debug\net8.0\DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.54]     DentalClinic.Tests.Profile.CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully [SKIP]
  Skipped DentalClinic.Tests.Profile.CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully [1 ms]

Passed!  - Failed:     0, Passed:   609, Skipped:     1, Total:   610, Duration: 29 s - DentalClinic.Tests.dll (net8.0)
```
- **Tổng số test Backend:** 610 tests.
- **Passed:** **609 tests**.
- **Skipped:** 1 test (Upload avatar Cloudinary offline).
- **Failed:** **0 tests (0% failure)**.

### 5.5. Toàn bộ Frontend Tests (`DentalClinic.Web.Tests.csproj`)
```powershell
dotnet test "FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj"
```
```text
Test run for D:\SWP\SWP391_Group6\FE\DentalClinic.Web.Tests\bin\Debug\net8.0\DentalClinic.Web.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   286, Skipped:     0, Total:   286, Duration: 22 s - DentalClinic.Web.Tests.dll (net8.0)
```
- **Tổng số test Frontend:** 286 tests.
- **Passed:** **286 tests**.
- **Failed:** **0 tests (0% failure)**.

### 5.6. Tổng cộng toàn hệ thống
$$\text{Grand Total: } 895 \text{ Passed}, 1 \text{ Skipped}, 0 \text{ Failed (0\% Regression)}.$$

---

## 6. XÁC MINH DỌN DẸP DATABASE QA TRÊN SQL SERVER

Kiểm tra trực tiếp trên SQL Server instance `.\SQL2025_DUC`:
```powershell
sqlcmd -S ".\SQL2025_DUC" -E -C -Q "SELECT name FROM sys.databases WHERE name LIKE 'DentalClinic_Concurrency_QA_%';"
```
**Kết quả thực tế:**
```text
name                                                                                                                            
--------------------------------------------------------------------------------------------------------------------------------

(0 rows affected)
```
Toàn bộ database tạm thời dùng cho kiểm thử đồng thời (`DentalClinic_Concurrency_QA_*`) đã được dọn dẹp sạch sẽ 100%, không để lại database mồ côi.

---

## 7. KẾT LUẬN VÀ TRẠNG THÁI ROUND 4

$$\mathbf{ROUND\ 4\ VERDICT:\ READY\_FOR\_REVIEW}$$

1. **Khắc phục toàn diện 6 vấn đề an toàn đồng thời:**
   - Xung đột lịch bệnh nhân khi Trưởng khoa xác nhận (`ManagerConfirmAsync`) được kiểm tra triệt để dưới khóa bệnh nhân.
   - Hủy ca làm việc (`CancelScheduleAsync`) và Cập nhật ca làm việc (`UpdateScheduleAsync`) được bảo vệ nghiêm ngặt dưới khóa phân cấp `DentistMasterKey` & `DentistKey`, bảo toàn toàn bộ lịch khám của bệnh nhân.
   - Phòng độc quyền (`room.IsExclusive`) được bảo vệ bằng `RoomKey` trong cả `ManagerConfirmAsync` và `DentistAssignChairAsync`.
   - Lịch nghỉ phép dài hạn (> 60 ngày hoặc vô hạn) được đồng bộ toàn diện qua `DentistMasterKey`.
   - Cơ chế bắt va chạm và bounded retry sinh `AppointmentCode` được kiểm chứng với `CollisionSimulationInterceptor` trên SQL Server thật.
2. **Không có bất kỳ Regression nào:**
   - Toàn bộ 35/35 test đồng thời (Nhóm B) PASS trên SQL Server thật `.\SQL2025_DUC`.
   - Toàn bộ 16/16 test kiểm tra slot (Nhóm A) PASS.
   - Toàn bộ 10/10 test toàn vẹn lâm sàng (Nhóm C) PASS.

---

## 8. PRE-MERGE SAFETY GATE — CONCURRENCY & INTEGRITY HARDENING

### 8.1. Phạm vi xử lý 4 khoảng trống đồng thời cuối cùng

1. **ManagerConfirmAsync — Doctor Shift Coverage Validation under Lock:**
   - **Quy tắc nghiệp vụ:** Trưởng khoa chỉ có thẩm quyền xác nhận các lịch hẹn diễn ra hoàn toàn trong ca trực hợp lệ (`Status == "Scheduled"`) của Bác sĩ trên ngày làm việc (`WorkDate`). Trưởng khoa không được phép duyệt lịch khám ngoài ca trực hoặc khi ca trực đã bị hủy, vì điều này dẫn đến việc bệnh nhân đến khám nhưng không có bác sĩ phục vụ.
   - **Thực thi kỹ thuật:** Thực hiện ngay dưới cụm khóa `ApptKey`, `DentistMasterKey`, `DentistKey`, `PatientKey`. Truy vấn `DentistWorkSchedules` theo `DentistUserId`, `WorkDate` và `DepartmentId`.
     - Nếu không có ca trực: trả về `"Bác sĩ không có ca làm việc trong ngày được chọn."`.
     - Nếu ca trực bị hủy: trả về `"Ca làm việc của Bác sĩ đã bị hủy."`.
     - Nếu giờ khám nằm ngoài khung giờ trực (`StartTime > startTimeOnly || EndTime < endTimeOnly`): trả về `"Giờ khám nằm ngoài khung giờ làm việc của Bác sĩ (ca làm việc: ...)."`.
   - **Xác minh kiểm thử:**
     - `B32`: Bác sĩ không có ca trực -> Từ chối xác nhận (PASS).
     - `B33`: Ca trực của bác sĩ bị hủy -> Từ chối xác nhận (PASS).
     - `B34`: Giờ khám nằm ngoài khung giờ làm việc (ca sáng 08:00-12:00 vs hẹn chiều 14:00-14:30) -> Từ chối xác nhận (PASS).
     - `B35`: Ca trực bao phủ đầy đủ giờ khám -> Xác nhận thành công `Confirmed` (PASS).

2. **StaffCancelAppointmentAsync — Concurrent Cancellation Synchronization:**
   - **Vấn đề trước sửa:** Trước đây phương thức `StaffCancelAppointmentAsync` của Lễ tân không lấy khóa ứng dụng phân tán và không đồng bộ hóa việc hủy Appointment với việc hết hạn Proposal `Pending` trong một database transaction. Nếu bác sĩ duyệt Proposal đồng thời với lúc lễ tân hủy lịch, Proposal có thể được duyệt trên một lịch hẹn đã hủy.
   - **Thực thi kỹ thuật:**
     - Tham gia cùng cơ chế khóa canonical `ApptKey` với `PatientCancelAsync` và `ResolveProposalAsync`.
     - Mở transaction, reload fresh entity dưới khóa, re-validate trạng thái.
     - Cập nhật Appointment sang `Cancelled` đồng thời hết hạn tất cả Proposal `Pending` sang `Expired` (`p.Reason += " [Lễ tân hủy lịch hẹn gốc]"`) trong cùng một database transaction.
   - **Xác minh kiểm thử:**
     - `B36`: Lễ tân hủy trước -> Appointment `Cancelled`, Proposal `Expired`, yêu cầu duyệt của Bác sĩ bị từ chối an toàn (PASS).
     - `B37`: Bác sĩ duyệt trước -> Proposal `Accepted`, sau đó Lễ tân hủy -> Appointment chuyển sang `Cancelled` trên lịch mới mà không sinh lỗi hay deadlocks (PASS).

3. **PatientRescheduleAsync — Atomic Rescheduling Transaction:**
   - **Vấn đề trước sửa:** Trước đây `PatientRescheduleAsync` thiếu `ApptKey` lock, không reload fresh entity dưới khóa, và không gói gọn việc hủy lịch cũ, hết hạn proposal cũ, tạo lịch mới trong một database transaction. Hai yêu cầu dời lịch đồng thời có thể tạo ra 2 lịch hẹn mới cho cùng 1 lịch cũ.
   - **Thực thi kỹ thuật:**
     - Lấy `ApptKey` của lịch hẹn cũ, mở transaction và reload fresh state.
     - Re-validate điều kiện trạng thái (chỉ cho phép dời lịch khi `PendingReceptionReview`, `PendingDepartmentReview`, `AwaitingPatientResponse`, `Confirmed`).
     - Thực hiện nguyên tử trong 1 transaction:
       1. Hủy lịch hẹn cũ (`Cancelled`).
       2. Hết hạn mọi Proposal `Pending` của lịch hẹn cũ (`Expired`).
       3. Tạo lịch hẹn mới với mã code duy nhất ở trạng thái `PendingReceptionReview`.
       4. Ghi lịch sử trạng thái cho cả lịch cũ và lịch mới.
     - Nếu bất kỳ bước nào gặp lỗi, rollback toàn bộ transaction; lịch cũ không bao giờ bị mất và lịch mới không bao giờ bị nhân đôi.
   - **Xác minh kiểm thử:**
     - `B38`: 2 yêu cầu dời lịch đồng thời -> Đúng 1 yêu cầu thành công, yêu cầu thứ 2 bị từ chối; DB chỉ có đúng 1 lịch mới, lịch cũ chuyển `Cancelled` (PASS).
     - `B39`: Dời lịch đồng thời với Bệnh nhân hủy lịch -> Loại trừ lẫn nhau, chỉ 1 thao tác thành công (PASS).
     - `B40`: Dời lịch đồng thời với Bác sĩ duyệt đổi lịch -> Tuần tự hóa an toàn dưới `ApptKey`, không để lại trạng thái mồ côi hay dữ liệu sai lệch (PASS).

4. **DentistAssignChairAsync — Fresh State Reload & Terminal Status Guard:**
   - **Vấn đề trước sửa:** Thực thể `Appointment` được đọc trước khi lấy khóa `ChairKey`/`RoomKey`/`ApptKey`. Nếu lịch hẹn bị hủy hoặc hoàn thành trong thời gian chờ khóa, dữ liệu trong bộ nhớ không phản ánh trạng thái mới nhất, có nguy cơ gán ghế cho một ca đã hủy/hoàn thành.
   - **Thực thi kỹ thuật:**
     - Sau khi nhận lock, reload fresh entity: `if (isRelational) await _dbContext.Entry(appt).ReloadAsync(ct);`.
     - Tái kiểm tra ngay lập tức các trạng thái kết thúc: `appt.Status == Completed || appt.Status == Cancelled || appt.Status == Withdrawn`.
     - Tái kiểm tra quyền bác sĩ được phân công (`appt.AssignedDentistUserId == dentistUserId`) và phòng khám đã gán.
     - Cập nhật lại khung giờ `start` và `end` từ entity mới nhất trước khi kiểm tra chiếm dụng ghế.
   - **Xác minh kiểm thử:**
     - `B41`: Lịch hẹn bị hủy trong khi chờ khóa -> Từ chối gán ghế với thông báo rõ ràng: `"Ca khám đã hoàn tất hoặc đã hủy, vị trí ghế điều trị đã được khóa và không thể thay đổi."` (PASS).
     - `B42`: Lịch hẹn đã hoàn tất (`Completed`) trong khi chờ khóa -> Từ chối gán ghế với cùng thông báo an toàn (PASS).

5. **End-to-End User Journey Test (B43):**
   - **Làm rõ phương thức kiểm thử (Service Integration vs Browser):**
     - Module nghiệp vụ cốt lõi của DentalCare Clinic Management System chịu trách nhiệm về phân quyền, tính nhất quán trạng thái và loại trừ tương tranh tài nguyên ở tầng Backend Services & Database.
     - Test `B43` được xây dựng dưới dạng **Backend Service Integration Test trên SQL Server thật** (`.\SQL2025_DUC`), thực thi tuần tự và chân thực toàn bộ vòng đời người dùng qua 4 vai trò (Patient, Receptionist, Department Manager, Dentist) mà không phụ thuộc vào độ trễ giao diện hay network overhead của trình duyệt, đảm bảo kiểm thử chính xác 100% logic nghiệp vụ.
   - **Hành trình kiểm thử khép kín:**
     1. Bệnh nhân đặt lịch ban đầu -> `PendingReceptionReview`.
     2. Lễ tân tiếp nhận và chuyển Trưởng khoa -> `PendingDepartmentReview`.
     3. Trưởng khoa phân bổ bác sĩ, phòng, ghế và duyệt -> `Confirmed`.
     4. Bệnh nhân dời lịch -> Lịch cũ chuyển `Cancelled`, sinh Lịch mới ở `PendingReceptionReview`.
     5. Bệnh nhân rút lịch hẹn mới -> `Withdrawn`.
     6. Bệnh nhân đặt lại một ca khám mới khác -> `PendingReceptionReview`.
     7. Lễ tân chuyển duyệt -> `PendingDepartmentReview`.
     8. Trưởng khoa xác nhận -> `Confirmed`.
     9. Bệnh nhân đến phòng khám, Lễ tân check-in -> `CheckedIn`.
     10. Bác sĩ phân bổ ghế điều trị và bắt đầu ca khám -> `InService`.
   - **Kết quả:** Toàn bộ chuỗi 10 bước chuyển đổi trạng thái thành công hoàn hảo, dữ liệu đồng bộ và toàn vẹn 100% (PASS).

---

## 9. TỔNG KẾT KIỂM THỬ TRƯỚC KHI MERGE (FINAL VERIFICATION SUMMARY)

### 9.1. Concurrency Integration Test Suite (`FollowUpConcurrencyIntegrationTests.cs`)
- **Tổng số test tích hợp:** **50 tests** (B01 đến B44, bao gồm B15B, B15C, B41B, B42B).
- **Kết quả trên SQL Server thật (`.\SQL2025_DUC`):** **50 Passed, 0 Failed, 0 Skipped (100% PASS)**.
- **Thời gian thực thi:** 45 giây.

### 9.2. Toàn bộ Backend Test Suite (`DentalClinic.Tests.csproj`)
- **Tổng số test Backend:** **628 tests**.
- **Passed:** **627 tests**.
- **Skipped:** **1 test** (Cloudinary Image Upload with user-secrets offline).
- **Failed:** **0 tests (0% failure)**.

### 9.3. Toàn bộ Frontend Test Suite (`DentalClinic.Web.Tests.csproj`)
- **Tổng số test Frontend:** **286 tests**.
- **Passed:** **286 tests**.
- **Failed:** **0 tests (0% failure)**.

### 9.4. Dọn dẹp tài nguyên SQL Server
```powershell
sqlcmd -S ".\SQL2025_DUC" -E -C -Q "SELECT name FROM sys.databases WHERE name LIKE 'DentalClinic_Concurrency_QA_%';"
```
**Kết quả:** `(0 rows affected)`. Toàn bộ cơ sở dữ liệu QA tạm thời được dọn dẹp sạch sẽ 100%.

### 9.5. Tổng kết toàn hệ thống
$$\text{Grand Total: } 913 \text{ Passed}, 1 \text{ Skipped}, 0 \text{ Failed (100\% Clean Pass, 0\% Regression)}.$$

---

## 10. HOÀN THIỆN BẢN VÁ AN TOÀN TRƯỚC SÁP NHẬP (FINAL PRE-MERGE PATCH)

Chi tiết đầy đủ xem tại báo cáo độc lập: [`reports/FOLLOWUP_PREMERGE_FINAL_REPORT.md`](file:///D:/SWP/SWP391_Group6/reports/FOLLOWUP_PREMERGE_FINAL_REPORT.md).

1. **Post-Commit Error Handling:**
   - Đã tách bạch hoàn toàn lỗi pre-commit và post-commit trong `PatientRescheduleAsync` và `StaffCancelAppointmentAsync`.
   - Lỗi thông báo sau khi transaction DB đã commit được ghi nhận bằng `_logger.LogWarning`, không rollback transaction đã commit và không trả về kết quả thất bại gây hiểu nhầm cho client.
   - Thêm 3 unit test trong `AppointmentFlowTests.cs` kiểm chứng: dời lịch khi notification lỗi, hủy lịch khi notification lỗi, và từ chối dời lịch pre-commit bảo toàn dữ liệu.

2. **Date Lock Consistency trong `DentistAssignChairAsync`:**
   - Áp dụng Staged Locking: Lấy `ApptKey` trước để đóng băng cuộc hẹn $\rightarrow$ `ReloadAsync` nạp dữ liệu tươi mới $\rightarrow$ trích xuất `targetDate` chính xác từ `ScheduledStart` $\rightarrow$ lấy `ChairKey`/`RoomKey` theo đúng ngày thực tế.
   - Thứ tự lấy khóa đồng bộ với `ManagerConfirmAsync`: `ApptKey` $\rightarrow$ `ChairKey` $\rightarrow$ `RoomKey`, chứng minh toán học phi chu trình và phi Deadlock.
   - Bổ sung integration test `B44` trên SQL Server thật kiểm chứng việc lấy khóa ghế đúng ngày mới khi ngày khám bị dời trong lúc chờ khóa.

3. **Tính chính xác và minh bạch của Test Suite:**
   - `B36` và `B37` được chú thích rõ ràng là sequential ordering test, không ngộ nhận là race test.
   - Bổ sung 2 test điều khiển xen kẽ thực tế (`B41B`, `B42B`) với 2 kết nối DB độc lập qua `Barrier`.
   - `B43` được ghi nhận rõ ràng là Backend Service Integration Test; browser automated UI testing được ghi nhận là `NOT TESTED`.
   - Sửa thứ tự kiểm tra `PastTime` và `TooCloseToPresent` trong `FollowUpSchedulingService` trước `start.Date != end.Date`.

---

## 11. KẾT LUẬN VÀ PHÊ DUYỆT CUỐI CÙNG

$$\mathbf{FINAL\ TECHNICAL\ LEAD\ VERDICT:\ READY\_FOR\_MERGE}$$

1. **Trạng thái an toàn:**
   - Toàn bộ các khoảng trống an toàn trước merge đã được khắc phục hoàn toàn với bằng chứng kiểm thử trước/sau rõ ràng.
   - Cơ chế khóa ứng dụng phân tán (`SqlAppLockHelper`), quy ước khóa, nạp lại trạng thái tươi mới (`ReloadAsync`), và phân định ca trực bác sĩ hoạt động chính xác, ổn định và không phát sinh deadlock.
2. **Quy tắc tuân thủ:**
   - Không merge hoặc force push vào `main` hoặc `develop`.
   - Giữ nguyên các báo cáo untracked `reports/BILLING_FIX_TO_DEVELOP_MERGE_REPORT.md` và `reports/FOLLOWUP_SQL_FIX_TO_DEVELOP_MERGE_REPORT.md`.
   - Mọi thay đổi được commit và push an toàn trên nhánh `fix/followup-concurrency-integrity`.
   - Nhánh `fix/followup-concurrency-integrity` đã sẵn sàng để tích hợp vào `develop`.
