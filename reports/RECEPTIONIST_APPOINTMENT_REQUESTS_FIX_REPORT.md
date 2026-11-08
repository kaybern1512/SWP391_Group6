# BÁO CÁO ĐIỀU TRA & KHẮC PHỤC LỖI HIỂN THỊ YÊU CẦU ĐẶT LỊCH LỄ TÂN (APPOINTMENT #98)

- **Dự án:** SWP391_GROUP6
- **Nhánh fix:** `fix/receptionist-appointment-requests-visibility`
- **Base commit:** `develop` (`eb5d39e2d811d1dd29e8e1458af3eb65b2bd8d37`)
- **Tài khoản kiểm thử:** Lễ tân Nguyễn Thị Mai (`receptionist@dentalcare.com`, UserId 3)
- **Đối tượng kiểm thử thực tế:** AppointmentId `98` (`APP-20261107-11C577`) trên cơ sở dữ liệu `DentalClinic_QA_E2E`
- **Trạng thái:** **FIX_VERIFIED** (Đã xác minh và nghiệm thu thành công)

---

## 1. TỔNG QUAN & KẾT LUẬN NGHIỆM THU (VERDICT)

### **VERDICT: FIX_VERIFIED**

Sau quá trình điều tra toàn diện từ Database, Backend API, DTO Serialization, Controller xử lý lỗi đến Giao diện Frontend Razor View, lỗi đã được xác định chính xác từ 4 nguyên nhân gốc và được khắc phục triệt để.

Toàn bộ 100% test hồi quy (918 test cases gồm 629 backend tests và 289 frontend tests) đều **PASS**. Dữ liệu thực tế của Appointment #98 hiển thị ngay hàng đầu tiên (Row 1) trên màn hình Lễ tân `/Receptionist/Appointments/Requests` với đầy đủ thông tin, thời lượng dịch vụ chuẩn xác (90 phút) và chức năng tìm kiếm/lọc hoạt động hoàn hảo.

---

## 2. XÁC MINH MÔI TRƯỜNG & DỮ LIỆU THỰC TẾ

### 2.1. Cơ sở dữ liệu và Tiến trình Backend
Tiến trình Backend API (.NET 8 Kestrel) đang chạy trên cổng `https://localhost:7350`.
Kiểm tra kết nối thực tế trên SQL Server `.\SQL2025_DUC` thông qua `sys.dm_exec_sessions`:

```sql
SELECT s.session_id, s.host_name, s.program_name, d.name AS db_name 
FROM sys.dm_exec_sessions s 
JOIN sys.databases d ON s.database_id = d.database_id 
WHERE d.name = 'DentalClinic_QA_E2E' AND s.program_name LIKE '%Core Microsoft SqlClient Data Provider%'
```

**Kết quả:**
- `session_id`: 80
- `program_name`: Core Microsoft SqlClient Data Provider
- `db_name`: **`DentalClinic_QA_E2E`** (Đúng 100% database thử nghiệm riêng theo yêu cầu)
- Cơ sở dữ liệu production `DentalClinicManagementDB` hoàn toàn **KHÔNG BỊ TÁC ĐỘNG**.

### 2.2. Dữ liệu thực tế của Appointment #98
Dữ liệu truy vấn trực tiếp từ bảng `Appointments` của database `DentalClinic_QA_E2E`:

| Thuộc tính | Giá trị thực tế |
| :--- | :--- |
| **AppointmentId** | `98` |
| **AppointmentCode** | `APP-20261107-11C577` |
| **PatientUserId** | `6` (Bệnh nhân: Lê Hoàng Minh, Mã BN: `PAT-202609-0001`, SĐT: `0988112233`) |
| **DepartmentId** | `1` (Khoa Răng Tổng Quát & Nội Nha) |
| **RequestedServiceId** | `11` (Cấy Ghép Implant Straumann QA 831, Đơn giá: 28.500.000 đ) |
| **Service Duration** | **90 phút** |
| **ScheduledStart** | `2026-11-09 08:30:00` |
| **ScheduledEnd** | `2026-11-09 10:00:00` |
| **Status** | `PendingReceptionReview` |
| **CreatedAt** | `2026-11-07 16:21:47.5541809` |
| **ReasonForVisit** | "Khám tổng quát định kỳ & kiểm tra răng miệng" |

---

## 3. NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

Qua đối soát kỹ thuật đa tầng, đội ngũ ghi nhận 4 nguyên nhân liên đới dẫn đến việc lễ tân không nhìn thấy hoặc không quản lý được lịch hẹn #98:

