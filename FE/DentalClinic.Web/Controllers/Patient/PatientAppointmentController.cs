using DentalClinic.Web.ViewModels.Services;

using DentalClinic.Web.Helpers;

using System;

using System.Linq;

using System.Security.Claims;

using System.Threading.Tasks;

using DentalClinic.Web.Models.ApiDtos;

using DentalClinic.Web.Services;

using DentalClinic.Web.ViewModels.Appointments;

using DentalClinic.Web.ViewModels.Feedback;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]

[Route("Patient/Appointment")]

[Route("Patient/Appointments")]

public class PatientAppointmentController : Controller

{

    private readonly IAppointmentApiService _appointmentApiService;

    private readonly IFeedbackApiService _feedbackApiService;
    private readonly IAppointmentDisruptionApiService? _disruptionApi;

public static readonly TimeZoneInfo ClinicTimeZone = ResolveClinicTimeZone();

private static TimeZoneInfo ResolveClinicTimeZone()

    {

        try

        {

            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

        }

        catch

        {

            try

            {

                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

            }

            catch

            {

                return TimeZoneInfo.Local;

            }

        }

    }

public static DateTime GetClinicNow()

    {

        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClinicTimeZone);

    }

public static DateOnly GetClinicToday()

    {

        return DateOnly.FromDateTime(GetClinicNow());

    }

    public PatientAppointmentController(
        IAppointmentApiService appointmentApiService,
        IFeedbackApiService feedbackApiService,
        IAppointmentDisruptionApiService? disruptionApi = null)
    {
        _appointmentApiService = appointmentApiService;
        _feedbackApiService = feedbackApiService;
        _disruptionApi = disruptionApi;
    }

[HttpGet("")]

    [HttpGet("Index")]

    public async Task<IActionResult> Index([FromQuery] string? status)

    {

        var result = await _appointmentApiService.GetPatientAppointmentsAsync(status);

        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();
        if (result.StatusCode == 401 || eligibilityResult?.StatusCode == 401)
        {
            TempData["ErrorMessage"] = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại để tiếp tục.";
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Index), "PatientAppointment") });
        }

var vm = new AppointmentListViewModel

        {

            Appointments = result.Data ?? new(),

            SelectedStatus = status,

            Role = "Patient",

            Eligibility = eligibilityResult?.Data

        };

        return View("~/Views/Patient/Appointment/Index.cshtml", vm);

    }

private static readonly string[] ExcludedDepartmentKeywords = new[] { "hình ảnh", "chuẩn đoán", "chẩn đoán", "phục hình" };

private static bool IsAllowedBookingDepartment(DepartmentDto d)

    {

        if (d == null || string.IsNullOrWhiteSpace(d.Name)) return false;

        return !ExcludedDepartmentKeywords.Any(k => d.Name.Contains(k, StringComparison.OrdinalIgnoreCase));

    }

private async Task<(List<DepartmentDto> Departments, List<DepartmentServiceDto> Services)> GetAllowedBookingCatalogAsync()

    {

        var deptResult = await _appointmentApiService.GetDepartmentsAsync();

        var allDepts = deptResult?.Data ?? new();

        var filteredDepts = allDepts.Where(IsAllowedBookingDepartment).ToList();

        var allowedDeptIds = filteredDepts.Select(d => d.DepartmentId).ToHashSet();

var servicesResult = await _appointmentApiService.GetServicesAsync(0);

        var allServices = servicesResult?.Data ?? new();

        var filteredServices = allServices.Where(s => allowedDeptIds.Contains(s.DepartmentId)).ToList();

return (filteredDepts, filteredServices);

    }

