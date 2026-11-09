# MA TRẬN KIỂM THỬ: RECEPTIONIST SHIFT, LEAVE & WORKFORCE MANAGEMENT

**Dự án:** DentalCare – Multi-Specialty Clinic Management System  
**Phân hệ:** Receptionist Shift, Leave & Workforce Management  
**Phiên bản:** 1.0.0 Hardened & Integration Ready  
**Tình trạng:** 100% Passed (1,129 Automated Tests across Backend, Frontend, Real SQL Server Concurrency, and Playwright E2E)  

---

## 1. MA TRẬN YÊU CẦU NGHIỆP VỤ & CA KIỂM THỬ

| Mã YC | Mô Tả Yêu Cầu Nghiệp Vụ | Loại Kiểm Thử | Tên File Kiểm Thử / Kịch Bản | Trạng Thái |
|---|---|---|---|---|
| **SCH-01** | Tạo ca trực đơn lẻ cho nhân viên lễ tân, kiểm tra giờ bắt đầu < giờ kết thúc | Unit Test | `ReceptionistWorkforceSchedulingTests.CreateScheduleAsync_ValidSchedule_CreatesSuccessfully` | **PASS** |
| **SCH-02** | Chặn tạo ca trực khi giờ kết thúc trước giờ bắt đầu | Unit Test | `ReceptionistWorkforceSchedulingTests.CreateScheduleAsync_EndTimeBeforeStartTime_ReturnsError` | **PASS** |
| **SCH-03** | Xem trước và phân ca trực hàng loạt theo khoảng ngày và thứ trong tuần | Integration Test | `ReceptionistWorkforceSchedulingTests.PreviewAndApplyBulkScheduleAsync_AppliesToDatesCorrectly` | **PASS** |
| **SCH-04** | Ghi đè ca trực cũ an toàn khi bật cờ `OverwriteExisting = true` | Integration Test | `ReceptionistWorkforceSchedulingTests.ApplyBulkScheduleAsync_OverwriteTrue_ReplacesExistingShifts` | **PASS** |
| **SCH-05** | Sao chép ca trực từ tháng nguồn sang tháng đích | Integration Test | `ReceptionistWorkforceSchedulingTests.CopyMonthScheduleAsync_CopiesShiftsBetweenMonths` | **PASS** |
| **SCH-06** | Ghi đè ca trực nguyên tử theo ngày (`Daily Override`) | Integration Test | `ReceptionistWorkforceSchedulingTests.DailyOverrideScheduleAsync_ReplacesAllShiftsForDate` | **PASS** |
| **RULE-01** | **Quy tắc 1:** Tối đa 2 lễ tân khác nhau được duyệt nghỉ mỗi ngày dương lịch. Người thứ 3 bị từ chối | Unit / Business Test | `ReceptionistLeaveRuleAndCoverageTests.Rule1_MaxTwoApprovedLeavesPerDay_ThirdReceptionistFails` | **PASS** |
| **RULE-01B** | Cùng 1 lễ tân có nhiều khoảng nghỉ trong ngày chỉ tính là 1 lễ tân nghỉ phép | Unit / Business Test | `ReceptionistLeaveRuleAndCoverageTests.Rule1_SameReceptionistMultipleLeavePeriods_CountsAsOne` | **PASS** |
| **RULE-02** | **Quy tắc 2:** Ca trực phải duy trì tối thiểu 2 lễ tân có mặt. Khi số người có mặt < 2, từ chối duyệt | Unit / Business Test | `ReceptionistLeaveRuleAndCoverageTests.Rule2_MinTwoAvailablePerShift_FailsWhenDroppingToOne` | **PASS** |
| **RULE-02B** | Kiểm tra phân đoạn giờ con (`Hourly sub-interval`): Bị chặn nếu khoảng giờ xin nghỉ làm số nhân sự < 2 | Unit / Business Test | `ReceptionistLeaveRuleAndCoverageTests.Rule2_HourlySubIntervalCheck_FailsWhenSpecificHourDropsBelowTwo` | **PASS** |
| **LVE-01** | Lễ tân rút đơn xin nghỉ phép khi đơn còn ở trạng thái `Pending` | Unit Test | `ReceptionistLeaveRuleAndCoverageTests.LeaveLifecycle_Withdraw_OnlyAllowedWhenPending` | **PASS** |
| **LVE-02** | Quản trị viên từ chối đơn xin nghỉ phép và lưu lý do từ chối | Unit Test | `ReceptionistLeaveRuleAndCoverageTests.LeaveLifecycle_Reject_MarksStatusAndSavesReason` | **PASS** |
| **SWP-01** | Vòng đời đổi ca hoàn chỉnh: Đề xuất &rarr; Đồng nghiệp chấp thuận &rarr; Admin phê duyệt &rarr; Hoán đổi ca nguyên tử | Integration Test | `ReceptionistShiftSwapTests.ShiftSwap_FullLifecycle_ProposeAcceptApprove_SwapsSuccessfully` | **PASS** |
| **SWP-02** | Đồng nghiệp từ chối đổi ca: Đơn chuyển sang `Declined`, giữ nguyên ca trực ban đầu | Unit Test | `ReceptionistShiftSwapTests.ShiftSwap_CounterpartyDeclines_StopsAtDeclined` | **PASS** |
| **SWP-03** | Chặn người dùng không phải người nhận của yêu cầu thực hiện phản hồi đổi ca | Security / Unit Test | `ReceptionistShiftSwapTests.ShiftSwap_NonTargetUserCannotRespond` | **PASS** |
| **EMG-01** | Lễ tân báo vắng khẩn cấp: Ghi nhận sự cố kể cả khi số nhân sự còn lại < 2 | Business Test | `ReceptionistEmergencyAbsenceAndIncidentTests.ReportEmergencyAbsence_CreatesIncidentEvenIfCoverageDropsBelowTwo` | **PASS** |
| **EMG-02** | Quản trị viên giải quyết sự cố thiếu nhân sự: Điều phối nhân sự thay thế và đánh dấu `Resolved` | Integration Test | `ReceptionistEmergencyAbsenceAndIncidentTests.ResolveIncident_AssignsReplacementAndMarksResolved` | **PASS** |
| **EMG-03** | Hệ thống phát hiện tình trạng thiếu nhân lực (`IsUnderstaffed = true`) trong ca trực | Unit Test | `ReceptionistEmergencyAbsenceAndIncidentTests.CheckShiftCoverageForDateAsync_DetectsUnderstaffing` | **PASS** |
| **HND-01** | Trích xuất số liệu thực tế về hàng đợi tiếp đón và hóa đơn viện phí tồn để phục vụ bàn giao | Integration Test | `ReceptionistWorkHandoverTests.GetCurrentHandoverSummaryAsync_ReturnsAccurateCounters` | **PASS** |
| **HND-02** | Tạo biên bản bàn giao và tiếp nhận bàn giao giữa các đồng nghiệp; chặn tự tiếp nhận biên bản chính mình | Security / Unit Test | `ReceptionistWorkHandoverTests.HandoverLifecycle_CreateAndAcknowledge_UpdatesStatus` | **PASS** |
| **CON-01** | Định dạng và xác thực các khóa khóa ứng dụng phân tán `SqlAppLockHelper` | Unit Test | `ReceptionistConcurrencyAndLockingTests.SqlAppLockHelper_Keys_FormatCorrectly` | **PASS** |
| **CON-02** | Kiểm thử tương tranh trong bộ nhớ: Hai luồng duyệt đơn cho lễ tân số 2 và 3 cùng lúc chỉ 1 thành công | Concurrency Test | `ReceptionistConcurrencyAndLockingTests.ConcurrentLeaveApprovals_GuaranteesAtMostTwoApprovedLeaves` | **PASS** |
| **SQL-CON-01** | **CASE 1 (Real SQL Server):** Concurrent Leave Approval tranh chấp Rule 1 (Max 2) qua `sp_getapplock` | SQL Concurrency | `SqlServerReceptionistConcurrencyIntegrationTests.Case1_ConcurrentLeaveApproval_GuaranteesAtMostTwoApprovedLeavesOnSqlServer` | **PASS** |
| **SQL-CON-02** | **CASE 2 (Real SQL Server):** Leave Approval vs Shift Swap đảm bảo không trùng ca cho cùng nhân sự | SQL Concurrency | `SqlServerReceptionistConcurrencyIntegrationTests.Case2_LeaveApprovalVsShiftSwap_MaintainsCoverageAndNoCorruptedSchedules` | **PASS** |
| **SQL-CON-03** | **CASE 3 (Real SQL Server):** Leave Approval vs Bulk Scheduling bảo vệ an toàn ngày đã duyệt nghỉ | SQL Concurrency | `SqlServerReceptionistConcurrencyIntegrationTests.Case3_LeaveApprovalVsBulkScheduling_ProtectsApprovedLeaveAndNoOrphanShifts` | **PASS** |
| **SQL-CON-04** | **CASE 4 (Real SQL Server):** Duplicate Leave Submission bảo vệ chống double-click tạo 2 đơn trùng | SQL Concurrency | `SqlServerReceptionistConcurrencyIntegrationTests.Case4_DuplicateLeaveSubmission_PreventsDuplicateRowsUnderConcurrentCalls` | **PASS** |
| **E2E-01** | Bảng điều khiển Quản trị viên: Kiểm tra hiển thị 5 tab, ma trận lịch trực, nút Phân ca & Sao chép tháng | Browser E2E | `e2e/receptionist-workforce.spec.js - E2E-REC-1` | **PASS** |
| **E2E-02** | Lịch làm việc Lễ tân: Xem ca trực hôm nay và modal báo vắng khẩn cấp | Browser E2E | `e2e/receptionist-workforce.spec.js - E2E-REC-2` | **PASS** |
| **E2E-03** | Quản lý nghỉ phép Lễ tân: Thẻ thống kê, banner Quy tắc 1 & 2 và modal tạo đơn 5 hình thức | Browser E2E | `e2e/receptionist-workforce.spec.js - E2E-REC-3` | **PASS** |
| **E2E-04** | Đổi ca trực Lễ tân: Tab yêu cầu gửi đi, lịch sử đổi ca và modal tạo đề xuất đổi ca | Browser E2E | `e2e/receptionist-workforce.spec.js - E2E-REC-4` | **PASS** |
| **E2E-05** | Bàn giao ca trực: Snapshot hàng đợi tiếp đón, hóa đơn chờ thu và modal lập biên bản | Browser E2E | `e2e/receptionist-workforce.spec.js - E2E-REC-5` | **PASS** |
| **E2E-06** | Tính toàn vẹn của menu Sidebar: Hiển thị đúng phân nhóm cho Lễ tân và Quản trị viên | Browser E2E | `e2e/receptionist-workforce.spec.js - E2E-REC-6` | **PASS** |

---

## 2. TỔNG KẾT BẢO ĐẢM CHẤT LƯỢNG

- **Tổng số ca kiểm thử tự động phân hệ mới:** 32 test cases (22 xUnit Backend InMemory/Logic Tests + 4 Real SQL Server Concurrency Tests + 6 Playwright E2E Scenarios).
- **Tỷ lệ vượt qua:** **100% (32/32)**.
- **Tổng số ca kiểm thử toàn hệ thống:** 1,130 test cases:
  - Backend: **808 Passed**, **1 Skipped** (`UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully` do thiếu Cloudinary User Secrets trong CI/local), **0 Failed**.
  - Frontend: **315 Passed**, **0 Skipped**, **0 Failed**.
  - Playwright E2E: **6 Passed**, **0 Failed**.
- **Hồi quy hệ thống (System Regressions):** **0 lỗi**.
- **Cảnh báo biên dịch (Compiler Warnings):** **0 cảnh báo**.