### Nguyên nhân 1 (Nghiêm trọng nhất): Nuốt lỗi API âm thầm tại Frontend Controller (Silent Error Swallowing)
Tại `ReceptionistAppointmentController.Requests()`:
```csharp
var result = await _appointmentApiService.GetReceptionistRequestsAsync();
var vm = new ReceptionistRequestsViewModel
{
    Requests = result.Data ?? new()
};
return View("~/Views/Receptionist/Appointments/Requests.cshtml", vm);
```
- Khi token xác thực trong phiên làm việc của Lễ tân bị hết hạn (401), hoặc khi kết nối mạng/API chập chờn, hoặc deserialization thất bại, `result.IsSuccess` bằng `false` và `result.Data` là `null`.
- Controller gán mặc định `Requests = new()` (danh sách rỗng) và **KHÔNG** ghi nhận bất kỳ `TempData["ErrorMessage"]` hay thông báo lỗi nào.
- Giao diện Razor View `Requests.cshtml` khi thấy `Model.Requests.Count == 0` lập tức render thông báo giả:
  > *"Không có yêu cầu chờ xử lý. Hiện tại không có bệnh nhân nào đang chờ tiếp nhận lịch khám."*
- Người dùng lễ tân hoàn toàn bị đánh lừa rằng không có lịch hẹn nào, trong khi thực tế là cuộc gọi API gặp sự cố.

