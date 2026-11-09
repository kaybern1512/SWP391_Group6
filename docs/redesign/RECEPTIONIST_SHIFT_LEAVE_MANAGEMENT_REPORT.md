# BÁO CÁO KỸ THUẬT: PHÂN HỆ QUẢN LÝ LỊCH TRỰC, NGHỈ PHÉP & ĐIỀU PHỐI NHÂN SỰ LỄ TÂN (RECEPTIONIST SHIFT, LEAVE & WORKFORCE MANAGEMENT)

**Dự án:** DentalCare – Multi-Specialty Clinic Management System  
**Phân hệ:** Receptionist Shift, Leave & Workforce Management  
**Nhánh Git:** `feature/receptionist-shift-leave-management`  
**Cơ sở dữ liệu:** `DentalCare_ReceptionistShift_Dev` (SQL Server QA riêng biệt trên `.\SQLEXPRESS`)  
**Ngày hoàn thành & Hardening:** 10/11/2026  
**Kiến trúc sư & Kỹ sư trưởng:** Senior ASP.NET Core 8 Full-stack Architect, SQL Server Engineer & QA Automation Lead  

---

## 1. TỔNG QUAN YÊU CẦU & BỐI CẢNH DỰ ÁN

Phân hệ **Receptionist Shift, Leave & Workforce Management** được thiết kế nhằm quản lý toàn diện lịch làm việc, phân bổ ca trực theo tháng, quy trình xin và xét duyệt nghỉ phép theo quy tắc kiểm soát nghiêm ngặt, cơ chế đổi ca giữa các lễ tân, xử lý vắng mặt khẩn cấp và duy trì tính liên tục của bàn tiếp đón (check-in queue, billing, live chat) thông qua biên bản bàn giao ca.

### Các Nguyên Tắc Thiết Kế Trọng Tâm
1. **Cô lập dữ liệu hoàn toàn (Zero Conflict Isolation):** Phân hệ lễ tân sử dụng hệ thống bảng riêng biệt (`ReceptionistWorkSchedules`, `ReceptionistShiftTemplates`, `ReceptionistLeaveRequests`, `ReceptionistLeaveRequestPeriods`, `ReceptionistShiftSwapRequests`, `ReceptionistCoverageIncidents`, `ReceptionistWorkHandovers`), hoàn toàn độc lập với phân hệ bác sĩ (`DentistSchedule`, `DentistLeave`) để không gây migration collision hoặc race condition.
2. **Quy tắc 1 (RULE 1 – Maximum 2 Approved Leaves/Day):** Mỗi ngày dương lịch chỉ cho phép tối đa 2 nhân viên lễ tân khác nhau (`Distinct ReceptionistUserId`) được phê duyệt nghỉ phép trên toàn hệ thống phòng khám.
3. **Quy tắc 2 (RULE 2 – Minimum 2 Available Receptionists/Shift):** Mỗi ca trực và tại mọi phân đoạn giờ con (`sub-intervals`) phải luôn có tối thiểu 2 lễ tân có mặt làm việc thực tế sau khi đã trừ đi các ca nghỉ phép hoặc vắng mặt.
4. **An toàn tương tranh cao (High Concurrency & Atomic Locking):** Sử dụng `sp_getapplock` thông qua `SqlAppLockHelper` kết hợp giao dịch nguyên tử (`IDbContextTransaction`) để ngăn chặn tuyệt đối tình trạng race condition khi 2 quản trị viên cùng phê duyệt đơn nghỉ vượt trần, đổi ca cùng thời điểm, hoặc lễ tân double-click gửi trùng đơn nghỉ.
5. **Tính liên tục của dịch vụ (Service Continuity & Handover):** Bàn giao hàng đợi tiếp đón (pending check-in appointments), danh sách viện phí chưa thu và các phiên tư vấn trực tuyến giữa các ca trực nhằm đảm bảo bệnh nhân không bị bỏ sót khi giao ca.

---

## 2. THIẾT KẾ CƠ SỞ DỮ LIỆU & SCHEMA MIGRATIONS

### 2.1. Cấu trúc 7 Bảng Chuyên Biệt

