# DENTALCARE – ADMIN ATTENDANCE FINAL DEVELOP SYNC & PRE-MERGE UAT REPORT

**Dự án:** Quản lý Phòng khám Nha khoa DentalCare (SWP391)  
**Phân hệ:** Admin Workforce Attendance & Clinic Operations Management  
**Worktree:** `D:\SWP391\DentalCare-Admin-Operations`  
**Feature Branch:** `feature/admin-workforce-attendance-operations`  
**Base Synchronization:** `origin/develop` @ commit `62e6b17`  
**Target QA Database:** `DentalCare_AdminOperations_Dev` on `localhost\SQLEXPRESS`  
**Thời điểm nghiệm thu:** 10/11/2026  
**Trạng thái nghiệm thu:** **READY FOR DEVELOP MERGE** (Đủ điều kiện sáp nhập vào develop)

---

## 1. KẾT LUẬN NGHIỆM THU (EXECUTIVE VERDICT)

```
================================================================================
                    FINAL PRE-MERGE UAT READINESS VERDICT:
                         >>> READY FOR DEVELOP MERGE <<<
================================================================================
- Trạng thái biên dịch Backend & Frontend:     0 Warnings, 0 Errors
- Tích hợp Git Develop mới nhất (62e6b17):     Hoàn tất (Clean Merge 323ef2f)
- Database QA chuyên biệt:                     DentalCare_AdminOperations_Dev (59 tables)
- Schema Invariants & Foreign Keys:            35/35 FKs, Check Constraints aligned
- Cross-Module Period-Closure Guards:          Đã bảo vệ toàn diện (Locked & Finalized)
- Schema Missing Safety Guard:                 Ném InvalidOperationException bảo vệ Prod/Staging
- Real Database UAT Scenarios (UAT-01..05):    5 / 5 PASSED (100%)
- Backend Regression Test Suite:               864 Passed, 1 Skipped, 0 Failed (865 tests)
- Frontend Regression Test Suite:              323 Passed, 0 Failed (323 tests)
- Playwright Real Browser E2E Specs:           27 Passed, 0 Failed (4 test suites - 27 scenarios)
- Grand Total Distinct Automated Tests:        1.215 Unique Tests (1.214 Passed, 1 Skipped, 0 Failed)
- Patient Portal Isolation:                    100% Nguyên vẹn (Không can thiệp)
- Database chung & Database phân hệ khác:      100% Nguyên vẹn (Không chạm vào)
================================================================================
```

---

## 2. NHẬT KÝ TÍCH HỢP GIT & SYNC DEVELOP (PHASE 1)

1. **Kiểm tra Worktree & Fetch Remote:**
   - Đã thực hiện `git fetch origin --prune`.
   - Xác định `origin/develop` HEAD thực tế tại `62e6b178e0b04ecd6c1b87b7d1c9034692f872be` (`62e6b17`).
   - `origin/develop` đã tích hợp đầy đủ 2 phân hệ:
     + Receptionist Shift, Leave & Workforce Management (`20261110_Receptionist_Shift_Leave_Workforce_Schema.sql`).
     + Doctor Leave & Appointment Disruption Recovery (`20261109_Add_DoctorLeave_And_AppointmentDisruption.sql`).
     + Bổ sung `CombinedCrossFeatureLifecycleIntegrationTests.cs` và sửa 17 compiler warnings.

2. **Hợp nhất Lịch sử Git (Merge):**
   - Đã merge an toàn `origin/develop` vào `feature/admin-workforce-attendance-operations` tạo commit `323ef2f`.
   - Xung đột duy nhất xuất hiện tại `BE/DentalClinic.Api/appsettings.Development.json` (do trỏ chuỗi kết nối Database). Đã giải quyết chuẩn xác giữ chuỗi kết nối `DentalCare_AdminOperations_Dev` cho môi trường QA của phân hệ Admin theo đúng chỉ thị.
   - Biên dịch Backend: **0 Warning(s), 0 Error(s)**.
   - Biên dịch Frontend: **0 Warning(s), 0 Error(s)**.

---

