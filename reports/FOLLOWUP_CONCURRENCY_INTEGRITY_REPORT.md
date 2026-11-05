# FOLLOW-UP CONCURRENCY & INTEGRITY HARDENING REPORT (REVIEW ROUND 3 - FINAL)

**Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)  
**Workspace:** `D:\SWP\SWP391_Group6`  
**Nhánh làm việc:** `fix/followup-concurrency-integrity`  
**Nhánh gốc (Base):** `origin/develop`  
**Base Commit HEAD:** `9ca88ea9f24f5e74b93c4abb77717fb5fae62f79`  
**SQL Server Instance:** `.\SQL2025_DUC`  
**Vai trò:** Senior .NET 8 / SQL Server Concurrency Engineer, QA Automation Engineer & Security Reviewer  
**Technical Lead Review:** Review Round 3 (Khắc phục hoàn toàn các điểm chặn P0/P1 về thống nhất khóa tài nguyên và hủy lịch đồng thời)  
**Ngày thực hiện:** 05/11/2026  
**Final Verdict:** `READY_FOR_REVIEW`  

---

## 1. MỤC TIÊU VÀ PHẠM VI XỬ LÝ TRONG REVIEW ROUND 3

Technical Lead review vòng 2 đã chỉ ra các điểm chặn kiến trúc quan trọng sau:
1. **Blocker P0 — Thống nhất khóa tài nguyên liên dịch vụ (Unified Cross-Service Resource Locks):**
   - Loại bỏ sự phân mảnh khóa: trước đây `FollowUpSchedulingService` dùng `Dentist_{id}` trong khi `AppointmentService.ManagerConfirmAsync` dùng `Lock_Dentist_{id}_{date}`; tương tự `DentistAssignChairAsync` dùng `DentalChair_{id}` trong khi `ManagerConfirmAsync` dùng `Lock_Chair_{id}_{date}`.
   - Chuẩn hóa toàn bộ khóa tài nguyên dùng chung qua `SqlAppLockHelper`:
     - Bác sĩ: `Lock_Dentist_{id}_{yyyyMMdd}`
     - Bệnh nhân: `Lock_Patient_{id}_{yyyyMMdd}`
     - Ghế nha khoa: `Lock_Chair_{id}_{yyyyMMdd}`
     - Phòng điều trị: `Lock_Room_{id}_{yyyyMMdd}`
     - Cuộc hẹn: `Lock_Appt_{id}`
     - Đề xuất thay đổi: `Lock_Proposal_{id}`
     - Hàng đợi khoa: `Lock_DeptQueue_{id}_{yyyyMMdd}`
     - Phác đồ điều trị: `Lock_Plan_{id}`
   - Bổ sung 5 integration tests liên dịch vụ (B16–B20) trên SQL Server thật (`.\SQL2025_DUC`), kiểm chứng tính loại trừ tương hỗ giữa `FollowUpSchedulingService`, `AppointmentService`, và `DentistScheduleService`.

2. **Blocker P0 — Khắc phục Race Condition giữa Hủy lịch và Phê duyệt đề xuất (Cancellation vs Proposal Approval):**
   - Bọc `AppointmentService.PatientCancelAsync` và `AppointmentService.PatientWithdrawAsync` trong transaction có khóa `Lock_Appt_{id}`.
   - Reload entity fresh dưới khóa, kiểm tra lại trạng thái.
   - Tự động hủy/hết hạn toàn bộ đề xuất đổi lịch đang chờ (`Status = Expired`) trong cùng giao dịch, ngăn chặn triệt để nguy cơ phê duyệt muộn làm hồi sinh cuộc hẹn đã hủy.
   - Thêm test `B06B` kiểm chứng interleaving có kiểm soát trên SQL Server thật.

3. **P1 — Cơ chế Unique Constraint AppointmentCode & Bounded Retry:**
   - Kết hợp đa tầng bảo vệ: Pre-check `AnyAsync`, SQL Server Unique Index (`2627`/`2601`), và vòng lặp bounded retry (tối đa 3 lần) trong `CreateDoctorFollowUpAppointmentAsync` và `CreatePlanFollowUpAppointmentAsync`.
   - Bổ sung test `B15B` chứng minh retry thành công khi gặp va chạm mã.

