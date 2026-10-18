using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Receptionist;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewComponents;
using DentalClinic.Web.ViewModels.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class ReceptionistChatControllerTests
{
    private readonly Mock<ISupportApiService> _supportApiMock;

    public ReceptionistChatControllerTests()
    {
        _supportApiMock = new Mock<ISupportApiService>();
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
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Index_ReturnsViewWithActiveConversations()
    {
        _supportApiMock.Setup(x => x.GetActiveConversationsAsync())
            .ReturnsAsync(ApiResult<List<ConversationSummaryDto>>.Success(new List<ConversationSummaryDto>
            {
                new() { ConversationId = "c1", CustomerName = "Khách A", LastMessage = "Alo" }
            }));

        var controller = new ReceptionistChatController(_supportApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(3, "Receptionist", "Lễ tân Mai")
        };

        var result = await controller.Index();

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task GetConversations_ReturnsJsonList()
    {
        _supportApiMock.Setup(x => x.GetActiveConversationsAsync())
            .ReturnsAsync(ApiResult<List<ConversationSummaryDto>>.Success(new List<ConversationSummaryDto>
            {
                new() { ConversationId = "c1", CustomerName = "Khách A" }
            }));

        var controller = new ReceptionistChatController(_supportApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(3, "Receptionist")
        };

        var result = await controller.GetConversations();

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var list = json.Value.Should().BeAssignableTo<IEnumerable<ConversationSummaryDto>>().Subject;
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task SendMessage_ValidInput_CallsApiAndReturnsJson()
    {
        _supportApiMock.Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>()))
            .ReturnsAsync(ApiResult<List<ChatMessageDto>>.Success(new List<ChatMessageDto>
            {
                new()
                {
                    MessageId = 1,
                    MessageText = "Xin chào"
                }
            }));

        var controller = new ReceptionistChatController(_supportApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(3, "Receptionist", "Lễ tân Mai")
        };

        var result = await controller.SendMessage("conv_1", "Xin chào");

        var json = result.Should().BeOfType<JsonResult>().Subject;
        _supportApiMock.Verify(x => x.SendMessageAsync(It.Is<SendMessageRequest>(r => r.ConversationId == "conv_1" && r.MessageText == "Xin chào")), Times.Once);
    }

    [Fact]
    public async Task SupportWidgetViewComponent_InvokeAsync_ReturnsDefaultViewWithModel()
    {
        _supportApiMock.Setup(x => x.GetSupportConfigAsync())
            .ReturnsAsync(ApiResult<SupportConfigDto>.Success(new SupportConfigDto
            {
                ClinicName = "DentalCare"
            }));

        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "6"),
            new(ClaimTypes.Name, "Lê Hoàng Minh"),
            new(ClaimTypes.Role, "Patient")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var viewContext = new ViewContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData()
        };

        var component = new SupportWidgetViewComponent(_supportApiMock.Object)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = viewContext
            }
        };

        var result = await component.InvokeAsync();

        var viewResult = result.Should().BeOfType<ViewViewComponentResult>().Subject;
        var model = viewResult.ViewData?.Model.Should().BeOfType<SupportWidgetViewModel>().Subject;
        model!.IsAuthenticated.Should().BeTrue();
        model.DefaultConversationId.Should().Be("patient_6");
        model.Config.ClinicName.Should().Be("DentalCare");
    }
}
