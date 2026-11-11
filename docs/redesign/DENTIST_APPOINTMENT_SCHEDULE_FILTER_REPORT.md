# DENTALCARE – BÁO CÁO TỐI ƯU HÓA HỆ THỐNG BỘ LỌC VÀ GIAO DIỆN LỊCH TRÌNH KHÁM & ĐIỀU TRỊ CỦA BÁC SĨ (DENTIST APPOINTMENT SCHEDULE FILTER & UI/UX OPTIMIZATION)

---

## 1. HIỆN TRẠNG TRƯỚC KHI TỐI ƯU
Trước khi thực hiện tối ưu hóa, chức năng theo dõi ca khám của bác sĩ tồn tại các hạn chế sau:
- **Tiêu đề và phạm vi xem lịch**: Màn hình mang tên "Lịch Trình Khám & Điều Trị Hôm Nay" / "Lịch Thăm Khám Hôm Nay" và chỉ phục vụ duy nhất việc xem danh sách theo từng ngày đơn lẻ (mặc định hôm nay).
- **Thiếu bộ lọc khoảng ngày (Date Range)**: Bác sĩ không thể xem tổng thể lịch khám trong tuần, khoảng 7 ngày, 14 ngày hoặc từ ngày A đến ngày B để chủ động điều phối công việc điều trị.
- **Thiếu bộ lọc khoảng giờ (Time Range)**: Không có khả năng lọc theo khung giờ (ví dụ ca sáng `08:00 - 12:00` hay ca chiều `13:30 - 17:30`).
- **Thiếu bộ lọc trạng thái (Status Filter)**: Không thể lọc nhanh danh sách ca khám theo trạng thái (`CheckedIn`, `Confirmed`, `InService`, `Completed`, `Cancelled`, `PendingReceptionReview`), khiến bác sĩ phải cuộn tìm kiếm thủ công trong danh sách lớn.
- **Thiếu bộ lọc phòng/ghế (Room/Chair)**: Không có bộ lọc theo vị trí phòng khám hoặc ghế nha khoa điều trị được phân công.
- **Thiếu tìm kiếm bệnh nhân**: Bác sĩ không thể tra cứu nhanh lịch theo tên bệnh nhân, mã hồ sơ bệnh nhân (`PAT-xxx`) hoặc mã cuộc hẹn (`APP-xxx`).
- **Thống kê KPI chưa động**: Số liệu ca khám chưa phản ánh theo tiêu chí lọc thực tế, chưa có thẻ chỉ số trực quan (Active KPI cards) để lọc nhanh 1 chạm.

---

## 2. DANH SÁCH FILE ĐÃ KHẢO SÁT & ĐÁNH GIÁ (AUDIT)

### 2.1. Phân hệ Backend (C# .NET 8 Web API & Infrastructure)
- `BE/DentalClinic.Api/Controllers/AppointmentsController.cs`: Controller tiếp nhận yêu cầu phân hệ bác sĩ, bảo đảm xác thực JWT token và phân quyền Role Dentist.
- `BE/DentalClinic.Application/Common/Interfaces/IAppointmentService.cs`: Hợp đồng dịch vụ đặt lịch và truy vấn lịch hẹn nha khoa.
- `BE/DentalClinic.Application/Features/Appointments/DTOs/AppointmentDtos.cs`: Các DTO trao đổi dữ liệu cuộc hẹn.
- `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`: Hiện thực nghiệp vụ truy vấn Entity Framework Core trên `_dbContext.Appointments`, `PatientProfiles`, `Rooms`, `DentalChairs`.
- `BE/DentalClinic.Infrastructure/Persistence/Entities/Appointment.cs`: Thực thể cuộc hẹn, quản lý các trường `AssignedDentistUserId`, `DepartmentId`, `RoomId`, `DentalChairId`, `ScheduledStart`, `ScheduledEnd`, `Status`, `BookingSource`, `QueueNumber`.
- `BE/DentalClinic.Infrastructure/Persistence/Entities/DentistDepartment.cs` & `Room.cs`: Quản lý phạm vi phòng khám trực thuộc khoa điều trị của bác sĩ.

