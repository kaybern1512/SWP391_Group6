using System.IO;
using FluentAssertions;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class MedicalImagingBasicViewerTests
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
    public void DentistMedicalRecords_ImagingLightboxModal_ContainsFitImageAndContainMarkup()
    {
        var root = FindSolutionRoot();
        var dentistViewPath = Path.Combine(root, "FE", "DentalClinic.Web", "Views", "Dentist", "MedicalRecords", "Index.cshtml");
        File.Exists(dentistViewPath).Should().BeTrue("Dentist medical records view file must exist");

        var content = File.ReadAllText(dentistViewPath);

        // Modal and viewport checks
        content.Should().Contain("id=\"imagingLightboxModal\"", "Basic viewer modal should exist");
        content.Should().Contain("basic-imaging-viewport", "Basic imaging viewport class should be present");
        content.Should().Contain("style=\"width: 100%; height: 100%; max-width: 100%; max-height: 100%; object-fit: contain;\"", 
            "Image must scale to 100% width and height with object-fit: contain to minimize empty black space");
        content.Should().Contain("id=\"lightboxImage\"", "Lightbox image element should exist");
        content.Should().NotContain("img-fluid d-none", "img-fluid must not be used on lightbox image as it prevents full container fit");

        // Safety viewport constraints ensuring header and footer are not obscured
        content.Should().Contain("max-height: calc(100vh - 200px)", "Viewport should leave space for modal header and footer");
        content.Should().Contain("id=\"lightboxDownloadBtn\"", "Download button must be present in footer");

        // Advanced viewer tools must NOT be present
        content.Should().NotContain("imaging-viewer-tools", "Advanced viewer tools should not be in basic viewer");
        content.Should().NotContain("btn-zoom-in", "Zoom tool should not be in basic viewer");
        content.Should().NotContain("btn-rotate", "Rotate tool should not be in basic viewer");
    }

    [Fact]
    public void PatientMedicalRecords_ImagingLightboxModal_ContainsFitImageAndContainMarkup()
    {
        var root = FindSolutionRoot();
        var patientViewPath = Path.Combine(root, "FE", "DentalClinic.Web", "Views", "Patient", "MedicalRecords", "Index.cshtml");
        File.Exists(patientViewPath).Should().BeTrue("Patient medical records view file must exist");

        var content = File.ReadAllText(patientViewPath);

        // Modal and viewport checks
        content.Should().Contain("id=\"imagingLightboxModal\"", "Basic viewer modal should exist");
        content.Should().Contain("basic-imaging-viewport", "Basic imaging viewport class should be present");
        content.Should().Contain("style=\"width: 100%; height: 100%; max-width: 100%; max-height: 100%; object-fit: contain;\"", 
            "Image must scale to 100% width and height with object-fit: contain to minimize empty black space");
        content.Should().Contain("id=\"lightboxImage\"", "Lightbox image element should exist");
        content.Should().NotContain("img-fluid d-none", "img-fluid must not be used on lightbox image as it prevents full container fit");

        // Safety viewport constraints ensuring header and footer are not obscured
        content.Should().Contain("max-height: calc(100vh - 200px)", "Viewport should leave space for modal header and footer");
        content.Should().Contain("id=\"lightboxDownloadBtn\"", "Download button must be present in footer");

        // Advanced viewer tools must NOT be present
        content.Should().NotContain("imaging-viewer-tools", "Advanced viewer tools should not be in basic viewer");
        content.Should().NotContain("btn-zoom-in", "Zoom tool should not be in basic viewer");
        content.Should().NotContain("btn-rotate", "Rotate tool should not be in basic viewer");
    }

}
