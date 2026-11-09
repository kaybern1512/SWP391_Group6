# BÁO CÁO TRIỂN KHAI HỆ THỐNG PHÂN CÔNG LỊCH TRỰC THEO THÁNG (MONTHLY DENTIST WORK SCHEDULING)

**Người thực hiện:** Senior ASP.NET Core 8 Full-stack Engineer / System Administrator Module Owner (Anti 1)  
**Nhánh Git:** `feature/admin-monthly-scheduling`  
**Base commit:** `0294de5` (HEAD of `feature/admin-redesign`)  
**Workspace:** `D:\SWP391\DentalCare-Admin`  
**Backend:** `https://localhost:7351`  
**Frontend:** `https://localhost:7121`  
**Database:** `DentalCare_Admin_Dev` (SQL Server `DESKTOP-1TUBGEU\SQLEXPRESS`)  
**Thời gian hoàn thành:** 2026-11-09  

---

## 1. Tóm Tắt Kết Quả & Nghiệp Vụ Hoàn Thành

Chức năng Quản lý Lịch trực Bác sĩ của phân hệ `SystemAdministrator` đã được nâng cấp toàn diện thành **HỆ THỐNG PHÂN CÔNG LỊCH TRỰC THEO THÁNG**, đáp ứng 100% các tiêu chí:

| Tiêu chí | Trạng thái | Chi tiết triển khai |
| :--- | :---: | :--- |
| **Tái sử dụng Schema hiện có** | Hoàn thành | Tái sử dụng bảng `DentistWorkSchedules` (`WorkDate` kiểu `date`, `StartTime`, `EndTime`). Không cần migration thay đổi schema database. |
| **Lưới Lịch Trực Tháng (Monthly Grid)** | Hoàn thành | Giao diện 7 cột trực quan (T2-CN), hiển thị thẻ ngày, trạng thái làm việc, ca sáng/chiều/tối/cả ngày, ngày nghỉ phép và số lịch hẹn đã xác nhận. |
| **Thao tác Hàng loạt (Bulk Scheduling)** | Hoàn thành | Phân công cùng lúc nhiều bác sĩ vào chuyên khoa, chọn khoảng ngày, chọn thứ trong tuần (T2-T6, T7, CN), chọn nhiều ca trong ngày, hỗ trợ Preset ca. |
| **Kiểm tra Trước & Báo cáo Xung đột (Preview)** | Hoàn thành | Kiểm tra trước tính hợp lệ, phát hiện ca trùng lặp (Duplicate), ca giao thoa (Overlap), trùng lịch nghỉ phép (LeaveConflict), sai chuyên khoa (DepartmentMismatch), vượt giờ mở cửa khoa. |
| **Sao chép Lịch từ Tháng Trước (Copy Month)** | Hoàn thành | Hỗ trợ 2 chế độ: **Theo thứ trong tuần (DayOfWeek Pattern)** và **Trùng ngày chính xác (Exact Day)**. Tự động xử lý tháng 28, 29, 30, 31 ngày và loại trừ ngày nghỉ của bác sĩ. |
| **Chỉnh sửa Ca Riêng Lẻ Trong Ngày (Daily Override)** | Hoàn thành | Cho phép thay đổi khung giờ, thêm/bớt ca hoặc chuyển thành ngày nghỉ (kèm lý do) cho từng ngày cụ thể của bác sĩ. |
| **Bảo vệ Lịch Hẹn (Appointment Guard)** | Hoàn thành | Ngăn chặn việc hủy hoặc dời ca trực làm mồ côi các cuộc hẹn của bệnh nhân đã xác nhận (`Confirmed`, `CheckedIn`, `Waiting`, `Called`, `InService`). Cảnh báo rõ ràng và trả về danh sách lịch hẹn cần điều phối. |
| **Đồng bộ Tự động với Booking (Single Source of Truth)** | Hoàn thành | `BookingService` đọc trực tiếp từ `DentistWorkSchedules`. Các slot đặt lịch của bệnh nhân xuất hiện ngay lập tức và biến mất ngay khi ca trực được cập nhật hoặc hủy. |
| **Kiểm soát Concurrency & Idempotency** | Hoàn thành | Sử dụng `SqlAppLockHelper.DentistMasterKey(dentistUserId)` và `alreadyExists` check trong transaction ngăn chặn hoàn toàn race condition và double-submit. |
| **Audit Logging & Phân quyền RBAC** | Hoàn thành | Ghi nhận mọi thay đổi vào `AuditLogs`. Phân quyền `SystemAdministrator` cho mọi thao tác cấu hình; `Dentist` chỉ được xem lịch của bản thân. |

---

## 2. Danh Sách Tệp Thay Đổi & Tạo Mới

