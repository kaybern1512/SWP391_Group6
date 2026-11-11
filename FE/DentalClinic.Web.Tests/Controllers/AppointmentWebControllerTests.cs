using DentalClinic.Web.ViewModels.Feedback;

using System;

using System.Collections.Generic;

using System.Security.Claims;

using System.Threading.Tasks;

using DentalClinic.Web.Controllers.Dentist;

using DentalClinic.Web.Controllers.Patient;

using DentalClinic.Web.Controllers.Receptionist;

using DentalClinic.Web.Models.Api;

using DentalClinic.Web.Models.ApiDtos;

using DentalClinic.Web.Services;

using DentalClinic.Web.ViewModels.Appointments;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Mvc.ViewFeatures;

using Moq;

using Xunit;



namespace DentalClinic.Web.Tests.Controllers;



public class AppointmentWebControllerTests

{

    private readonly Mock<IAppointmentApiService> _apiMock;

    private readonly Mock<IFeedbackApiService> _feedbackApiMock;
    private readonly Mock<IAppointmentDisruptionApiService> _disruptionApiMock;



    public AppointmentWebControllerTests()

    {

        _apiMock = new Mock<IAppointmentApiService>();

        _feedbackApiMock = new Mock<IFeedbackApiService>();
        _disruptionApiMock = new Mock<IAppointmentDisruptionApiService>();

    }



    private static ControllerContext CreateContextWithUser(long userId, string role, string name = "User")

    {

        var httpContext = new DefaultHttpContext();

        var claims = new List<Claim>

        {

            new(ClaimTypes.NameIdentifier, userId.ToString()),

            new(ClaimTypes.Name, name),

            new(ClaimTypes.Role, role)

        };

        var identity = new ClaimsIdentity(claims, "CookieAuth");

        httpContext.User = new ClaimsPrincipal(identity);

        return new ControllerContext { HttpContext = httpContext };

    }



    [Fact]

    public async Task Patient_Book_Get_ReturnsViewWithDepartments()