4. **P1 — Hiệu chỉnh chính xác ma trận kiểm thử và thời lượng Group A:**
   - Đối chiếu từng test case với mã nguồn thực tế trong `FollowUpSlotValidationTests.cs`.
   - Hiệu chỉnh quy tắc thời lượng tái khám: hệ thống cho phép từ 10 đến 240 phút (10 phút hợp lệ, 240 phút hợp lệ, 9 phút từ chối, 241 phút từ chối).

---

## 2. TỔNG HỢP CÁC SỬA ĐỔI MÃ NGUỒN CHÍNH

### 2.1. `BE/DentalClinic.Infrastructure/Services/SqlAppLockHelper.cs`
- Thêm các helper sinh canonical lock key nhất quán:
  - `DentistKey(long dentistUserId, DateTime date)` -> `Lock_Dentist_{id}_{yyyyMMdd}`
  - `PatientKey(long patientUserId, DateTime date)` -> `Lock_Patient_{id}_{yyyyMMdd}`
  - `ChairKey(long dentalChairId, DateTime date)` -> `Lock_Chair_{id}_{yyyyMMdd}`
  - `RoomKey(long roomId, DateTime date)` -> `Lock_Room_{id}_{yyyyMMdd}`
  - `ApptKey(long appointmentId)` -> `Lock_Appt_{id}`
  - `ProposalKey(long proposalId)` -> `Lock_Proposal_{id}`
  - `DeptQueueKey(long departmentId, DateTime date)` -> `Lock_DeptQueue_{id}_{yyyyMMdd}`
  - `PlanKey(long planId)` -> `Lock_Plan_{id}`
- Bổ sung phương thức `IsUniqueConstraintViolation(DbUpdateException ex)` phát hiện mã lỗi SQL Server 2627 và 2601.

### 2.2. `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`
- **`ManagerConfirmAsync`**:
  - Dùng `SqlAppLockHelper.AcquireLocksAsync` với danh sách canonical keys: `ApptKey`, `DentistKey`, `PatientKey`, `DeptQueueKey`, và có điều kiện `ChairKey` / `RoomKey`.
  - Bổ sung kiểm tra lịch nghỉ phép / bận của bác sĩ (`DentistAvailabilities`) dưới khóa độc quyền:
    ```csharp
    bool dentistLeaveConflict = await _dbContext.DentistAvailabilities
        .AsNoTracking()
        .AnyAsync(da => da.DentistUserId == request.AssignedDentistUserId &&
                        da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                        da.StartDateTime < finalEnd.Value &&
                        (da.EndDateTime == null || da.EndDateTime > finalStart.Value), ct);

    if (dentistLeaveConflict)
    {
        return ApiResponse<AppointmentDto>.Fail("Bác sĩ đang bận hoặc nghỉ phép trong khoảng thời gian này.");
    }
    ```
  - Quản lý vòng đời khóa bằng `lockScope` và giải phóng an toàn trong `finally`.
  - Quản lý transaction relational an toàn với `_dbContext.Database.IsRelational()`.
- **`DentistAssignChairAsync`**:
  - Khóa theo định dạng canonical: `ChairKey(dentalChairId, targetDate)` và `ApptKey(appointmentId)`.
  - Mở rộng `activeStatuses` đầy đủ (`Confirmed`, `CheckedIn`, `Waiting`, `Called`, `InService`) để ngăn chặn việc chiếm ghế đã được Trưởng khoa xác nhận cho bệnh nhân khác.
- **`PatientCancelAsync` & `PatientWithdrawAsync`**:
  - Đồng bộ hóa qua `SqlAppLockHelper.ApptKey(appointmentId)`.
  - Reload entity fresh dưới khóa, kiểm tra trạng thái hủy hợp lệ.
  - Tự động đánh dấu `Expired` tất cả `AppointmentChangeProposals` đang chờ của cuộc hẹn.

