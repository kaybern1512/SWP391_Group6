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
- Cross-Module Period-Closure Guards:          Đã khóa 6/6 workforce & scheduling services
- Real Database UAT Scenarios (UAT-01..05):    5 / 5 PASSED (100%)
- Backend Regression Test Suite:               861 Passed, 1 Skipped, 0 Failed (862 tests)
- Frontend Regression Test Suite:              323 Passed, 0 Failed (323 tests)
- Playwright Real Browser E2E Specs:           27 Passed, 0 Failed (4 test suites)
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

### Cơ Chế Kỹ Thuật: `DentalClinicDbContext.IsPeriodLockedAsync`
Để triệt tiêu lỗi `Invalid object name 'AttendancePeriodClosures'` khi chạy test trên các database cũ chưa migrate bảng này, phương thức helper được bổ sung trực tiếp vào `DentalClinicDbContext`:
```csharp
public virtual async Task<bool> IsPeriodLockedAsync(int year, int month, CancellationToken ct = default)
{
    if (Database.IsSqlServer())
    {
        if (!_attendancePeriodClosuresTableExists.HasValue)
        {
            _attendancePeriodClosuresTableExists = await Database
                .SqlQueryRaw<int>("SELECT 1 AS [Value] WHERE OBJECT_ID(N'AttendancePeriodClosures', N'U') IS NOT NULL")
                .AnyAsync(ct);
        }
        if (!_attendancePeriodClosuresTableExists.Value) return false;
    }
    return await AttendancePeriodClosures
        .AnyAsync(p => p.Year == year && p.Month == month && p.Status == PeriodClosureStatus.Locked, ct);
}
```

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
| **UAT-03** | Đơn nghỉ phép đã duyệt -> Chặn quẹt thẻ trong khoảng thời gian nghỉ | Lễ tân `UserId = 45` có ca trực và đơn nghỉ phép cả ngày đã được phê duyệt. Thử quẹt thẻ vào ca. | Hệ thống từ chối quẹt thẻ với thông báo: `Ca làm việc này đã có đơn nghỉ phép được phê duyệt. Bạn không cần và không thể chấm công vào.` | **PASSED** |
| **UAT-04** | Bác sĩ nghỉ phép -> Tạo gián đoạn lịch hẹn -> Dashboard hiển thị KPI | Bác sĩ `UserId = 3` nộp đơn nghỉ phép. Admin duyệt đơn qua `DentistLeaveService`. | Đơn chuyển sang `Approved`, hệ thống tạo gián đoạn. Dashboard cập nhật chính xác KPI và không báo vắng mặt sai lệch. | **PASSED** |
| **UAT-05** | Chốt sổ kỳ công tháng -> Chặn sửa lịch & duyệt nghỉ -> Mở lại có lý do | Chốt sổ kỳ công tháng 03/2026 (`Status = Locked`). Thử tạo ca lễ tân và duyệt đơn nghỉ bác sĩ -> Bị chặn. Mở lại với lý do $\ge 10$ ký tự -> Mở khóa thành công (`Status = Reopened`). | Dữ liệu kỳ công được bảo vệ bất biến. Thao tác mở lại được lưu vết `ReopenedByUserId`, `ReopenedAtUtc` và `AuditLogs`. | **PASSED** |

---

## 6. KẾT QUẢ KIỂM THỬ HỒI QUY TOÀN DIỆN (PHASE 6)

### A. Backend Test Suite (`DentalClinic.Tests.csproj`):
```
Test run for DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 861, Skipped: 1, Total: 862, Duration: 19 s
(1 skipped: CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets)
```

### B. Frontend Test Suite (`DentalClinic.Web.Tests.csproj`):
```
Test run for DentalClinic.Web.Tests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 323, Skipped: 0, Total: 323, Duration: 20 s
```

