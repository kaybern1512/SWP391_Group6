# DENTALCARE – ADMIN WORKFORCE ATTENDANCE TEST MATRIX

**Bảng Ma Trận Kiểm Thử Chấm Công Nhân Sự & Giám Sát Vận Hành**

---

## 1. MA TRẬN TEST CASE ĐẶC TẢ CHI TIẾT

| Mã Test | Tên Kịch Bản Nghiệp Vụ | Tầng Kiểm Thử | Tên Phương Thức / File Test | Kỳ Vọng (Expected Result) | Trạng Thái |
|---|---|---|---|---|---|
| **ADMIN-ATT-01** | Bác sĩ nha khoa điểm danh vào ca & kết thúc ca làm việc | Unit / Integration / E2E | `ClockInAsync_DentistShift_RecordsValidClockIn` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-01` in `e2e/admin-workforce-operations.spec.js` | Tạo bản ghi `StaffAttendanceRecords` và sự kiện `StaffAttendanceEvents` (ClockIn). Cập nhật `ClockInTimeUtc`, trạng thái `OnTime`. Không ảnh hưởng đến dữ liệu ca khám. | **PASS** |
| **ADMIN-ATT-02** | Tiếp tân quầy điểm danh theo mẫu ca trực chuẩn | Unit / Integration / E2E | `ClockInAsync_Receptionist_ResolvesCanonicalShift_AndRecordsPunch` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-02` in `e2e/admin-workforce-operations.spec.js` | Ca Sáng (07:00 - 13:00), Ca Chiều (13:00 - 19:00) hoặc Ca Cả Ngày (07:00 - 19:00) được phân giải tự động qua `ReceptionistShiftProvider` theo ca mẫu thực tế trong DB. Bản ghi điểm danh ghi nhận đúng ca và giờ quẹt. | **PASS** |
| **ADMIN-ATT-03** | Tính toán đi muộn, về sớm và thời gian ân hạn (Grace Period) | Unit / Integration | `ClockInAsync_LatePunch_CalculatesLateMinutes_BeyondGracePeriod` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-03` in `e2e/admin-workforce-operations.spec.js` | Quẹt thẻ trong vòng 15 phút sau giờ bắt đầu ca: Tính `OnTime` ($Late = 0$). Quẹt sau 15 phút: Trạng thái `Late`, $LateMinutes = t_{in} - T_{start}$. Quẹt về trước 15 phút: `EarlyDeparture`. | **PASS** |
| **ADMIN-ATT-04** | Ngăn chặn điểm danh lặp và chống Race Condition | Unit / Integration | `ClockInAsync_DuplicatePunch_ThrowsInvalidOperationException` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-04` in `e2e/admin-workforce-operations.spec.js` | Quẹt thẻ 2 lần trong cùng một ca bị chặn bởi `SqlAppLockHelper.AttendancePunchKey`. API từ chối với thông báo lỗi rõ ràng. | **PASS** |
| **ADMIN-ATT-05** | Bảng đối soát chấm công tháng & Xuất file CSV chống mã độc | Unit / Controller / E2E | `GetMonthlyWorkforceSummaryAsync_AggregatesWorkedHours_AndStandardWorkDays` / `AttendanceAndOperationsTests.cs`<br>`ExportCsv_SanitizesFormulaInjection` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-05` in `e2e/admin-workforce-operations.spec.js` | Thống kê chính xác: số ca phân công, số ca thực tế, tổng giờ làm, số phút trễ, ngày công quy đổi chuẩn (1 công = 8h). File CSV có UTF-8 BOM, mọi ký tự nguy hiểm (`=`, `+`, `-`, `@`) được tiền tố hóa dấu `'`. | **PASS** |
| **ADMIN-ATT-06** | Chốt sổ kỳ công bất biến & Mở lại kỳ công có lý do bắt buộc | Unit / Controller / E2E | `FinalizeMonthlyPeriodAsync_CreatesImmutableSnapshot_AndLocksPeriod` / `AttendanceAndOperationsTests.cs`<br>`ClockInAsync_LockedPeriod_ThrowsInvalidOperationException` / `AttendanceAndOperationsTests.cs`<br>`ReopenMonthlyPeriodAsync_ValidReason_UnlocksPeriod_AndAudits` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-06` in `e2e/admin-workforce-operations.spec.js` | Chốt sổ lưu snapshot JSON vào `AttendancePeriodClosures`. Khi đã đóng sổ, cấm mọi thao tác điểm danh/chỉnh sửa. Mở lại yêu cầu lý do $\ge 10$ ký tự và ghi nhật ký kiểm toán. | **PASS** |
| **ADMIN-ATT-07** | Nhân viên gửi đơn giải trình & Quản trị viên phê duyệt/từ chối | Unit / Controller / E2E | `SubmitCorrectionRequestAsync_PendingApproval_CreatesRequest` / `AttendanceAndOperationsTests.cs`<br>`ReviewCorrectionRequestAsync_Approve_UpdatesAttendanceRecord` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-07` in `e2e/admin-workforce-operations.spec.js` | Nhân viên gửi đơn giải trình ngày công bị quên. Admin phê duyệt tự động cập nhật lại giờ công, tính lại số phút đi muộn, ngày công và ghi event `SystemCorrection`. | **PASS** |
| **ADMIN-ATT-08** | Giám sát vận hành thời gian thực: Thiếu lễ tân & Bác sĩ vắng mặt | Unit / Controller / E2E | `GetOperationsDashboardAsync_FlagsShortage_WhenReceptionistsLessThanTwo` / `AttendanceAndOperationsTests.cs`<br>`GetOperationsDashboardAsync_FlagsAlert_WhenDentistHasAppointments_AndNotPresent` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-08` in `e2e/admin-workforce-operations.spec.js` | Dashboard cảnh báo đỏ nếu ca trực có $< 2$ lễ tân. Hiển thị cảnh báo nguy cấp khi bác sĩ có lịch hẹn khám nhưng chưa điểm danh. Hiển thị pipeline ca khám, viện phí và kho dược. | **PASS** |

| **ADMIN-ATT-09** | Giám sát vận hành: Tích hợp phân hệ Nghỉ phép Bác sĩ & Gián đoạn lịch hẹn | Unit / Controller / E2E | `ClinicOperations_Dashboard_IntegratesDoctorLeaveAndDisruptionKpis` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-09` in `e2e/admin-workforce-operations.spec.js` | Dashboard tích hợp đầy đủ KPI: đơn nghỉ phép bác sĩ chờ duyệt, số ca gián đoạn khẩn cấp (< 24h), ca chờ bệnh nhân phản hồi, ca chờ tiếp tân điều phối. | **PASS** |
| **ADMIN-ATT-10** | Chặn điều chỉnh lịch trực Lễ tân trong kỳ công đã khóa sổ | Unit / Cross-Module Guard | `MonthlyClosureGuard_ModifyingReceptionistScheduleInLockedPeriod_IsBlocked` / `AttendanceAndOperationsTests.cs` | Từ chối tạo/sửa/xóa ca trực trong tháng đã chốt sổ với thông báo: `Kỳ công tháng MM/yyyy đã được chốt sổ. Không thể phân ca trực.` | **PASS** |
| **ADMIN-ATT-11** | Chặn phê duyệt đơn nghỉ phép Bác sĩ hồi tố trong kỳ công đã khóa sổ | Unit / Cross-Module Guard | `MonthlyClosureGuard_ApprovingDentistLeaveInLockedPeriod_IsBlocked` / `AttendanceAndOperationsTests.cs` | Từ chối duyệt đơn nghỉ phép bác sĩ hồi tố vào tháng đã chốt sổ, yêu cầu Quản trị viên mở lại kỳ công trước khi thực hiện. | **PASS** |

