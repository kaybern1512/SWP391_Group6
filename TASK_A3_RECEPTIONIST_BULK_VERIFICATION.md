# DENTALCARE SWP391 — TASK A.3 ADDENDUM: BULK RECEPTIONIST SCHEDULING WITH REAL DATABASE CONFLICT PREVIEW VERIFICATION REPORT

**Date:** 2026-11-16  
**Repository Worktree:** `D:\SWP391\DentalCare-Scheduling`  
**Branch:** `feature/fix-scheduling-demo`  
**Database Instance:** `.\SQLEXPRESS`  
**Database Name:** `DentalCare_SWP391_UAT_Fixture_TEST`  
**System Status:** **STABLE & PRODUCTION READY**  
**Automated Tests:** **698 / 698 PASS (100%)** (359 BE Tests + 339 FE Tests)  
**Browser QA Steps:** **8 / 8 PASS (100%)** with full screenshot evidence  

---

## 1. DANH SÁCH NHÂN SỰ THỰC TẾ ỨNG VỚI r1 / r2

Truy vấn từ cơ sở dữ liệu thực tế `DentalCare_SWP391_UAT_Fixture_TEST`:

```sql
SELECT u.UserId, u.Role, u.Email, u.Status, sp.FullName, sp.EmploymentStatus 
FROM UserAccounts u 
LEFT JOIN StaffProfiles sp ON u.UserId = sp.UserId 
WHERE u.Role = 'Receptionist' AND u.Status = 'Active';
```

| Alias | StaffUserId | Họ Và Tên | Email | Mã Nhân Viên | Vai Trò | Trạng Thái Tài Khoản |
| :---: | :---: | :--- | :--- | :---: | :---: | :---: |
| **r1** | `2` | **Trần Thị Lễ Tân** | `receptionist@dentalcare.test` | `EMP-2` | Receptionist | `Active` |
| **r2** | `10` | **Lễ Tân Demo 02** | `receptionist2@dentalcare.test` | `EMP-10` | Receptionist | `Active` |

*Ghi chú: Cả hai tài khoản đều là nhân viên lễ tân chính thức đang hoạt động trong hệ thống, không sử dụng ID giả lập.*

---

## 2. GIAO DIỆN MODAL PHÂN CÔNG HÀNG LOẠT (SECTIONS A — F)

Modal `#bulkReceptionistScheduleModal` được thiết kế đúng thứ tự và đáp ứng toàn bộ đặc tả:

- **Phần A — Chọn nhân sự:**
  - Danh sách checklist có ô tìm kiếm (`#bulkReceptionistSearch`) lọc nhanh theo tên/mã/email.
  - Hiển thị đầy đủ Họ tên, Mã nhân viên (`EMP-{id}`), Email, Badge trạng thái `Hoạt động`.
  - Nút "Chọn tất cả" / "Bỏ chọn".
  - Chỉ hiển thị nhân viên Lễ tân (không hiển thị Bác sĩ hay chuyên khoa).
- **Phần B — Khoảng thời gian:**
  - Từ ngày (`#bulkRecFromDate`, `min = today`) và Đến ngày (`#bulkRecToDate`).
  - Chỉ cho phép chọn ngày hôm nay trở đi (bảo vệ quá khứ).
- **Phần C — Các ngày áp dụng:**
  - Button group với các nút bấm trực quan: `T2 | T3 | T4 | T5 | T6 | T7 | CN`.
  - Mặc định chọn Thứ 2 tới Thứ 6 (T2–T6).
  - Mapping chính xác: Sunday=0, Monday=1, ... Saturday=6.
- **Phần D — Ca làm việc cố định:**
  - Hai switch/checkbox độc lập:
    - Ca Sáng: `08:00 – 12:00`
    - Ca Chiều: `13:30 – 17:30`
  - Không có input StartTime / EndTime tự do, bảo toàn chuẩn ca phòng khám.
- **Phần E — Tóm tắt dự kiến (Summary Card):**
  - Tự động tính toán và hiển thị theo thời gian thực (real-time recalculation):
    - **Nhân viên:** `2`
    - **Số ngày:** `5`
    - **Số ca / ngày:** `2`
    - **Tổng ca dự kiến:** `20` ca ($2 \times 5 \times 2 = 20$).