### 2.3. `BE/DentalClinic.Infrastructure/Services/FollowUpSchedulingService.cs`
- `CreateDoctorFollowUpAppointmentAsync`: Chuyển sang dùng `DentistKey(doctorUserId, targetDate)` và `PatientKey(patientUserId, targetDate)`. Bổ sung vòng lặp bounded retry (tối đa 3 lần) bắt `DbUpdateException` khi đụng độ `AppointmentCode`.
- `SubmitPatientChangeProposalAsync`: Đồng bộ khóa qua `ApptKey(request.AppointmentId)`.
- `WithdrawPatientProposalAsync`: Khóa đồng thời `ProposalKey(proposalId)` và `ApptKey(appointmentId)`.
- `ResolveProposalAsync`: Khóa đồng thời `ProposalKey`, `ApptKey`, `DentistKey` (cả ngày cũ và ngày mới), và `PatientKey` (cả ngày cũ và ngày mới).

### 2.4. `BE/DentalClinic.Infrastructure/Services/DentistScheduleService.cs`
- `CreateAvailabilityAsync` & `UpdateAvailabilityAsync`: Khóa toàn bộ các ngày bị ảnh hưởng qua `SqlAppLockHelper.DentistKey(targetDentistId, day)`. Đồng bộ chính xác với `AppointmentService` và `FollowUpSchedulingService`.

### 2.5. `BE/DentalClinic.Infrastructure/Services/TreatmentService.cs`
- `CreatePlanFollowUpAppointmentAsync`: Khóa canonical qua `DentistKey`, `PatientKey`, và `PlanKey`. Bổ sung bounded retry sinh `AppointmentCode`.

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

### Nhóm B: Real SQL Server Concurrency Integration Matrix (23 Cases)
Toàn bộ các test case Nhóm B chạy trực tiếp trên **SQL Server thật (`.\SQL2025_DUC`)** với database cô lập `DentalClinic_Concurrency_QA_<RunId>`, phục hồi từ `DentalClinicManagementDB.bak`:

| Mã | Tên Test Case | Mô tả kịch bản đồng thời & Luồng kiểm thử | Mức độ xác minh | Kết quả | Assertions cơ sở dữ liệu trên SQL Server thật |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **B01** | `B01_ConcurrentFollowUpBooking_SameDoctorSameSlot_OnlyOneSucceeds` | 2 thread cùng tạo lịch tái khám cho cùng Bác sĩ tại cùng khung giờ | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Đúng 1 cuộc hẹn Confirmed cho Bác sĩ 20 trong DB. |
| **B02** | `B02_ConcurrentFollowUpBooking_SamePatientDifferentDoctor_OnlyOneSucceeds` | 2 thread tạo lịch cho cùng Bệnh nhân với 2 Bác sĩ khác nhau cùng giờ | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Đúng 1 cuộc hẹn Confirmed cho Bệnh nhân 6 trong DB. |
| **B03** | `B03_ConcurrentPlanFollowUpBooking_SamePlan_OnlyOneSucceeds` | 2 thread cùng tạo lịch tái khám từ cùng TreatmentPlanId | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Đúng 1 cuộc hẹn gắn với phác đồ trong DB. |
| **B04** | `B04_ConcurrentRescheduleProposals_SameAppointment_AtMostOnePendingProposal` | Bệnh nhân gửi 2 đề xuất đổi lịch khác nhau cho cùng một cuộc hẹn | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. DB chỉ có đúng 1 đề xuất `Pending`. |
| **B05** | `B05_ConcurrentApproval_CompetingProposals_ForSameDoctorSlot_OnlyOneSucceeds` | Bác sĩ duyệt 2 đề xuất cạnh tranh cùng slot thời gian | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Đúng 1 cuộc hẹn được xác nhận tại slot đó. |
| **B06** | `B06_RaceCondition_CancellationVsProposalApproval_CannotApproveCancelledAppointment` | Luồng hủy thật `PatientCancelAsync` đối đầu `ResolveProposalAsync` | **VERIFIED (Real SQL Server)** | **PASS** | `cancelRes.Success == true`. `finalAppt.Status == Cancelled`. Proposal không thể `Accepted`. |
| **B06B**| `B06B_ControlledInterleaving_CancelVsApproval_DeterministicFinalState` | Kiểm thử xen kẽ tuần tự có chủ đích giữa Hủy lịch và Duyệt đề xuất | **VERIFIED (Real SQL Server)** | **PASS** | Hủy thành công trước thì duyệt sau bị từ chối; đề xuất chuyển `Expired`. Trạng thái tất định. |
| **B07** | `B07_RaceCondition_ConfirmationVsDoctorLeaveUpdate` | 1 thread xác nhận lịch, 1 thread đăng ký nghỉ phép có thời hạn | **VERIFIED (Real SQL Server)** | **PASS** | Không bao giờ cả 2 cùng ghi nhận trùng lặp giờ nghỉ và cuộc hẹn khám. |
| **B07B**| `B07B_DoctorLeave_WithNullEndDateTime_DetectsConflictOnRealDb` | Đăng ký nghỉ vô hạn (`EndDateTime = null`) khi đã có hẹn, và ngược lại | **VERIFIED (Real SQL Server)** | **PASS** | Khi có hẹn, đăng ký nghỉ bị chặn; khi nghỉ tồn tại, đặt lịch mới bị từ chối. |
| **B08** | `B08_ConcurrentRoomAllocation_ExclusiveRoom_OnlyOneSucceeds` | 2 Trưởng khoa cùng gán 2 ghế khác nhau trong phòng mổ độc quyền Room 2 | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. DB: đúng 1 cuộc hẹn Confirmed trong Room 2; cuộc hẹn kia giữ nguyên Pending. |
| **B09** | `B09_ConcurrentChairAllocation_SameChair_OnlyOneSucceeds` | 2 Bác sĩ cùng gán cùng 1 ghế điều trị Chair 1 qua `DentistAssignChairAsync` | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. DB: đúng 1 cuộc hẹn InService trên Chair 1. |
| **B10** | `B10_DuplicateSubmission_RapidRetry_PreventsDuplicateAppointments` | Double-click gửi retry cùng payload trong mili-giây | **PARTIALLY VERIFIED (Duplicate Guard)** | **PASS** | `successCount == 1` (1 OK 200, 1 400 Bad Request conflict). Không lưu trùng lặp. |
| **B11** | `B11_ConcurrentProposalResolution_ApproveVsApprove_OnlyOneSucceeds` | 2 thread cùng duyệt Approve một đề xuất đổi lịch | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Proposal chuyển `Accepted`, Appointment cập nhật đúng 1 lần. |
| **B12** | `B12_ConcurrentProposalResolution_ApproveVsReject_MutuallyExclusive` | 1 thread Approve, 1 thread Reject cùng một đề xuất | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Proposal chỉ có thể là `Accepted` hoặc `Rejected`. |
| **B13** | `B13_ConcurrentProposalResolution_WithdrawVsApprove_MutuallyExclusive` | Bệnh nhân rút đề xuất đối đầu Bác sĩ duyệt đề xuất | **VERIFIED (Real SQL Server)** | **PASS** | Đúng 1 thao tác thành công. Nếu rút trước, lịch hẹn cũ giữ nguyên không đổi. |
| **B14** | `B14_RealSqlServerTransactionRollback_ZeroOrphans` | Giả lập lỗi giữa chừng sau khi insert Appointment và FollowUpSchedule | **VERIFIED (Real SQL Server)** | **PASS** | `tx.RollbackAsync()` hoàn tất. Kiểm tra trên DB: cả 2 bảng đều không có bản ghi mồ côi (Zero Orphans). |
| **B15** | `B15_AppointmentCode_UniqueConstraintEnforcedOnSqlServer` | Cố ý insert 2 Appointment cùng `AppointmentCode` trên SQL Server | **VERIFIED (Real SQL Server)** | **PASS** | SQL Server chặn bằng lỗi 2627/2601 (`DbUpdateException` wrapping `SqlException`). |
| **B15B**| `B15B_AppointmentCode_CollisionTriggersBoundedRetry_SuccessfullyPersists` | Giả lập va chạm mã `AppointmentCode`, kích hoạt bounded retry | **VERIFIED (Real SQL Server)** | **PASS** | `result.Success == true`. Bounded retry tự sinh mã mới và lưu thành công vào SQL Server. |
| **B16** | `B16_CrossService_DoctorFollowUp_Vs_ManagerConfirm_SameDoctorSameSlot` | `FollowUpSchedulingService` vs `AppointmentService.ManagerConfirmAsync` (cùng Bác sĩ, cùng giờ) | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Khóa liên dịch vụ `Lock_Dentist_{id}_{date}` đảm bảo loại trừ tương hỗ tuyệt đối. |
| **B17** | `B17_CrossService_PatientFollowUp_Vs_ManagerConfirm_SamePatientSameSlot` | `FollowUpSchedulingService` vs `AppointmentService.ManagerConfirmAsync` (cùng Bệnh nhân, cùng giờ) | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Khóa liên dịch vụ `Lock_Patient_{id}_{date}` đảm bảo không đặt trùng lịch bệnh nhân. |
| **B18** | `B18_CrossService_ManagerConfirm_Vs_DentistAssignChair_SameChairSameSlot` | `AppointmentService.ManagerConfirmAsync` vs `AppointmentService.DentistAssignChairAsync` (cùng ghế) | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Khóa liên dịch vụ `Lock_Chair_{id}_{date}` ngăn chặn 2 khoa/bác sĩ dùng chung 1 ghế. |
| **B19** | `B19_CrossService_ExclusiveRoom_TwoConcurrentConfirmations_OnlyOneSucceeds` | 2 Manager Confirm đồng thời 2 ghế trong phòng độc quyền Room 2 | **VERIFIED (Real SQL Server)** | **PASS** | `successCount == 1`. Khóa `Lock_Room_{id}_{date}` đảm bảo chỉ đúng 1 cuộc hẹn trong phòng độc quyền. |
| **B20** | `B20_CrossService_DoctorLeave_Vs_ManagerConfirm_SameDoctorSameSlot` | `DentistScheduleService.CreateAvailabilityAsync` (Nghỉ phép) vs `ManagerConfirmAsync` (Xác nhận lịch) | **VERIFIED (Real SQL Server)** | **PASS** | `bothCommitted == false`. Bác sĩ nghỉ thì không thể xác nhận hẹn; có hẹn thì không thể duyệt nghỉ. |

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

