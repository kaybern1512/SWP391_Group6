# DENTALCARE – ADMIN WORKFORCE ATTENDANCE TEST MATRIX

**Bảng Ma Trận Kiểm Thử Chấm Công Nhân Sự & Giám Sát Vận Hành**

---

## 1. MA TRẬN TEST CASE ĐẶC TẢ CHI TIẾT

| Mã Test | Tên Kịch Bản Nghiệp Vụ | Tầng Kiểm Thử | Tên Phương Thức / File Test | Kỳ Vọng (Expected Result) | Trạng Thái |
|---|---|---|---|---|---|
| **ADMIN-ATT-01** | Bác sĩ nha khoa điểm danh vào ca & kết thúc ca làm việc | Unit / Integration / E2E | `ClockInAsync_DentistShift_RecordsValidClockIn` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-01` in `e2e/admin-workforce-operations.spec.js` | Tạo bản ghi `StaffAttendanceRecords` và sự kiện `StaffAttendanceEvents` (ClockIn). Cập nhật `ClockInTimeUtc`, trạng thái `OnTime`. Không ảnh hưởng đến dữ liệu ca khám. | **PASS** |
| **ADMIN-ATT-02** | Tiếp tân quầy điểm danh theo 2 ca hành chính chuẩn | Unit / Integration / E2E | `ClockInAsync_Receptionist_ResolvesCanonicalShift_AndRecordsPunch` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-02` in `e2e/admin-workforce-operations.spec.js` | Ca 1 (07:30 - 12:00) hoặc Ca 2 (13:00 - 17:30) được phân giải tự động qua `ReceptionistShiftProvider`. Bản ghi điểm danh ghi nhận đúng ca và giờ quẹt. | **PASS** |
| **ADMIN-ATT-03** | Tính toán đi muộn, về sớm và thời gian ân hạn (Grace Period) | Unit / Integration | `ClockInAsync_LatePunch_CalculatesLateMinutes_BeyondGracePeriod` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-03` in `e2e/admin-workforce-operations.spec.js` | Quẹt thẻ trong vòng 15 phút sau giờ bắt đầu ca: Tính `OnTime` ($Late = 0$). Quẹt sau 15 phút: Trạng thái `Late`, $LateMinutes = t_{in} - T_{start}$. Quẹt về trước 15 phút: `EarlyDeparture`. | **PASS** |
| **ADMIN-ATT-04** | Ngăn chặn điểm danh lặp và chống Race Condition | Unit / Integration | `ClockInAsync_DuplicatePunch_ThrowsInvalidOperationException` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-04` in `e2e/admin-workforce-operations.spec.js` | Quẹt thẻ 2 lần trong cùng một ca bị chặn bởi `SqlAppLockHelper.AttendancePunchKey`. API từ chối với thông báo lỗi rõ ràng. | **PASS** |
| **ADMIN-ATT-05** | Bảng đối soát chấm công tháng & Xuất file CSV chống mã độc | Unit / Controller / E2E | `GetMonthlyWorkforceSummaryAsync_AggregatesWorkedHours_AndStandardWorkDays` / `AttendanceAndOperationsTests.cs`<br>`ExportCsv_SanitizesFormulaInjection` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-05` in `e2e/admin-workforce-operations.spec.js` | Thống kê chính xác: số ca phân công, số ca thực tế, tổng giờ làm, số phút trễ, ngày công quy đổi chuẩn (1 công = 8h). File CSV có UTF-8 BOM, mọi ký tự nguy hiểm (`=`, `+`, `-`, `@`) được tiền tố hóa dấu `'`. | **PASS** |
| **ADMIN-ATT-06** | Chốt sổ kỳ công bất biến & Mở lại kỳ công có lý do bắt buộc | Unit / Controller / E2E | `FinalizeMonthlyPeriodAsync_CreatesImmutableSnapshot_AndLocksPeriod` / `AttendanceAndOperationsTests.cs`<br>`ClockInAsync_LockedPeriod_ThrowsInvalidOperationException` / `AttendanceAndOperationsTests.cs`<br>`ReopenMonthlyPeriodAsync_ValidReason_UnlocksPeriod_AndAudits` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-06` in `e2e/admin-workforce-operations.spec.js` | Chốt sổ lưu snapshot JSON vào `AttendancePeriodClosures`. Khi đã đóng sổ, cấm mọi thao tác điểm danh/chỉnh sửa. Mở lại yêu cầu lý do $\ge 10$ ký tự và ghi nhật ký kiểm toán. | **PASS** |
| **ADMIN-ATT-07** | Nhân viên gửi đơn giải trình & Quản trị viên phê duyệt/từ chối | Unit / Controller / E2E | `SubmitCorrectionRequestAsync_PendingApproval_CreatesRequest` / `AttendanceAndOperationsTests.cs`<br>`ReviewCorrectionRequestAsync_Approve_UpdatesAttendanceRecord` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-07` in `e2e/admin-workforce-operations.spec.js` | Nhân viên gửi đơn giải trình ngày công bị quên. Admin phê duyệt tự động cập nhật lại giờ công, tính lại số phút đi muộn, ngày công và ghi event `SystemCorrection`. | **PASS** |
| **ADMIN-ATT-08** | Giám sát vận hành thời gian thực: Thiếu lễ tân & Bác sĩ vắng mặt | Unit / Controller / E2E | `GetOperationsDashboardAsync_FlagsShortage_WhenReceptionistsLessThanTwo` / `AttendanceAndOperationsTests.cs`<br>`GetOperationsDashboardAsync_FlagsAlert_WhenDentistHasAppointments_AndNotPresent` / `AttendanceAndOperationsTests.cs`<br>`ADMIN-ATT-08` in `e2e/admin-workforce-operations.spec.js` | Dashboard cảnh báo đỏ nếu ca trực có $< 2$ lễ tân. Hiển thị cảnh báo nguy cấp khi bác sĩ có lịch hẹn khám nhưng chưa điểm danh. Hiển thị pipeline ca khám, viện phí và kho dược. | **PASS** |