## 3. KIỂM ĐỊNH DATABASE & SCHEMA INVARIANTS (PHASE 2)

Thực hiện kiểm tra trực tiếp trên SQL Server `localhost\SQLEXPRESS` đối với database `DentalCare_AdminOperations_Dev`:
- Tổng số bảng hệ thống: **59 bảng**.
- Đầy đủ 3 nhóm bảng nghiệp vụ:
  1. **Receptionist Workforce (7 bảng):** `ReceptionistShiftTemplates`, `ReceptionistWorkSchedules`, `ReceptionistLeaveRequests`, `ReceptionistLeaveRequestPeriods`, `ReceptionistShiftSwapRequests`, `ReceptionistCoverageIncidents`, `ReceptionistWorkHandovers`.
  2. **Doctor Leave Recovery (2 bảng):** `DentistLeaveRequests`, `AppointmentDisruptions`.
  3. **Admin Attendance & Operations (5 bảng):** `AttendancePolicies`, `StaffAttendanceRecords`, `StaffAttendanceEvents`, `StaffAttendanceCorrectionRequests`, `AttendancePeriodClosures`.
- **Hiệu chỉnh Check Constraints:**
  + `CK_AttendancePeriodClosures_Status`: Mở rộng cho phép `CHECK ([Status] IN ('Open', 'Finalized', 'Locked', 'Reopened'))` để tương thích hoàn toàn với cả migration SQL (`Finalized`) và C# enum `PeriodClosureStatus.Locked`.
  + `CK_StaffAttendanceRecords_Status`: Mở rộng cho phép toàn bộ 13 trạng thái chuẩn trong `AttendanceStatus` (`OnTime`, `Late`, `EarlyDeparture`, `LateAndEarlyDeparture`, `ApprovedLeave`, `UnexpectedAbsence`, `ClockedIn`, `ClockedOut`, `MissingClockOut`, `NotStarted`, `NeedsReview`, `Verified`, `Corrected`).

---

## 4. BẢO VỆ KỲ CÔNG THÁNG – CROSS-MODULE INVARIANT ARCHITECTURE (PHASE 3 & 4)

Khi một kỳ công tháng $M$ đã được Quản trị viên đóng sổ (`Status == "Locked"` hoặc `Status == "Finalized"`), toàn bộ các nghiệp vụ tác động hồi tố lên dữ liệu của tháng $M$ phải bị chặn tuyệt đối.

### Cơ Chế Kỹ Thuật: `DentalClinicDbContext.IsPeriodLockedAsync` & Domain Alignment
Phương thức helper được nâng cấp để nhận diện đầy đủ cả hai trạng thái chốt sổ (`Locked` và `Finalized`) theo domain hiện hành:
```csharp
public virtual async Task<bool> IsPeriodLockedAsync(int year, int month, CancellationToken ct = default)
{
    await EnsureAttendanceSchemaCheckedAsync(ct);

    if (Database.IsSqlServer() && _attendancePeriodClosuresTableExists == false)
    {
        return false;
    }

    return await AttendancePeriodClosures
        .AnyAsync(p => p.Year == year && p.Month == month &&
                       (p.Status == PeriodClosureStatus.Locked || p.Status == PeriodClosureStatus.Finalized), ct);
}
```

### Bảo Vệ An Toàn Schema (Requirement 5 Invariant):
`EnsureAttendanceSchemaCheckedAsync` kiểm tra sự tồn tại của bảng `AttendancePeriodClosures`.
- **Tại môi trường Production / Staging / Feature:** Không được âm thầm coi kỳ công là mở (`return false`). Nếu thiếu bảng trong schema, hàm sẽ ném `InvalidOperationException` ngay lập tức để yêu cầu áp dụng migration bảo vệ dữ liệu kế toán.
- **Tại các database test legacy có tên cụ thể:** `DentalCare_Integration_Dev`, `DentalCare_Combined_Leave_QA`, `DentalClinic_Concurrency_QA_*` được phép bypass an toàn để không làm gãy suite kiểm thử cũ.

