using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Notification;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class NotificationWebControllerTests
{
    private readonly Mock<INotificationApiService> _apiMock;

    public NotificationWebControllerTests()
    {
        _apiMock = new Mock<INotificationApiService>();
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

    private static TempDataDictionary CreateTempData()
    {
        var httpContext = new DefaultHttpContext();
        return new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task Index_ReturnsViewWithSummary()
    {
        _apiMock.Setup(x => x.GetNotificationsAsync(50, null, null))
            .ReturnsAsync(ApiResult<NotificationSummaryDto>.Success(new NotificationSummaryDto
            {
                UnreadCount = 3,
                Notifications = new List<NotificationDto>
                {
                    new() { NotificationId = 1, Subject = "Lịch khám mới", IsRead = false }
                }
            }));

        var controller = new NotificationController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient")
        };

        var result = await controller.Index(null, null);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<NotificationListViewModel>().Subject;
        vm.Summary.UnreadCount.Should().Be(3);
        vm.Summary.Notifications.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSummary_ReturnsJsonWithUnreadCountAndNotifications()
    {
        _apiMock.Setup(x => x.GetNotificationsAsync(8, null, null))
            .ReturnsAsync(ApiResult<NotificationSummaryDto>.Success(new NotificationSummaryDto
            {
                UnreadCount = 2,
                Notifications = new List<NotificationDto>
                {
                    new() { NotificationId = 1, Subject = "Test", IsRead = false }
                }
            }));

        var controller = new NotificationController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient")
        };

        var result = await controller.GetSummary(8);

        result.Should().BeOfType<JsonResult>();
    }

    [Fact]
    public async Task MarkAsRead_ReturnsJsonSuccess()
    {
        _apiMock.Setup(x => x.MarkAsReadAsync(100))
            .ReturnsAsync(ApiResult<bool>.Success(true));

        var controller = new NotificationController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient")
        };

        var result = await controller.MarkAsRead(100);

        result.Should().BeOfType<JsonResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_RedirectsToIndex()
    {
        _apiMock.Setup(x => x.MarkAllAsReadAsync())
            .ReturnsAsync(ApiResult<bool>.Success(true));

        var controller = new NotificationController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient"),
            TempData = CreateTempData()
        };

        var result = await controller.MarkAllAsRead(null);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
    }
}
