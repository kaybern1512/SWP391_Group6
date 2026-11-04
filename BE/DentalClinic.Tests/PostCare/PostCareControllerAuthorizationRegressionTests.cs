using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Api.Controllers;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DentalClinic.Tests.PostCare;

public class PostCareControllerAuthorizationRegressionTests
{
    [Fact]
    public void UpdateFollowUpStatus_HasCorrectAuthorizeAttributeAndRoles()
    {
        var method = typeof(PostCareController).GetMethod(nameof(PostCareController.UpdateFollowUpStatus));
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var roles = authAttr.Roles.Split(',', StringSplitOptions.TrimEntries);
        Assert.Contains(UserRole.Receptionist, roles);
        Assert.Contains(UserRole.Dentist, roles);
        Assert.Contains(UserRole.SystemAdministrator, roles);
        Assert.Contains(UserRole.Patient, roles);
        Assert.Contains(UserRole.DepartmentManager, roles);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_UnauthenticatedUser_ReturnsUnauthorized()
    {
        var mockService = new Mock<IPostCareService>();
        var mockScheduling = new Mock<IFollowUpSchedulingService>();
        var controller = new PostCareController(mockService.Object, mockScheduling.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()) // no claims
                }
            }
        };

        var result = await controller.UpdateFollowUpStatus(123, new UpdateFollowUpStatusRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_ForbiddenServiceResult_Returns403Forbidden()
    {
        var mockService = new Mock<IPostCareService>();
        var mockScheduling = new Mock<IFollowUpSchedulingService>();

        mockService
            .Setup(s => s.UpdateFollowUpStatusAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdateFollowUpStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<FollowUpScheduleDto>.Fail("Bạn không có quyền thao tác trên lịch tái khám của bệnh nhân khác."));

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "99"),
            new Claim(ClaimTypes.Role, UserRole.Patient)
        }, "TestAuth"));

        var controller = new PostCareController(mockService.Object, mockScheduling.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

        var result = await controller.UpdateFollowUpStatus(123, new UpdateFollowUpStatusRequest(), CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_BadRequestServiceResult_Returns400BadRequest()
    {
        var mockService = new Mock<IPostCareService>();
        var mockScheduling = new Mock<IFollowUpSchedulingService>();

        mockService
            .Setup(s => s.UpdateFollowUpStatusAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdateFollowUpStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<FollowUpScheduleDto>.Fail("Ngày dời hẹn tái khám phải là ngày trong tương lai."));

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "50"),
            new Claim(ClaimTypes.Role, UserRole.Receptionist)
        }, "TestAuth"));

        var controller = new PostCareController(mockService.Object, mockScheduling.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

        var result = await controller.UpdateFollowUpStatus(123, new UpdateFollowUpStatusRequest(), CancellationToken.None);

        var badReq = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badReq.StatusCode);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_SuccessfulServiceResult_Returns200Ok()
    {
        var mockService = new Mock<IPostCareService>();
        var mockScheduling = new Mock<IFollowUpSchedulingService>();

        mockService
            .Setup(s => s.UpdateFollowUpStatusAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdateFollowUpStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<FollowUpScheduleDto>.Ok(new FollowUpScheduleDto { FollowUpId = 123 }));

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "50"),
            new Claim(ClaimTypes.Role, UserRole.Receptionist)
        }, "TestAuth"));

        var controller = new PostCareController(mockService.Object, mockScheduling.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

        var result = await controller.UpdateFollowUpStatus(123, new UpdateFollowUpStatusRequest(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_StructuredErrorCodeForbidden_Returns403Forbidden()
    {
        var mockService = new Mock<IPostCareService>();
        var mockScheduling = new Mock<IFollowUpSchedulingService>();

        mockService
            .Setup(s => s.UpdateFollowUpStatusAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdateFollowUpStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<FollowUpScheduleDto>.Forbidden("Access denied by object policy."));

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "99"),
            new Claim(ClaimTypes.Role, UserRole.Patient)
        }, "TestAuth"));

        var controller = new PostCareController(mockService.Object, mockScheduling.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

        var result = await controller.UpdateFollowUpStatus(123, new UpdateFollowUpStatusRequest(), CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
    }
}