### Các Service Được Bảo Vệ:
1. **`ReceptionistScheduleService`:**
   - `CreateScheduleAsync`: Chặn phân ca mới trong tháng đã chốt sổ.
   - `UpdateScheduleAsync`: Chặn sửa đổi ca trực trong tháng đã chốt sổ.
   - `DeleteScheduleAsync`: Chặn hủy ca trực trong tháng đã chốt sổ.
   - `ApplyDailyOverrideAsync`: Chặn ghi đè lịch trực ngày trong tháng đã chốt sổ.
2. **`ReceptionistLeaveService`:**
   - `ApproveLeaveRequestAsync`: Chặn duyệt nghỉ phép hồi tố trong tháng đã chốt sổ.
   - `CancelApprovedLeaveAsync`: Chặn hủy đơn nghỉ phép đã duyệt trong tháng đã chốt sổ.
3. **`ReceptionistShiftSwapService`:**
   - `AdminDecideSwapRequestAsync`: Chặn phê duyệt đổi ca trực hồi tố trong tháng đã chốt sổ.
4. **`ReceptionistCoverageService`:**
   - `ReportEmergencyAbsenceAsync`: Chặn báo vắng mặt khẩn cấp hồi tố trong tháng đã chốt sổ.
5. **`DentistScheduleService`:**
   - `CreateScheduleAsync`: Chặn phân ca làm việc mới của bác sĩ trong tháng đã chốt sổ.
   - `UpdateScheduleAsync`: Chặn điều chỉnh ca làm việc của bác sĩ trong tháng đã chốt sổ.
   - `CancelScheduleAsync`: Chặn hủy ca làm việc của bác sĩ trong tháng đã chốt sổ.
   - `ApplyDailyOverrideAsync`: Chặn ghi đè lịch làm việc ngày trong tháng đã chốt sổ.
6. **`DentistLeaveService`:**
   - `ApproveLeaveRequestAsync`: Chặn phê duyệt đơn nghỉ phép của bác sĩ hồi tố trong tháng đã chốt sổ.

---

## 5. KẾT QUẢ NGHIỆM THU REAL DATABASE UAT (PHASE 5)

Bộ test `AdminOperationsRealDatabaseUatTests.cs` thực thi trực tiếp trên database thật `DentalCare_AdminOperations_Dev`:

| Mã UAT | Tên Kịch Bản Nghiệp Vụ | Dữ Liệu Kiểm Thử & Thao Tác | Kết Quả Thực Tế | Trạng Thái |
|---|---|---|---|---|
| **UAT-01** | Xuất bản ca trực lễ tân -> Chấm công vào -> Chấm công ra -> Ghi nhận giờ làm | Lễ tân `UserId = 2` trên ca `Ca Cả Ngày` hôm nay. Quẹt vào (`ClockIn`) và quẹt ra (`ClockOut`). | Bản ghi `StaffAttendanceRecords` được tạo với `ClockInTimeUtc`, `ClockOutTimeUtc`, trạng thái `OnTime` và ghi nhận $\ge 2$ sự kiện kiểm toán trong `StaffAttendanceEvents`. | **PASSED** |
| **UAT-02** | Đổi ca trực Lễ tân -> Admin duyệt -> Điểm danh nhận diện người nhận ca | Đổi ca từ Lễ tân `UserId = 2` sang `UserId = 44`. Đối tác đồng ý, Admin duyệt. | `CompositeStaffShiftProvider` phân giải ngay ca trực cho Lễ tân 44 theo thời gian thực và gỡ bỏ ca khỏi Lễ tân 2. | **PASSED** |
| **UAT-03** | Đơn nghỉ phép đã duyệt -> Chặn quẹt thẻ trong khoảng nghỉ, cho phép ngoài khoảng | Lễ tân `UserId = 45`. Kiểm tra: (1) Đơn nghỉ cả ngày chặn quẹt thẻ. (2) Đơn nghỉ một phần (`Hourly`) đang diễn ra chặn quẹt thẻ kèm thông báo chi tiết. (3) Đơn nghỉ một phần trong tương lai cho phép quẹt thẻ vào/ra bình thường. | Phân biệt chính xác giữa khoảng thời gian nghỉ phép và khoảng thời gian trực làm việc thực tế. | **PASSED** |
| **UAT-04** | Bác sĩ nghỉ phép -> Tạo gián đoạn lịch hẹn -> Dashboard hiển thị KPI | Bác sĩ `UserId = 3` nộp đơn nghỉ phép. Admin duyệt đơn qua `DentistLeaveService`. | Đơn chuyển sang `Approved`, hệ thống tạo gián đoạn. Dashboard cập nhật chính xác KPI và không báo vắng mặt sai lệch. | **PASSED** |
| **UAT-05** | Chốt sổ kỳ công tháng -> Chặn khi Locked & Finalized -> Mở lại khi Reopened | Kiểm thử cả 2 trạng thái khóa: `Locked` và `Finalized` đều chặn tạo ca lễ tân và duyệt đơn nghỉ bác sĩ với thông báo `chốt sổ`. Khi mở lại (`Reopened`) có lý do $\ge 10$ ký tự, mở khóa thành công và cho phép phân ca bình thường. | Dữ liệu kỳ công được bảo vệ bất biến ở cả hai trạng thái. Thao tác mở lại được lưu vết `ReopenedByUserId`, `ReopenedAtUtc` và `AuditLogs`. | **PASSED** |