### 2.2. Phân hệ Frontend (ASP.NET Core 8 MVC Razor)
- `FE/DentalClinic.Web/Controllers/Dentist/DentistAppointmentController.cs`: Controller tiếp nhận request từ giao diện Web, xử lý các query parameters, validation và gọi API Service.
- `FE/DentalClinic.Web/Services/IAppointmentApiService.cs` & `AppointmentApiService.cs`: Tầng kết nối RESTful API với Backend thông qua HTTP client có gắn Bearer Token.
- `FE/DentalClinic.Web/ViewModels/Appointments/RoleAppointmentViewModels.cs`: Định nghĩa ViewModel `DentistTodayAppointmentsViewModel` và các thuộc tính thống kê KPI, nhóm ngày.
- `FE/DentalClinic.Web/Views/Dentist/Appointments/Today.cshtml`: Giao diện Razor hiển thị danh sách lịch hẹn và toàn bộ hệ thống bộ lọc.
- `FE/DentalClinic.Web/Views/Dentist/Appointments/_AppointmentRow.cshtml`: Partial view hiển thị từng hàng ca khám chuẩn hóa và tái sử dụng cho cả chế độ 1 ngày và nhiều ngày.
- `FE/DentalClinic.Web/Views/Shared/_Sidebar.cshtml`: Menu điều hướng phân hệ bác sĩ (cập nhật nhãn "Lịch Trình Khám & Điều Trị").

---

## 3. KẾ HOẠCH VÀ THIẾT KẾ UI/UX MỚI

```
+----------------------------------------------------------------------------------------------------+
| [Phân Hệ Bác Sĩ] [Khoảng Ngày]                                                                     |
| 📅 LỊCH TRÌNH KHÁM & ĐIỀU TRỊ                                                                     |
| Phụ trách điều trị: Bác sĩ chuyên khoa                                                             |
+----------------------------------------------------------------------------------------------------+
| THỐNG KÊ NHANH (DYNAMIC KPI CARDS):                                                                |
| [📊 Tất Cả: 12] [⏳ Chờ Đến: 3] [✅ Đã Check-in: 4] [🩺 Đang Điều Trị: 1] [🏁 Đã Xong: 3] [❌ Đã Hủy: 1] |
+----------------------------------------------------------------------------------------------------+
| BỘ LỌC CHÍNH (PRESETS & NGÀY):                                                                     |
| [Hôm Nay] [Ngày Mai] [Tuần Này]  | Từ ngày: [ 2026-11-11 ] -> Đến ngày: [ 2026-11-17 ] [Áp Dụng]  |
| [⚙️ Bộ Lọc Nâng Cao ▼]                                                                            |
+----------------------------------------------------------------------------------------------------+
| VÙNG LỌC NÂNG CAO (COLLAPSIBLE):                                                                   |
| - Khung giờ: Từ [ 08:00 ] Đến [ 12:00 ]                                                            |
| - Trạng thái: [ Tất Cả Trạng Thái / Đã Check-in / Đang Khám / Đã Xong... ]                         |
| - Phòng điều trị: [ Tất Cả Phòng / Phòng Khám 101 / Phòng Khám 102... ]                            |
| - Tìm kiếm: [ Nhập tên BN, mã PAT-xxx, mã APP-xxx... ]                                            |
| [Áp Dụng Bộ Lọc] [Đặt Lại]                                                                        |
+----------------------------------------------------------------------------------------------------+
| ACTIVE FILTER CHIPS (Thẻ điều kiện lọc đang bật):                                                  |
| 🏷️ Ngày: 11/11/2026 - 17/11/2026 | 🏷️ Giờ: 08:00 - 12:00 | 🏷️ Trạng thái: Đã Check-in | ❌ Xóa bộ lọc |
+----------------------------------------------------------------------------------------------------+
| DANH SÁCH CA KHÁM (PHÂN NHÓM THEO NGÀY NẾU CHỌN NHIỀU NGÀY):                                      |
| 📅 THỨ TƯ, 11/11/2026 (HÔM NAY) - 5 ca khám                                                       |
|   - STT | Mã Hẹn | Bệnh Nhân | Dịch Vụ | Khung Giờ | Vị Trí Ghế | Lý Do Khám | Trạng Thái | Thao Tác |
| 📅 THỨ NĂM, 12/11/2026 (NGÀY MAI) - 4 ca khám                                                      |
|   ...                                                                                              |
+----------------------------------------------------------------------------------------------------+
```