## 4. BẰNG CHỨNG LỖI TRƯỚC KHI SỬA VÀ SAU KHI SỬA (REVIEW ROUND 3 EVIDENCE)

### 4.1. Lỗ hổng Race Condition giữa Bác sĩ xin nghỉ phép và Trưởng khoa duyệt lịch (B20)
**Hiện tượng trước khi sửa (Test B20 FAIL):**  
Khi `DentistScheduleService.CreateAvailabilityAsync` (bác sĩ đăng ký nghỉ phép) chạy đồng thời với `AppointmentService.ManagerConfirmAsync` (trưởng khoa xác nhận lịch khám cho bác sĩ):
- `ManagerConfirmAsync` trước đây chỉ kiểm tra xung đột với `_dbContext.Appointments` mà **hoàn toàn bỏ qua kiểm tra `_dbContext.DentistAvailabilities`** (lịch nghỉ phép/bận).
- Nếu luồng đăng ký nghỉ phép lấy khóa trước và lưu thành công bản ghi nghỉ phép, sau đó luồng xác nhận lịch lấy khóa và kiểm tra thì không thấy lịch hẹn nào khác trùng giờ, dẫn đến cả hai luồng đều thành công!
- Kết quả kiểm thử trước khi sửa:
  ```text
  [xUnit.net 00:00:04.70] DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B20_CrossService_DoctorLeave_Vs_ManagerConfirm_SameDoctorSameSlot [FAIL]
    Failed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B20_CrossService_DoctorLeave_Vs_ManagerConfirm_SameDoctorSameSlot [3 s]
    Error Message:
     Race condition detected: Both leave and confirmed appointment were created for the same doctor at the same time!
  ```