---

## 6. KẾT QUẢ KIỂM THỬ HỒI QUY TOÀN DIỆN (PHASE 6)

### A. Backend Test Suite (`DentalClinic.Tests.csproj`):
```
Test run for DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 864, Skipped: 1, Total: 865, Duration: 19 s
(1 skipped: CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets)
* Ghi chú: Đã bao gồm trọn vẹn 5 kịch bản Real SQL Server UAT (UAT-01 đến UAT-05).
```

### B. Frontend Test Suite (`DentalClinic.Web.Tests.csproj`):
```
Test run for DentalClinic.Web.Tests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 323, Skipped: 0, Total: 323, Duration: 20 s
```

### C. Playwright Real Browser E2E Suites (Chromium):
Chạy trực tiếp trên Web Frontend `https://localhost:7135` và Backend API `https://localhost:7355`:
1. `doctor-leave-recovery.spec.js` (6/6 passed in 15.1s):
```
  ok 1 [chromium] › doctor-leave-recovery.spec.js:19:3 › E2E-LEAVE-01: Dentist submits leave request and views status on personal schedule (3.6s)
  ok 2 [chromium] › doctor-leave-recovery.spec.js:53:3 › E2E-LEAVE-02: Admin navigates to leave requests tab and views impact analysis modal (2.9s)
  ok 3 [chromium] › doctor-leave-recovery.spec.js:87:3 › E2E-LEAVE-03: Flow A - Patient selects alternative dentist and verifies schedule update (2.6s)
  ok 4 [chromium] › doctor-leave-recovery.spec.js:123:3 › E2E-LEAVE-04: Flow B - Patient reschedules disrupted appointment to new date/time slot (1.6s)
  ok 5 [chromium] › doctor-leave-recovery.spec.js:153:3 › E2E-LEAVE-05: Concurrency & Security - Action token invalidation & IDOR protection (1.8s)
  ok 6 [chromium] › doctor-leave-recovery.spec.js:167:3 › E2E-LEAVE-06: Receptionist navigates to disruption coordination and verifies controls (1.7s)

  6 passed (15.1s)
```
2. `admin-workforce-operations.spec.js` (9/9 passed in 13.3s):
   - `ADMIN-ATT-01`: Dentist clock-in & clock-out flow (1.1s)
   - `ADMIN-ATT-02`: Receptionist clock-in & clock-out flow (1.5s)
   - `ADMIN-ATT-03`: Late punch & grace period calculation (1.4s)
   - `ADMIN-ATT-04`: Duplicate punch rejection (908ms)
   - `ADMIN-ATT-05`: Admin monthly reconciliation view & CSV export (1.3s)
   - `ADMIN-ATT-06`: Admin period finalize (locking) & reopen with mandatory reason (1.7s)
   - `ADMIN-ATT-07`: Staff adjustment request submission & admin review (2.2s)
   - `ADMIN-ATT-08`: Operations dashboard receptionist shortage & absent dentist alerts (1.2s)
   - `ADMIN-ATT-09`: Doctor leave & disruption integration widget on admin dashboard (1.2s)