### 2.1. Backend API & Infrastructure
- `BE/DentalClinic.Application/Features/DentistSchedule/DTOs/DentistScheduleDtos.cs`:
  - Thêm `MonthlyDentistScheduleFilterRequest`, `MonthlyDayScheduleDto`, `MonthlyDentistScheduleDto`.
  - Thêm `BulkScheduleShiftDto`, `BulkScheduleAssignmentRequest`, `BulkSchedulePreviewItemDto`, `BulkSchedulePreviewResultDto`, `BulkScheduleApplyRequest`, `BulkScheduleApplyResultDto`.
  - Thêm `CopyMonthScheduleRequest`, `CopyMonthSchedulePreviewDto`.
  - Thêm `DailyScheduleOverrideRequest`, `AffectedAppointmentDto`.
- `BE/DentalClinic.Application/Common/Interfaces/IDentistScheduleService.cs`:
  - Khai báo 7 methods: `GetMonthlyScheduleAsync`, `PreviewBulkScheduleAsync`, `ApplyBulkScheduleAsync`, `PreviewCopyMonthScheduleAsync`, `ApplyCopyMonthScheduleAsync`, `GetAffectedAppointmentsAsync`, `ApplyDailyOverrideAsync`.
- `BE/DentalClinic.Infrastructure/Services/DentistScheduleService.cs`:
  - Cài đặt đầy đủ 7 methods xử lý logic phân công tháng, preview xung đột, copy tháng, daily override và appointment guard với concurrency lock.
- `BE/DentalClinic.Api/Controllers/DentistScheduleController.cs`:
  - Bổ sung 7 RESTful endpoints:
    - `GET /api/DentistSchedule/monthly`
    - `POST /api/DentistSchedule/bulk-preview`
    - `POST /api/DentistSchedule/bulk-apply`
    - `POST /api/DentistSchedule/copy-month-preview`
    - `POST /api/DentistSchedule/copy-month-apply`
    - `GET /api/DentistSchedule/affected-appointments`
    - `PUT /api/DentistSchedule/daily-override`

### 2.2. Frontend Web Razor & Services
- `FE/DentalClinic.Web/Models/ApiDtos/DentistScheduleDtos.cs`: Đồng bộ toàn bộ DTOs lịch tháng và bulk scheduling.
- `FE/DentalClinic.Web/Services/IDentistScheduleApiService.cs`: Khai báo 7 client methods gọi Backend API.
- `FE/DentalClinic.Web/Services/DentistScheduleApiService.cs`: Cài đặt gọi HTTP RESTful endpoints tương ứng.
- `FE/DentalClinic.Web/ViewModels/Admin/AdminViewModels.cs`: Bổ sung `MonthlySchedule`, `SelectedYear`, `SelectedMonth` vào `AdminDentistScheduleManageViewModel`.
- `FE/DentalClinic.Web/Controllers/Admin/AdminDentistScheduleController.cs`:
  - Bổ sung tab `monthly` làm tab mặc định trong action `Index`.
  - Bổ sung action `Monthly`.
  - Bổ sung 6 AJAX actions: `BulkPreview`, `BulkApply`, `CopyMonthPreview`, `CopyMonthApply`, `AffectedAppointments`, `DailyOverride`.
- `FE/DentalClinic.Web/Views/Admin/DentistSchedule/_MonthlyScheduleTab.cshtml` (Tạo mới):
  - Bộ lọc Tháng/Năm với chuyển tháng `<` `>`.
  - 4 Thẻ KPI tóm tắt: Tổng ngày trực, Tổng giờ làm việc, Ngày nghỉ phép/bận, Lịch hẹn đã chốt.
  - Lưới lịch 7 cột (T2 - CN) hiển thị thẻ ca trực, mã ca, nhãn nghỉ phép và số lịch hẹn.
  - Modal Phân công hàng loạt có Dynamic Shifts rows, Presets và Preview bảng xung đột.
  - Modal Sao chép lịch tháng trước với chế độ DayOfWeek Pattern và Preview.
  - Modal Chỉnh sửa ca ngày với cảnh báo Appointment Guard và Dynamic Shifts.
- `FE/DentalClinic.Web/Views/Admin/DentistSchedule/Index.cshtml`: Tích hợp tab Lịch trực theo tháng và liên kết các modal.