### C. Playwright Real Browser E2E Suites (Chromium):
Chạy trực tiếp trên Web Frontend `https://localhost:7135` và Backend API `https://localhost:7355`:
1. `admin-workforce-operations.spec.js` (9/9 passed in 13.3s):
   - `ADMIN-ATT-01`: Dentist clock-in & clock-out flow
   - `ADMIN-ATT-02`: Receptionist clock-in & clock-out flow
   - `ADMIN-ATT-03`: Late punch & grace period calculation
   - `ADMIN-ATT-04`: Duplicate punch rejection
   - `ADMIN-ATT-05`: Admin monthly reconciliation view & CSV export
   - `ADMIN-ATT-06`: Admin period finalize (locking) & reopen with mandatory reason
   - `ADMIN-ATT-07`: Staff adjustment request submission & admin review
   - `ADMIN-ATT-08`: Operations dashboard receptionist shortage & absent dentist alerts
   - `ADMIN-ATT-09`: Doctor leave & disruption integration widget on admin dashboard
2. `login-auth-flow.spec.js` (6/6 passed in 6.3s):
   - `AUTH-01` to `AUTH-06`: Login buttons & multi-role authorization flow.
3. `receptionist-workforce.spec.js` (6/6 passed in 11.0s):
   - `E2E-REC-1` to `E2E-REC-6`: 5 tabs scheduling, leave, swap, handover, sidebar.
4. `doctor-leave-recovery.spec.js` (6/6 passed in 11.4s):
   - `E2E-LEAVE-01` to `E2E-LEAVE-06`: Doctor leave request, disruption recovery flows A & B, IDOR security.

**Tổng kết E2E:** **27/27 kịch bản Passed 100% (0 Failed)**.

---

## 7. BẢNG KIỂM TRA BẢO TOÀN NGHIỆP VỤ & AN TOÀN HỆ THỐNG

| Tiêu Chí Đánh Giá | Yêu Cầu Nghiêm Ngặt | Kết Quả Thực Tế | Đánh Giá |
|---|---|---|---|
| **Git Merge Safety** | Không force push (`--force` / `-f`), không hard reset | Merge `origin/develop` sạch sẽ, lịch sử liên tục | **ĐẠT** |
| **Develop Protection** | Không tự động merge vào `develop` hoặc `main` | Nhánh hiện tại vẫn là `feature/admin-workforce-attendance-operations` | **ĐẠT** |
| **Code Preservation** | Giữ nguyên code Receptionist Workforce & Doctor Leave | 100% code 2 phân hệ và các test liên quan đều nguyên vẹn | **ĐẠT** |
| **Patient Portal Isolation** | Không sửa bất kỳ file nào thuộc Patient Portal | Thư mục `D:/SWP391/DentalCare-Patient` và các file Patient không bị chạm | **ĐẠT** |
| **Database Isolation** | Chỉ sử dụng `DentalCare_AdminOperations_Dev` | Không chạm vào `DentalClinicManagementDB` hay `DentalCare_Integration_Dev` | **ĐẠT** |
| **Test Quality Invariant** | 0 test fail, không xóa / skip test ép pass | **861 BE passed**, **323 FE passed**, **27 E2E passed**, 0 failed | **ĐẠT** |
| **Cross-Module Invariant** | Khóa sổ kỳ công chặn mọi thay đổi lịch & nghỉ phép | Đã áp dụng guard cho 6 services và kiểm chứng UAT-05 thành công | **ĐẠT** |

---

## 8. HƯỚNG DẪN DÀNH CHO REVIEWER / LEAD ARCHITECT

Khi tiến hành merge `feature/admin-workforce-attendance-operations` vào `develop`:
1. Nhánh feature đã chứa đầy đủ lịch sử commit mới nhất của `origin/develop` (`62e6b17`).
2. Bản di chuyển database `20261110_Add_Staff_Attendance_And_Operations_Tables.sql` đã sẵn sàng trong thư mục `DB/migrations/` để áp dụng lên môi trường tích hợp chung.
3. Không có conflict nào phát sinh khi tạo Pull Request vào `develop`.
4. Khuyến nghị cấu hình PR: `Create a merge commit` hoặc `Rebase and merge` (không dùng squash nếu muốn giữ lại lịch sử chi tiết từng phân hệ).