---

## 4. TRIỂN KHAI BACKEND (API & DATABASE QUERIES)

### 4.1. Filter DTO (`DentistAppointmentFilterQuery`)
Được tạo trong `DentalClinic.Application/Features/Appointments/DTOs/AppointmentDtos.cs`:
- `FromDate` (`DateOnly?`): Ngày bắt đầu lọc.
- `ToDate` (`DateOnly?`): Ngày kết thúc lọc.
- `FromTime` (`TimeOnly?`): Giờ bắt đầu lọc.
- `ToTime` (`TimeOnly?`): Giờ kết thúc lọc.
- `Status` (`string?`): Trạng thái ca khám (hỗ trợ lọc nhóm hoặc trạng thái cụ thể).
- `RoomId` (`long?`): Mã phòng khám.
- `DentalChairId` (`long?`): Mã ghế nha khoa.
- `SearchTerm` (`string?`): Từ khóa tìm kiếm đa tiêu chí.

### 4.2. Truy vấn Entity Framework Core an toàn & tối ưu
Trong `AppointmentService.GetDentistAppointmentsAsync`:
```csharp
var query = _dbContext.Appointments
    .AsNoTracking()
    .Include(a => a.PatientUser).ThenInclude(u => u.PatientProfile)
    .Include(a => a.Department)
    .Include(a => a.RequestedService)
    .Include(a => a.Room)
    .Include(a => a.DentalChair)
    .Where(a => a.AssignedDentistUserId == dentistUserId);
```
- **Lọc theo khoảng ngày**:
  ```csharp
  if (filter.FromDate.HasValue) {
      var startDt = filter.FromDate.Value.ToDateTime(TimeOnly.MinValue);
      query = query.Where(a => a.ScheduledStart >= startDt);
  }
  if (filter.ToDate.HasValue) {
      var endDt = filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
      query = query.Where(a => a.ScheduledStart <= endDt);
  }
  ```
- **Lọc theo khung giờ (Time of Day)**:
  So sánh trực tiếp `TimeOfDay` của ca khám với khoảng `FromTime` và `ToTime`.
- **Lọc trạng thái**:
  Hỗ trợ các trạng thái thực tế: `Confirmed`, `CheckedIn`, `Waiting`, `InService`, `Completed`, `Cancelled`, `Rejected`, `PendingReceptionReview`.
- **Lọc phòng và ghế**: So sánh `a.RoomId == filter.RoomId` và `a.DentalChairId == filter.DentalChairId`.
- **Tìm kiếm đa năng**:
  ```csharp
  var term = filter.SearchTerm.Trim().ToLower();
  query = query.Where(a =>
      a.AppointmentCode.ToLower().Contains(term) ||
      (a.PatientUser != null && a.PatientUser.PatientProfile != null && a.PatientUser.PatientProfile.FullName.ToLower().Contains(term)) ||
      (a.PatientUser != null && a.PatientUser.PatientProfile != null && a.PatientUser.PatientProfile.PatientCode.ToLower().Contains(term)) ||
      (a.PatientUser != null && a.PatientUser.PhoneNumber != null && a.PatientUser.PhoneNumber.Contains(term)));
  ```

### 4.3. API Endpoint Danh Sách Phòng Của Bác Sĩ
- `GET /api/dentist/rooms`: Truy vấn danh sách phòng khám đang hoạt động trực thuộc chuyên khoa của bác sĩ thông qua bảng quan hệ `DentistDepartments`.

---

## 5. TRIỂN KHAI FRONTEND (MVC & RAZOR)

### 5.1. Cập nhật Service API (`IAppointmentApiService`)
- Bổ sung phương thức `GetDentistAppointmentsAsync` có đầy đủ tham số bộ lọc.
- Bổ sung phương thức `GetDentistRoomsAsync` để nạp danh mục phòng cho dropdown bộ lọc.

