using System;
using System.Reflection;
using DentalClinic.Api.Controllers;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DentalClinic.Tests.Admin;

public class FourRoleRbacAuthorizationRegressionTests
{
    // ================================================================================================
    // RULE 1: Admin không được thao tác lâm sàng thay Dentist (Clinical Entries & Prescribing)
    // ================================================================================================
    [Fact]
    public void Rule1_MedicalRecord_CreateClinicalEntry_MustBeDentistOnly_NeverAdminOrReceptionist()
    {
        var method = typeof(MedicalRecordController).GetMethod(nameof(MedicalRecordController.CreateClinicalEntry));
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var roles = authAttr.Roles.Split(',', StringSplitOptions.TrimEntries);
        Assert.Contains(UserRole.Dentist, roles);
        Assert.DoesNotContain(UserRole.SystemAdministrator, roles);
        Assert.DoesNotContain(UserRole.Receptionist, roles);
        Assert.DoesNotContain(UserRole.Patient, roles);
    }

    // ================================================================================================
    // RULE 2: Receptionist không được quản trị kho thuốc (Chỉ SystemAdministrator quản lý kho)
    // ================================================================================================
    [Theory]
    [InlineData(nameof(MedicineController.CreateMedicine))]
    [InlineData(nameof(MedicineController.UpdateMedicine))]
    [InlineData(nameof(MedicineController.ImportBatch))]
    [InlineData(nameof(MedicineController.AdjustStock))]
    [InlineData(nameof(MedicineController.GetInventorySummary))]
    [InlineData(nameof(MedicineController.GetStockTransactions))]
    public void Rule2_MedicineController_InventoryManagement_MustBeStrictlyAdminOnly_NeverReceptionist(string actionName)
    {
        var method = typeof(MedicineController).GetMethod(actionName);
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var roles = authAttr.Roles.Split(',', StringSplitOptions.TrimEntries);
        Assert.Contains(UserRole.SystemAdministrator, roles);
        Assert.DoesNotContain(UserRole.Receptionist, roles);
        Assert.DoesNotContain(UserRole.Patient, roles);
    }

    [Theory]
    [InlineData(nameof(MedicineController.ImportBatch))]
    [InlineData(nameof(MedicineController.AdjustStock))]
    [InlineData(nameof(MedicineController.CreateMedicine))]
    public void Rule2b_MedicineController_DentistCannotMutateMedicineStock(string actionName)
    {
        var method = typeof(MedicineController).GetMethod(actionName);
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var roles = authAttr.Roles.Split(',', StringSplitOptions.TrimEntries);
        Assert.DoesNotContain(UserRole.Dentist, roles);
    }

    // ================================================================================================
    // RULE 3: Patient không được truy cập bệnh án người khác (IDOR Protection)
    // ================================================================================================
    [Fact]
    public void Rule3_MedicalRecord_GetByPatientId_MustNotAllowPatientRoleDirectAccess()
    {
        var method = typeof(MedicalRecordController).GetMethod(nameof(MedicalRecordController.GetByPatientId));
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var roles = authAttr.Roles.Split(',', StringSplitOptions.TrimEntries);
        // Only clinical staff / receptionist / admin can look up records by arbitrary ID
        Assert.DoesNotContain(UserRole.Patient, roles);
    }

    [Fact]
    public void Rule3b_MedicalRecord_GetMyMedicalRecord_MustAllowOnlyPatientRole()
    {
        var method = typeof(MedicalRecordController).GetMethod(nameof(MedicalRecordController.GetMyMedicalRecord));
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(UserRole.Patient, authAttr.Roles);
    }

    // ================================================================================================
    // RULE 4: Dentist không được viết Blog (Chỉ Receptionist & Admin được viết bài)
    // ================================================================================================
    [Theory]
    [InlineData(nameof(BlogController.CreatePost))]
    [InlineData(nameof(BlogController.UpdatePost))]
    [InlineData(nameof(BlogController.DeletePost))]
    [InlineData(nameof(BlogController.UploadThumbnail))]
    [InlineData(nameof(BlogController.GetMyPosts))]
    public void Rule4_BlogController_AuthoringEndpoints_MustNotAllowDentist(string actionName)
    {
        var method = typeof(BlogController).GetMethod(actionName);
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var roles = authAttr.Roles.Split(',', StringSplitOptions.TrimEntries);
        Assert.Contains(UserRole.Receptionist, roles);
        Assert.Contains(UserRole.SystemAdministrator, roles);
        Assert.DoesNotContain(UserRole.Dentist, roles);
        Assert.DoesNotContain(UserRole.Patient, roles);
    }

    [Theory]
    [InlineData(nameof(BlogController.GetAllPostsForAdmin))]
    [InlineData(nameof(BlogController.ReviewPost))]
    public void Rule4b_BlogController_ModerationEndpoints_MustBeStrictlyAdminOnly(string actionName)
    {
        var method = typeof(BlogController).GetMethod(actionName);
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(UserRole.SystemAdministrator, authAttr.Roles);
    }

    // ================================================================================================
    // RULE 5: Feedback Moderation must be Admin only
    // ================================================================================================
    [Theory]
    [InlineData(nameof(FeedbackController.ModerateFeedback))]
    [InlineData(nameof(FeedbackController.RespondFeedback))]
    public void Rule5_FeedbackController_ModerationEndpoints_MustBeAdminOnly(string actionName)
    {
        var method = typeof(FeedbackController).GetMethod(actionName);
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(UserRole.SystemAdministrator, authAttr.Roles);
    }
}