- **Phần F — Nút hành động:**
  - `Hủy` (đóng modal, không mất dữ liệu đã nhập).
  - `Kiểm tra xung đột` (`#btnBulkRecPreview`): gửi request đối chiếu database thật.
  - `Áp dụng các ca hợp lệ` (`#btnBulkRecApply`): chỉ bật khi Preview hoàn tất và có $\ge 1$ ca hợp lệ (`AvailableCount > 0`).
  - Mọi thao tác thay đổi input sau khi Preview đều lập tức vô hiệu hóa kết quả Preview cũ và tắt nút Áp dụng.

---

## 3. PREVIEW ĐỐI CHIẾU DATABASE THẬT & 5 THẺ TỔNG QUAN

Hệ thống Preview được xử lý 100% tại Backend (`ReceptionistScheduleService.PreviewBulkScheduleAsync`), đối chiếu trực tiếp với các bảng:
- `StaffWorkSchedules` (phát hiện ca trùng giờ hoặc chồng lấn thời gian).
- `StaffLeaveRequests` (phát hiện lịch nghỉ phép `Approved` và `Pending`).
- `UserAccounts` & `StaffProfiles` (kiểm tra trạng thái tài khoản active và vai trò Receptionist).

Mỗi ca trực được phân loại vào đúng 1 trong các trạng thái chuẩn:
- `Available`: có thể tạo mới hợp lệ.
- `AlreadyExists`: ca tương đương đã tồn tại trong database.
- `Conflict`: trùng hoặc chồng lấn với ca trực khác trong ngày.
- `OnApprovedLeave`: trùng lịch nghỉ phép đã duyệt (**bảo vệ tuyệt đối**).
- `PendingLeave`: trùng đơn xin nghỉ phép đang chờ duyệt.
- `PastOrStarted`: ca trực thuộc quá khứ hoặc đã bắt đầu trong ngày.
- `InvalidStaff`: tài khoản nhân viên không hợp lệ hoặc bị khóa.

### 5 Thẻ Tổng Quan & Bộ Lọc Xung Đột

1. **Tổng dự kiến** (`TotalProposedShifts`)
2. **Có thể thêm** (`AvailableCount`)
3. **Đã tồn tại** (`AlreadyExistsCount`)
4. **Bị chặn do nghỉ** (`LeaveBlockedCount` = Approved + Pending)
5. **Xung đột khác** (`OtherConflictCount` = Conflict + PastOrStarted + InvalidStaff)
- Switch filter: **"Chỉ xem ca bị xung đột / bỏ qua"** (`#chkFilterRecConflictsOnly`) cho phép lọc nhanh các ca không thể áp dụng để Admin kiểm tra nguyên nhân.

---

## 4. TỔNG SỐ CA DỰ KIẾN, HỢP LỆ, TRÙNG, NGHỈ TRÊN DỮ LIỆU THỰC TẾ

### 4.1. Kịch bản 1: Tập ngày chuẩn 23–27/11/2026 (Phát hiện ca đã tồn tại từ Database thật)

- **Cấu hình:** 2 nhân viên (r1, r2) × 5 ngày (23–27/11/2026, T2–T6) × 2 ca (Sáng, Chiều).
- **Kết quả Preview từ Database:**
  - Tổng ca dự kiến: **20**
  - Có thể thêm (`Available`): **0**
  - Đã tồn tại (`AlreadyExists`): **20**
  - Bị chặn do nghỉ (`OnApprovedLeave` / `PendingLeave`): **0**
  - Xung đột khác: **0**
- **Hành vi hệ thống:** Nút *"Áp Dụng Các Ca Hợp Lệ"* bị **vô hiệu hóa** (disabled), bảo vệ tuyệt đối hệ thống khỏi việc tạo bản ghi trùng lặp (idempotency guard).

### 4.2. Kịch bản 2: Tập ngày tương lai 07–11/12/2026 (Phát hiện nghỉ phép đã duyệt và tạo ca hợp lệ)