[HttpGet("Services")]

    [HttpGet("/Patient/Services")]

    public async Task<IActionResult> Services([FromQuery] string? search = null, [FromQuery] long? departmentId = null)

    {

        var (allowedDepts, allowedServices) = await GetAllowedBookingCatalogAsync();

var query = allowedServices.AsEnumerable();

        if (departmentId.HasValue && departmentId.Value > 0)

        {

            query = query.Where(s => s.DepartmentId == departmentId.Value);

        }

if (!string.IsNullOrWhiteSpace(search))

        {

            var term = search.Trim();

            query = query.Where(s =>

                (!string.IsNullOrWhiteSpace(s.ServiceName) && s.ServiceName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||

                (!string.IsNullOrWhiteSpace(s.Description) && s.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) ||

                (!string.IsNullOrWhiteSpace(s.DepartmentName) && s.DepartmentName.Contains(term, StringComparison.OrdinalIgnoreCase)));

        }

var dentistsByDept = new Dictionary<long, List<DentistOptionDto>>();

        foreach (var dept in allowedDepts)

        {

            var dentistsResult = await _appointmentApiService.GetDentistsAsync(dept.DepartmentId);

            if (dentistsResult?.Data != null)

            {

                dentistsByDept[dept.DepartmentId] = dentistsResult.Data;

            }

        }

var vm = new PatientServicesDiscoveryViewModel

        {

            Departments = allowedDepts,

            Services = query.ToList(),

            SelectedDepartmentId = departmentId,

            SearchTerm = search,

            DentistsByDepartment = dentistsByDept

        };

return View("~/Views/Patient/Services/Index.cshtml", vm);

    }

        public async Task<List<BookingComboViewModel>> GetCombosCatalogAsync()
    {
        try
        {
            var apiResult = await _appointmentApiService.GetAvailableCombosAsync();
            if (apiResult.IsSuccess && apiResult.Data != null && apiResult.Data.Any())
            {
                return apiResult.Data;
            }
        }
        catch
        {
            // fallback if API not reachable
        }

        return GetFallbackCombos();
    }

    public static List<BookingComboViewModel> GetAvailableCombos() => GetFallbackCombos();

    public static List<BookingComboViewModel> GetFallbackCombos()
    {
        return new List<BookingComboViewModel>
        {
            new()
            {
                Code = "COMBO_CARE",
                Title = "Chăm Sóc & Vệ Sinh Nụ Cười Sáng Khỏe",
                Tagline = "Làm sạch mảng bám, ngừa sâu răng và viêm nướu định kỳ",
                Badge = "Bán chạy nhất",
                IconClass = "bi-shield-check",
                Description = "Gói chăm sóc răng miệng toàn diện giúp loại bỏ hoàn toàn cao răng tích tụ, đánh bóng men răng sáng bóng và bổ sung lớp bảo vệ Flour chống sâu răng hiệu quả.",
                OriginalPrice = 450000,
                ComboPrice = 350000,
                DurationMinutes = 45,
                DepartmentId = 1,
                DepartmentName = "Khoa Răng Hàm Mặt Tổng Quát",
                ServiceId = 2,
                ServiceName = "Lấy cao răng & Đánh bóng",
                IncludedServices = new()
                {
                    "Khám tổng quát & Đánh giá sức khỏe răng nướu",
                    "Lấy cao răng siêu âm vô cảm không đau",
                    "Đánh bóng loại bỏ mảng bám ố vàng",
                    "Bôi Flouride tái khoáng men răng ngừa sâu răng"
                },
                Benefits = new()
                {
                    "Tiết kiệm 22% so với đặt từng dịch vụ lẻ",
                    "Bảo vệ men răng và nướu chắc khỏe suốt 6 tháng",
                    "Bác sĩ chuyên khoa trực tiếp thực hiện nhẹ nhàng"
                }
            },
            new()
            {
                Code = "COMBO_WHITENING",
                Title = "Nụ Cười Trắng Sáng Laser Whitening",
                Tagline = "Bật 2 - 3 tone màu răng ngay sau 60 phút với công nghệ Mỹ",
                Badge = "Thẩm mỹ cao cấp",
                IconClass = "bi-stars",
                Description = "Gói thẩm mỹ cao cấp kết hợp vệ sinh làm sạch sâu bề mặt và kích hoạt hoạt chất tẩy trắng an toàn bằng ánh sáng Laser lạnh chuẩn nha khoa quốc tế.",
                OriginalPrice = 2250000,
                ComboPrice = 1950000,
                DurationMinutes = 60,
                DepartmentId = 2,
                DepartmentName = "Khoa Chỉnh Nha & Nha Khoa Thẩm Mỹ",
                ServiceId = 6,
                ServiceName = "Tẩy trắng răng Laser Whitening",
                IncludedServices = new()
                {
                    "Khám đo độ sáng màu răng hiện tại theo bảng Vita",
                    "Lấy cao răng & Vệ sinh chuẩn bị bề mặt men răng",
                    "Cách ly bảo vệ nướu và môi chuyên dụng",
                    "Chiếu đèn Laser Whitening kích hoạt trắng sáng",
                    "Thoa gel chống ê buốt và hướng dẫn duy trì nụ cười đẹp"
                },
                Benefits = new()
                {
                    "Hiệu quả tức thì, không làm mòn men răng",
                    "Tiết kiệm 300.000đ khi kết hợp lấy cao răng",
                    "Cam kết độ sáng bền lâu từ 1.5 - 2 năm"
                }
            },
            new()
            {
                Code = "COMBO_ORTHO",
                Title = "Thăm Khám & Tư Vấn Chỉnh Nha Toàn Diện",
                Tagline = "Khảo sát khớp cắn, lấy dấu 3D và lập phác đồ niềng răng chi tiết",
                Badge = "Chuyên gia chỉnh nha",
                IconClass = "bi-emoji-smile",
                Description = "Gói dành riêng cho khách hàng có nhu cầu niềng răng (mắc cài kim loại, sứ, hoặc khay trong suốt Invisalign). Bác sĩ chuyên khoa II chỉnh nha sẽ trực tiếp thăm khám và lên phác đồ cá nhân hóa.",
                OriginalPrice = 500000,
                ComboPrice = 300000,
                DurationMinutes = 45,
                DepartmentId = 2,
                DepartmentName = "Khoa Chỉnh Nha & Nha Khoa Thẩm Mỹ",
                ServiceId = 5,
                ServiceName = "Khám & Tư vấn Chỉnh nha",
                IncludedServices = new()
                {
                    "Khám trực tiếp cùng Chuyên gia Chỉnh nha",
                    "Khảo sát tương quan xương hàm & đường cười thẩm mỹ",
                    "Lấy dấu răng mẫu hàm hoặc quét dấu kỹ thuật số 3D",
                    "Lập kế hoạch phác đồ điều trị và dự toán thời gian/chi phí"
                },
                Benefits = new()
                {
                    "Tư vấn 1:1 chuyên sâu cùng Bác sĩ Chỉnh nha",
                    "Xem trước mô phỏng kết quả sau khi niềng răng",
                    "Miễn phí 100% chi phí khám nếu đăng ký gói niềng răng"
                }
            },
            new()
            {
                Code = "COMBO_IMPLANT",
                Title = "Thăm Khám & Khảo Sát Chuyên Sâu Cấy Ghép Implant",
                Tagline = "Đánh giá chất lượng xương hàm và kế hoạch phục hình răng mất",
                Badge = "Chuyên khoa Implant",
                IconClass = "bi-award",
                Description = "Gói chuyên sâu dành cho người mất 1 hoặc nhiều răng. Bác sĩ phẫu thuật sẽ đo độ dày và mật độ xương cung hàm, tư vấn chọn dòng trụ Implant tương thích sinh học cao nhất.",
                OriginalPrice = 700000,
                ComboPrice = 500000,
                DurationMinutes = 45,
                DepartmentId = 3,
                DepartmentName = "Khoa Phẫu Thuật Trong Miệng & Cấy Ghép Implant",
                ServiceId = 7,
                ServiceName = "Khám chuyên sâu & Tư vấn cấy ghép Implant",
                IncludedServices = new()
                {
                    "Thăm khám lâm sàng và tiền sử bệnh nền toàn thân",
                    "Đọc phim khảo sát xoang hàm & dây thần kinh răng dưới",
                    "Đo mật độ xương và chỉ định kỹ thuật ghép xương (nếu cần)",
                    "Lên phương án phục hình răng sứ trên trụ Implant"
                },
                Benefits = new()
                {
                    "Bác sĩ chuyên gia trên 10 năm kinh nghiệm phẫu thuật Implant",
                    "Tư vấn dòng trụ chính hãng Châu Âu / Hàn Quốc bảo hành trọn đời",
                    "Khấu trừ toàn bộ phí khám khi thực hiện cấy trụ"
                }
            }
        };
    }

[HttpGet("Book")]
    public async Task<IActionResult> Book(
        [FromQuery] long? departmentId = null,
        [FromQuery] long? serviceId = null,
        [FromQuery] long? dentistId = null,
        [FromQuery] string? date = null,
        [FromQuery] string? reason = null,
        [FromQuery] string? category = null,
        [FromQuery] string? combo = null)
    {
        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();
        if (eligibilityResult?.StatusCode == 401)
        {
            TempData["ErrorMessage"] = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại để tiếp tục.";
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Book), "PatientAppointment", new { departmentId, serviceId, dentistId, date, reason, category, combo }) });
        }

        var eligibility = eligibilityResult?.Data ?? new BookingEligibilityDto();
        var (filteredDepts, filteredServices) = await GetAllowedBookingCatalogAsync();
        var availableCombos = await GetCombosCatalogAsync();

        var vm = new PatientBookViewModel
        {
            Departments = filteredDepts,
            Services = filteredServices,
            Eligibility = eligibility,
            Combos = availableCombos,
            DepartmentId = departmentId ?? 0,
            RequestedServiceId = serviceId ?? 0,
            RequestedDentistUserId = dentistId,
            AppointmentDate = !string.IsNullOrWhiteSpace(date) ? date : GetClinicToday().AddDays(1).ToString("yyyy-MM-dd"),
            ReasonForVisit = reason ?? string.Empty,
            PatientFullName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? User.FindFirst("unique_name")?.Value,
            PatientEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        };

        // Categorization & pre-selection based on query parameters or defaults
        if (!string.IsNullOrEmpty(combo))
        {
            var matchedCombo = availableCombos.FirstOrDefault(c =>
                c.Code.Equals(combo, StringComparison.OrdinalIgnoreCase) ||
                (combo.Equals("COMBO_HYGIENE", StringComparison.OrdinalIgnoreCase) && c.Code.Equals("COMBO_CARE", StringComparison.OrdinalIgnoreCase)) ||
                (combo.Equals("COMBO_CARE", StringComparison.OrdinalIgnoreCase) && c.Code.Equals("COMBO_HYGIENE", StringComparison.OrdinalIgnoreCase)));
            if (matchedCombo != null)
            {
                vm.BookingCategory = "Combo";
                vm.SelectedComboCode = combo.Equals("COMBO_HYGIENE", StringComparison.OrdinalIgnoreCase) ? combo : matchedCombo.Code;
                vm.DepartmentId = matchedCombo.DepartmentId;
                vm.RequestedServiceId = matchedCombo.ServiceId;
            }
        }
        else if (!string.IsNullOrEmpty(category))
        {
            vm.BookingCategory = category;
            if (category == "GeneralCheckup")
            {
                vm.DepartmentId = 1;
                vm.RequestedServiceId = 1;
            }
            else if (category == "Combo" && availableCombos.Any())
            {
                var first = availableCombos.First();
                vm.SelectedComboCode = first.Code;
                vm.DepartmentId = first.DepartmentId;
                vm.RequestedServiceId = first.ServiceId;
            }
        }
        else if (serviceId.HasValue && serviceId.Value > 0)
        {
            var matchedSrv = filteredServices.FirstOrDefault(s => s.DepartmentServiceId == serviceId.Value);
            if (matchedSrv != null)
            {
                vm.BookingCategory = "SingleService";
                vm.DepartmentId = matchedSrv.DepartmentId;
                vm.RequestedServiceId = matchedSrv.DepartmentServiceId;
            }
            else
            {
                vm.BookingCategory = "SingleService";
                vm.DepartmentId = departmentId ?? 1;
                vm.RequestedServiceId = serviceId.Value;
            }
        }
        else if (departmentId.HasValue && departmentId.Value > 0)
        {
            vm.BookingCategory = "SingleService";
            vm.DepartmentId = departmentId.Value;
            var firstSrv = filteredServices.FirstOrDefault(s => s.DepartmentId == departmentId.Value);
            if (firstSrv != null)
            {
                vm.RequestedServiceId = firstSrv.DepartmentServiceId;
            }
        }
        else
        {
            // Default: Khám tổng quát / Chưa biết nhu cầu
            vm.BookingCategory = "GeneralCheckup";
            vm.DepartmentId = 1;
            vm.RequestedServiceId = 1;
        }

        return View("~/Views/Patient/Appointment/Book.cshtml", vm);
    }
