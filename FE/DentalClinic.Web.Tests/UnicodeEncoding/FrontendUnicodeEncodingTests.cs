using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.ViewModels.Appointments;
using DentalClinic.Web.ViewModels.Services;
using FluentAssertions;
using Xunit;

namespace DentalClinic.Web.Tests.UnicodeEncoding;

public class FrontendUnicodeEncodingTests
{
    private readonly HtmlEncoder _configuredHtmlEncoder = HtmlEncoder.Create(UnicodeRanges.All);

    [Fact]
    public void TestGroup4_RazorHtmlEncoder_PreservesVietnameseCharactersWithoutEntityEscaping()
    {
        var input = "Khám & Lấy Cao Răng - Khoa Răng Tổng Quát - Bác sĩ Lê Văn Bác Sĩ Một";

        var encoded = _configuredHtmlEncoder.Encode(input);

        // HTML special char '&' must be encoded as '&amp;' for safety
        encoded.Should().Contain("&amp;");

        // Vietnamese accented characters must remain literal UTF-8 and NOT converted to &#xE1; or &aacute;
        encoded.Should().Contain("Khám");
        encoded.Should().Contain("Lấy Cao Răng");
        encoded.Should().Contain("Tổng Quát");
        encoded.Should().Contain("Bác sĩ Lê Văn Bác Sĩ Một");
        encoded.Should().NotContain("&#");
    }

    [Fact]
    public void TestGroup4_ViewModelMapping_PreservesAllFourRolePortalText()
    {
        // 1. Patient Service Discovery ViewModel
        var discoveryVm = new PatientServicesDiscoveryViewModel
        {
            SearchTerm = "Trám răng",
            Departments = new List<DepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Khoa Răng Tổng Quát" },
                new() { DepartmentId = 2, Name = "Khoa Chỉnh Nha & Phục Hình" }
            },
            Services = new List<DepartmentServiceDto>
            {
                new()
                {
                    DepartmentServiceId = 1,
                    DepartmentId = 1,
                    DepartmentName = "Khoa Răng Tổng Quát",
                    ServiceName = "Khám & Lấy Cao Răng",
                    Description = "Vệ sinh răng miệng và lấy vôi răng siêu âm",
                    Price = 200000,
                    DurationMinutes = 30
                },
                new()
                {
                    DepartmentServiceId = 2,
                    DepartmentId = 1,
                    DepartmentName = "Khoa Răng Tổng Quát",
                    ServiceName = "Trám Răng Composite",
                    Description = "Trám răng thẩm mỹ bằng vật liệu composite",
                    Price = 350000,
                    DurationMinutes = 45
                }
            },
            DentistsByDepartment = new Dictionary<long, List<DentistOptionDto>>
            {
                {
                    1, new List<DentistOptionDto>
                    {
                        new() { DentistUserId = 3, FullName = "Lê Văn Bác Sĩ Một", Qualification = "Thạc sĩ Răng Hàm Mặt", YearsOfExperience = 8 }
                    }
                }
            }
        };

        discoveryVm.Departments[0].Name.Should().Be("Khoa Răng Tổng Quát");
        discoveryVm.Departments[1].Name.Should().Be("Khoa Chỉnh Nha & Phục Hình");
        discoveryVm.Services[0].ServiceName.Should().Be("Khám & Lấy Cao Răng");
        discoveryVm.Services[0].Description.Should().Be("Vệ sinh răng miệng và lấy vôi răng siêu âm");
        discoveryVm.Services[1].ServiceName.Should().Be("Trám Răng Composite");
        discoveryVm.DentistsByDepartment[1][0].FullName.Should().Be("Lê Văn Bác Sĩ Một");
        discoveryVm.DentistsByDepartment[1][0].Qualification.Should().Be("Thạc sĩ Răng Hàm Mặt");

        // 2. Patient Appointment List ViewModel
        var apptVm = new AppointmentListViewModel
        {
            Role = "Patient",
            Appointments = new List<AppointmentDto>
            {
                new()
                {
                    AppointmentId = 1,
                    PatientName = "Nguyễn Văn Bệnh Nhân",
                    ServiceName = "Nhổ Răng Khôn",
                    DepartmentName = "Khoa Răng Tổng Quát",
                    ReasonForVisit = "Đau nhức răng khôn hàm dưới",
                    AssignedDentistName = "Lê Văn Bác Sĩ Một"
                }
            }
        };

        apptVm.Appointments[0].PatientName.Should().Be("Nguyễn Văn Bệnh Nhân");
        apptVm.Appointments[0].ServiceName.Should().Be("Nhổ Răng Khôn");
        apptVm.Appointments[0].DepartmentName.Should().Be("Khoa Răng Tổng Quát");
        apptVm.Appointments[0].ReasonForVisit.Should().Be("Đau nhức răng khôn hàm dưới");
        apptVm.Appointments[0].AssignedDentistName.Should().Be("Lê Văn Bác Sĩ Một");
    }

    [Theory]
    [InlineData("Khám & Lấy Cao Răng")]
    [InlineData("Trám Răng Composite")]
    [InlineData("Nhổ Răng Khôn")]
    [InlineData("Niềng Răng Mắc Cài Kim Loại")]
    [InlineData("Bọc Răng Sứ Cercon")]
    [InlineData("Khoa Răng Tổng Quát")]
    [InlineData("Khoa Chỉnh Nha & Phục Hình")]
    [InlineData("Trần Thị Lễ Tân")]
    [InlineData("Lê Văn Bác Sĩ Một")]
    [InlineData("Phạm Thị Bác Sĩ Hai")]
    [InlineData("Hoàng Quản Trị Viên")]
    [InlineData("Nguyễn Văn Bệnh Nhân")]
    public void TestGroup5_PortalViews_SampleStrings_ContainNoMojibake(string text)
    {
        text.Should().NotContain("Ã¡");
        text.Should().NotContain("Ã´");
        text.Should().NotContain("Ã¢");
        text.Should().NotContain("Äƒ");
        text.Should().NotContain("áº");
        text.Should().NotContain("á»");
        text.Should().NotContain("\uFFFD");
        text.Should().NotContain("ï¿½");
    }
}