- **Thiết lập:** Lễ Tân Demo 02 (UserId 10) có lịch nghỉ phép đã duyệt (`Approved`, `LeaveRequestId = 6`) vào Thứ Tư ngày `2026-12-09`.
- **Cấu hình:** 2 nhân viên (r1, r2) × 5 ngày (07–11/12/2026, T2–T6) × 2 ca (Sáng, Chiều).
- **Kết quả Preview từ Database:**
  - Tổng ca dự kiến: **20**
  - Có thể thêm (`Available`): **18**
  - Đã tồn tại (`AlreadyExists`): **0**
  - Bị chặn do nghỉ (`LeaveBlocked`): **2** (UserId 10 bị chặn cả ca sáng và ca chiều ngày 09/12/2026 do trùng lịch nghỉ phép đã duyệt)
  - Xung đột khác: **0**
- **Hành vi áp dụng:**
  - Nút *"Áp Dụng Các Ca Hợp Lệ"* được **bật** (enabled).
  - Admin bấm áp dụng $\rightarrow$ Backend tạo đúng **18 ca hợp lệ mới**.
  - Bỏ qua đúng **2 ca nghỉ phép**.
  - Không tạo ca cho UserId 10 vào ngày 09/12/2026.

---

## 5. DANH SÁCH BẢN GHI ĐƯỢC TẠO TRÊN DATABASE VÀ KIỂM CHỨNG SQL

### 5.1. Kiểm tra tổng số ca tạo mới trong tuần 07–11/12/2026
```sql
SELECT COUNT(*) AS TotalCreatedDecShifts 
FROM StaffWorkSchedules 
WHERE StaffRole = 'Receptionist' 
  AND WorkDate >= '2026-12-07' AND WorkDate <= '2026-12-11';
```
**Kết quả:**
```
TotalCreatedDecShifts
---------------------
                   18
```
*(Đúng chính xác 18 ca, loại bỏ 2 ca trùng lịch nghỉ phép)*

### 5.2. Kiểm tra chi tiết ngày nghỉ phép 09/12/2026
```sql
-- Lễ Tân Demo 02 (UserId 10) trên ngày nghỉ 09/12/2026:
SELECT WorkScheduleId, StaffUserId, WorkDate, StartTime, EndTime, Status 
FROM StaffWorkSchedules 
WHERE StaffUserId = 10 AND WorkDate = '2026-12-09';
```
**Kết quả:**
```
(0 rows affected)
```
*(Số ca tạo cho UserId 10 trên ngày nghỉ phép = 0. Bảo vệ nghỉ phép 100%.)*

```sql
-- Trần Thị Lễ Tân (UserId 2) trên ngày 09/12/2026 (đi làm bình thường):
SELECT WorkScheduleId, StaffUserId, WorkDate, StartTime, EndTime, Status 
FROM StaffWorkSchedules 
WHERE StaffUserId = 2 AND WorkDate = '2026-12-09';
```
**Kết quả:**
```
WorkScheduleId  StaffUserId  WorkDate    StartTime         EndTime           Status
178             2            2026-12-09  08:00:00.0000000  12:00:00.0000000  Scheduled
179             2            2026-12-09  13:30:00.0000000  17:30:00.0000000  Scheduled
```

---

## 6. BẰNG CHỨNG LỊCH NGHỈ PHÉP GIỮ NGUYÊN TRƯỚC VÀ SAU KHI APPLY

Kiểm tra trạng thái bản ghi đơn nghỉ phép của Lễ Tân Demo 02 trước và sau toàn bộ thao tác:

```sql
SELECT LeaveRequestId, StaffUserId, StartDate, EndDate, LeaveType, Status, Reason 
FROM StaffLeaveRequests 
WHERE LeaveRequestId = 6;
```
**Kết quả sau khi Apply:**
```
LeaveRequestId  StaffUserId  StartDate   EndDate     LeaveType  Status    Reason
6               10           2026-12-09  2026-12-09  Annual     Approved  Nghỉ phép cá nhân đã duyệt
```

- Trạng thái vẫn là **`Approved`**.
- Không bị xóa, không bị hủy, không bị sửa ngày.
- Quá trình phân công hàng loạt tuân thủ nghiêm ngặt nguyên tắc chỉ đọc (`AsNoTracking`) đối với `StaffLeaveRequests`.

---

## 7. BẰNG CHỨNG SCREENSHOT TRÌNH DUYỆT (8 ẢNH CHỤP E2E)

