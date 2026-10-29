using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class OdontogramToothStatusLabelTests
{
    private static string FindSolutionRoot()
    {
        var currentDir = AppContext.BaseDirectory;
        while (currentDir != null)
        {
            if (Directory.Exists(Path.Combine(currentDir, "FE")) && Directory.Exists(Path.Combine(currentDir, "BE")))
            {
                return currentDir;
            }
            var parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }

        return @"D:\SWP\SWP391_Group6";
    }

    [Fact]
    public void DentistMedicalRecords_Odontogram_ContainsValidToothStatusTagStyles()
    {
        var root = FindSolutionRoot();
        var dentistViewPath = Path.Combine(root, "FE", "DentalClinic.Web", "Views", "Dentist", "MedicalRecords", "Index.cshtml");
        File.Exists(dentistViewPath).Should().BeTrue("Dentist medical records view file must exist");

        var content = File.ReadAllText(dentistViewPath);

        // Odontogram container & arch checks
        content.Should().Contain(".odontogram-wrapper", "Odontogram wrapper class should be defined");
        content.Should().Contain("overflow-x: auto;", "Odontogram wrapper should support horizontal scroll on narrow viewports");

        // Tooth card dimension checks
        content.Should().Contain("width: 50px;", "Tooth card width should be 50px to provide sufficient space for text");
        content.Should().Contain("height: 72px;", "Tooth card height should be 72px to prevent vertical compression");
        content.Should().Contain("box-sizing: border-box;", "Tooth card should use border-box");

        // Tooth status label styling checks
        content.Should().Contain(".tooth-status-tag", "Tooth status tag class should be defined");
        content.Should().Contain("display: flex;", "Status tag should use flexbox for precise centering");
        content.Should().Contain("align-items: center;", "Status tag should vertically center text");
        content.Should().Contain("justify-content: center;", "Status tag should horizontally center text");
        content.Should().Contain("white-space: nowrap;", "Status tag must strictly prevent awkward line wraps like 'HEAL' / 'Y'");
        content.Should().Contain("overflow: hidden;", "Status tag must clip excessive text safely");
        content.Should().Contain("text-overflow: ellipsis;", "Status tag should use ellipsis for long conditions");
        content.Should().Contain("min-height: 17px;", "Status tag should have adequate height to prevent clipping letter descenders");
    }

    [Fact]
    public void PatientMedicalRecords_Odontogram_ContainsValidToothStatusTagStyles()
    {
        var root = FindSolutionRoot();
        var patientViewPath = Path.Combine(root, "FE", "DentalClinic.Web", "Views", "Patient", "MedicalRecords", "Index.cshtml");
        File.Exists(patientViewPath).Should().BeTrue("Patient medical records view file must exist");

        var content = File.ReadAllText(patientViewPath);

        // Odontogram container & arch checks
        content.Should().Contain(".odontogram-wrapper", "Odontogram wrapper class should be defined");
        content.Should().Contain("overflow-x: auto;", "Odontogram wrapper should support horizontal scroll on narrow viewports");

        // Tooth card dimension checks
        content.Should().Contain("width: 50px;", "Tooth card width should be 50px to provide sufficient space for text");
        content.Should().Contain("height: 72px;", "Tooth card height should be 72px to prevent vertical compression");
        content.Should().Contain("box-sizing: border-box;", "Tooth card should use border-box");

        // Tooth status label styling checks
        content.Should().Contain(".tooth-status-tag", "Tooth status tag class should be defined");
        content.Should().Contain("display: flex;", "Status tag should use flexbox for precise centering");
        content.Should().Contain("align-items: center;", "Status tag should vertically center text");
        content.Should().Contain("justify-content: center;", "Status tag should horizontally center text");
        content.Should().Contain("white-space: nowrap;", "Status tag must strictly prevent awkward line wraps like 'HEAL' / 'Y'");
        content.Should().Contain("overflow: hidden;", "Status tag must clip excessive text safely");
        content.Should().Contain("text-overflow: ellipsis;", "Status tag should use ellipsis for long conditions");
        content.Should().Contain("min-height: 17px;", "Status tag should have adequate height to prevent clipping letter descenders");
    }

    [Fact]
    public void DentistAndPatientViews_OdontogramToothStyles_AreSynchronized()
    {
        var root = FindSolutionRoot();
        var dentistContent = File.ReadAllText(Path.Combine(root, "FE", "DentalClinic.Web", "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientContent = File.ReadAllText(Path.Combine(root, "FE", "DentalClinic.Web", "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        var commonTokens = new[]
        {
            "width: 50px;",
            "height: 72px;",
            "min-height: 17px;",
            "height: 17px;",
            "font-size: 0.52rem;",
            "letter-spacing: -0.2px;",
            "white-space: nowrap;",
            "overflow: hidden;",
            "text-overflow: ellipsis;",
            "display: flex;",
            "align-items: center;",
            "justify-content: center;"
        };

        foreach (var token in commonTokens)
        {
            dentistContent.Should().Contain(token, $"Dentist view should contain {token}");
            patientContent.Should().Contain(token, $"Patient view should contain {token}");
        }
    }
}