[HttpGet("GetServices")]

    public async Task<IActionResult> GetServices(long departmentId)

    {

        var result = await _appointmentApiService.GetServicesAsync(departmentId);

        var services = result?.Data ?? new();

        var deptResult = await _appointmentApiService.GetDepartmentsAsync();

        if (deptResult?.Data != null && deptResult.Data.Any())

        {

            var allowedDeptIds = deptResult.Data

                .Where(IsAllowedBookingDepartment)

                .Select(d => d.DepartmentId)

                .ToHashSet();

var filtered = services.Where(s => allowedDeptIds.Contains(s.DepartmentId)).ToList();

            return Json(filtered);

        }

        return Json(services);

    }

[HttpGet("GetDentists")]

    public async Task<IActionResult> GetDentists(long departmentId)

    {

        var result = await _appointmentApiService.GetDentistsAsync(departmentId);

        return Json(result.Data ?? new());

    }

[HttpGet("GetSlots")]

    public async Task<IActionResult> GetSlots(long departmentId, long serviceId, string date, long? dentistUserId)

    {

        if (!DateOnly.TryParse(date, out var parsedDate))

        {

            return BadRequest(new { message = "Định dạng ngày không hợp lệ." });

        }

var clinicToday = GetClinicToday();

        if (parsedDate < clinicToday)

        {

            return Json(new List<AvailableSlotDto>());

        }

var result = await _appointmentApiService.GetAvailableSlotsAsync(departmentId, serviceId, parsedDate, dentistUserId);

        var slots = result?.Data ?? new List<AvailableSlotDto>();

if (parsedDate == clinicToday)

        {

            var clinicNow = GetClinicNow();

            slots = slots.Where(s => s.StartTime > clinicNow.AddMinutes(15)).ToList();

        }

return Json(slots);

    }

