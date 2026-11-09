using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Application.Features.Feedback.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Admin;

public class AdminEndToEndSqlServerIntegrationTests
{
    private const string ConnectionString = "Server=.\\SQLEXPRESS;Database=DentalCare_Integration_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static DentalClinicDbContext CreateRealDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    [Fact]
    public async Task SqlServer_E2E_StaffAndDentistCreation_EnsuresDentistProfileAndAllocationsCreated()
    {
        using var db = CreateRealDbContext();
        var mockAudit = new Mock<IAuditLogService>();
        var mockLogger = new Mock<ILogger<AdminService>>();
        var mockHasher = new Mock<IPasswordHasherService>();
        mockHasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => $"HASHED_{p}");
        var adminService = new AdminService(db, mockAudit.Object, mockLogger.Object, mockHasher.Object);

        var testEmail = $"e2e.dentist.{Guid.NewGuid():N}@dentalcare.com";
        var dept = await db.Departments.FirstOrDefaultAsync();
        if (dept == null)
        {
            dept = new Department { Name = "Khoa Cấy Ghép Implant", Status = "Active" };
            db.Departments.Add(dept);
            await db.SaveChangesAsync();
        }

        var req = new CreateStaffRequest
        {
            Email = testEmail,
            Password = "Password123!",
            FullName = "Bác sĩ Test E2E",
            PhoneNumber = $"09{Random.Shared.Next(10000000, 99999999)}",
            Role = UserRole.Dentist,
            Specialty = "Implant Phẫu thuật",
            Education = "Thạc sĩ Răng Hàm Mặt",
            ExperienceYears = 8,
            DepartmentId = dept.DepartmentId
        };

        var result = await adminService.CreateStaffAccountAsync(req, 1);
        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        var userId = result.Data.UserId;