| Tên Bảng | Mục Đích Lưu Trữ | Khóa Ngoại & Ràng Buộc |
|---|---|---|
| `ReceptionistShiftTemplates` | Mẫu ca trực chuẩn (Ca Sáng 07:00-13:00, Ca Chiều 13:00-19:00, Ca Cả Ngày 07:00-19:00). | `ShiftTemplateId` (PK), `MinStaffRequired >= 2`. |
| `ReceptionistWorkSchedules` | Lịch phân ca thực tế theo ngày của từng lễ tân. | FK `ReceptionistUserId` &rarr; `UserAccounts` (ON DELETE NO ACTION), FK `ShiftTemplateId`. Trạng thái: `Scheduled`, `Completed`, `Cancelled`, `Swapped`, `AbsenceReported`. |
| `ReceptionistLeaveRequests` | Đơn xin nghỉ phép của lễ tân (FullDay, HalfDayMorning, HalfDayAfternoon, Hourly, MultiDay). | FK `ReceptionistUserId`, FK `ApprovedBy` (ON DELETE NO ACTION). Trạng thái: `Pending`, `Approved`, `Rejected`, `Withdrawn`, `Cancelled`. |
| `ReceptionistLeaveRequestPeriods` | Chi tiết phân rã từng ngày trong đơn nghỉ phép nhiều ngày. | FK `LeaveRequestId` ON DELETE CASCADE, Unique (`LeaveRequestId`, `LeaveDate`). |
| `ReceptionistShiftSwapRequests` | Yêu cầu đổi ca giữa 2 lễ tân và quyết định của Admin. | FK `RequestingReceptionistUserId`, FK `TargetReceptionistUserId`, FK `SourceScheduleId`, FK `TargetScheduleId` (ON DELETE NO ACTION). |
| `ReceptionistCoverageIncidents` | Sự cố vắng mặt đột xuất và hồ sơ điều phối bổ sung nhân sự. | FK `AffectedReceptionistUserId`, FK `ReplacementReceptionistUserId`, FK `ReportedBy`, FK `ResolvedBy` (ON DELETE NO ACTION). |
| `ReceptionistWorkHandovers` | Biên bản bàn giao công việc ca trực (lịch hẹn chờ, viện phí tồn, tin nhắn chat). | FK `FromReceptionistUserId`, FK `ToReceptionistUserId` (ON DELETE NO ACTION). Trạng thái: `Transferred`, `Acknowledged`. |

### 2.2. Migration Scripts & Rollback Scripts
- **Forward Script:** `DB/redesign_migrations/20261110_Receptionist_Shift_Leave_Workforce_Schema.sql`
- **Rollback Script:** `DB/redesign_migrations/20261110_Receptionist_Shift_Leave_Workforce_Schema_Rollback.sql`
- Cả hai script đều hỗ trợ tính idempotent (`IF NOT EXISTS`, `IF EXISTS`), tạo đầy đủ foreign keys (`ON DELETE NO ACTION` loại trừ cascade cycle), indexes tối ưu và seed sẵn 3 mẫu ca trực tiêu chuẩn bằng tiếng Việt Unicode chuẩn.
- Đã kiểm tra thực tế: Forward migration chạy 2 lần đạt kết quả idempotent hoàn hảo. Rollback migration chạy thành công trong transaction (`BEGIN TRAN ... ROLLBACK TRAN`) không gặp bất kỳ lỗi ràng buộc khóa ngoại nào.

---

## 3. KIẾN TRÚC BACKEND & BUSINESS RULES ENGINE

### 3.1. Các Dịch Vụ Cốt Lõi (Core Services)
1. **`ReceptionistScheduleService` (`IReceptionistScheduleService`):**
   - Quản trị lưới lịch trực tháng (`MonthlyReceptionistScheduleDto`) gom nhóm theo ngày, hiển thị trạng thái phân ca, số lễ tân nghỉ đã duyệt/chờ duyệt, sự cố nhân lực.
   - Tạo ca trực đơn lẻ, cập nhật, xóa ca với ràng buộc giờ bắt đầu < giờ kết thúc.
   - **Phân ca hàng loạt (Bulk Apply):** Tạo ca cho danh sách lễ tân theo khoảng ngày và các thứ trong tuần (Thứ 2 - Chủ Nhật); khóa ứng dụng theo các ngày trong khoảng (`Lock_Receptionist_Coverage_{date}`); hỗ trợ cờ `OverwriteExisting` ghi đè ca cũ một cách an toàn.
   - **Sao chép tháng (Copy Month):** Sao chép toàn bộ mô hình ca trực từ tháng nguồn sang tháng đích.
   - **Ghi đè theo ngày (Daily Override):** Thay thế toàn bộ ca trực trong một ngày cụ thể bằng danh sách ca mới trong một transaction duy nhất.