[HttpPost("Book")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(PatientBookViewModel model)
    {
        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();
        var eligibility = eligibilityResult?.Data ?? new BookingEligibilityDto();

        if (!eligibility.IsEligible)
        {
            TempData["ErrorMessage"] = eligibility.BlockReason ?? "Tài khoản của bạn hiện đang bị tạm khóa quyền đặt lịch trực tuyến.";
            model.Eligibility = eligibility;
            var catalog = await GetAllowedBookingCatalogAsync();
            model.Departments = catalog.Departments;
            model.Services = catalog.Services;
            model.Combos = GetAvailableCombos();
            return View("~/Views/Patient/Appointment/Book.cshtml", model);
        }

        var clinicNow = GetClinicNow();
        if (model.ScheduledStart.HasValue && model.ScheduledStart.Value <= clinicNow.AddMinutes(15))
        {
            ModelState.AddModelError("ScheduledStart", "Thời gian khám không được nằm trong quá khứ hoặc quá sát giờ hiện tại (cần đặt trước tối thiểu 15 phút). Vui lòng chọn khung giờ khám hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            model.Eligibility = eligibility;
            var catalog = await GetAllowedBookingCatalogAsync();
            model.Departments = catalog.Departments;
            model.Services = catalog.Services;
            model.Combos = GetAvailableCombos();
            return View("~/Views/Patient/Appointment/Book.cshtml", model);
        }

        var availableCombos = await GetCombosCatalogAsync();
        var reasonToSubmit = model.ReasonForVisit ?? string.Empty;
        if (model.BookingCategory == "Combo" && !string.IsNullOrEmpty(model.SelectedComboCode))
        {
            var matchedCombo = availableCombos.FirstOrDefault(c => c.Code.Equals(model.SelectedComboCode, StringComparison.OrdinalIgnoreCase));
            if (matchedCombo != null && !reasonToSubmit.Contains($"[Gói Combo: {matchedCombo.Title}]"))
            {
                reasonToSubmit = $"[Gói Combo: {matchedCombo.Title}] {(string.IsNullOrWhiteSpace(reasonToSubmit) ? "Đăng ký khám trọn gói combo." : reasonToSubmit)}";
            }
        }

        // Service -> Department integrity safeguard: ensure department matches the selected service
        if (model.RequestedServiceId > 0)
        {
            var catalog = await GetAllowedBookingCatalogAsync();
            var chosenService = catalog.Services.FirstOrDefault(s => s.DepartmentServiceId == model.RequestedServiceId);
            if (chosenService != null && chosenService.DepartmentId != model.DepartmentId)
            {
                model.DepartmentId = chosenService.DepartmentId;
            }
        }

        var request = new CreateAppointmentRequest
        {
            DepartmentId = model.DepartmentId,
            RequestedServiceId = model.RequestedServiceId,
            RequestedDentistUserId = string.Equals(model.DoctorSelectionMode, "ClinicAssignment", StringComparison.OrdinalIgnoreCase) ? null : model.RequestedDentistUserId,
            ScheduledStart = model.ScheduledStart!.Value,
            ReasonForVisit = reasonToSubmit
        };

        var result = await _appointmentApiService.BookAppointmentAsync(request);
        if (!result.IsSuccess)
        {
            if (result.StatusCode == 401)
            {
                TempData["ErrorMessage"] = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại để tiếp tục.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Book), "PatientAppointment") });
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể đặt lịch khám.");
            model.Eligibility = eligibility;
            var catalog = await GetAllowedBookingCatalogAsync();
            model.Departments = catalog.Departments;
            model.Services = catalog.Services;
            model.Combos = GetAvailableCombos();
            return View("~/Views/Patient/Appointment/Book.cshtml", model);
        }

        TempData["SuccessMessage"] = "Yêu cầu đặt lịch khám đã được gửi thành công!";
        return RedirectToAction(nameof(Success), new { id = result.Data!.AppointmentId });
    }