### 5.2. ViewModel `DentistTodayAppointmentsViewModel`
Bổ sung các thuộc tính:
- `DatePreset`: Nhận diện bộ lọc nhanh (`today`, `tomorrow`, `this_week`, `specific`, `custom_range`).
- `FromDate`, `ToDate`: Khoảng ngày truy vấn thực tế.
- `FromTime`, `ToTime`: Khoảng giờ truy vấn thực tế.
- `AvailableRooms`: Danh sách phòng khám hỗ trợ cho dropdown.
- `ValidationError`: Lưu trữ thông báo lỗi validation nếu nhập sai ngày/giờ.
- `IsMultiDay`: Xác định chế độ hiển thị một ngày hay nhiều ngày.
- `GroupedAppointments`: Tự động phân nhóm cuộc hẹn theo `DateOnly` tăng dần.
- `TotalCount`, `ConfirmedCount`, `CheckedInCount`, `InServiceCount`, `CompletedCount`, `CancelledCount`: Thống kê KPI động dựa trên tập dữ liệu đã lọc.
- `HasActiveFilters`: Kiểm tra các bộ lọc có đang được áp dụng khác giá trị mặc định để hiển thị thanh Filter Chips.

### 5.3. Tái cấu trúc View `Today.cshtml` và Partial View `_AppointmentRow.cshtml`
- Tổ chức lại layout gọn gàng: Header -> KPI Cards -> Quick Preset Bar -> Collapsible Filter Drawer -> Active Filter Chips -> Alerts -> Data Table.
- Toàn bộ row hiển thị ca khám được đóng gói vào `_AppointmentRow.cshtml`, bảo toàn 100% các thao tác lâm sàng:
  - Nút **Vào Khám**: Bắt đầu tiếp nhận khi bệnh nhân đã check-in và đã có ghế.
  - Nút **Pick Ghế**: Mở modal chọn ghế nha khoa nhanh trong phòng.
  - Nút **Tiếp Tục Khám**: Chuyển nhanh vào hồ sơ bệnh án khi ca khám đang diễn ra (`InService`).
  - Nút **Bệnh Án**: Mở hồ sơ bệnh án và sơ đồ răng của bệnh nhân.
  - Nút **Phác Đồ**: Xem và lập phác đồ điều trị.
  - Nút **Đơn Thuốc**: Mở kê đơn thuốc điện tử khi đang khám hoặc đã khám xong (có cơ chế `returnUrl` quay lại đúng bộ lọc hiện tại).

---

## 6. CHI TIẾT HỆ THỐNG BỘ LỌC

| Bộ Lọc | Các Lựa Chọn | Hành Vi Mặc Định | Cơ Chế Backend / Frontend |
|---|---|---|---|
| **Bộ lọc nhanh (Date Presets)** | `Hôm nay`, `Ngày mai`, `Tuần này` | `Hôm nay` | Client tính toán chính xác ngày đầu tuần (Thứ Hai) đến cuối tuần (Chủ Nhật) hoặc ngày mai; Backend lọc `ScheduledStart`. |
| **Chọn ngày cụ thể** | Ngày bất kỳ qua date picker | Ngày hiện tại | Thiết lập `FromDate = ToDate = SelectedDate`, truy vấn đúng ngày đã chọn. |
| **Chọn khoảng ngày** | Từ ngày (FromDate) - Đến ngày (ToDate) | Hôm nay | Lọc toàn bộ cuộc hẹn trong khoảng từ `00:00:00` ngày bắt đầu đến `23:59:59` ngày kết thúc. |
| **Khoảng giờ trong ngày** | Từ giờ (FromTime) - Đến giờ (ToTime) | Trống (Xem cả ngày) | So sánh `TimeOfDay` của thời gian hẹn `ScheduledStart`. Cho phép xóa trắng để xem lại toàn ca. |
| **Trạng thái cuộc hẹn** | 6 nhóm: `All`, `Confirmed`, `CheckedIn`, `InService`, `Completed`, `Cancelled` | `All` (Tất cả) | Lọc chính xác theo Enum `AppointmentStatus`. |
| **Phòng khám** | Danh mục phòng thực tế của khoa | `Tất cả phòng` | Lọc theo `RoomId`, lấy từ dữ liệu thực tế trong DB. |
| **Tìm kiếm bệnh nhân** | Ô tìm kiếm tự do | Trống | Tìm kiếm song song `PatientName`, `PatientCode`, `AppointmentCode`, `PhoneNumber`. |