    {

        _apiMock.Setup(x => x.GetDepartmentsAsync())

            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>

            {

                new() { DepartmentId = 1, Name = "Khoa Răng Tổng Quát" }

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.Book();



        var viewResult = result.Should().BeOfType<ViewResult>().Subject;

        var model = viewResult.Model.Should().BeOfType<PatientBookViewModel>().Subject;

        model.Departments.Should().HaveCount(1);

    }



    [Fact]

    public async Task Patient_Book_Post_Valid_RedirectsToSuccess()

    {

        _apiMock.Setup(x => x.BookAppointmentAsync(It.IsAny<CreateAppointmentRequest>()))

            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto

            {

                AppointmentId = 555,

                AppointmentCode = "APP-20261003-ABCDEF"

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var model = new PatientBookViewModel

        {

            DepartmentId = 1,

            RequestedServiceId = 10,

            ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),

            ReasonForVisit = "Đau răng cần khám gấp"

        };



        var result = await controller.Book(model);



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Success");

        redirect.RouteValues!["id"].Should().Be(555L);

    }



    [Fact]

    public async Task Patient_Book_Post_OptionB_ClinicAssignment_ForcesNullDentistId()

    {

        CreateAppointmentRequest? capturedRequest = null;

        _apiMock.Setup(x => x.BookAppointmentAsync(It.IsAny<CreateAppointmentRequest>()))

            .Callback<CreateAppointmentRequest>(r => capturedRequest = r)

            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto { AppointmentId = 777 }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var model = new PatientBookViewModel

        {

            DepartmentId = 1,

            RequestedServiceId = 10,

            DoctorSelectionMode = "ClinicAssignment",

            RequestedDentistUserId = 999,

            ScheduledStart = DateTime.Today.AddDays(2).AddHours(10),

            ReasonForVisit = "Đau buốt răng số 6 khi uống nước lạnh"

        };



        var result = await controller.Book(model);



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Success");

        capturedRequest.Should().NotBeNull();

        capturedRequest!.RequestedDentistUserId.Should().BeNull();

    }



    [Fact]

    public async Task Patient_Book_Post_OptionA_PreferredDentist_RetainsDentistId()

    {

        CreateAppointmentRequest? capturedRequest = null;

        _apiMock.Setup(x => x.BookAppointmentAsync(It.IsAny<CreateAppointmentRequest>()))

            .Callback<CreateAppointmentRequest>(r => capturedRequest = r)

            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto { AppointmentId = 888 }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var model = new PatientBookViewModel

        {

            DepartmentId = 1,

            RequestedServiceId = 10,

            DoctorSelectionMode = "PreferredDentist",

            RequestedDentistUserId = 205,

            ScheduledStart = DateTime.Today.AddDays(2).AddHours(14),

            ReasonForVisit = "Tái khám kiểm tra răng sâu"

        };



        var result = await controller.Book(model);



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Success");

        capturedRequest.Should().NotBeNull();

        capturedRequest!.RequestedDentistUserId.Should().Be(205);

    }

[Fact]
    public async Task Patient_Book_Get_WithComboParam_PreselectsComboCategoryAndService()
    {
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Khoa Răng Tổng Quát" },
                new() { DepartmentId = 2, Name = "Khoa Chỉnh Nha" }
            }));
        _apiMock.Setup(x => x.GetServicesAsync(It.IsAny<long>()))
            .ReturnsAsync(ApiResult<List<DepartmentServiceDto>>.Success(new List<DepartmentServiceDto>
            {
                new() { DepartmentServiceId = 2, DepartmentId = 1, ServiceName = "Lấy cao răng" }
            }));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.Book(combo: "COMBO_HYGIENE");

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<PatientBookViewModel>().Subject;
        model.BookingCategory.Should().Be("Combo");
        model.SelectedComboCode.Should().Be("COMBO_HYGIENE");
        model.DepartmentId.Should().Be(1);
        model.RequestedServiceId.Should().Be(2);
        model.Combos.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Patient_Book_Get_WithCategoryGeneralCheckup_PreselectsGeneralCheckup()
    {
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Khoa Răng Tổng Quát" }
            }));
        _apiMock.Setup(x => x.GetServicesAsync(It.IsAny<long>()))
            .ReturnsAsync(ApiResult<List<DepartmentServiceDto>>.Success(new List<DepartmentServiceDto>()));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.Book(category: "GeneralCheckup");

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<PatientBookViewModel>().Subject;
        model.BookingCategory.Should().Be("GeneralCheckup");
        model.DepartmentId.Should().Be(1);
        model.RequestedServiceId.Should().Be(1);
    }

    [Fact]
    public async Task Patient_Book_Get_WithDepartmentAndService_PreselectsSingleService()
    {
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>
            {
                new() { DepartmentId = 2, Name = "Khoa Chỉnh Nha" }
            }));
        _apiMock.Setup(x => x.GetServicesAsync(It.IsAny<long>()))
            .ReturnsAsync(ApiResult<List<DepartmentServiceDto>>.Success(new List<DepartmentServiceDto>
            {
                new() { DepartmentServiceId = 6, DepartmentId = 2, ServiceName = "Tẩy trắng Laser" }
            }));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.Book(departmentId: 2, serviceId: 6);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<PatientBookViewModel>().Subject;
        model.BookingCategory.Should().Be("SingleService");
        model.DepartmentId.Should().Be(2);
        model.RequestedServiceId.Should().Be(6);
    }

    [Fact]
    public async Task Patient_Book_Get_DefaultNoParams_DefaultsToGeneralCheckup()
    {
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Khoa Răng Tổng Quát" }
            }));
        _apiMock.Setup(x => x.GetServicesAsync(It.IsAny<long>()))
            .ReturnsAsync(ApiResult<List<DepartmentServiceDto>>.Success(new List<DepartmentServiceDto>()));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.Book();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<PatientBookViewModel>().Subject;
        model.BookingCategory.Should().Be("GeneralCheckup");
        model.DepartmentId.Should().Be(1);
        model.RequestedServiceId.Should().Be(1);
    }

    [Fact]
    public async Task Patient_Book_Post_ComboCategory_PrependsComboTitleToReasonForVisit()
    {
        CreateAppointmentRequest? capturedRequest = null;
        _apiMock.Setup(x => x.BookAppointmentAsync(It.IsAny<CreateAppointmentRequest>()))
            .Callback<CreateAppointmentRequest>(r => capturedRequest = r)
            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto { AppointmentId = 999 }));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new PatientBookViewModel
        {
            DepartmentId = 2,
            RequestedServiceId = 6,
            BookingCategory = "Combo",
            SelectedComboCode = "COMBO_WHITENING",
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(14),
            ReasonForVisit = "Mong muốn làm trắng răng đón Tết"
        };

        var result = await controller.Book(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Success");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.ReasonForVisit.Should().StartWith("[Gói Combo: Nụ Cười Trắng Sáng Laser Whitening]");
        capturedRequest.ReasonForVisit.Should().Contain("Mong muốn làm trắng răng đón Tết");
    }



    [Fact]
    public async Task Patient_Services_ReturnsViewWithFilteredCatalog()
    {
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Khoa Răng Tổng Quát" },
                new() { DepartmentId = 2, Name = "Khoa Chỉnh Nha" }
            }));

        _apiMock.Setup(x => x.GetServicesAsync(It.IsAny<long>()))
            .ReturnsAsync(ApiResult<List<DepartmentServiceDto>>.Success(new List<DepartmentServiceDto>
            {
                new() { DepartmentServiceId = 1, ServiceName = "Cạo Vôi Răng", DepartmentId = 1, Price = 200000, DurationMinutes = 30 },
                new() { DepartmentServiceId = 2, ServiceName = "Niềng Răng Mắc Cài", DepartmentId = 2, Price = 25000000, DurationMinutes = 60 }
            }));

        _apiMock.Setup(x => x.GetDentistsAsync(It.IsAny<long>()))
            .ReturnsAsync(ApiResult<List<DentistOptionDto>>.Success(new List<DentistOptionDto>()));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.Services("cạo vôi", 1);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DentalClinic.Web.ViewModels.Services.PatientServicesDiscoveryViewModel>().Subject;
        model.Services.Should().HaveCount(1);
        model.Services.First().ServiceName.Should().Be("Cạo Vôi Răng");
        model.SearchTerm.Should().Be("cạo vôi");
        model.SelectedDepartmentId.Should().Be(1);
    }

    [Fact]
    public async Task Receptionist_Requests_ReturnsViewWithList()

    {

        _apiMock.Setup(x => x.GetReceptionistRequestsAsync())

            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>

            {

                new() { AppointmentId = 1, AppointmentCode = "APP-001" },

                new() { AppointmentId = 2, AppointmentCode = "APP-002" }

            }));



        var controller = new ReceptionistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(201, "Receptionist")

        };



        var result = await controller.Requests();



        var view = result.Should().BeOfType<ViewResult>().Subject;

        var vm = view.Model.Should().BeOfType<ReceptionistRequestsViewModel>().Subject;

        vm.Requests.Should().HaveCount(2);

        vm.HasError.Should().BeFalse();

    }



    [Fact]

    public async Task Receptionist_Requests_ApiFailure_SetsErrorMessageAndDoesNotMaskAsSilentEmpty()

    {

        _apiMock.Setup(x => x.GetReceptionistRequestsAsync())

            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Failure("Máy chủ API quá tải hoặc phiên đăng nhập đã hết hạn.", statusCode: 401));



        var controller = new ReceptionistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(201, "Receptionist"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var result = await controller.Requests();



        var view = result.Should().BeOfType<ViewResult>().Subject;

        var vm = view.Model.Should().BeOfType<ReceptionistRequestsViewModel>().Subject;

        vm.Requests.Should().BeEmpty();

        vm.HasError.Should().BeTrue();

        vm.ErrorMessage.Should().Contain("Máy chủ API");

        controller.TempData["ErrorMessage"].Should().Be(vm.ErrorMessage);

    }



    [Fact]

    public async Task Receptionist_Requests_SearchQuery_FiltersRequestsCorrectly()

    {

        _apiMock.Setup(x => x.GetReceptionistRequestsAsync())

            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>

            {

                new() { AppointmentId = 1, AppointmentCode = "APP-001", PatientName = "Nguyễn Văn A", PatientPhone = "0901234567" },

                new() { AppointmentId = 98, AppointmentCode = "APP-20261107-11C577", PatientName = "Lê Hoàng Minh", PatientPhone = "0988112233" }

            }));



        var controller = new ReceptionistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(201, "Receptionist")

        };



        var result = await controller.Requests("11C577");



        var view = result.Should().BeOfType<ViewResult>().Subject;

        var vm = view.Model.Should().BeOfType<ReceptionistRequestsViewModel>().Subject;

        vm.Requests.Should().HaveCount(1);

        vm.Requests[0].AppointmentId.Should().Be(98);

        vm.Requests[0].AppointmentCode.Should().Be("APP-20261107-11C577");

        vm.SearchQuery.Should().Be("11C577");

    }



    [Fact]

    public void Receptionist_AppointmentDto_DeserializesServiceDurationCorrectly()

    {

        var json = """

        {

            "appointmentId": 98,

            "appointmentCode": "APP-20261107-11C577",

            "serviceName": "Cấy Ghép Implant Straumann QA 831",

            "serviceDuration": 90,

            "servicePrice": 28500000.00

        }

        """;



        var dto = System.Text.Json.JsonSerializer.Deserialize<AppointmentDto>(json, new System.Text.Json.JsonSerializerOptions

        {

            PropertyNameCaseInsensitive = true

        });



        dto.Should().NotBeNull();

        dto!.DurationMinutes.Should().Be(90);

        dto.ServiceDuration.Should().Be(90);

        dto.ServicePrice.Should().Be(28500000.00m);

    }



    [Fact]

    public async Task Receptionist_Forward_Success_RedirectsToRequestsWithTempData()

    {

        _apiMock.Setup(x => x.ReceptionistForwardAsync(10, "OK"))

            .ReturnsAsync(ApiResult.Success("OK"));



        var controller = new ReceptionistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(201, "Receptionist"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var result = await controller.Forward(10, "OK");



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Requests");

        controller.TempData["SuccessMessage"].Should().NotBeNull();

    }



    [Fact]

    public async Task Patient_Book_Get_WhenBlocked_ReturnsEligibilityInModel()

    {

        _apiMock.Setup(x => x.GetDepartmentsAsync())

            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>()));



        _apiMock.Setup(x => x.GetBookingEligibilityAsync())

            .ReturnsAsync(ApiResult<BookingEligibilityDto>.Success(new BookingEligibilityDto

            {

                IsEligible = false,

                StrikeCount = 3,

                BlockReason = "Tài khoản đã tích lũy 3 lần vi phạm."

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.Book();



        var viewResult = result.Should().BeOfType<ViewResult>().Subject;

        var model = viewResult.Model.Should().BeOfType<PatientBookViewModel>().Subject;

        model.Eligibility.Should().NotBeNull();

        model.Eligibility!.IsEligible.Should().BeFalse();

        model.Eligibility.StrikeCount.Should().Be(3);

    }



    [Fact]

    public async Task Patient_Book_Post_WhenBlocked_RejectsAndReturnsViewWithErrorMessage()

    {

        _apiMock.Setup(x => x.GetDepartmentsAsync())

            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>()));



        _apiMock.Setup(x => x.GetBookingEligibilityAsync())

            .ReturnsAsync(ApiResult<BookingEligibilityDto>.Success(new BookingEligibilityDto

            {

                IsEligible = false,

                StrikeCount = 3,

                BlockReason = "Tài khoản bị tạm khóa 30 ngày."

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var model = new PatientBookViewModel

        {

            DepartmentId = 1,

            RequestedServiceId = 10,

            ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),

            ReasonForVisit = "Đau răng"

        };



        var result = await controller.Book(model);



        var viewResult = result.Should().BeOfType<ViewResult>().Subject;

        controller.TempData["ErrorMessage"]?.ToString().Should().Contain("tạm khóa");

    }



    [Fact]

    public async Task Receptionist_NoShow_Success_RedirectsToTodayCheckInWithSuccessMessage()

    {

        _apiMock.Setup(x => x.ReceptionistMarkNoShowAsync(10, "Bệnh nhân không đến"))

            .ReturnsAsync(ApiResult.Success("OK"));



        var controller = new ReceptionistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(201, "Receptionist"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var result = await controller.NoShow(10, "Bệnh nhân không đến");



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("TodayCheckIn");

        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("vắng mặt");

    }



    [Fact]

    public async Task Patient_GetServices_ReturnsJsonResult()

    {

        _apiMock.Setup(x => x.GetDepartmentsAsync())

            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>

            {

                new() { DepartmentId = 1, Name = "Răng Tổng Quát" }

            }));

        _apiMock.Setup(x => x.GetServicesAsync(1))

            .ReturnsAsync(ApiResult<List<DepartmentServiceDto>>.Success(new List<DepartmentServiceDto>

            {

                new() { DepartmentServiceId = 10, DepartmentId = 1, ServiceName = "Khám Răng" }

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.GetServices(1);



        var json = result.Should().BeOfType<JsonResult>().Subject;

        var list = json.Value.Should().BeAssignableTo<IEnumerable<DepartmentServiceDto>>().Subject;

        list.Should().HaveCount(1);

    }



    [Fact]

    public async Task Patient_GetDentists_ReturnsJsonResult()

    {

        _apiMock.Setup(x => x.GetDentistsAsync(1))

            .ReturnsAsync(ApiResult<List<DentistOptionDto>>.Success(new List<DentistOptionDto>

            {

                new() { DentistUserId = 401, FullName = "Bác Sĩ An" }

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.GetDentists(1);



        var json = result.Should().BeOfType<JsonResult>().Subject;

        var list = json.Value.Should().BeAssignableTo<IEnumerable<DentistOptionDto>>().Subject;

        list.Should().HaveCount(1);

    }



    [Fact]

    public async Task Patient_GetSlots_ValidDate_ReturnsJsonResult()

    {

        var futureDate = PatientAppointmentController.GetClinicToday().AddDays(3);

        var dateStr = futureDate.ToString("yyyy-MM-dd");

        var startTime = futureDate.ToDateTime(new TimeOnly(9, 0));

        var endTime = futureDate.ToDateTime(new TimeOnly(9, 30));



        _apiMock.Setup(x => x.GetAvailableSlotsAsync(1, 10, futureDate, null))

            .ReturnsAsync(ApiResult<List<AvailableSlotDto>>.Success(new List<AvailableSlotDto>

            {

                new() { StartTime = startTime, EndTime = endTime }

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.GetSlots(1, 10, dateStr, null);



        var json = result.Should().BeOfType<JsonResult>().Subject;

        var list = json.Value.Should().BeAssignableTo<IEnumerable<AvailableSlotDto>>().Subject;

        list.Should().HaveCount(1);

    }



    [Fact]

    public async Task Patient_GetSlots_PastDate_ReturnsEmptyList()

    {

        var pastDate = PatientAppointmentController.GetClinicToday().AddDays(-2);

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.GetSlots(1, 10, pastDate.ToString("yyyy-MM-dd"), null);



        var json = result.Should().BeOfType<JsonResult>().Subject;

        var list = json.Value.Should().BeAssignableTo<IEnumerable<AvailableSlotDto>>().Subject;

        list.Should().BeEmpty();

    }



    [Fact]

    public async Task Patient_Book_PastScheduledStart_ReturnsModelError()

    {

        var model = new PatientBookViewModel

        {

            DepartmentId = 1,

            RequestedServiceId = 10,

            ScheduledStart = PatientAppointmentController.GetClinicNow().AddHours(-3),

            ReasonForVisit = "Đau nhức răng cần khám gấp"

        };



        _apiMock.Setup(x => x.GetBookingEligibilityAsync())

            .ReturnsAsync(ApiResult<BookingEligibilityDto>.Success(new BookingEligibilityDto { IsEligible = true }));

        _apiMock.Setup(x => x.GetDepartmentsAsync())

            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>()));

        _apiMock.Setup(x => x.GetServicesAsync(0))

            .ReturnsAsync(ApiResult<List<DepartmentServiceDto>>.Success(new List<DepartmentServiceDto>()));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.Book(model);



        controller.ModelState.IsValid.Should().BeFalse();

        controller.ModelState.ContainsKey("ScheduledStart").Should().BeTrue();

    }



    [Fact]

    public async Task Patient_GetSlots_InvalidDate_ReturnsBadRequest()

    {

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.GetSlots(1, 10, "invalid-date", null);



        result.Should().BeOfType<BadRequestObjectResult>();

    }



    [Fact]

    public async Task Patient_Detail_Success_ReturnsViewWithDetail()

    {

        _apiMock.Setup(x => x.GetAppointmentDetailAsync(100))

            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto

            {

                AppointmentId = 100,

                AppointmentCode = "APP-001",

                Status = "Confirmed"

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.Detail(100);



        var view = result.Should().BeOfType<ViewResult>().Subject;

        var vm = view.Model.Should().BeOfType<AppointmentDetailViewModel>().Subject;

        vm.Appointment.AppointmentId.Should().Be(100);

        vm.CurrentUserRole.Should().Be("Patient");

    }



    [Fact]

    public async Task Patient_Cancel_Success_RedirectsToDetailWithTempData()

    {

        _apiMock.Setup(x => x.PatientCancelAsync(100, "Bận việc đột xuất"))

            .ReturnsAsync(ApiResult.Success("Hủy thành công"));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var result = await controller.Cancel(100, "Bận việc đột xuất");



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Detail");

        controller.TempData["SuccessMessage"].Should().NotBeNull();

    }



        [Fact]
    public async Task Receptionist_TodayCheckIn_ReturnsViewWithList()
    {
        _apiMock.Setup(x => x.GetReceptionistTodayConfirmedAsync("test", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<string>()))
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>
            {
                new() { AppointmentId = 1, AppointmentCode = "APP-TODAY-01", Status = "Confirmed", QueueNumber = "Q-TQ-001" }
            }));

        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>()));

        var controller = new ReceptionistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Receptionist")
        };

        var result = await controller.TodayCheckIn("test");

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<ReceptionistTodayCheckInViewModel>().Subject;
        vm.ConfirmedAppointments.Should().HaveCount(1);
        vm.SearchQuery.Should().Be("test");
    }

    [Fact]
    public async Task Receptionist_SearchPatient_ReturnsJsonResult()
    {
        _apiMock.Setup(x => x.GetPatientsAsync("0901234567"))
            .ReturnsAsync(ApiResult<List<AdminUserDto>>.Success(new List<AdminUserDto>
            {
                new() { UserId = 50, FullName = "Bệnh nhân test", PhoneNumber = "0901234567", Code = "P-050" }
            }));

        var controller = new ReceptionistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Receptionist")
        };

        var result = await controller.SearchPatient("0901234567");
        result.Should().BeOfType<JsonResult>();
    }

    [Fact]
    public async Task Receptionist_WalkInCheckIn_Success_RegistersPatientAndRedirects()
    {
        _apiMock.Setup(x => x.RegisterWalkInPatientAsync(It.IsAny<RegisterWalkInPatientRequest>()))
            .ReturnsAsync(ApiResult<RegisterWalkInPatientResponse>.Success(new RegisterWalkInPatientResponse
            {
                PatientUserId = 999,
                PatientCode = "P-999",
                FullName = "Nguyễn Văn Vãng Lai"
            }));

        _apiMock.Setup(x => x.CreateReceptionistAppointmentAsync(It.IsAny<CreateReceptionistAppointmentRequest>()))
            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto
            {
                AppointmentId = 888,
                AppointmentCode = "APP-WALKIN-888",
                QueueNumber = "Q-TQ-005",
                Status = "CheckedIn"
            }));

        var controller = new ReceptionistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Receptionist"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var request = new WalkInCheckInRequest
        {
            FullName = "Nguyễn Văn Vãng Lai",
            PhoneNumber = "0912345678",
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 20,
            RoomId = 1,
            DentalChairId = 2
        };

        var result = await controller.WalkInCheckIn(request);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("TodayCheckIn");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("Q-TQ-005");
    }



    [Fact]

    public async Task Receptionist_CheckIn_Success_RedirectsToTodayCheckInWithSuccessMessage()

    {

        _apiMock.Setup(x => x.ReceptionistCheckInAsync(10, "Bệnh nhân đã đến"))

            .ReturnsAsync(ApiResult.Success("Check-in thành công"));



        var controller = new ReceptionistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(201, "Receptionist"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var result = await controller.CheckIn(10, "Bệnh nhân đã đến");



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("TodayCheckIn");

        controller.TempData["SuccessMessage"].Should().NotBeNull();

    }



    [Fact]

    public async Task Receptionist_NoShow_Failure_RedirectsToTodayCheckInWithErrorMessage()

    {

        _apiMock.Setup(x => x.ReceptionistMarkNoShowAsync(10, "Không đến khám"))

            .ReturnsAsync(ApiResult.Failure("Lỗi ghi nhận vắng mặt"));



        var controller = new ReceptionistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(201, "Receptionist"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var result = await controller.NoShow(10, "Không đến khám");



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("TodayCheckIn");

        controller.TempData["ErrorMessage"].Should().Be("Lỗi ghi nhận vắng mặt");

    }



    [Fact]

    public async Task DentistAppointment_Today_ReturnsViewWithAppointments()

    {

        _apiMock.Setup(x => x.GetDentistTodayAppointmentsAsync())

            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>

            {

                new()

                {

                    AppointmentId = 99,

                    AppointmentCode = "APT-99",

                    PatientName = "Nguyễn Văn A",

                    ServiceName = "Khám răng định kỳ",

                    Status = "CheckedIn"

                }

            }));



        var controller = new DentistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(301, "Dentist")

        };



        var result = await controller.Today();



        var view = result.Should().BeOfType<ViewResult>().Subject;

        var vm = view.Model.Should().BeOfType<DentistTodayAppointmentsViewModel>().Subject;

        vm.Appointments.Should().HaveCount(1);

        vm.Appointments[0].AppointmentCode.Should().Be("APT-99");

    }



    [Fact]

    public async Task Patient_Reschedule_Get_ValidAppointment_ReturnsViewResultWithModel()

    {

        _apiMock.Setup(x => x.GetAppointmentDetailAsync(10))

            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto

            {

                AppointmentId = 10,

                AppointmentCode = "APP-20261016-111111",

                DepartmentId = 1,

                DepartmentName = "Khoa Chỉnh Nha",

                RequestedServiceId = 2,

                ServiceName = "Tư vấn niềng răng",

                Status = "Confirmed",

                ScheduledStart = DateTime.Today.AddDays(2).AddHours(9)

            }));

        _apiMock.Setup(x => x.GetBookingEligibilityAsync())

            .ReturnsAsync(ApiResult<BookingEligibilityDto>.Success(new BookingEligibilityDto { IsEligible = true }));

        _apiMock.Setup(x => x.GetDentistsAsync(1))

            .ReturnsAsync(ApiResult<List<DentistOptionDto>>.Success(new List<DentistOptionDto>

            {

                new() { UserId = 401, FullName = "Bác Sĩ Nguyễn Văn A" }

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.Reschedule(10);



        var view = result.Should().BeOfType<ViewResult>().Subject;

        var model = view.Model.Should().BeOfType<PatientRescheduleViewModel>().Subject;

        model.AppointmentId.Should().Be(10);

        model.AppointmentCode.Should().Be("APP-20261016-111111");

        model.DepartmentName.Should().Be("Khoa Chỉnh Nha");

        model.ServiceName.Should().Be("Tư vấn niềng răng");

        model.Dentists.Should().HaveCount(1);

    }



    [Fact]

    public async Task Patient_Reschedule_Post_Success_RedirectsToSuccessAction()

    {

        _apiMock.Setup(x => x.GetBookingEligibilityAsync())

            .ReturnsAsync(ApiResult<BookingEligibilityDto>.Success(new BookingEligibilityDto { IsEligible = true }));



        var newStart = DateTime.Today.AddDays(3).AddHours(10);

        _apiMock.Setup(x => x.PatientRescheduleAsync(10, It.IsAny<PatientRescheduleRequest>()))

            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto

            {

                AppointmentId = 25,

                AppointmentCode = "APP-20261016-222222",

                Status = "PendingReceptionReview",

                ScheduledStart = newStart

            }, "Dời lịch khám thành công"));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var model = new PatientRescheduleViewModel

        {

            AppointmentId = 10,

            NewScheduledStart = newStart,

            Reason = "Trùng lịch bận đột xuất"

        };



        var result = await controller.Reschedule(10, model);



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Success");

        redirect.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(25L);

        controller.TempData["SuccessMessage"].Should().Be("Dời lịch khám thành công");

    }

    [Fact]

    public async Task Receptionist_Create_Get_ReturnsViewWithDepartments()

    {

        _apiMock.Setup(x => x.GetDepartmentsAsync())

            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>

            {

                new() { DepartmentId = 1, Name = "Khoa Răng Tổng Quát" }

            }));



        var controller = new ReceptionistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(201, "Receptionist")

        };



        var result = await controller.Create((long?)null);



        var view = result.Should().BeOfType<ViewResult>().Subject;

        var vm = view.Model.Should().BeOfType<ReceptionistBookAppointmentViewModel>().Subject;

        vm.Departments.Should().HaveCount(1);

    }



    [Fact]

    public async Task Receptionist_Create_Post_Success_RedirectsToRequests()

    {

        _apiMock.Setup(x => x.CreateReceptionistAppointmentAsync(It.IsAny<CreateReceptionistAppointmentRequest>()))

            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto

            {

                AppointmentId = 50,

                AppointmentCode = "APP-20261031-123456"

            }));



        var controller = new ReceptionistAppointmentController(_apiMock.Object)

        {

            ControllerContext = CreateContextWithUser(201, "Receptionist"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var model = new ReceptionistBookAppointmentViewModel

        {

            PatientUserId = 101,

            DepartmentId = 1,

            RequestedServiceId = 10,

            ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),

            ReasonForVisit = "Đau nhức răng hàm"

        };



        var result = await controller.Create(model);



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Requests");

        controller.TempData["SuccessMessage"].Should().NotBeNull();

    }



    [Fact]

    public async Task Patient_Feedback_Get_CompletedAppointment_ReturnsViewWithForm()

    {

        _apiMock.Setup(x => x.GetAppointmentDetailAsync(10))

            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto

            {

                AppointmentId = 10,

                AppointmentCode = "APP-10",

                Status = "Completed",

                AssignedDentistName = "BS Nguyễn Văn A"

            }));

        _feedbackApiMock.Setup(x => x.GetFeedbackByAppointmentIdAsync(10))

            .ReturnsAsync(ApiResult<VisitFeedbackDto?>.Success(null));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient")

        };



        var result = await controller.Feedback(10);



        var view = result.Should().BeOfType<ViewResult>().Subject;

        var vm = view.Model.Should().BeOfType<PatientFeedbackFormViewModel>().Subject;

        vm.AppointmentId.Should().Be(10);

        vm.IsAlreadySubmitted.Should().BeFalse();

    }



    [Fact]

    public async Task Patient_Feedback_Get_NotCompletedAppointment_RedirectsToDetail()

    {

        _apiMock.Setup(x => x.GetAppointmentDetailAsync(10))

            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto

            {

                AppointmentId = 10,

                AppointmentCode = "APP-10",

                Status = "Confirmed"

            }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var result = await controller.Feedback(10);



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Detail");

        controller.TempData["ErrorMessage"]!.ToString().Should().Contain("đã hoàn thành");

    }



    [Fact]

    public async Task Patient_Feedback_Post_Success_RedirectsToDetailWithSuccessMessage()

    {

        _feedbackApiMock.Setup(x => x.CreateFeedbackAsync(It.IsAny<CreateFeedbackRequest>()))

            .ReturnsAsync(ApiResult<VisitFeedbackDto>.Success(new VisitFeedbackDto { FeedbackId = 1 }));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var model = new PatientFeedbackFormViewModel

        {

            AppointmentId = 10,

            DentistRating = 5,

            ClinicRating = 5,

            Comment = "Rất hài lòng!"

        };



        var result = await controller.SubmitFeedback(model);



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Detail");

        controller.TempData["SuccessMessage"].Should().NotBeNull();

    }



    [Fact]

    public async Task Patient_Withdraw_Post_Success_RedirectsToDetail()

    {

        _apiMock.Setup(x => x.PatientWithdrawAsync(10, It.IsAny<string?>()))

            .ReturnsAsync(ApiResult.Success("Đã rút yêu cầu đặt lịch thành công"));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var result = await controller.Withdraw(10, "Bận việc đột xuất");



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Detail");

        controller.TempData["SuccessMessage"].Should().Be("Đã rút yêu cầu đặt lịch thành công");

    }



    [Fact]

    public async Task Patient_AcceptProposal_Success_RedirectsToDetail()

    {

        _apiMock.Setup(x => x.AcceptProposalAsync(10))

            .ReturnsAsync(ApiResult.Success("Đã xác nhận phương án điều chỉnh"));



        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object, _disruptionApiMock.Object)

        {

            ControllerContext = CreateContextWithUser(101, "Patient"),

            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())

        };



        var result = await controller.AcceptProposal(10);



        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;

        redirect.ActionName.Should().Be("Detail");

        controller.TempData["SuccessMessage"]!.ToString().Should().Contain("Đã đồng ý phương án đề xuất");

    }

}

