using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Admin;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class AdminMedicineWebControllerTests
{
    private readonly Mock<IMedicineApiService> _apiMock;

    public AdminMedicineWebControllerTests()
    {
        _apiMock = new Mock<IMedicineApiService>();
    }

    private static ControllerContext CreateAdminContext(long userId = 1, string name = "Admin User")
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, "SystemAdministrator"),
            new(ClaimTypes.Email, "admin@clinic.com")
        };
        var identity = new ClaimsIdentity(claims, "CookieAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        return new ControllerContext { HttpContext = httpContext };
    }

    private static TempDataDictionary CreateTempData()
    {
        return new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task Index_ReturnsViewWithSummaryAndMedicines()
    {
        // Arrange
        _apiMock.Setup(a => a.GetInventorySummaryAsync())
            .ReturnsAsync(ApiResult<MedicineInventorySummaryDto>.Success(new MedicineInventorySummaryDto
            {
                TotalMedicines = 10,
                ActiveMedicines = 8,
                TotalStockUnits = 500,
                LowStockMedicinesCount = 2
            }));

        _apiMock.Setup(a => a.GetMedicinesAsync(It.IsAny<MedicineSearchFilter>()))
            .ReturnsAsync(ApiResult<List<MedicineDto>>.Success(new List<MedicineDto>
            {
                new() { MedicineId = 1, MedicineCode = "MED-01", MedicineName = "Thuốc A", TotalAvailableStock = 50, Unit = "Viên" },
                new() { MedicineId = 2, MedicineCode = "MED-02", MedicineName = "Thuốc B", TotalAvailableStock = 5, Unit = "Lọ" }
            }));

        var controller = new AdminMedicineController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.Index(new MedicineSearchFilter());

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<AdminMedicineIndexViewModel>().Subject;
        model.Summary.TotalMedicines.Should().Be(10);
        model.Medicines.Should().HaveCount(2);
    }

    [Fact]
    public async Task Detail_ValidId_ReturnsViewWithMedicineDetails()
    {
        // Arrange
        _apiMock.Setup(a => a.GetMedicineByIdAsync(1))
            .ReturnsAsync(ApiResult<MedicineDetailDto>.Success(new MedicineDetailDto
            {
                MedicineId = 1,
                MedicineCode = "MED-AMOX-500",
                MedicineName = "Amoxicillin 500mg",
                TotalAvailableStock = 45,
                Unit = "Viên",
                UnitPrice = 5000
            }));

        var controller = new AdminMedicineController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.Detail(1);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<AdminMedicineDetailViewModel>().Subject;
        model.Medicine.MedicineCode.Should().Be("MED-AMOX-500");
        model.Medicine.TotalAvailableStock.Should().Be(45);
    }

    [Fact]
    public async Task Edit_ValidRequest_UpdatesMedicineAndRedirects()
    {
        // Arrange
        _apiMock.Setup(a => a.UpdateMedicineAsync(1, It.IsAny<UpdateMedicineRequest>()))
            .ReturnsAsync(ApiResult<MedicineDto>.Success(new MedicineDto
            {
                MedicineId = 1,
                MedicineName = "Amoxicillin 500mg (GSK)",
                UnitPrice = 6000
            }));

        var controller = new AdminMedicineController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        var request = new UpdateMedicineRequest
        {
            MedicineName = "Amoxicillin 500mg (GSK)",
            Unit = "Viên",
            UnitPrice = 6000
        };

        // Act
        var result = await controller.Edit(1, request);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detail");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateStock_AddOperation_CallsApiAndSetsSuccessMessage()
    {
        // Arrange
        _apiMock.Setup(a => a.UpdateStockAsync(1, It.Is<UpdateMedicineStockRequest>(r => r.OperationType == "Add" && r.Quantity == 40)))
            .ReturnsAsync(ApiResult<MedicineStockUpdateResultDto>.Success(new MedicineStockUpdateResultDto
            {
                MedicineId = 1,
                MedicineCode = "MED-01",
                MedicineName = "Thuốc Test",
                OperationType = "Add",
                QuantityBefore = 1,
                QuantityChange = 40,
                QuantityAfter = 41,
                Unit = "Viên"
            }));

        var controller = new AdminMedicineController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        var request = new UpdateMedicineStockRequest
        {
            OperationType = "Add",
            Quantity = 40,
            Reason = "Nhập thêm hàng"
        };

        // Act
        var result = await controller.UpdateStock(1, request);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detail");
        controller.TempData["SuccessMessage"]!.ToString().Should().Contain("Nhập thêm thành công");
        controller.TempData["SuccessMessage"]!.ToString().Should().Contain("41");
    }

    [Fact]
    public async Task UpdateStock_DeductOperation_CallsApiAndSetsSuccessMessage()
    {
        // Arrange
        _apiMock.Setup(a => a.UpdateStockAsync(1, It.Is<UpdateMedicineStockRequest>(r => r.OperationType == "Deduct" && r.Quantity == 5)))
            .ReturnsAsync(ApiResult<MedicineStockUpdateResultDto>.Success(new MedicineStockUpdateResultDto
            {
                MedicineId = 1,
                MedicineCode = "MED-01",
                MedicineName = "Thuốc Test",
                OperationType = "Deduct",
                QuantityBefore = 41,
                QuantityChange = -5,
                QuantityAfter = 36,
                Unit = "Viên"
            }));

        var controller = new AdminMedicineController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        var request = new UpdateMedicineStockRequest
        {
            OperationType = "Deduct",
            Quantity = 5,
            Reason = "Xuất hao hụt"
        };

        // Act
        var result = await controller.UpdateStock(1, request);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detail");
        controller.TempData["SuccessMessage"]!.ToString().Should().Contain("Giảm tồn thành công");
        controller.TempData["SuccessMessage"]!.ToString().Should().Contain("36");
    }

    [Fact]
    public async Task UpdateStock_ReconcileOperation_CallsApiAndSetsSuccessMessage()
    {
        // Arrange
        _apiMock.Setup(a => a.UpdateStockAsync(1, It.Is<UpdateMedicineStockRequest>(r => r.OperationType == "Reconcile" && r.Quantity == 30)))
            .ReturnsAsync(ApiResult<MedicineStockUpdateResultDto>.Success(new MedicineStockUpdateResultDto
            {
                MedicineId = 1,
                MedicineCode = "MED-01",
                MedicineName = "Thuốc Test",
                OperationType = "Reconcile",
                QuantityBefore = 36,
                QuantityChange = -6,
                QuantityAfter = 30,
                Unit = "Viên"
            }));

        var controller = new AdminMedicineController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        var request = new UpdateMedicineStockRequest
        {
            OperationType = "Reconcile",
            Quantity = 30,
            Reason = "Kiểm kê lại"
        };

        // Act
        var result = await controller.UpdateStock(1, request);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detail");
        controller.TempData["SuccessMessage"]!.ToString().Should().Contain("Kiểm kê lại thành công");
        controller.TempData["SuccessMessage"]!.ToString().Should().Contain("30");
    }

    [Fact]
    public async Task UpdateStock_ApiFails_SetsErrorMessage()
    {
        // Arrange
        _apiMock.Setup(a => a.UpdateStockAsync(1, It.IsAny<UpdateMedicineStockRequest>()))
            .ReturnsAsync(ApiResult<MedicineStockUpdateResultDto>.Failure("Số lượng giảm không được vượt quá số lượng tồn kho hiện tại (30)."));

        var controller = new AdminMedicineController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        var request = new UpdateMedicineStockRequest
        {
            OperationType = "Deduct",
            Quantity = 50
        };

        // Act
        var result = await controller.UpdateStock(1, request);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detail");
        controller.TempData["ErrorMessage"]!.ToString().Should().Contain("vượt quá số lượng tồn kho");
    }
}