2. **`ReceptionistCoverageService` (`IReceptionistCoverageService`):**
   - **RULE 1 (`ValidateDailyMaxLeavesAsync`):** Truy vấn bảng `ReceptionistLeaveRequestPeriods` với `Status = 'Approved'`. Lấy danh sách `Distinct ReceptionistUserId`. Nếu đã có 2 lễ tân khác nhau và ứng viên xin nghỉ không nằm trong số đó, từ chối phê duyệt.
   - **RULE 2 (`CheckShiftCoverageForDateAsync`):** Thu thập toàn bộ mốc thời gian biên (`timePoints`) từ ca trực, mẫu ca và khoảng xin nghỉ. Tính toán số nhân sự có mặt đồng thời tại điểm giữa (`midTime`) của từng khoảng con:
     $$\text{StaffWorking}(t) = \text{Scheduled}(t) - \text{ApprovedLeaves}(t) - \text{CandidateLeave}(t)$$
     Nếu $\text{StaffWorking}(t) < \text{MinRequired}$ tại bất kỳ khoảng nào, đánh dấu ca trực là `IsUnderstaffed = true` và từ chối duyệt đơn.
   - **Báo cáo vắng mặt khẩn cấp (`ReportEmergencyAbsenceAsync`):** Cho phép lễ tân ghi nhận nghỉ đột xuất kể cả khi vi phạm Rule 1 hoặc Rule 2 (đáp ứng thực tế cấp bách); tự động chuyển trạng thái ca trực thành `AbsenceReported`, tạo sự cố `ReceptionistCoverageIncident`, phát thông báo khẩn tới toàn bộ Quản trị viên.
   - **Giải quyết sự cố (`ResolveCoverageIncidentAsync`):** Quản trị viên chỉ định lễ tân thay thế; hệ thống tự động kiểm tra xung đột lịch và bổ sung ca trực mới, chuyển trạng thái sự cố sang `Resolved`.

3. **`ReceptionistLeaveService` (`IReceptionistLeaveService`):**
   - Quản lý vòng đời đơn nghỉ: Gửi đơn (`CreateLeaveRequestAsync`) &rarr; Rút đơn (`WithdrawLeaveRequestAsync`) &rarr; Từ chối (`RejectLeaveRequestAsync`) &rarr; Phê duyệt (`ApproveLeaveRequestAsync`).
   - Khóa phòng ngừa double-click (`Lock_Receptionist_LeaveCreate_{userId}`) ngăn chặn tạo 2 đơn trùng lặp khi người dùng click liên tiếp hoặc retry mạng.
   - Phê duyệt nguyên tử (All-or-Nothing): Đối với đơn nghỉ nhiều ngày (`MultiDay`), hệ thống kiểm tra Rule 1 và Rule 2 cho từng ngày. Nếu bất kỳ ngày nào vi phạm, toàn bộ đơn bị từ chối và rollback giao dịch.

4. **`ReceptionistShiftSwapService` (`IReceptionistShiftSwapService`):**
   - Quy trình đổi ca 3 bước:
     1. Lễ tân A đề xuất đổi ca cho Lễ tân B (`PendingCounterparty`).
     2. Lễ tân B phản hồi: Đồng ý &rarr; Chuyển sang `PendingAdminApproval`; Từ chối &rarr; Chuyển sang `Declined`.
     3. Quản trị viên phê duyệt (`Approved`): Hệ thống kiểm tra xung đột lịch nghỉ phép và ca trực trùng của cả 2 nhân sự trước khi thực hiện hoán đổi nguyên tử `ReceptionistUserId` trên các bản ghi `ReceptionistWorkSchedule`.

