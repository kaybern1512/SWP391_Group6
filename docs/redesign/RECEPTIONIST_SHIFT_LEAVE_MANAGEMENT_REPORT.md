# BÁO CÁO KỸ THUẬT: PHÂN HỆ QUẢN LÝ LỊCH TRỰC, NGHỈ PHÉP & ĐIỀU PHỐI NHÂN SỰ LỄ TÂN (RECEPTIONIST SHIFT, LEAVE & WORKFORCE MANAGEMENT)

**Dự án:** DentalCare – Multi-Specialty Clinic Management System  
**Phân hệ:** Receptionist Shift, Leave & Workforce Management  
**Nhánh Git:** `feature/receptionist-shift-leave-management`  
**Cơ sở dữ liệu:** `DentalCare_ReceptionistShift_Dev` (SQL Server)  
**Ngày hoàn thành:** 10/11/2026  
**Kiến trúc sư & Kỹ sư trưởng:** Senior ASP.NET Core 8 Full-stack Architect & SQL Server Database Engineer  

---

## 1. TỔNG QUAN YÊU CẦU & BỐI CẢNH DỰ ÁN

Phân hệ **Receptionist Shift, Leave & Workforce Management** được thiết kế nhằm quản lý toàn diện lịch làm việc, phân bổ ca trực theo tháng, quy trình xin và xét duyệt nghỉ phép theo quy tắc kiểm soát nghiêm ngặt, cơ chế đổi ca giữa các lễ tân, xử lý vắng mặt khẩn cấp và duy trì tính liên tục của bàn tiếp đón (check-in queue, billing, live chat) thông qua biên bản bàn giao ca.

### Các Nguyên Tắc Thiết Kế Trọng Tâm
1. **Cô lập dữ liệu hoàn toàn (Zero Conflict Isolation):** Phân hệ lễ tân sử dụng hệ thống bảng riêng biệt (`ReceptionistWorkSchedules`, `ReceptionistShiftTemplates`, `ReceptionistLeaveRequests`, `ReceptionistLeaveRequestPeriods`, `ReceptionistShiftSwapRequests`, `ReceptionistCoverageIncidents`, `ReceptionistWorkHandovers`), hoàn toàn độc lập với phân hệ bác sĩ (`DentistSchedule`, `DentistLeave`) để không gây migration collision hoặc race condition.
2. **Quy tắc 1 (RULE 1 – Maximum 2 Approved Leaves/Day):** Mỗi ngày dương lịch chỉ cho phép tối đa 2 nhân viên lễ tân khác nhau (`Distinct ReceptionistUserId`) được phê duyệt nghỉ phép trên toàn hệ thống phòng khám.
3. **Quy tắc 2 (RULE 2 – Minimum 2 Available Receptionists/Shift):** Mỗi ca trực và tại mọi phân đoạn giờ con (`sub-intervals`) phải luôn có tối thiểu 2 lễ tân có mặt làm việc thực tế sau khi đã trừ đi các ca nghỉ phép hoặc vắng mặt.
4. **An toàn tương tranh cao (High Concurrency & Atomic Locking):** Sử dụng `sp_getapplock` thông qua `SqlAppLockHelper` và giao dịch nguyên tử (`IDbContextTransaction`) để ngăn chặn tuyệt đối tình trạng race condition khi 2 quản trị viên cùng phê duyệt đơn nghỉ vượt trần hoặc đổi ca cùng thời điểm.
5. **Tính liên tục của dịch vụ (Service Continuity & Handover):** Bàn giao hàng đợi tiếp đón (pending check-in appointments), danh sách viện phí chưa thu và các phiên tư vấn trực tuyến giữa các ca trực nhằm đảm bảo bệnh nhân không bị bỏ sót khi giao ca.

---

## 2. THIẾT KẾ CƠ SỞ DỮ LIỆU & SCHEMA MIGRATIONS

### 2.1. Cấu trúc 7 Bảng Chuyên Biệt