---

## 2. MA TRẬN KIỂM THỬ UAT DATABASE THỰC TẾ (REAL SQL SERVER UAT SUITE)

| Mã UAT | Kịch Bản Nghiệp Vụ Tích Hợp | File & Phương Thức Thực Thi | Dữ Liệu Thực Tế Trên `DentalCare_AdminOperations_Dev` | Kết Quả Nghiệm Thu | Trạng Thái |
|---|---|---|---|---|---|
| **UAT-01** | Xuất bản ca lễ tân -> ClockIn -> ClockOut -> Lưu vết sự kiện | `UAT01_ReceptionistShift_ClockIn_ClockOut_PersistsWorkedHoursAndEvents`<br>`AdminOperationsRealDatabaseUatTests.cs` | Lễ tân `UserId = 2` trên ca `ShiftTemplateId = 3` (Ca Cả Ngày). Xác minh `StaffAttendanceRecords` lưu đủ `ClockInTimeUtc`, `ClockOutTimeUtc`, $\ge 2$ sự kiện `StaffAttendanceEvents`. | Ghi nhận chính xác giờ công và lưu vết kiểm toán bất biến. | **PASS** |
| **UAT-02** | Đổi ca trực Lễ tân -> Phê duyệt -> Provider phân giải ca mới | `UAT02_ShiftSwap_AttendanceProvider_ReflectsNewAssigneeInRealTime`<br>`AdminOperationsRealDatabaseUatTests.cs` | Chuyển ca từ Lễ tân `UserId = 2` sang `UserId = 44`. Admin duyệt swap. Provider `CompositeStaffShiftProvider` phân giải ngay ca trực cho người nhận và gỡ bỏ khỏi người chuyển. | Điểm danh phản ánh người trực thay thế theo thời gian thực. | **PASS** |
| **UAT-03** | Đơn nghỉ phép đã duyệt -> Chặn quẹt thẻ trong khoảng nghỉ, cho phép ngoài khoảng | `UAT03_ApprovedLeave_PartialOrFullDay_BlocksClockInDuringLeaveWindow`<br>`AdminOperationsRealDatabaseUatTests.cs` | Lễ tân `UserId = 45`. Kiểm thử 3 trường hợp: (1) Nghỉ cả ngày (`FullDay`) chặn điểm danh. (2) Nghỉ một phần (`Hourly`) bao trùm giờ hiện tại chặn điểm danh kèm lý do chi tiết. (3) Nghỉ một phần ngoài giờ hiện tại cho phép điểm danh vào/ra bình thường. | Ngăn chặn gian lận chấm công khi nghỉ phép và đảm bảo linh hoạt cho nghỉ một phần. | **PASS** |
| **UAT-04** | Nghỉ phép Bác sĩ -> Tạo gián đoạn -> Dashboard cập nhật KPI | `UAT04_DentistLeave_DisruptionsAndClinicOperationsDashboardKpi`<br>`AdminOperationsRealDatabaseUatTests.cs` | Bác sĩ `UserId = 3` xin nghỉ -> Admin duyệt -> Tạo gián đoạn lịch hẹn -> Dashboard hiển thị chính xác KPI và không báo vắng mặt sai lệch. | Đồng bộ trạng thái nghỉ phép và gián đoạn vận hành. | **PASS** |
| **UAT-05** | Chốt sổ tháng -> Chặn sửa khi Locked/Finalized -> Mở lại khi Reopened | `UAT05_MonthlyClosureInvariant_LockAndReopenGuard`<br>`AdminOperationsRealDatabaseUatTests.cs` | Kiểm thử trạng thái khóa kép trên SQL Server QA: Cả `Locked` và `Finalized` đều chặn tạo ca trực và duyệt nghỉ phép với thông báo `chốt sổ`. Khi mở lại (`Reopened`) có lý do $\ge 10$ ký tự, mở khóa thành công và cho phép phân ca bình thường. | Bảo toàn tính bất biến của kỳ công kế toán trên cả 2 trạng thái khóa. | **PASS** |