---

## 7. XỬ LÝ VALIDATION VÀ TRẠNG THÁI LỖI
- **Quy tắc ngày bắt đầu & kết thúc**:
  - `FromDate` không được lớn hơn `ToDate`.
  - Nếu vi phạm: Hiển thị cảnh báo màu đỏ: *"Ngày bắt đầu (dd/MM/yyyy) không được lớn hơn ngày kết thúc (dd/MM/yyyy)."*
  - **Giữ nguyên ngày người dùng đã nhập**, không tự ý ghi đè về hôm nay, giúp người dùng nhận biết và chỉnh sửa lại ngày mong muốn.
  - Không gửi truy vấn rỗng hoặc lỗi xuống DB khi dữ liệu không hợp lệ.
- **Quy tắc giờ bắt đầu & kết thúc**:
  - `FromTime` phải nhỏ hơn `ToTime`.
  - Nếu vi phạm: Hiển thị cảnh báo: *"Giờ bắt đầu (HH:mm) phải nhỏ hơn giờ kết thúc (HH:mm)."*
  - Giữ nguyên giờ đã nhập trong form để người dùng dễ dàng điều chỉnh.
- **Xử lý ngày không có lịch khám**:
  - Hiển thị giao diện Empty State chuyên nghiệp với icon lịch trống, thông điệp rõ ràng và 2 nút hành động: *Xem Lịch Hôm Nay Mặc Định* hoặc *Xem Lịch Làm Việc Cả Tháng*.

---

## 8. THỐNG KÊ KPI ĐỘNG VÀ PHÂN NHÓM LỊCH HẸN
- **Dynamic KPI Cards**:
  - Các ô đếm: **Tổng Ca Khám**, **Chờ Đến Khám** (`Confirmed`), **Đã Check-in** (`CheckedIn`/`Waiting`), **Đang Khám** (`InService`), **Đã Khám Xong** (`Completed`), **Đã Hủy** (`Cancelled`/`Rejected`).
  - Toàn bộ số liệu cập nhật tự động theo tập dữ liệu sau lọc.
  - Nhấp vào từng ô KPI sẽ kích hoạt nhanh bộ lọc trạng thái tương ứng.
- **Phân nhóm theo ngày (Multi-day Grouping)**:
  - Khi khoảng ngày được chọn lớn hơn 1 ngày (`IsMultiDay = true`), hệ thống tự động gom nhóm lịch hẹn theo từng ngày với dải phân cách trực quan:
    `THỨ HAI, 16/11/2026 • 4 ca khám`, `THỨ BA, 17/11/2026 • 3 ca khám`.
  - Giúp bác sĩ nắm bắt khối lượng công việc từng ngày mà không bị lẫn lộn giữa các mốc thời gian.

---

## 9. KIỂM SOÁT PHÂN QUYỀN (RBAC & ISOLATION)
- **Bảo mật tuyệt đối cấp độ Backend**:
  - Bác sĩ chỉ xem được lịch hẹn được phân công cho chính mình (`AssignedDentistUserId == dentistUserId`).
  - `dentistUserId` được trích xuất trực tiếp từ Claims/JWT token đăng nhập tại Controller (`GetCurrentUserId()`), không bao giờ nhận hoặc tin tưởng tham số `dentistId` từ Query String.
  - Danh sách phòng khám được truy xuất nghiêm ngặt theo các chuyên khoa mà bác sĩ đó có đăng ký công tác (`DentistDepartments`).

---

## 10. TỐI ƯU HIỆU NĂNG TRUY VẤN
- Sử dụng `.AsNoTracking()` trong Entity Framework Core đối với các câu lệnh truy vấn danh sách để giảm thiểu chi phí bộ nhớ của Change Tracker.
- Tận dụng Index sẵn có trên cột `AssignedDentistUserId`, `ScheduledStart`, `Status`, `RoomId`.
- Các điều kiện lọc ngày được chuyển đổi sang mốc thời gian `DateTime` chính xác để EF Core biên dịch thành câu lệnh SQL `WHERE ScheduledStart >= @startDt AND ScheduledStart <= @endDt` tối ưu index scan.