[HttpGet("Success")]

    public async Task<IActionResult> Success(long id)

    {

        var result = await _appointmentApiService.GetAppointmentDetailAsync(id);

        if (!result.IsSuccess || result.Data == null)

        {

            return RedirectToAction(nameof(Index));

        }

return View("~/Views/Patient/Appointment/Success.cshtml", result.Data);

    }

[HttpGet("Detail/{id:long}")]

    public async Task<IActionResult> Detail(long id)

    {

        var result = await _appointmentApiService.GetAppointmentDetailAsync(id);

        if (!result.IsSuccess || result.Data == null)

        {

            TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy thông tin lịch hẹn.";

            return RedirectToAction(nameof(Index));

        }

var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && result.Data.PatientUserId > 0)

        {

            if (result.Data.PatientUserId != currentUserId)

            {

                if (TempData != null) TempData["ErrorMessage"] = "Bạn không có quyền truy cập thông tin ca khám này.";

                return RedirectToAction(nameof(Index));

            }

        }

var vm = new AppointmentDetailViewModel

        {

            Appointment = result.Data,

            ActiveProposal = result.Data.ActiveProposal,

            StatusHistories = result.Data.StatusHistories,

            CurrentUserRole = "Patient",

            CurrentUserId = currentUserId

        };

return View("~/Views/Patient/Appointment/Detail.cshtml", vm);

    }