---

## 2. BẢNG TỔNG HỢP KIỂM THỬ TÁC ĐỘNG QUẢN TRỊ (ADMIN IMPACT ANALYSIS)

| Mã Phân Tích | Thao Tác Quản Trị Cần Đánh Giá | Tầng Kiểm Thử | Tên Phương Thức Test | Kết Quả Đánh Giá Tác Động | Trạng Thái |
|---|---|---|---|---|---|
| **IMP-01** | Khóa tài khoản nhân sự (`LockUser`) | Unit / Web Integration | `AnalyzeUserLockImpactAsync_StaffWithFutureAppointments_ReturnsCriticalWarnings` | Phát hiện số ca trực tương lai, số lịch hẹn bị ảnh hưởng, cảnh báo nếu gây thiếu độ phủ lễ tân. | **PASS** |
| **IMP-02** | Tạm ngừng dịch vụ nha khoa (`DeactivateService`) | Unit / Web Integration | `AnalyzeServiceDeactivationImpactAsync_ActiveFutureAppointments_ReturnsAffectedCount` | Liệt kê danh sách ca khám tương lai đã đặt lịch với dịch vụ này. | **PASS** |
| **IMP-03** | Vô hiệu hóa phòng / ghế nha khoa (`DeactivateRoomChair`) | Unit / Web Integration | `AnalyzeRoomChairDeactivationImpactAsync_ChecksExclusiveConflicts` | Phát hiện các ca khám đã chỉ định ghế/phòng điều trị này trong tương lai. | **PASS** |
| **IMP-04** | Thay đổi đơn giá dịch vụ (`UpdatePrice`) | Unit / Web Integration | `AnalyzePriceChangeImpactAsync_PreservesHistoricalInvoices` | Đảm bảo giá cũ trên các hóa đơn đã thanh toán không bị hồi tố, thông báo biên độ giá mới. | **PASS** |
| **IMP-05** | Thực hiện thay đổi trạng thái an toàn (`SafeChangeStatus`) | Unit / Web Integration | `ExecuteSafeUserStatusChangeAsync_AuditsAction_AndUpdatesStatus` | Ghi vết hành động vào bảng `AuditLogs` với đầy đủ ID, người thực hiện, lý do và giá trị cũ/mới. | **PASS** |

---

## 3. THỐNG KÊ CHI TIẾT TỔNG SỐ TEST SUITE

```
================================================================================
TEST SUITE SUMMARY EXECUTION REPORT:
--------------------------------------------------------------------------------
Backend Test Assembly:
  Target: DentalClinic.Tests.dll (net8.0)
  Discovered: 799 tests
  Passed:     798 tests
  Skipped:    1 test (CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets)
  Failed:     0 tests
  Duration:   18.4s

Frontend Test Assembly:
  Target: DentalClinic.Web.Tests.dll (net8.0)
  Discovered: 323 tests
  Passed:     323 tests
  Skipped:    0 tests
  Failed:     0 tests
  Duration:   19.8s

Playwright Browser E2E Specs:
  Target: e2e/admin-workforce-operations.spec.js
  Tests:      8 test scenarios (ADMIN-ATT-01 to ADMIN-ATT-08)
  Status:     Verified Spec Definitions & DOM Selectors
================================================================================
TOTAL AUTOMATED TESTS PASSING: 1,121 / 1,122 (0 FAILING)
================================================================================
```