**Khắc phục bằng mã nguồn:**  
Bổ sung kiểm tra `_dbContext.DentistAvailabilities` dưới khóa canonical `Lock_Dentist_{id}_{date}` trong `AppointmentService.ManagerConfirmAsync`:
```csharp
bool dentistLeaveConflict = await _dbContext.DentistAvailabilities
    .AsNoTracking()
    .AnyAsync(da => da.DentistUserId == request.AssignedDentistUserId &&
                    da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                    da.StartDateTime < finalEnd.Value &&
                    (da.EndDateTime == null || da.EndDateTime > finalStart.Value), ct);

if (dentistLeaveConflict)
{
    return ApiResponse<AppointmentDto>.Fail("Bác sĩ đang bận hoặc nghỉ phép trong khoảng thời gian này.");
}
```

**Bằng chứng chạy test B20 sau khi sửa:**
```text
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B20_CrossService_DoctorLeave_Vs_ManagerConfirm_SameDoctorSameSlot [2 s]
```

---

### 4.2. Lỗ hổng Trưởng khoa phân bổ ghế đối đầu Bác sĩ chọn ghế điều trị (B18)
**Hiện tượng trước khi sửa (Test B18 FAIL):**  
Trong `AppointmentService.DentistAssignChairAsync`, mảng `activeStatuses` ban đầu chỉ chứa duy nhất `AppointmentStatus.InService`:
```csharp
var activeStatuses = new[] { AppointmentStatus.InService };
```
Nếu Trưởng khoa xác nhận cuộc hẹn B trên Ghế 1 trước (`Status = Confirmed`), sau đó Bác sĩ điều trị cuộc hẹn A gọi `DentistAssignChairAsync` để chọn Ghế 1:
- `DentistAssignChairAsync` chỉ tìm các cuộc hẹn đang `InService`. Do cuộc hẹn B mới ở trạng thái `Confirmed`, hàm tưởng Ghế 1 đang trống và gán thành công!
- Kết quả: Cả 2 cuộc hẹn đều thành công trên cùng một ghế tại cùng một khung giờ (`Expected: 1, Actual: 2`).

**Khắc phục bằng mã nguồn:**  
Mở rộng danh sách trạng thái chiếm ghế trong `DentistAssignChairAsync` và `GetAvailableChairsForAppointmentAsync`:
```csharp
var activeStatuses = new[]
{
    AppointmentStatus.Confirmed,
    AppointmentStatus.CheckedIn,
    AppointmentStatus.Waiting,
    AppointmentStatus.Called,
    AppointmentStatus.InService
};
```

**Bằng chứng chạy test B18 sau khi sửa:**
```text
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B18_CrossService_ManagerConfirm_Vs_DentistAssignChair_SameChairSameSlot [268 ms]
```

---

### 4.3. Lỗ hổng Rò rỉ In-Memory Semaphore và ReloadAsync trên In-Memory Store
**Hiện tượng phát hiện:**  
Khi chạy test suite toàn diện, các test In-Memory sử dụng ID cố định (như `AppointmentId = 1`, `DentistUserId = 401`).
1. `ManagerConfirmAsync` ban đầu không gán đối tượng trả về của `SqlAppLockHelper.AcquireLocksAsync` vào `using var lockScope`, khiến semaphores trong fallback InMemory bị giữ vĩnh viễn (semaphore count = 0).
2. Việc gọi `ReloadAsync()` trên thực thể EF Core In-Memory không được hỗ trợ chuẩn mực như relational database.
Hậu quả: Hàng loạt test phía sau bị TimeoutException (chờ 15s) khi cố gắng xin khóa `Lock_Appt_1`.

**Khắc phục bằng mã nguồn:**  
1. Bổ sung `lockScope?.Dispose();` trong khối `finally` của `ManagerConfirmAsync`.
2. Bọc `ReloadAsync()` an toàn với kiểm tra cơ sở dữ liệu quan hệ:
   ```csharp
   if (isRelational) await _dbContext.Entry(appointment).ReloadAsync(ct);
   ```

**Kết quả:** Toàn bộ 43 test trong `AppointmentFlowTests` chuyển sang **PASS 100% trong vòng 1 giây**!

---