---

## 11. TRẢI NGHIỆM NGƯỜI DÙNG (UX ENHANCEMENTS)
- **Active Filter Chips**: Hiển thị danh sách các badge điều kiện lọc đang có hiệu lực cùng nút "Xóa bộ lọc" để người dùng dễ dàng theo dõi lý do vì sao danh sách ca khám bị thu hẹp.
- **Collapsible Advanced Drawer**: Các bộ lọc chuyên sâu (giờ, trạng thái, phòng, tìm kiếm) được đặt gọn gàng trong khối mở rộng, không làm rối màn hình chính nhưng sẵn sàng khi cần.
- **Preserve Navigation State (`returnUrl`)**: Bác sĩ từ danh sách ca khám chuyển sang trang Kê Đơn Thuốc hoặc Hồ Sơ Bệnh Án có thể quay lại đúng trạng thái bộ lọc đang chọn mà không bị reset về trang mặc định.
- **Loading Indicators**: Nút "Vào Khám" và modal "Pick Ghế" có hiệu ứng spinner trực quan ngăn ngừa double-click.

---

## 12. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (AUTOMATED TEST VERIFICATION)

Đã xây dựng và bổ sung 2 bộ test case mới kiểm tra toàn diện chức năng bộ lọc:

### 12.1. Backend Test Suite (`DentalClinic.Tests.DentistRedesign.DentistScheduleFilterTests`)
1. `GetDentistAppointments_TodayPreset_ReturnsOnlyTodayAppointmentsForCallingDentist`: **PASSED**
2. `GetDentistAppointments_DateRange_ReturnsAppointmentsWithinRange`: **PASSED**
3. `GetDentistAppointments_TimeRange_ReturnsOnlyMatchingAppointments`: **PASSED**
4. `GetDentistAppointments_StatusFilter_ReturnsMatchingAppointments`: **PASSED**
5. `GetDentistAppointments_RoomFilter_ReturnsOnlyAppointmentsInRoom`: **PASSED**
6. `GetDentistAppointments_SearchTerm_MatchesPatientNameAndCode`: **PASSED**
7. `GetDentistAppointments_DentistIsolation_DentistCannotViewOtherDentistAppointments`: **PASSED**
8. `GetDentistRooms_ReturnsOnlyActiveRoomsInDentistDepartments`: **PASSED**

### 12.2. Frontend Test Suite (`DentalClinic.Web.Tests.Controllers.DentistScheduleFilterWebTests`)
1. `Today_DefaultInvocation_LoadsTodayAppointmentsAndAvailableRooms`: **PASSED**
2. `Today_WithDatePresetTomorrow_ComputesTomorrowRange`: **PASSED**
3. `Today_WithDatePresetThisWeek_ComputesMondayToSundayRange`: **PASSED**
4. `Today_WithInvalidDateRange_FromDateAfterToDate_SetsValidationError`: **PASSED**
5. `Today_WithInvalidTimeRange_FromTimeAfterToTime_SetsValidationError`: **PASSED**
6. `Today_WithAdvancedFilters_PassesAllParametersToApi`: **PASSED**

### 12.3. Tổng thể kiểm thử toàn dự án (`sync-and-test.ps1`)
- **Backend Tests (`DentalClinic.Tests`)**: **917 Passed**, 0 Failed, 1 Skipped.
- **Frontend Tests (`DentalClinic.Web.Tests`)**: **344 Passed**, 0 Failed.
- **Tổng cộng**: **1,261 Passed / 1,261 Executed (100% Pass Rate)**.

---

## 13. CÁC VẤN ĐỀ PHÁT SINH VÀ GIẢI PHÁP ĐÃ XỬ LÝ
1. **Lỗi DLL Lock khi build**: Tiến trình dotnet daemon đang chạy khóa file `DentalClinic.Infrastructure.dll`.
   - *Giải pháp*: Sử dụng script `stop-daemons.ps1` chấm dứt an toàn các tiến trình trước khi build và test.