---

## 3. BẢNG TỔNG HỢP KIỂM THỬ TÁC ĐỘNG QUẢN TRỊ (ADMIN IMPACT ANALYSIS)

| Mã Phân Tích | Thao Tác Quản Trị Cần Đánh Giá | Tầng Kiểm Thử | Tên Phương Thức Test | Kết Quả Đánh Giá Tác Động | Trạng Thái |
|---|---|---|---|---|---|
| **IMP-01** | Khóa tài khoản nhân sự (`LockUser`) | Unit / Web Integration | `AnalyzeUserLockImpactAsync_StaffWithFutureAppointments_ReturnsCriticalWarnings` | Phát hiện số ca trực tương lai, số lịch hẹn bị ảnh hưởng, cảnh báo nếu gây thiếu độ phủ lễ tân. | **PASS** |
| **IMP-02** | Tạm ngừng dịch vụ nha khoa (`DeactivateService`) | Unit / Web Integration | `AnalyzeServiceDeactivationImpactAsync_ActiveFutureAppointments_ReturnsAffectedCount` | Liệt kê danh sách ca khám tương lai đã đặt lịch với dịch vụ này. | **PASS** |
| **IMP-03** | Vô hiệu hóa phòng / ghế nha khoa (`DeactivateRoomChair`) | Unit / Web Integration | `AnalyzeRoomChairDeactivationImpactAsync_ChecksExclusiveConflicts` | Phát hiện các ca khám đã chỉ định ghế/phòng điều trị này trong tương lai. | **PASS** |
| **IMP-04** | Thay đổi đơn giá dịch vụ (`UpdatePrice`) | Unit / Web Integration | `AnalyzePriceChangeImpactAsync_PreservesHistoricalInvoices` | Đảm bảo giá cũ trên các hóa đơn đã thanh toán không bị hồi tố, thông báo biên độ giá mới. | **PASS** |
| **IMP-05** | Thực hiện thay đổi trạng thái an toàn (`SafeChangeStatus`) | Unit / Web Integration | `ExecuteSafeUserStatusChangeAsync_AuditsAction_AndUpdatesStatus` | Ghi vết hành động vào bảng `AuditLogs` với đầy đủ ID, người thực hiện, lý do và giá trị cũ/mới. | **PASS** |