### Nguyên nhân 2: Lỗi sắp xếp hàng đợi (Queue Ordering FIFO thay vì LIFO)
Tại `AppointmentService.GetReceptionistRequestsAsync`:
```csharp
var ids = await _dbContext.Appointments
    .AsNoTracking()
    .Where(a => a.Status == AppointmentStatus.PendingReceptionReview)
    .OrderBy(a => a.CreatedAt) // <-- ASCENDING ORDER
    .Select(a => a.AppointmentId)
    .ToListAsync(ct);
```
- Backend sắp xếp tăng dần theo `CreatedAt` (`OrderBy`).
- Các lịch hẹn test cũ từ tháng 10 (`2026-10-15`) luôn chiếm các vị trí đầu bảng (Hàng 1 đến Hàng 8).
- Lịch hẹn bệnh nhân thật mới đặt vào ngày `07/11/2026` (#98) bị đẩy xuống tận đáy bảng (Hàng 9/9), nằm ngoài tầm nhìn cuộn trang đầu tiên của lễ tân.

### Nguyên nhân 3: Lệch tên thuộc tính DTO Serialization (Missing JsonPropertyName)
- Backend DTO (`AppointmentDtos.cs`) trả về trường `serviceDuration`: `90`.
- Frontend DTO (`BookingAndAppointmentDtos.cs`) khai báo `public int? DurationMinutes { get; set; }` nhưng thiếu `[JsonPropertyName("serviceDuration")]`.
- Khi deserialization, `DurationMinutes` bị `null`, khiến giao diện hiển thị:
  `Thời lượng: @(req.DurationMinutes ?? 30) phút` -> Lễ tân nhìn thấy sai thành 30 phút thay vì thời lượng thực tế 90 phút của ca Cấy Ghép Implant.

### Nguyên nhân 4: Thiếu công cụ tìm kiếm / lọc nhanh tại hàng đợi tiếp nhận
- Trang `Requests.cshtml` không trang bị ô tìm kiếm theo Mã hẹn, Tên, Mã BN hay SĐT, khiến lễ tân gặp khó khăn khi số lượng yêu cầu tồn đọng tăng lên.

---

## 4. CHI TIẾT CÁC THAY ĐỔI ĐÃ THỰC HIỆN (CODE FIXES)

### 4.1. Sửa sắp xếp hàng đợi Backend (LIFO - Yêu cầu mới nhất lên đầu)
**File:** `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`
```csharp
public async Task<ApiResponse<List<AppointmentDto>>> GetReceptionistRequestsAsync(CancellationToken ct = default)
{
    var ids = await _dbContext.Appointments
        .AsNoTracking()
        .Where(a => a.Status == AppointmentStatus.PendingReceptionReview)
        .OrderByDescending(a => a.CreatedAt) // Mới nhất hiển thị trước
        .Select(a => a.AppointmentId)
        .ToListAsync(ct);
...
```

### 4.2. Khắc phục DTO Deserialization Mismatch
**File:** `FE/DentalClinic.Web/Models/ApiDtos/BookingAndAppointmentDtos.cs`
Bổ sung bí danh đa chiều cho cả `serviceDuration` / `durationMinutes` và `servicePrice` / `price`:
```csharp
private int? _durationMinutes;

[JsonPropertyName("serviceDuration")]
public int? ServiceDuration
{
    get => _durationMinutes;
    set { if (value.HasValue) _durationMinutes = value; }
}

[JsonPropertyName("durationMinutes")]
public int? DurationMinutes
{
    get => _durationMinutes;
    set { if (value.HasValue) _durationMinutes = value; }
}

private decimal? _servicePrice;

[JsonPropertyName("servicePrice")]
public decimal? ServicePrice
{
    get => _servicePrice;
    set { if (value.HasValue) _servicePrice = value; }
}

[JsonPropertyName("price")]
public decimal? Price
{
    get => _servicePrice;
    set { if (value.HasValue) _servicePrice = value; }
}
```

### 4.3. Nâng cấp ViewModel & Xử lý lỗi API minh bạch tại Controller
**File:** `FE/DentalClinic.Web/ViewModels/Appointments/RoleAppointmentViewModels.cs`
```csharp
public class ReceptionistRequestsViewModel
{
    public List<AppointmentDto> Requests { get; set; } = new();
    public string? SearchQuery { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}
```

**File:** `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistAppointmentController.cs`
```csharp
[HttpGet("")]
[HttpGet("Requests")]
public async Task<IActionResult> Requests([FromQuery] string? search = null)
{
    var result = await _appointmentApiService.GetReceptionistRequestsAsync();
    var vm = new ReceptionistRequestsViewModel
    {
        SearchQuery = search
    };

    if (!result.IsSuccess)
    {
        var errMsg = result.Message ?? "Không thể tải danh sách yêu cầu đặt lịch từ máy chủ API. Vui lòng thử lại sau.";
        _logger?.LogWarning("Failed to retrieve receptionist appointment requests: {Message} (Status {StatusCode})", errMsg, result.StatusCode);
        vm.ErrorMessage = errMsg;
        if (TempData != null)
        {
            TempData["ErrorMessage"] = errMsg;
        }
        return View("~/Views/Receptionist/Appointments/Requests.cshtml", vm);
    }

    var requests = result.Data ?? new();
    if (!string.IsNullOrWhiteSpace(search))
    {
        var q = search.Trim();
        requests = requests.Where(r =>
            (!string.IsNullOrEmpty(r.AppointmentCode) && r.AppointmentCode.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(r.PatientName) && r.PatientName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(r.PatientCode) && r.PatientCode.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(r.PatientPhone) && r.PatientPhone.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(r.ServiceName) && r.ServiceName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(r.DepartmentName) && r.DepartmentName.Contains(q, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    vm.Requests = requests;
    return View("~/Views/Receptionist/Appointments/Requests.cshtml", vm);
}
```

### 4.4. Cải tiến Giao diện Hàng đợi Tiếp nhận (Error Banner + Search Filter)
**File:** `FE/DentalClinic.Web/Views/Receptionist/Appointments/Requests.cshtml`
- Hiển thị Alert Banner cảnh báo lỗi rõ ràng kèm nút "Thử lại" khi API gặp sự cố (`Model.HasError`).
- Bổ sung thanh tìm kiếm nhanh cho phép Lễ tân lọc tức thời theo mã hẹn, tên, SĐT bệnh nhân.
- Phân biệt rõ trạng thái "Không tìm thấy kết quả phù hợp với từ khóa" và "Không có yêu cầu nào đang chờ xử lý".

---

## 5. BẰNG CHỨNG KIỂM THỬ THỰC TẾ (BEFORE VS AFTER)

### 5.1. So sánh Trước và Sau khi sửa

| Tiêu chí | Trước khi sửa (Before) | Sau khi sửa (After) |
| :--- | :--- | :--- |
| **Vị trí của Appointment #98** | Nằm ở đáy danh sách (Hàng 9/9), bị che khuất bởi các yêu cầu cũ từ tháng 10. | **Hàng 1 (Vị trí đầu tiên)** ngay khi mở trang `/Receptionist/Appointments/Requests`. |
| **Thời lượng dịch vụ hiển thị** | Bị fallback về `30 phút` do lỗi DTO mapping. | Hiển thị chính xác **`90 phút`** đúng với dịch vụ Cấy Ghép Implant Straumann. |
| **Xử lý khi lỗi kết nối / token 401** | Bị nuốt lỗi, biến thành màn hình rỗng: *"Không có yêu cầu chờ xử lý"*. | Hiển thị Banner đỏ: *"Lỗi khi tải hàng đợi tiếp nhận lịch khám"* kèm chi tiết lỗi và nút **"Thử lại"**. |
| **Khả năng tra cứu lịch #98** | Không có ô tìm kiếm. | Hỗ trợ thanh tìm kiếm `?search=11C577` hoặc `Lê Hoàng Minh`, lọc tức thời 1 kết quả duy nhất. |

### 5.2. Bằng chứng HTTP Status & Rendered HTML thực tế
Chạy phiên kiểm thử trực tiếp bằng tài khoản `receptionist@dentalcare.com`:

```
=== 1. VERIFY BACKEND DATABASE CONNECTION ===
session_id: 80 | host_name: DESKTOP-LAPTOP | db_name: DentalClinic_QA_E2E (SQL Server 2025)

=== 2. VERIFY BACKEND API (NEW ORDERING & DTO PAYLOAD) ===
API Login Success: True
API Requests Count: 9
First item in queue: AppointmentId = 98, Code = APP-20261107-11C577, CreatedAt = 2026-11-07T16:21:47.5541809
Appointment 98 Position in Queue: Row 1 of 9
Service Duration: 90 minutes
Patient Name: Lê Hoàng Minh
Department: Khoa Răng Tổng Quát & Nội Nha

=== 3. VERIFY FRONTEND WEB SESSION & RENDERING ===
Web Login Status: HTTP 302 -> Redirect to /Receptionist/Dashboard
Requests Page HTTP Status: 200 OK
Top Appointment in Receptionist Table: APP-20261107-11C577
Rendered HTML Row 1 Data:
- Mã hẹn: APP-20261107-11C577 (Ngày tạo: 07/11 16:21)
- Bệnh nhân: Lê Hoàng Minh (Mã: PAT-202609-0001 • SĐT: 0988112233)
- Khoa & Dịch vụ: Cấy Ghép Implant Straumann QA 831 (Khoa Răng Tổng Quát & Nội Nha)
- Thời gian khám: 09/11/2026 08:30 (Thời lượng: 90 phút)
- Thao tác: Nút "Chuyển Trưởng Khoa" (#forwardModal-98), Nút "Từ Chối" (#rejectModal-98)

=== 4. VERIFY SEARCH FILTER ===
Search for '11C577': Contains Appt 98 = True, Displayed Count = 1 yêu cầu
Search for 'NonExistentKey999': Has empty message = "Không tìm thấy yêu cầu phù hợp" (True)
```

---

## 6. KẾT QUẢ AUTOMATED TESTS HỒI QUY

### 6.1. Backend Tests (`DentalClinic.Tests`)
- Lệnh chạy: `dotnet test BE\DentalClinic.Tests\DentalClinic.Tests.csproj`
- Kết quả: **Passed! - Failed: 0, Passed: 629, Skipped: 1, Total: 630**
- Bao gồm test case mới bổ sung: `GetReceptionistRequestsAsync_OrdersDescendingByCreatedAt_LatestRequestFirst` (PASS).

### 6.2. Frontend Tests (`DentalClinic.Web.Tests`)
- Lệnh chạy: `dotnet test FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj`
- Kết quả: **Passed! - Failed: 0, Passed: 289, Skipped: 0, Total: 289**
- Bao gồm 3 test case mới bổ sung:
  1. `Receptionist_Requests_ApiFailure_SetsErrorMessageAndDoesNotMaskAsSilentEmpty` (PASS)
  2. `Receptionist_Requests_SearchQuery_FiltersRequestsCorrectly` (PASS)
  3. `Receptionist_AppointmentDto_DeserializesServiceDurationCorrectly` (PASS)

---

## 7. QUẢN LÝ NHÁNH & CAM KẾT AN TOÀN

1. **Nhánh làm việc:** `fix/receptionist-appointment-requests-visibility`
   - Commit SHA: `05a8d8dc566fa8cec1f2cd49b7a770089d7ec0c7`
   - Đã push lên GitHub origin: `https://github.com/kaybern1512/SWP391_Group6/pull/new/fix/receptionist-appointment-requests-visibility`
2. **Tuân thủ an toàn:**
   - **KHÔNG** tự ý merge vào `develop` hoặc `main`.
   - **KHÔNG** xóa hoặc sửa dữ liệu của Appointment #98.
   - **KHÔNG** động chạm cơ sở dữ liệu `DentalClinicManagementDB`.
   - Giữ nguyên toàn bộ business rule của quy trình đặt lịch và chuyển tiếp lịch hẹn.