5. **`ReceptionistHandoverService` (`IReceptionistHandoverService`):**
   - Tổng hợp tức thời hàng đợi bệnh nhân chờ tiếp đón (`PendingReceptionReview`), hóa đơn viện phí chưa thanh toán (`RemainingAmount > 0`), phiên chat tư vấn trực tuyến.
   - Tạo biên bản bàn giao và yêu cầu đồng nghiệp ca kế tiếp tiếp nhận xác nhận (`Acknowledged`); ngăn chặn người tạo tự tiếp nhận biên bản của chính mình.

### 3.2. Cơ Chế Khóa Ứng Dụng Phân Tán (Distributed Locks & Dual Owner Mode)
Cải tiến `SqlAppLockHelper` hỗ trợ chế độ kép:
- **Transaction-scoped (`@LockOwner = 'Transaction'`):** Tự động phát hiện transaction hiện có (`CurrentTransaction != null`). Giữ khóa xuyên suốt transaction và tự động giải phóng khi transaction Commit hoặc Rollback.
- **Session-scoped (`@LockOwner = 'Session'`):** Dùng khi không có transaction. Trả về `SessionLockReleaser` tự động gọi `sp_releaseapplock @LockOwner = 'Session'` khi dispose.
- **Các khóa canonical:**
  - `Lock_Receptionist_Coverage_{yyyyMMdd}`: Khóa bảo vệ Rule 1 & Rule 2 theo ngày.
  - `Lock_Receptionist_{userId}_{yyyyMMdd}`: Khóa ca trực của nhân viên theo ngày.
  - `Lock_ReceptionistLeave_{leaveId}`: Khóa đơn xin nghỉ phép.
  - `Lock_ReceptionistSwap_{swapId}`: Khóa yêu cầu đổi ca trực.
  - `Lock_Receptionist_LeaveCreate_{userId}`: Khóa chống double-click gửi đơn nghỉ.

---

## 4. KẾT QUẢ KIỂM THỬ THỰC TẾ & BẢO ĐẢM CHẤT LƯỢNG

### 4.1. SQL Server Concurrency Integration Tests (Real SQL Server QA Database)
Thực thi trên cơ sở dữ liệu `DentalCare_ReceptionistShift_Dev` tại `.\SQLEXPRESS`:
```
  Passed Case1_ConcurrentLeaveApproval_GuaranteesAtMostTwoApprovedLeavesOnSqlServer [86 ms]
  Passed Case2_LeaveApprovalVsShiftSwap_MaintainsCoverageAndNoCorruptedSchedules [132 ms]
  Passed Case3_LeaveApprovalVsBulkScheduling_ProtectsApprovedLeaveAndNoOrphanShifts [1 s]
  Passed Case4_DuplicateLeaveSubmission_PreventsDuplicateRowsUnderConcurrentCalls [78 ms]

Test Run Successful. Total tests: 4. Passed: 4. Failed: 0. Duration: 2.47s.
```
- **Case 1 (Concurrent Leave Approval):** Đã có A được duyệt nghỉ. Admin 1 và Admin 2 cùng duyệt đơn cho B và C đồng thời. Khóa `Lock_Receptionist_Coverage_20261120` kích hoạt, chính xác 1 đơn được duyệt và 1 đơn bị từ chối (Rule 1). Tổng số lễ tân nghỉ được duyệt trên DB là đúng 2 người.
- **Case 2 (Leave Approval vs Shift Swap):** Admin 1 duyệt đổi ca cho B và D; Admin 2 duyệt đơn nghỉ cho C. Dữ liệu ca trực sau hoán đổi nguyên tử, không có bất kỳ ca trực nào bị trùng giờ cho cùng một lễ tân.
- **Case 3 (Leave Approval vs Bulk Scheduling):** Admin 1 duyệt đơn nghỉ của B, Admin 2 chạy phân ca hàng loạt cho B và C. B được bảo vệ an toàn không bị gán ca đè lên ngày đã duyệt nghỉ.
- **Case 4 (Duplicate Leave Submission):** 2 request gửi đơn nghỉ cùng 1 mili-giây từ cùng 1 lễ tân. Chính xác 1 request thành công, 1 request bị chặn với thông báo đơn đang chờ duyệt.