| Tên Bảng | Mục Đích Lưu Trữ | Khóa Ngoại & Ràng Buộc |
|---|---|---|
| `ReceptionistShiftTemplates` | Mẫu ca trực chuẩn (Ca Sáng 07:00-13:00, Ca Chiều 13:00-19:00, Ca Cả Ngày 07:00-19:00). | `ShiftTemplateId` (PK), `MinStaffRequired >= 2`. |
| `ReceptionistWorkSchedules` | Lịch phân ca thực tế theo ngày của từng lễ tân. | FK `ReceptionistUserId` &rarr; `UserAccounts`, FK `ShiftTemplateId`. Trạng thái: `Scheduled`, `Completed`, `Cancelled`, `AbsenceReported`. |
| `ReceptionistLeaveRequests` | Đơn xin nghỉ phép của lễ tân (FullDay, HalfDayMorning, HalfDayAfternoon, Hourly, MultiDay). | FK `ReceptionistUserId`, FK `ApprovedBy`. Trạng thái: `Pending`, `Approved`, `Rejected`, `Withdrawn`. |
| `ReceptionistLeaveRequestPeriods` | Chi tiết phân rã từng ngày trong đơn nghỉ phép nhiều ngày. | FK `LeaveRequestId` ON DELETE CASCADE, Unique (`LeaveRequestId`, `LeaveDate`). |
| `ReceptionistShiftSwapRequests` | Yêu cầu đổi ca giữa 2 lễ tân và quyết định của Admin. | FK `RequestingReceptionistUserId`, FK `TargetReceptionistUserId`, FK `SourceScheduleId`, FK `TargetScheduleId`. |
| `ReceptionistCoverageIncidents` | Sự cố vắng mặt đột xuất và hồ sơ điều phối bổ sung nhân sự. | FK `AffectedReceptionistUserId`, FK `ReplacementReceptionistUserId`, FK `ReportedBy`, FK `ResolvedBy`. |
| `ReceptionistWorkHandovers` | Biên bản bàn giao công việc ca trực (lịch hẹn chờ, viện phí tồn, tin nhắn chat). | FK `FromReceptionistUserId`, FK `ToReceptionistUserId`. Trạng thái: `Transferred`, `Acknowledged`. |

### 2.2. Migration Scripts & Rollback Scripts
- **Forward Script:** `DB/redesign_migrations/20261110_Receptionist_Shift_Leave_Workforce_Schema.sql`
- **Rollback Script:** `DB/redesign_migrations/20261110_Receptionist_Shift_Leave_Workforce_Schema_Rollback.sql`
- Cả hai script đều hỗ trợ tính idempotent (`IF NOT EXISTS`, `IF EXISTS`), tạo đầy đủ foreign keys, indexes tối ưu và seed sẵn 3 mẫu ca trực tiêu chuẩn bằng tiếng Việt Unicode chuẩn.

---

## 3. KIẾN TRÚC BACKEND & BUSINESS RULES ENGINE

### 3.1. Các Dịch Vụ Cốt Lõi (Core Services)
1. **`ReceptionistScheduleService` (`IReceptionistScheduleService`):**
   - Quản trị lưới lịch trực tháng (`MonthlyReceptionistScheduleDto`) gom nhóm theo ngày, hiển thị trạng thái phân ca, số lễ tân nghỉ đã duyệt/chờ duyệt, sự cố nhân lực.
   - Tạo ca trực đơn lẻ, cập nhật, xóa ca với ràng buộc giờ bắt đầu < giờ kết thúc.
   - **Phân ca hàng loạt (Bulk Apply):** Tạo ca cho danh sách lễ tân theo khoảng ngày và các thứ trong tuần (Thứ 2 - Chủ Nhật); hỗ trợ cờ `OverwriteExisting` ghi đè ca cũ một cách an toàn.
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
   - Phê duyệt nguyên tử (All-or-Nothing): Đối với đơn nghỉ nhiều ngày (`MultiDay`), hệ thống kiểm tra Rule 1 và Rule 2 cho từng ngày. Nếu bất kỳ ngày nào vi phạm, toàn bộ đơn bị từ chối và rollback giao dịch.

4. **`ReceptionistShiftSwapService` (`IReceptionistShiftSwapService`):**
   - Quy trình đổi ca 3 bước:
     1. Lễ tân A đề xuất đổi ca cho Lễ tân B (`PendingCounterparty`).
     2. Lễ tân B phản hồi: Đồng ý &rarr; Chuyển sang `PendingAdminApproval`; Từ chối &rarr; Chuyển sang `Declined`.
     3. Quản trị viên phê duyệt (`Approved`): Hệ thống thực hiện hoán đổi nguyên tử `ReceptionistUserId` trên các bản ghi `ReceptionistWorkSchedule`.

5. **`ReceptionistHandoverService` (`IReceptionistHandoverService`):**
   - Tổng hợp tức thời hàng đợi bệnh nhân chờ tiếp đón (`PendingReceptionReview`), hóa đơn viện phí chưa thanh toán (`RemainingAmount > 0`), phiên chat tư vấn trực tuyến.
   - Tạo biên bản bàn giao và yêu cầu đồng nghiệp ca kế tiếp tiếp nhận xác nhận (`Acknowledged`); ngăn chặn người tạo tự tiếp nhận biên bản của chính mình.