---

## 4. THỐNG KÊ CHI TIẾT TỔNG SỐ TEST SUITE (RECONCILED DISTINCT METRICS)

```
================================================================================
FINAL VERIFICATION TEST SUITE EXECUTION SUMMARY:
--------------------------------------------------------------------------------
Backend Test Assembly:
  Target:     DentalClinic.Tests.dll (net8.0)
  Discovered: 865 tests (bao gồm 5 Real SQL Server UAT tests UAT-01 đến UAT-05)
  Passed:     864 tests
  Skipped:    1 test (CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets)
  Failed:     0 tests
  Duration:   19s

Frontend Test Assembly:
  Target:     DentalClinic.Web.Tests.dll (net8.0)
  Discovered: 323 tests
  Passed:     323 tests
  Skipped:    0 tests
  Failed:     0 tests
  Duration:   20s

Playwright Real Browser E2E Specs (Chromium against FE:7135 & BE:7355):
  Suites:     e2e/admin-workforce-operations.spec.js (9 tests)
              e2e/login-auth-flow.spec.js (6 tests)
              e2e/receptionist-workforce.spec.js (6 tests)
              e2e/doctor-leave-recovery.spec.js (6 tests)
  Total E2E:  27 scenarios
  Passed:     27 tests
  Failed:     0 tests
  Duration:   ~28s

--------------------------------------------------------------------------------
GIẢI TRÌNH ĐỐI SOÁT TRÁNH ĐẾM TRÙNG (RECONCILIATION):
- Báo cáo trước ghi nhận 1.217 tests do cộng dồn:
  862 (Backend) + 323 (Frontend) + 27 (E2E) + 5 (Real DB UAT) = 1.217 tests.
  Tuy nhiên, 5 bài test Real SQL Server UAT nằm bên trong assembly DentalClinic.Tests.dll.
  Vì vậy tổng số test độc lập thực tế ban đầu là: 862 + 323 + 27 = 1.212 tests.
- Sau khi bổ sung 3 test guards bảo vệ kỳ công kép (Locked & Finalized),
  số lượng test backend tăng từ 862 lên 865.
- Tổng số kiểm thử tự động ĐỘC LẬP (DISTINCT) hiện tại là:
  865 (BE) + 323 (FE) + 27 (E2E) = 1.215 tests.

================================================================================
GRAND TOTAL DISTINCT AUTOMATED TESTS:
  Total Executed: 1,215 unique tests
  Passed:         1,214 tests
  Skipped:        1 test (Cloudinary Secrets)
  Failed:         0 tests (100% SUCCESS)
================================================================================
```