### 4.2. Playwright Browser E2E Test Suite (Chạy Trực Tiếp Với Live FE & BE)
Máy chủ Web (`https://localhost:7129`) và API (`https://localhost:7350`) khởi động và kết nối QA DB:
```
Running 6 tests using 1 worker

  ok 1 [chromium] › receptionist-workforce.spec.js:16:3 › E2E-REC-1: Admin Receptionist Scheduling Console - 5 Tabs & Monthly Grid (2.1s)
  ok 2 [chromium] › receptionist-workforce.spec.js:48:3 › E2E-REC-2: Receptionist Personal Schedule - Today Shift & Emergency Absence Modal (1.4s)
  ok 3 [chromium] › receptionist-workforce.spec.js:75:3 › E2E-REC-3: Receptionist Leave Management - Stat Cards, Rules Banner & Leave Creation Modal (1.7s)
  ok 4 [chromium] › receptionist-workforce.spec.js:112:3 › E2E-REC-4: Receptionist Shift Swap - Outgoing & History Tabs & Create Swap Modal (1.6s)
  ok 5 [chromium] › receptionist-workforce.spec.js:139:3 › E2E-REC-5: Receptionist Work Handover - Live Queue Counters & Handover Modal (2.4s)
  ok 6 [chromium] › receptionist-workforce.spec.js:166:3 › E2E-REC-6: Sidebar Navigation Links - Receptionist & Admin Group Verification (1.9s)

  6 passed (12.0s)
```

### 4.3. Tổng Kết Số Lượng Test Cases Toàn Hệ Thống

| Dự Án Kiểm Thử | Tổng Số Tests | Passed | Failed | Skipped | Tỷ Lệ Đạt |
|---|---|---|---|---|---|
| **Backend Unit & Integration (`DentalClinic.Tests.csproj`)** | 809 | **808** | 0 | 1* | **100%** |
| *Trong đó: Phân hệ Receptionist Workforce Tests* | 26 | **26** | 0 | 0 | **100%** |
| *Trong đó: Real SQL Server Concurrency Tests* | 4 | **4** | 0 | 0 | **100%** |
| **Frontend Razor & MVC (`DentalClinic.Web.Tests.csproj`)** | 315 | **315** | 0 | 0 | **100%** |
| **Playwright Browser E2E (`e2e/receptionist-workforce.spec.js`)** | 6 | **6** | 0 | 0 | **100%** |
| **TỔNG TOÀN HỆ THỐNG** | **1,130** | **1,129** | **0** | **1\*** | **100%** |

*\* Ghi chú minh bạch: 1 test skipped duy nhất là `UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully` do môi trường CI/local chưa cấu hình Cloudinary API secret.*

### 4.4. Cảnh Báo Trình Biên Dịch (Compiler Diagnostics)
- **Backend (`DentalClinic.Backend.sln`):** **0 Error(s), 0 Warning(s)**
- **Frontend (`DentalClinic.Web.sln`):** **0 Error(s), 0 Warning(s)**

---

## 5. BÁO CÁO TÍNH SẴN SÀNG TÍCH HỢP (INTEGRATION READINESS STATEMENT)

1. **Độc lập tuyệt đối với phân hệ Bác sĩ (Dentist Leave & Recovery):**
   - Phân hệ Receptionist hoàn toàn độc lập về schema (các bảng đều có tiền tố `Receptionist`), không chia sẻ bảng dữ liệu hay model entity với Dentist.
   - Không đụng chạm vào worktree `DentalCare-Leave` hoặc nhánh `feature/dentist-leave-appointment-recovery`.
2. **Đã đồng bộ commit mới nhất từ `origin/develop`:**
   - Đã fetch và merge sạch (clean merge) commit mới nhất `d4e6cfc` từ `origin/develop` vào `feature/receptionist-shift-leave-management`.
3. **An toàn cơ sở dữ liệu:**
   - Script forward migration và rollback migration đã được thẩm định an toàn, hỗ trợ nâng cấp và hạ cấp không mất mát dữ liệu của các phân hệ khác.
4. **Nhánh sẵn sàng tạo Pull Request:**
   - Nhánh `feature/receptionist-shift-leave-management` đạt trạng thái **100% GREEN, 0 WARNING, PRODUCTION READY**.