2. **Yêu cầu không tự động thay đổi ngày khi validation thất bại**:
   - *Giải pháp*: Trong `DentistAppointmentController`, khi phát hiện `FromDate > ToDate`, hệ thống gán `ValidationError`, giữ nguyên giá trị người dùng đã nhập vào ViewModel, không gọi API truy vấn để tránh sai lệch dữ liệu.
3. **Cấu trúc dữ liệu `Appointment` yêu cầu `BookingSource` và `ReasonForVisit`**:
   - *Giải pháp*: Cập nhật mock seed data trong test suite đáp ứng đầy đủ các thuộc tính bắt buộc của Entity Framework Core.

---

## 14. ĐÁNH GIÁ TÁC ĐỘNG (BLAST RADIUS ANALYSIS)
- **Tác động trực tiếp**:
  - Chỉ tác động đến Controller `DentistAppointmentController`, View `Today.cshtml`, `_AppointmentRow.cshtml` và các endpoint phụ trợ riêng cho phân hệ Bác sĩ (`/api/dentist/appointments`, `/api/dentist/rooms`).
- **Các phân hệ không bị ảnh hưởng**:
  - Phân hệ Bệnh nhân (Patient Portal), Lễ tân (Receptionist), Quản trị viên (Admin) và Trưởng khoa (Department Manager) hoàn toàn giữ nguyên logic và luồng hoạt động cũ.
  - Các quy trình nghiệp vụ lâm sàng (Bệnh án, Sơ đồ răng, Kê đơn thuốc, Lập phác đồ, Chọn ghế) hoạt động ổn định và liền mạch.

---

## 15. HƯỚNG DẪN BÁC SĨ SỬ DỤNG
1. **Xem lịch hôm nay**: Mở thanh menu trái, nhấp vào **Lịch Trình Khám & Điều Trị**. Màn hình mặc định hiển thị ngay các ca khám trong ngày hôm nay.
2. **Xem lịch ngày mai hoặc cả tuần**: Nhấp vào nút **Ngày Mai** hoặc **Tuần Này** trên thanh bộ lọc nhanh.
3. **Tra cứu khoảng ngày bất kỳ**: Chọn ngày bắt đầu tại ô **Từ ngày** và ngày kết thúc tại ô **Đến ngày**, sau đó nhấp nút **Áp Dụng**.
4. **Lọc ca khám buổi sáng/chiều**: Bấm nút **Bộ Lọc Nâng Cao**, nhập khung giờ (ví dụ `08:00` đến `12:00`) và bấm **Áp Dụng**.
5. **Tìm kiếm bệnh nhân**: Nhập tên hoặc mã bệnh nhân vào ô tìm kiếm để lọc tức thì.
6. **Lọc nhanh theo trạng thái**: Nhấp trực tiếp vào các thẻ chỉ số KPI ở đầu trang (ví dụ nhấp vào thẻ màu xanh lá **Đã Check-in** để xem danh sách bệnh nhân đang đợi trước cửa phòng).
7. **Đặt lại bộ lọc**: Nhấp vào nút **Đặt lại** hoặc biểu tượng thùng rác **Xóa bộ lọc** để quay về xem lịch hôm nay.

---

## 16. KHUYẾN NGHỊ NÂNG CẤP TIẾP THEO
1. **Chế độ xem dạng Lịch biểu (Calendar View / Timeline Grid)**: Bổ sung chế độ xem theo dạng lưới thời gian trực quan (như Google Calendar) bên cạnh chế độ xem dạng bảng hiện tại.
2. **Thông báo Real-time (SignalR)**: Khi lễ tân check-in bệnh nhân tại quầy tiếp đón, dòng ca khám trên màn hình bác sĩ tự động đổi màu và cập nhật trạng thái mà không cần tải lại trang.
3. **Xuất danh sách ca khám (Export Excel/PDF)**: Cung cấp tính năng xuất danh sách ca khám theo kết quả lọc để bác sĩ lưu trữ hoặc phục vụ giao ban chuyên môn.