Thư mục lưu trữ bằng chứng:  
`C:\Users\ASUS\.gemini\antigravity\brain\3f2a7316-c8f2-45b9-97c9-e38ffdd8b62a\browser_evidence\task_a3_addendum\`

| STT | Tên Tệp Ảnh | Dung Lượng | Mô Tả & Hành Vi Quan Sát Thực Tế |
| :---: | :--- | :---: | :--- |
| **01** | `01_receptionist_bulk_form_standard_config.png` | 173 KB | Form phân công hàng loạt với đầy đủ Phần A (chọn r1, r2), Phần B (23–27/11/2026), Phần C (nút bấm T2–T6), Phần D (Ca Sáng, Ca Chiều), và Phần E (Tóm tắt: 2 NV, 5 ngày, 2 ca/ngày, 20 ca dự kiến). |
| **02** | `02_receptionist_bulk_preview_real_db_conflicts.png` | 183 KB | Kết quả Preview đối chiếu DB thật tuần 23–27/11/2026: 5 thẻ tổng quan (20 dự kiến, 0 có thể thêm, 20 đã tồn tại), bảng liệt kê 20 ca `AlreadyExists` ("Ca tương đương đã tồn tại"), nút Áp dụng bị disabled. |
| **03** | `03_receptionist_bulk_filter_conflicts_only.png` | 183 KB | Bật công tắc *"Chỉ xem ca bị xung đột / bỏ qua"*, bảng lọc và hiển thị danh sách các ca trùng lặp. |
| **04** | `04_receptionist_bulk_future_range_with_leave_preview.png` | 206 KB | Chọn tuần 07–11/12/2026: Preview phát hiện 18 ca hợp lệ và 2 ca bị chặn do nghỉ phép của Lễ Tân Demo 02 vào ngày 09/12/2026. Nút Áp dụng được bật. |
| **05** | `05_receptionist_bulk_apply_success_alert.png` | 210 KB | Nhấp Áp dụng ca hợp lệ: hiển thị thông báo thành công chi tiết *"Đã áp dụng thành công 18 ca trực mới cho lễ tân (Đã có sẵn: 0, Nghỉ phép: 2, Xung đột khác: 0)"*. |
| **06** | `06_receptionist_weekly_view_updated_december.png` | 221 KB | Lịch tuần ngày 07–11/12/2026 cập nhật đầy đủ các ca trực mới. Thứ 4 ngày 09/12 hiển thị 1 người trực (Trần Thị Lễ Tân), các ngày khác 2 người trực. |
| **07** | `07_receptionist_drawer_details_december.png` | 213 KB | Nhấp vào ca sáng Thứ 2 ngày 07/12/2026 mở drawer `#shiftDetailsDrawer`, hiển thị chi tiết danh sách 2 nhân viên được phân công: Trần Thị Lễ Tân và Lễ Tân Demo 02. |
| **08** | `08_receptionist_monthly_view_december.png` | 208 KB | Lịch tháng 12/2026 hiển thị các ca trực mới trên lưới 7 cột, ngày 09/12 có badge nghỉ phép "1 nghỉ". |

---

## 8. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (.NET 8)

```bash
# Backend Test Suite
dotnet test D:\SWP391\DentalCare-Scheduling\BE\DentalClinic.Tests\DentalClinic.Tests.csproj --no-restore
# Passed: 359, Failed: 0, Skipped: 0 (100% PASS)

# Frontend Test Suite
dotnet test D:\SWP391\DentalCare-Scheduling\FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj --no-restore
# Passed: 339, Failed: 0, Skipped: 0 (100% PASS)
```

$$\text{Tổng số Test} = 359 + 339 = \mathbf{698 \text{ PASS (100\%)}}$$