### 3.2. Cơ Chế Khóa Ứng Dụng (Distributed Application Locks)
Để bảo đảm an toàn tương tranh cao giữa các luồng xử trị, hệ thống sử dụng các khóa ứng dụng chuẩn qua `SqlAppLockHelper`:
- `Lock_Receptionist_Coverage_{yyyyMMdd}`: Khóa ngày kiểm tra quy tắc nhân sự và duyệt nghỉ phép.
- `Lock_Receptionist_{userId}_{yyyyMMdd}`: Khóa ca trực của một nhân viên trong ngày.
- `Lock_ReceptionistLeave_{leaveId}`: Khóa trạng thái đơn xin nghỉ phép.
- `Lock_ReceptionistSwap_{swapId}`: Khóa quy trình đổi ca trực.

---

## 4. GIAO DIỆN NGƯỜI DÙNG (FRONTEND MVC & RAZOR VIEWS)

### 4.1. Bảng điều khiển Quản trị viên (Admin Receptionist Console)
- **Tuyến đường:** `/Admin/ReceptionistSchedule`
- **Bộ điều khiển:** `AdminReceptionistScheduleController`
- **Giao diện:** `FE/DentalClinic.Web/Views/Admin/ReceptionistSchedule/Index.cshtml`
- **5 Tab tính năng:**
  1. *Lịch Làm Việc Theo Tháng:* Lưới lịch ma trận trực quan, bộ lọc nhân sự, nút phân ca đơn, sao chép tháng, ghi đè ngày.
  2. *Phân Ca Hàng Loạt:* Form gán ca theo khoảng ngày, chọn thứ trong tuần, xem trước (`Preview`) và áp dụng (`Apply`).
  3. *Duyệt Đơn Nghỉ Phép:* Danh sách đơn nghỉ kèm nút xem trước tác động nhân sự (`Preview Coverage Impact`), phê duyệt, từ chối kèm lý do.
  4. *Yêu Cầu Đổi Ca:* Danh sách đề xuất đổi ca đã được đồng nghiệp đồng thuận, nút duyệt đổi ca nguyên tử.
  5. *Giám Sát Nhân Lực & Sự Cố:* Theo dõi ca thiếu người, nút điều phối lễ tân thay thế giải quyết sự cố.

### 4.2. Không gian làm việc Lễ tân (Receptionist Workforce Portal)
1. **Lịch làm việc của tôi (`/Receptionist/Schedule`):** Hiển thị thẻ ca trực hôm nay, nút báo vắng mặt khẩn cấp mở modal chuyên dụng, bảng các ca trực sắp tới.
2. **Đơn xin nghỉ phép (`/Receptionist/Leave`):** Thẻ thống kê 4 chỉ số (Tổng đơn, Chờ duyệt, Đã duyệt, Bị từ chối), banner lưu ý Quy tắc 1 & Quy tắc 2, modal tạo đơn xin nghỉ linh hoạt theo 5 hình thức, thao tác rút đơn khi còn pending.
3. **Đổi ca trực (`/Receptionist/ShiftSwap`):** Bảng yêu cầu cần phản hồi từ đồng nghiệp (Đồng ý/Từ chối kèm lời nhắn), tab yêu cầu đã gửi (có nút rút đơn), tab lịch sử đổi ca.
4. **Bàn giao ca trực (`/Receptionist/Handover`):** Thẻ theo dõi snapshot hàng đợi tiếp đón, hóa đơn chờ thu viện phí, chat tư vấn; form lập biên bản bàn giao và bảng tiếp nhận ca.
5. **Thanh điều hướng (`_Sidebar.cshtml`):** Tích hợp phân nhóm điều hướng mới *"Ca Trực & Nhân Sự"* dành cho Lễ tân và link *"Lịch Trực Lễ Tân & Nhân Sự"* dành cho Quản trị viên.

---

## 5. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG & BẢO ĐẢM CHẤT LƯỢNG

Hệ thống đã triển khai đầy đủ các tầng kiểm thử:

### 5.1. Kết Quả xUnit Backend Test Suite
- **Thư mục kiểm thử:** `BE/DentalClinic.Tests/ReceptionistWorkforce/`
  1. `ReceptionistWorkforceSchedulingTests.cs`: 6 tests (Tạo ca, kiểm tra giờ, preview/apply hàng loạt, ghi đè ca, copy tháng, daily override).
  2. `ReceptionistLeaveRuleAndCoverageTests.cs`: 6 tests (Rule 1 max 2 distinct leaves, cùng user 2 period tính 1, Rule 2 min 2 staff, kiểm tra sub-interval giờ, rút đơn, từ chối đơn).
  3. `ReceptionistShiftSwapTests.cs`: 3 tests (Quy trình propose -> accept -> approve hoán đổi lịch, từ chối đổi ca, chặn người ngoài phản hồi).
  4. `ReceptionistEmergencyAbsenceAndIncidentTests.cs`: 3 tests (Báo vắng khẩn cấp khi thiếu người, giải quyết sự cố điều phối thay thế, phát hiện understaffing).
  5. `ReceptionistWorkHandoverTests.cs`: 2 tests (Tổng hợp số liệu hàng đợi thực tế, vòng đời bàn giao và chặn tự tiếp nhận).
  6. `ReceptionistConcurrencyAndLockingTests.cs`: 2 tests (Định dạng khóa `SqlAppLockHelper`, mô phỏng 2 luồng duyệt đơn song song tranh chấp chỉ 1 luồng được duyệt).
- **Kết quả chạy kiểm thử:** **22/22 tests PASSED (100%)**.
- **Toàn bộ Backend Suite:** **804/804 tests PASSED (100%)**.

### 5.2. Kết Quả Frontend Unit Test Suite
- **Dự án:** `FE/DentalClinic.Web.Tests/DentalClinic.Web.Tests.csproj`
- **Kết quả chạy kiểm thử:** **315/315 tests PASSED (100%)**.

### 5.3. Playwright Browser E2E Test Suite
- **File kịch bản:** `e2e/receptionist-workforce.spec.js`
- **Kịch bản bao phủ:**
  - `E2E-REC-1`: Admin điều hướng console 5 tab và lưới lịch tháng.
  - `E2E-REC-2`: Lễ tân xem lịch cá nhân và kích hoạt modal báo vắng khẩn cấp.
  - `E2E-REC-3`: Lễ tân tạo đơn nghỉ phép với 5 hình thức và thông điệp Quy tắc 1 & 2.
  - `E2E-REC-4`: Lễ tân tạo đề xuất đổi ca trực với đồng nghiệp.
  - `E2E-REC-5`: Lễ tân xem snapshot hàng đợi tiếp đón & lập biên bản bàn giao ca.
  - `E2E-REC-6`: Kiểm tra tính toàn vẹn của menu sidebar cho cả 2 role Admin và Lễ tân.

### 5.4. Trạng Thái Cảnh Báo Trình Biên Dịch (Compiler Diagnostics)
- **Backend (`DentalClinic.Backend.sln`):** **0 Error(s), 0 Warning(s)**
- **Frontend (`DentalClinic.Web.sln`):** **0 Error(s), 0 Warning(s)**

---

## 6. HƯỚNG DẪN TRIỂN KHAI & BẬT TÍNH NĂNG

1. **Chạy Migration SQL trên môi trường đích:**
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d DentalCare_Integration_Dev -i DB\redesign_migrations\20261110_Receptionist_Shift_Leave_Workforce_Schema.sql
   ```
2. **Build và Khởi Động Backend & Frontend:**
   ```powershell
   dotnet build BE/DentalClinic.Backend.sln
   dotnet build FE/DentalClinic.Web.sln
   ```
3. **Truy Cập Quản Trị Viên:**
   - Đăng nhập bằng `admin@dentalcare.com` / `Password123@`
   - Vào menu: **Quản trị Danh mục & Lịch &rarr; Lịch Trực Lễ Tân & Nhân Sự** (`/Admin/ReceptionistSchedule`).
4. **Truy Cập Lễ Tân:**
   - Đăng nhập bằng `receptionist@dentalcare.com` / `Password123@`
   - Vào menu: **Ca Trực & Nhân Sự &rarr; Lịch Làm Việc Của Tôi** (`/Receptionist/Schedule`).

---

## 7. KẾT LUẬN

Phân hệ **Receptionist Shift, Leave & Workforce Management** đã được hiện thực hóa toàn diện từ tầng cơ sở dữ liệu, nghiệp vụ, API, giao diện web đến hệ thống kiểm thử tự động, tuân thủ nghiêm ngặt các quy tắc nghiệp vụ (Tối đa 2 người nghỉ/ngày, Tối thiểu 2 người trực/ca), bảo đảm độ tin cậy và hiệu năng cao trong vận hành thực tế phòng khám.