        // Verify in database: UserAccount, StaffProfile, DentistProfile, DentistDepartment
        var user = await db.UserAccounts
            .Include(u => u.StaffProfile)
                .ThenInclude(sp => sp!.DentistProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        Assert.NotNull(user);
        Assert.Equal(testEmail, user.Email);
        Assert.Equal(UserRole.Dentist, user.Role);
        Assert.NotNull(user.StaffProfile);
        Assert.Equal("Bác sĩ Test E2E", user.StaffProfile.FullName);

        Assert.NotNull(user.StaffProfile.DentistProfile);
        Assert.Equal(8, user.StaffProfile.DentistProfile.YearsOfExperience);
        Assert.Contains("Thạc sĩ", user.StaffProfile.DentistProfile.Qualification);
        Assert.NotEmpty(user.StaffProfile.DentistProfile.LicenseNumber ?? string.Empty);

        var allocations = await db.DentistDepartments.Where(dd => dd.DentistUserId == userId).ToListAsync();
        Assert.NotEmpty(allocations);
        Assert.Contains(allocations, dd => dd.DepartmentId == dept.DepartmentId && dd.Status == "Active");

        // Cleanup
        db.DentistDepartments.RemoveRange(allocations);
        db.DentistProfiles.Remove(user.StaffProfile.DentistProfile);
        db.StaffProfiles.Remove(user.StaffProfile);
        db.UserAccounts.Remove(user);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SqlServer_E2E_ClinicalServiceCustomization_VerifiedReadableByBooking()
    {
        using var db = CreateRealDbContext();
        var mockAudit = new Mock<IAuditLogService>();
        var mockLogger = new Mock<ILogger<AdminService>>();
        var mockHasher = new Mock<IPasswordHasherService>();
        var adminService = new AdminService(db, mockAudit.Object, mockLogger.Object, mockHasher.Object);

        var dept = await db.Departments.FirstOrDefaultAsync();
        Assert.NotNull(dept);

        var serviceName = $"E2E Service {Guid.NewGuid():N}";
        var createReq = new CreateDepartmentServiceRequest
        {
            DepartmentId = dept.DepartmentId,
            ServiceName = serviceName,
            Description = "Dịch vụ thử nghiệm lâm sàng E2E",
            Price = 18500000m,
            DurationMinutes = 90,
            ServiceType = "Implant",
            RequiresExclusiveRoom = true,
            RequiresConsultationFirst = true,
            MinLeadTimeHours = 24,
            BookingInstructions = "Nhịn ăn 4 tiếng trước giờ phẫu thuật."
        };

        var createRes = await adminService.CreateServiceAsync(createReq, 1);
        Assert.True(createRes.Success, createRes.Message);
        var serviceId = createRes.Data!.DepartmentServiceId;

        // Verify simulated Booking API query reads exact clinical fields
        var queriedService = await db.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == serviceId);

        Assert.NotNull(queriedService);
        Assert.Equal(90, queriedService.DurationMinutes);
        Assert.Equal("Implant", queriedService.ServiceType);
        Assert.True(queriedService.RequiresExclusiveRoom);
        Assert.True(queriedService.RequiresConsultationFirst);
        Assert.Equal(24, queriedService.MinLeadTimeHours);
        Assert.Equal("Nhịn ăn 4 tiếng trước giờ phẫu thuật.", queriedService.BookingInstructions);

        // Update Duration and clinical fields
        var updateReq = new UpdateDepartmentServiceRequest
        {
            DepartmentId = dept.DepartmentId,
            ServiceName = serviceName,
            Price = 20000000m,
            DurationMinutes = 120,
            ServiceType = "Surgery",
            RequiresExclusiveRoom = true,
            RequiresConsultationFirst = true,
            MinLeadTimeHours = 48,
            BookingInstructions = "Cần làm xét nghiệm máu trước phẫu thuật.",
            Status = "Active"
        };

        var updateRes = await adminService.UpdateServiceAsync(serviceId, updateReq, 1);
        Assert.True(updateRes.Success, updateRes.Message);

        var queriedAfterUpdate = await db.DepartmentServices.FindAsync(serviceId);
        Assert.NotNull(queriedAfterUpdate);
        Assert.Equal(120, queriedAfterUpdate.DurationMinutes);
        Assert.Equal(48, queriedAfterUpdate.MinLeadTimeHours);
        Assert.Equal("Surgery", queriedAfterUpdate.ServiceType);
        Assert.Equal("Cần làm xét nghiệm máu trước phẫu thuật.", queriedAfterUpdate.BookingInstructions);

        // Cleanup
        db.DepartmentServices.Remove(queriedAfterUpdate);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SqlServer_E2E_BlogModeration_ReceptionistCreate_AdminApproveOrReject()
    {
        using var db = CreateRealDbContext();
        var mockAudit = new Mock<IAuditLogService>();
        var mockLogger = new Mock<ILogger<BlogService>>();
        var mockStorage = new Mock<IMediaStorageService>();
        var mockValidator = new Mock<IBlogMediaFileValidator>();
        var blogService = new BlogService(db, mockAudit.Object, mockStorage.Object, mockValidator.Object, mockLogger.Object);

        var category = await db.BlogCategories.FirstOrDefaultAsync();
        if (category == null)
        {
            category = new BlogCategory { Name = "Chăm Sóc Răng Miệng", Slug = "cham-soc-rang-mieng" };
            db.BlogCategories.Add(category);
            await db.SaveChangesAsync();
        }

        var author = await db.UserAccounts.FirstOrDefaultAsync(u => u.Role == UserRole.Receptionist) 
                     ?? await db.UserAccounts.FirstAsync();

        // 1. Receptionist creates draft/pending review
        var createReq = new CreateBlogPostRequest
        {
            Title = $"Bài viết E2E {Guid.NewGuid():N}",
            Summary = "Tóm tắt bài viết kiểm thử",
            Content = "<p>Nội dung chia sẻ mẹo nha khoa an toàn.</p>",
            CategoryId = category.CategoryId,
            PostType = "Blog",
            Status = BlogPostStatus.PendingReview
        };

        var createRes = await blogService.CreatePostAsync(author.UserId, createReq);
        Assert.True(createRes.Success, createRes.Message);
        var post = createRes.Data!;
        Assert.Equal(BlogPostStatus.PendingReview, post.Status);

        // Verify post is NOT visible to public
        var publicList = await blogService.GetPublicPostsAsync(new BlogFilterRequest());
        Assert.DoesNotContain(publicList.Data!.Items, p => p.BlogPostId == post.BlogPostId);

        // 2. Admin rejects with reason
        var adminUser = await db.UserAccounts.FirstOrDefaultAsync(u => u.Role == UserRole.SystemAdministrator) ?? author;
        var rejectReq = new ReviewBlogPostRequest
        {
            Status = "Rejected",
            RejectionReason = "Cần trích dẫn hướng dẫn chi tiết từ hiệp hội nha khoa."
        };
        var rejectRes = await blogService.ReviewPostAsync(post.BlogPostId, adminUser.UserId, rejectReq);
        Assert.True(rejectRes.Success, rejectRes.Message);
        Assert.Equal("Rejected", rejectRes.Data!.Status);
        Assert.Equal("Cần trích dẫn hướng dẫn chi tiết từ hiệp hội nha khoa.", rejectRes.Data.RejectionReason);

        // 3. Admin approves and publishes
        var approveReq = new ReviewBlogPostRequest
        {
            Status = BlogPostStatus.Published
        };
        var approveRes = await blogService.ReviewPostAsync(post.BlogPostId, adminUser.UserId, approveReq);
        Assert.True(approveRes.Success, approveRes.Message);
        Assert.Equal(BlogPostStatus.Published, approveRes.Data!.Status);
        Assert.NotNull(approveRes.Data.ApprovedAt);

        // Verify post is now visible to public
        var publicListAfter = await blogService.GetPublicPostsAsync(new BlogFilterRequest());
        Assert.Contains(publicListAfter.Data!.Items, p => p.BlogPostId == post.BlogPostId);

        // Cleanup
        var entity = await db.BlogPosts.FindAsync(post.BlogPostId);
        if (entity != null)
        {
            db.BlogPosts.Remove(entity);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SqlServer_E2E_FeedbackModeration_PreservesRatingAndExcludesHidden()
    {
        using var db = CreateRealDbContext();
        var feedbackService = new FeedbackService(db);

        var appt = await db.Appointments
            .Include(a => a.PatientUser)
            .Include(a => a.AssignedDentistUser)
            .FirstOrDefaultAsync();

        if (appt != null && appt.AssignedDentistUserId.HasValue)
        {
            var feedback = new VisitFeedback
            {
                AppointmentId = appt.AppointmentId,
                PatientUserId = appt.PatientUserId,
                DentistUserId = appt.AssignedDentistUserId,
                DentistRating = 4,
                ClinicRating = 5,
                Comment = "Đánh giá E2E kiểm duyệt phản hồi",
                Status = "Approved",
                CreatedAt = DateTime.UtcNow
            };
            db.VisitFeedbacks.Add(feedback);
            await db.SaveChangesAsync();

            // Admin moderates to Hidden with notes
            var modReq = new ModerateFeedbackRequest
            {
                Status = "Hidden",
                AdminNotes = "Ẩn do chứa thông tin cá nhân"
            };
            var adminUser = await db.UserAccounts.FirstOrDefaultAsync(u => u.Role == UserRole.SystemAdministrator)
                            ?? await db.UserAccounts.FirstAsync();

            var modRes = await feedbackService.ModerateFeedbackAsync(feedback.FeedbackId, modReq, adminUser.UserId);
            Assert.True(modRes.Success);
            Assert.Equal("Hidden", modRes.Data!.Status);
            Assert.Equal(4, modRes.Data.DentistRating); // Rating preserved!
            Assert.Equal(5, modRes.Data.ClinicRating);  // Rating preserved!
            Assert.Equal("Ẩn do chứa thông tin cá nhân", modRes.Data.AdminNotes);

            // Verify dentist summary excludes Hidden feedback
            var summaryRes = await feedbackService.GetDentistFeedbackSummaryAsync(appt.AssignedDentistUserId.Value);
            if (summaryRes.Success && summaryRes.Data != null)
            {
                Assert.DoesNotContain(summaryRes.Data.RecentFeedbacks, f => f.FeedbackId == feedback.FeedbackId);
            }

            // Cleanup
            db.VisitFeedbacks.Remove(feedback);
            await db.SaveChangesAsync();
        }
    }
}