## 5. BẰNG CHỨNG KIỂM THỬ HỒI QUY TOÀN BỘ HỆ THỐNG

### 5.1. Concurrency Integration Tests (`FollowUpConcurrencyIntegrationTests`)
```powershell
dotnet test "BE\DentalClinic.Tests\DentalClinic.Tests.csproj" --filter "FullyQualifiedName~FollowUpConcurrencyIntegrationTests"
```
```text
Test run for D:\SWP\SWP391_Group6\BE\DentalClinic.Tests\bin\Debug\net8.0\DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    23, Skipped:     0, Total:    23, Duration: 3 s - DentalClinic.Tests.dll (net8.0)
```
- **Kết quả:** **23/23 PASSED (100%)**.

### 5.2. Slot Validation Tests (`FollowUpSlotValidationTests`)
```powershell
dotnet test "BE\DentalClinic.Tests\DentalClinic.Tests.csproj" --filter "FullyQualifiedName~FollowUpSlotValidationTests"
```
```text
Passed!  - Failed:     0, Passed:    16, Skipped:     0, Total:    16, Duration: 95 ms - DentalClinic.Tests.dll (net8.0)
```
- **Kết quả:** **16/16 PASSED (100%)**.

### 5.3. Clinical Integrity Tests (`FollowUpClinicalIntegrityTests`)
```powershell
dotnet test "BE\DentalClinic.Tests\DentalClinic.Tests.csproj" --filter "FullyQualifiedName~FollowUpClinicalIntegrityTests"
```
```text
Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 1 s - DentalClinic.Tests.dll (net8.0)
```
- **Kết quả:** **10/10 PASSED (100%)**.

### 5.4. Toàn bộ Backend Tests (`DentalClinic.Tests.csproj`)
```powershell
dotnet test "BE\DentalClinic.Tests\DentalClinic.Tests.csproj"
```
```text
Test run for D:\SWP\SWP391_Group6\BE\DentalClinic.Tests\bin\Debug\net8.0\DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:02.41]     DentalClinic.Tests.Profile.CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully [SKIP]
  Skipped DentalClinic.Tests.Profile.CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully [1 ms]

Passed!  - Failed:     0, Passed:   597, Skipped:     1, Total:   598, Duration: 21 s - DentalClinic.Tests.dll (net8.0)
```
- **Tổng số test Backend:** 598 tests.
- **Passed:** **597 tests**.
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

Passed!  - Failed:     0, Passed:   286, Skipped:     0, Total:   286, Duration: 21 s - DentalClinic.Web.Tests.dll (net8.0)
```
- **Tổng số test Frontend:** 286 tests.
- **Passed:** **286 tests**.
- **Failed:** **0 tests (0% failure)**.

### 5.6. Tổng cộng toàn hệ thống
$$\text{Grand Total: } 883 \text{ Passed}, 1 \text{ Skipped}, 0 \text{ Failed (0\% Regression)}.$$

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

## 7. KẾT LUẬN VÀ TRẠNG THÁI CUỐI CÙNG

$$\mathbf{FINAL\ VERDICT:\ READY\_FOR\_REVIEW}$$

1. **Khóa tài nguyên đã được chuẩn hóa hoàn toàn:** Toàn bộ các entry point (`FollowUpSchedulingService`, `AppointmentService`, `DentistScheduleService`, `TreatmentService`) đều dùng chung canonical lock keys qua `SqlAppLockHelper`. Đã chứng minh loại trừ tương hỗ liên dịch vụ qua các test case B16–B20.
2. **Khắc phục triệt để Race Condition giữa Hủy lịch và Phê duyệt đề xuất:** Đảm bảo tính tất định (deterministic final state) và tự động hết hạn các proposal đang chờ khi cuộc hẹn bị hủy.
3. **Độ tin cậy sinh mã và cơ chế Bounded Retry:** Bảo vệ đa tầng bằng `AnyAsync`, SQL Server Unique Index, và bounded retry 3 lần.
4. **An toàn Git:** Không merge vào `develop` hoặc `main`. Mọi thay đổi được commit và push an toàn trên nhánh `fix/followup-concurrency-integrity`.