[HttpPost("Withdraw/{id:long}")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Withdraw(long id, [FromForm] string? reason)

    {

        var detailRes = await _appointmentApiService.GetAppointmentDetailAsync(id);

        var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (detailRes?.IsSuccess == true && detailRes.Data != null && long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && detailRes.Data.PatientUserId > 0)

        {

            if (detailRes.Data.PatientUserId != currentUserId)

            {

                if (TempData != null) TempData["ErrorMessage"] = "Bạn không có quyền thu hồi ca khám này.";

                return RedirectToAction(nameof(Index));

            }

        }

var result = await _appointmentApiService.PatientWithdrawAsync(id, reason);

        if (result.IsSuccess)

        {

            TempData["SuccessMessage"] = !string.IsNullOrWhiteSpace(result.Message) ? result.Message : "Đã thu hồi yêu cầu đặt lịch khám thành công.";

        }

        else

        {

            TempData["ErrorMessage"] = result.Message ?? "Không thể thu hồi yêu cầu đặt lịch.";

        }

return RedirectToAction(nameof(Detail), new { id });

    }

[HttpPost("Cancel/{id:long}")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Cancel(long id, [FromForm] string reason)

    {

        if (string.IsNullOrWhiteSpace(reason))

        {

            TempData["ErrorMessage"] = "Vui lòng nhập lý do hủy lịch hẹn.";

            return RedirectToAction(nameof(Detail), new { id });

        }

var detailRes = await _appointmentApiService.GetAppointmentDetailAsync(id);

        var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (detailRes?.IsSuccess == true && detailRes.Data != null && long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && detailRes.Data.PatientUserId > 0)

        {

            if (detailRes.Data.PatientUserId != currentUserId)

            {

                if (TempData != null) TempData["ErrorMessage"] = "Bạn không có quyền hủy ca khám này.";

                return RedirectToAction(nameof(Index));

            }

        }

var result = await _appointmentApiService.PatientCancelAsync(id, reason);

        if (result.IsSuccess)

        {

            TempData["SuccessMessage"] = !string.IsNullOrWhiteSpace(result.Message) ? result.Message : "Đã hủy lịch hẹn thành công.";

        }

        else

        {

            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy lịch hẹn.";

        }

return RedirectToAction(nameof(Detail), new { id });

    }

[HttpGet("Reschedule/{id:long}")]

    public async Task<IActionResult> Reschedule(long id)

    {

        var result = await _appointmentApiService.GetAppointmentDetailAsync(id);

        if (!result.IsSuccess || result.Data == null)

        {

            TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy lịch hẹn.";

            return RedirectToAction(nameof(Index));

        }

var appt = result.Data;

        var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && appt.PatientUserId > 0)

        {

            if (appt.PatientUserId != currentUserId)

            {

                if (TempData != null) TempData["ErrorMessage"] = "Bạn không có quyền dời ca khám này.";

                return RedirectToAction(nameof(Index));

            }

        }

var validStatuses = new[] { "PendingReceptionReview", "PendingDepartmentReview", "AwaitingPatientResponse", "Confirmed" };

        if (!validStatuses.Contains(appt.Status))

        {

            TempData["ErrorMessage"] = $"Không thể dời lịch khi ca khám ở trạng thái {appt.Status}.";

            return RedirectToAction(nameof(Detail), new { id });

        }

var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();

        var dentistsResult = await _appointmentApiService.GetDentistsAsync(appt.DepartmentId);

var vm = new PatientRescheduleViewModel

        {

            AppointmentId = appt.AppointmentId,

            AppointmentCode = appt.AppointmentCode,

            DepartmentId = appt.DepartmentId,

            DepartmentName = appt.DepartmentName,

            RequestedServiceId = appt.RequestedServiceId,

            ServiceName = appt.ServiceName,

            CurrentScheduledStart = appt.ScheduledStart,

            CurrentDentistName = appt.AssignedDentistName ?? appt.RequestedDentistName,

            NewRequestedDentistUserId = appt.RequestedDentistUserId,

            NewAppointmentDate = ClinicTimeHelper.Today.AddDays(1).ToString("yyyy-MM-dd"),

            Dentists = dentistsResult.Data ?? new(),

            Eligibility = eligibilityResult?.Data

        };

return View("~/Views/Patient/Appointment/Reschedule.cshtml", vm);

    }

[HttpPost("Reschedule/{id:long}")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Reschedule(long id, PatientRescheduleViewModel model)

    {

        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();

        var eligibility = eligibilityResult?.Data ?? new BookingEligibilityDto();

if (!eligibility.IsEligible)

        {

            TempData["ErrorMessage"] = eligibility.BlockReason ?? "Tài khoản của bạn hiện đang bị tạm khóa quyền đặt/dời lịch khám trực tuyến.";

            return RedirectToAction(nameof(Detail), new { id });

        }

var rescheduleClinicNow = GetClinicNow();

        if (model.NewScheduledStart.HasValue && model.NewScheduledStart.Value <= rescheduleClinicNow.AddMinutes(15))

        {

            ModelState.AddModelError("NewScheduledStart", "Thời gian khám mới không được nằm trong quá khứ hoặc quá sát giờ hiện tại (cần đặt trước tối thiểu 15 phút). Vui lòng chọn khung giờ khám hợp lệ.");

        }

if (!ModelState.IsValid)

        {

            var detailRes = await _appointmentApiService.GetAppointmentDetailAsync(id);

            if (detailRes.IsSuccess && detailRes.Data != null)

            {

                var appt = detailRes.Data;

                model.AppointmentCode = appt.AppointmentCode;

                model.DepartmentName = appt.DepartmentName;

                model.ServiceName = appt.ServiceName;

                model.CurrentScheduledStart = appt.ScheduledStart;

                model.CurrentDentistName = appt.AssignedDentistName ?? appt.RequestedDentistName;

                var dentistsResult = await _appointmentApiService.GetDentistsAsync(appt.DepartmentId);

                model.Dentists = dentistsResult.Data ?? new();

                model.Eligibility = eligibility;

            }

            return View("~/Views/Patient/Appointment/Reschedule.cshtml", model);

        }

var req = new PatientRescheduleRequest

        {

            NewScheduledStart = model.NewScheduledStart!.Value,

            NewRequestedDentistUserId = model.NewRequestedDentistUserId,

            Reason = model.Reason

        };

var result = await _appointmentApiService.PatientRescheduleAsync(id, req);

        if (!result.IsSuccess)

        {

            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể dời lịch khám.");

            var detailRes = await _appointmentApiService.GetAppointmentDetailAsync(id);

            if (detailRes.IsSuccess && detailRes.Data != null)

            {

                var appt = detailRes.Data;

                model.AppointmentCode = appt.AppointmentCode;

                model.DepartmentName = appt.DepartmentName;

                model.ServiceName = appt.ServiceName;

                model.CurrentScheduledStart = appt.ScheduledStart;

                model.CurrentDentistName = appt.AssignedDentistName ?? appt.RequestedDentistName;

                var dentistsResult = await _appointmentApiService.GetDentistsAsync(appt.DepartmentId);

                model.Dentists = dentistsResult.Data ?? new();

                model.Eligibility = eligibility;

            }

            return View("~/Views/Patient/Appointment/Reschedule.cshtml", model);

        }

TempData["SuccessMessage"] = result.Message ?? "Đã hủy lịch hẹn cũ và chuyển sang lịch khám mới thành công!";

        return RedirectToAction(nameof(Success), new { id = result.Data!.AppointmentId });

    }

[HttpPost("Proposal/Accept/{id:long}")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> AcceptProposal(long id)

    {

        var result = await _appointmentApiService.AcceptProposalAsync(id);

        if (result.IsSuccess)

        {

            TempData["SuccessMessage"] = "Đã đồng ý phương án đề xuất! Lịch hẹn của bạn đã được xác nhận.";

        }

        else

        {

            TempData["ErrorMessage"] = result.Message ?? "Không thể đồng ý đề xuất thay đổi.";

        }

return RedirectToAction(nameof(Detail), new { id });

    }

[HttpPost("Proposal/Reject/{id:long}")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> RejectProposal(long id)

    {

        var result = await _appointmentApiService.RejectProposalAsync(id);

        if (result.IsSuccess)

        {

            TempData["SuccessMessage"] = "Đã từ chối phương án thay đổi. Yêu cầu đã được chuyển về bộ phận Lễ tân phòng khám xử lý.";

        }

        else

        {

            TempData["ErrorMessage"] = result.Message ?? "Không thể từ chối đề xuất.";

        }

return RedirectToAction(nameof(Detail), new { id });

    }

[HttpGet("Feedback/{id:long}")]

    public async Task<IActionResult> Feedback(long id)

    {

        var apptResult = await _appointmentApiService.GetAppointmentDetailAsync(id);

        if (!apptResult.IsSuccess || apptResult.Data == null)

        {

            TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn.";

            return RedirectToAction(nameof(Index));

        }

var appt = apptResult.Data;

        var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && appt.PatientUserId > 0)

        {

            if (appt.PatientUserId != currentUserId)

            {

                if (TempData != null) TempData["ErrorMessage"] = "Bạn chỉ có quyền đánh giá các ca khám của chính mình.";

                return RedirectToAction(nameof(Index));

            }

        }

if (appt.Status != "Completed")

        {

            TempData["ErrorMessage"] = "Chỉ có thể đánh giá ca khám đã hoàn thành.";

            return RedirectToAction(nameof(Detail), new { id });

        }

var fbResult = await _feedbackApiService.GetFeedbackByAppointmentIdAsync(id);

        var existingFb = fbResult.IsSuccess ? fbResult.Data : null;

var vm = new PatientFeedbackFormViewModel

        {

            AppointmentId = appt.AppointmentId,

            AppointmentCode = appt.AppointmentCode,

            DentistName = appt.AssignedDentistName ?? appt.RequestedDentistName,

            DepartmentName = appt.DepartmentName,

            ServiceName = appt.ServiceName,

            AppointmentDate = appt.ScheduledStart,

            IsAlreadySubmitted = existingFb != null,

            ExistingFeedback = existingFb,

            DentistRating = existingFb?.DentistRating ?? 5,

            ClinicRating = existingFb?.ClinicRating ?? 5,

            Comment = existingFb?.Comment

        };

return View("~/Views/Patient/Appointment/Feedback.cshtml", vm);

    }

[HttpPost("Feedback")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SubmitFeedback(PatientFeedbackFormViewModel model)

    {

        if (!ModelState.IsValid)

        {

            TempData["ErrorMessage"] = "Dữ liệu đánh giá không hợp lệ. Vui lòng chấm điểm từ 1 đến 5 sao.";

            return RedirectToAction(nameof(Feedback), new { id = model.AppointmentId });

        }

var req = new CreateFeedbackRequest

        {

            AppointmentId = model.AppointmentId,

            DentistRating = model.DentistRating,

            ClinicRating = model.ClinicRating,

            Comment = model.Comment?.Trim()

        };

var result = await _feedbackApiService.CreateFeedbackAsync(req);

        if (result.IsSuccess)

        {

            TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi đánh giá và đóng góp ý kiến cho phòng khám!";

        }

        else

        {

            TempData["ErrorMessage"] = result.Message ?? "Không thể gửi đánh giá.";

        }

return RedirectToAction(nameof(Detail), new { id = model.AppointmentId });

    }

    // ==========================================
    // DOCTOR LEAVE DISRUPTION RECOVERY ACTIONS
    // ==========================================

    [HttpGet("Disruption/{id:long}")]
    public async Task<IActionResult> Disruption(long id, [FromQuery] string? token)
    {
        if (_disruptionApi == null)
        {
            TempData["ErrorMessage"] = "Dịch vụ xử lý gián đoạn lịch khám hiện chưa khả dụng.";
            return RedirectToAction(nameof(Index));
        }

        var disruptionResult = await _disruptionApi.GetDisruptionDetailAsync(id, token);
        if (!disruptionResult.IsSuccess || disruptionResult.Data == null)
        {
            TempData["ErrorMessage"] = disruptionResult.Message ?? "Không tìm thấy thông tin sự cố gián đoạn lịch hẹn hoặc liên kết đã hết hiệu lực.";
            return RedirectToAction(nameof(Index));
        }

        var disruption = disruptionResult.Data;
        var altResult = await _disruptionApi.GetAvailableAlternativeDentistsAsync(id);
        var alternatives = altResult.IsSuccess && altResult.Data != null ? altResult.Data : new List<AlternativeDentistDto>();

        var apptResult = await _appointmentApiService.GetAppointmentDetailAsync(disruption.AppointmentId);
        var appt = apptResult.IsSuccess ? apptResult.Data : null;

        var vm = new PatientDisruptionViewModel
        {
            Disruption = disruption,
            AlternativeDentists = alternatives,
            Appointment = appt,
            ActionToken = token ?? disruption.ActionToken
        };

        return View("~/Views/Patient/Appointment/DisruptionDetail.cshtml", vm);
    }

    [HttpPost("Disruption/{id:long}/ChooseDentist")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChooseAlternativeDentist(long id, [FromForm] long newDentistUserId, [FromForm] string? note, [FromForm] string? actionToken)
    {
        if (_disruptionApi == null)
        {
            TempData["ErrorMessage"] = "Dịch vụ xử lý gián đoạn lịch khám hiện chưa khả dụng.";
            return RedirectToAction(nameof(Index));
        }

        var req = new PatientChooseNewDentistRequest
        {
            NewDentistUserId = newDentistUserId,
            Note = note,
            ActionToken = actionToken
        };

        var result = await _disruptionApi.PatientChooseNewDentistAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã chuyển đổi bác sĩ điều trị thành công. Lịch hẹn của bạn giữ nguyên ngày giờ đã đặt!";
            var apptId = result.Data?.AppointmentId ?? id;
            return RedirectToAction(nameof(Detail), new { id = apptId });
        }

        TempData["ErrorMessage"] = result.Message ?? "Không thể chọn bác sĩ thay thế.";
        return RedirectToAction(nameof(Disruption), new { id, token = actionToken });
    }

    [HttpPost("Disruption/{id:long}/Reschedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleDisruption(long id, [FromForm] DateTime newScheduledStart, [FromForm] string? reason, [FromForm] string? actionToken)
    {
        if (_disruptionApi == null)
        {
            TempData["ErrorMessage"] = "Dịch vụ xử lý gián đoạn lịch khám hiện chưa khả dụng.";
            return RedirectToAction(nameof(Index));
        }

        var req = new PatientDisruptionRescheduleRequest
        {
            NewScheduledStart = newScheduledStart,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Bệnh nhân dời lịch do bác sĩ phụ trách nghỉ phép" : reason,
            ActionToken = actionToken
        };

        var result = await _disruptionApi.PatientRescheduleDisruptionAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã dời lịch hẹn khám mới thành công! Hệ thống đã gửi email xác nhận đến bạn.";
            var targetId = result.Data?.ResolvedAppointmentId ?? result.Data?.AppointmentId ?? id;
            return RedirectToAction(nameof(Detail), new { id = targetId });
        }

        TempData["ErrorMessage"] = result.Message ?? "Không thể dời lịch hẹn.";
        return RedirectToAction(nameof(Disruption), new { id, token = actionToken });
    }
}