### Các bài test trọng tâm bổ sung trong `ReceptionistSafeBulkSchedulingTests`:
- `RECBULK14_StandardConfig_2Staff_5Days_2Shifts_Calculates20ProposedShifts`: Kiểm tra đúng 20 ca dự kiến và tạo đủ 20 ca khi không có xung đột.
- `RECBULK15_RealDatabaseConflictBreakdown_5SummaryCards`: Kiểm tra chính xác 5 thẻ tổng quan khi có ca đã tồn tại, đơn nghỉ phép đã duyệt và đơn nghỉ phép chờ duyệt.
- `RECBULK16_ConcurrentLeaveApprovedBetweenPreviewAndApply_IsProtected`: Kiểm tra an toàn tương tranh khi đơn nghỉ phép được duyệt giữa lúc Preview và Apply.
- `RECBULK17_NegativeTests_ValidationGuards`: Kiểm tra danh sách nhân viên rỗng, khoảng ngày không hợp lệ (ToDate < FromDate), danh sách thứ trong tuần rỗng.
- `RECBULK18_InvalidStaff_LockedOrNonReceptionist_FlaggedAsInvalidStaff`: Kiểm tra tài khoản bị khóa hoặc sai vai trò bị gắn cờ `InvalidStaff`.
- `RECBULK19_Idempotent_ApplyTwice_ZeroDuplicatesCreated`: Kiểm tra tính lũy kế (idempotency), bấm Áp dụng 2 lần không sinh thêm ca trùng lặp.

---

## 9. DANH SÁCH CÁC TỆP ĐÃ SỬA ĐỔI

1. `BE/DentalClinic.Application/Features/ReceptionistWorkforce/DTOs/ReceptionistWorkforceDtos.cs`
   - Bổ sung 5 thẻ tổng quan và các trạng thái chi tiết vào `ReceptionistBulkSchedulePreviewDto`.
   - Tạo mới `ReceptionistBulkApplyResultDto` với `RequestedCount`, `CreatedCount`, `AlreadyExistsCount`, `SkippedLeaveCount`, `SkippedConflictCount`.
2. `FE/DentalClinic.Web/Models/ApiDtos/ReceptionistWorkforceDtos.cs`
   - Đồng bộ hoàn toàn các thuộc tính và DTO từ Backend sang Frontend.
3. `BE/DentalClinic.Application/Common/Interfaces/IReceptionistScheduleService.cs`
   - Cập nhật chữ ký hàm `ApplyBulkScheduleAsync` trả về `ApiResponse<ReceptionistBulkApplyResultDto>`.
4. `BE/DentalClinic.Infrastructure/Services/ReceptionistScheduleService.cs`
   - Triển khai logic tính toán 5 thẻ tổng quan từ database thật trong `PreviewBulkScheduleAsync`.
   - Bổ sung xác thực tài khoản nhân viên (Active & Receptionist).
   - Triển khai `ApplyBulkScheduleAsync` với concurrency lock, re-query bảo vệ nghỉ phép và trả về DTO kết quả chi tiết.
5. `BE/DentalClinic.Api/Controllers/AdminReceptionistScheduleController.cs`
   - Cập nhật endpoint `bulk-apply` trả về `ApiResponse<ReceptionistBulkApplyResultDto>`.
6. `FE/DentalClinic.Web/Services/IReceptionistWorkforceApiService.cs` & `ReceptionistWorkforceApiService.cs`
   - Cập nhật `ApplyBulkScheduleAsync` trả về `ApiResult<ReceptionistBulkApplyResultDto>`.
7. `FE/DentalClinic.Web/Views/Admin/ReceptionistSchedule/Index.cshtml`
   - Tái cấu trúc modal `#bulkReceptionistScheduleModal` theo đúng 6 phần A–F.
   - Thêm Phần E: Thẻ Tóm tắt dự kiến tự động tính toán số ca theo thời gian thực.
   - Bổ sung 5 thẻ tổng quan và công tắc lọc ca xung đột.
   - Nâng cấp JavaScript xử lý Preview thật, lọc bảng và thông báo kết quả chi tiết sau khi áp dụng.
8. `BE/DentalClinic.Tests/ReceptionistWorkforce/ReceptionistSafeBulkSchedulingTests.cs`
   - Bổ sung 6 unit tests bao phủ toàn bộ các kịch bản chuẩn và negative test của Addendum.

---

## 10. TÌNH TRẠNG GIT VÀ BLOCKERS

- **Branch hiện tại:** `feature/fix-scheduling-demo`
- **Working Tree:** Sạch sẽ, không còn uncommitted changes.
- **Tuân thủ quy định:**
  - Không sửa Task B (Clinical, Billing, Booking, Medicines, X-ray).
  - Không sửa database schema.
  - Không merge vào `develop`.
  - Không push vào `main`.
- **Blockers:** **NONE**. Toàn bộ tính năng phân công hàng loạt lễ tân với Preview database thật và bảo vệ nghỉ phép đã hoàn thành 100%.