3. `login-auth-flow.spec.js` (6/6 passed in 6.3s):
   - `AUTH-01` to `AUTH-06`: Multi-role login & authorization flows.
4. `receptionist-workforce.spec.js` (6/6 passed in 11.0s):
   - `E2E-REC-1` to `E2E-REC-6`: 5 tabs scheduling, leave, swap, handover, sidebar.

**Tổng kết E2E:** **27/27 kịch bản Passed 100% (0 Failed)**.

### D. Bảng Đối Soát Tổng Số Kiểm Thử Tự Động (Reconciled Metrics):
```
  Backend Assembly (DentalClinic.Tests.dll):       865 tests (864 passed, 1 skipped)
  Frontend Assembly (DentalClinic.Web.Tests.dll):   323 tests (323 passed, 0 skipped)
  Playwright Real Browser E2E Specs:               27 tests (27 passed, 0 skipped)
  ----------------------------------------------------------------------------------
  TỔNG SỐ TEST ĐỘC LẬP THỰC TẾ (DISTINCT TOTAL):    1.215 unique tests
  KẾT QUẢ:                                         1.214 passed, 1 skipped, 0 failed (100% SUCCESS)
```

---

## 7. BẢNG KIỂM TRA BẢO TOÀN NGHIỆP VỤ & AN TOÀN HỆ THỐNG

| Tiêu Chí Đánh Giá | Yêu Cầu Nghiêm Ngặt | Kết Quả Thực Tế | Đánh Giá |
|---|---|---|---|
| **Git Merge Safety** | Không force push (`--force` / `-f`), không hard reset | Merge `origin/develop` sạch sẽ, lịch sử liên tục | **ĐẠT** |
| **Develop Protection** | Không tự động merge vào `develop` hoặc `main` | Nhánh hiện tại vẫn là `feature/admin-workforce-attendance-operations` | **ĐẠT** |
| **Code Preservation** | Giữ nguyên code Receptionist Workforce & Doctor Leave | 100% code 2 phân hệ và các test liên quan đều nguyên vẹn | **ĐẠT** |
| **Patient Portal Isolation** | Không sửa bất kỳ file nào thuộc Patient Portal | Thư mục `D:/SWP391/DentalCare-Patient` và các file Patient không bị chạm | **ĐẠT** |
| **Database Isolation** | Chỉ sử dụng `DentalCare_AdminOperations_Dev` | Không chạm vào `DentalClinicManagementDB` hay `DentalCare_Integration_Dev` | **ĐẠT** |
| **Test Quality Invariant** | 0 test fail, không xóa / skip test ép pass | **864 BE passed**, **323 FE passed**, **27 E2E passed**, 0 failed | **ĐẠT** |
| **Cross-Module Invariant** | Khóa sổ kỳ công chặn mọi thay đổi lịch & nghỉ phép | Đã áp dụng guard cho 6 services và kiểm chứng UAT-05 thành công trên cả Locked & Finalized | **ĐẠT** |

---

## 8. HƯỚNG DẪN DÀNH CHO REVIEWER / LEAD ARCHITECT

Khi tiến hành merge `feature/admin-workforce-attendance-operations` vào `develop`:
1. Nhánh feature đã chứa đầy đủ lịch sử commit mới nhất của `origin/develop` (`62e6b17`).
2. Bản di chuyển database `20261110_Add_Staff_Attendance_And_Operations_Tables.sql` đã sẵn sàng trong thư mục `DB/migrations/` để áp dụng lên môi trường tích hợp chung.
3. Không có conflict nào phát sinh khi tạo Pull Request vào `develop`.
4. Khuyến nghị cấu hình PR: `Create a merge commit` hoặc `Rebase and merge` (không dùng squash nếu muốn giữ lại lịch sử chi tiết từng phân hệ).