### 2.3. Automated Test Suites
- `BE/DentalClinic.Tests/DentistSchedule/MonthlyDentistScheduleServiceTests.cs` (Tạo mới):
  - 16 unit & integration tests kiểm thử toàn diện các kịch bản:
    1. `GetMonthlySchedule_CalculatesKPIsAndDaysAccurately`: Tính đúng tổng ngày, giờ làm, số ngày trong tháng.
    2. `GetMonthlySchedule_WithLeave_MarksDayAsOnLeave`: Nhận diện ngày nghỉ và tính đúng `TotalOffDays`.
    3. `BulkPreviewSchedule_SingleDentist_WeekdaysOnly_ReturnsValidShifts`: Kiểm tra phân công thứ 2-6 cho 1 bác sĩ.
    4. `BulkPreviewSchedule_MultipleDentists_GeneratesMatrixShifts`: Kiểm tra phân công đồng thời nhiều bác sĩ.
    5. `BulkPreviewSchedule_OverlappingShifts_DetectsOverlap`: Phát hiện ca trực trùng/giao thoa thời gian.
    6. `BulkPreviewSchedule_DentistOnLeave_DetectsLeaveConflict`: Phát hiện ca trực rơi vào ngày bác sĩ đăng ký nghỉ phép.
    7. `BulkPreviewSchedule_DentistNotAssignedToDept_DetectsDepartmentMismatch`: Phát hiện bác sĩ chưa được phân bổ vào khoa.
    8. `BulkApplySchedule_CreatesShiftsAndSkipsConflicts`: Áp dụng ca hợp lệ và tự động bỏ qua ca xung đột.
    9. `BulkApplySchedule_OverwriteExisting_ReplacesExistingShiftsWhenNoAppointments`: Ghi đè an toàn khi không có lịch hẹn.
    10. `CopyMonthSchedulePreview_DayOfWeekPattern_MatchesCorrectDayOfWeek`: Sao chép nhịp tuần (VD: các ngày Thứ 2 tháng trước sang các ngày Thứ 2 tháng này).
    11. `CopyMonthScheduleApply_CopiesShiftsToTargetMonthSuccessfully`: Sao chép chính xác sang tháng đích.
    12. `DailyOverrideSchedule_UpdatesShiftsForDay`: Cập nhật cấu hình ca trực riêng cho ngày được chọn.
    13. `DailyOverrideSchedule_AppointmentGuard_RejectsRemovalWhenConfirmedAppointmentsExist`: Chặn an toàn khi có Appointment Confirmed.
    14. `DailyOverrideSchedule_MarksDayOffWithReason_WhenNoAppointments`: Ghi nhận ngày nghỉ an toàn khi không có lịch hẹn.
    15. `BookingService_SynchronizesWithMonthlySchedule_ImmediatelyReflectsBulkAssignedSlots`: Chứng minh Single Source of Truth giữa Schedule Service và Booking Service.
    16. `DentistScheduleController_RBAC_HasAuthorizeSystemAdministratorAttribute`: Kiểm chứng phân quyền RBAC API.
- `FE/DentalClinic.Web.Tests/Controllers/AdminDentistScheduleWebControllerTests.cs` (Tạo mới):
  - 6 tests kiểm thử Web Controller Index, BulkPreview, BulkApply, CopyMonthPreview, DailyOverride, AffectedAppointments.

---

## 3. Kết Quả Kiểm Thử Thực Tế

### 3.1. Backend Tests
```
Test run for D:\SWP391\DentalCare-Admin\BE\DentalClinic.Tests\bin\Debug\net8.0\DentalClinic.Tests.dll
A total of 1 test files matched the specified pattern.
Passed!  - Failed: 0, Passed: 23, Skipped: 0, Total: 23, Duration: 1 s (DentistSchedule suite)
Passed!  - Failed: 0, Passed: 92, Skipped: 0, Total: 92, Duration: 2 s (Admin + DentistSchedule suites)
```

### 3.2. Frontend Tests
```
Test run for D:\SWP391\DentalCare-Admin\FE\DentalClinic.Web.Tests\bin\Debug\net8.0\DentalClinic.Web.Tests.dll
A total of 1 test files matched the specified pattern.
Passed!  - Failed: 0, Passed: 295, Skipped: 0, Total: 295, Duration: 21 s
```

---

## 4. Hướng Dẫn Vận Hành & Tích Hợp

1. **Khởi chạy Backend (Port 7351):**
   ```powershell
   cd D:\SWP391\DentalCare-Admin\BE\DentalClinic.Api
   dotnet run --launch-profile https
   ```
2. **Khởi chạy Frontend (Port 7121):**
   ```powershell
   cd D:\SWP391\DentalCare-Admin\FE\DentalClinic.Web
   dotnet run --launch-profile https
   ```
3. **Truy cập Giao diện:**
   - Đăng nhập với tài khoản `SystemAdministrator` (`admin@dentalcare.com`).
   - Vào menu: **Vận hành lâm sàng -> Quản lý Bác sĩ & Lịch làm việc** (`/Admin/DentistSchedule`).
   - Tab mặc định hiển thị: **Lịch Trực Theo Tháng**.
   - Bấm **Phân Công Hàng Loạt** để cấu hình ca trực tuần hoặc tháng nhanh chóng.
   - Bấm **Sao Chép Tháng Trước** để chuyển nhịp tuần của tháng cũ sang tháng mới.
   - Bấm **Sửa ca ngày** trên từng ô ngày để điều chỉnh ca hoặc ghi nhận nghỉ phép có Appointment Guard.
