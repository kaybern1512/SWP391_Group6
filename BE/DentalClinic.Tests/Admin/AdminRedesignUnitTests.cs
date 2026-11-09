using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

public class AdminRedesignUnitTests
{
    private static DentalClinicDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    // =========================================================================
    // 1. DỊCH VỤ NHA KHOA: THỜI LƯỢNG RIÊNG BIỆT & YÊU CẦU LÂM SÀNG
    // =========================================================================
    [Fact]
    public async Task ServiceManagement_CustomDurationAndClinicalRequirements_SavedCorrectly()
    {
        // Arrange
        using var db = CreateInMemoryDb(nameof(ServiceManagement_CustomDurationAndClinicalRequirements_SavedCorrectly));
        var mockAudit = new Mock<IAuditLogService>();
        var mockLogger = new Mock<ILogger<AdminService>>();
        var adminService = new AdminService(db, mockAudit.Object, mockLogger.Object);

        var dept = new Department { DepartmentId = 1, Name = "Khoa Cấy Ghép Implant", Status = "Active" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var createReq = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Cấy ghép Implant Straumann",
            Description = "Phẫu thuật cấy ghép trụ Implant Thụy Sĩ",
            DurationMinutes = 90, // Thời lượng riêng 90 phút, KHÔNG ép về 30 phút General Consultation
            Price = 25000000m,
            Status = "Active",
            ServiceType = "Surgery",
            RequiresExclusiveRoom = true, // Yêu cầu phòng vô trùng độc quyền
            RequiresConsultationFirst = true, // Bắt buộc khám tư vấn trước
            MinLeadTimeHours = 24, // Cần đặt trước 24 giờ để chuẩn bị phòng phẫu thuật
            BookingInstructions = "Bệnh nhân cần nhịn ăn trước phẫu thuật 4 tiếng và chụp phim CT Cone Beam."
        };

        // Act
        var createdRes = await adminService.CreateServiceAsync(createReq, 1);

        // Assert
        Assert.True(createdRes.Success);
        var created = createdRes.Data!;
        Assert.Equal("Cấy ghép Implant Straumann", created.ServiceName);
        Assert.Equal(90, created.DurationMinutes);
        Assert.Equal("Surgery", created.ServiceType);
        Assert.True(created.RequiresExclusiveRoom);
        Assert.True(created.RequiresConsultationFirst);
        Assert.Equal(24, created.MinLeadTimeHours);
        Assert.Equal("Bệnh nhân cần nhịn ăn trước phẫu thuật 4 tiếng và chụp phim CT Cone Beam.", created.BookingInstructions);

        // Update test: cập nhật thời lượng và giá
        var updateReq = new UpdateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Cấy ghép Implant Straumann Cao Cấp",
            Description = "Phẫu thuật cấy ghép trụ Implant Thụy Sĩ kèm màng sinh học",
            DurationMinutes = 120, // Nâng thời lượng lên 120 phút
            Price = 28000000m,
            Status = "Active",
            ServiceType = "Surgery",
            RequiresExclusiveRoom = true,
            RequiresConsultationFirst = true,
            MinLeadTimeHours = 48,
            BookingInstructions = "Đến sớm 30 phút để kiểm tra huyết áp."
        };

        var updatedRes = await adminService.UpdateServiceAsync(created.DepartmentServiceId, updateReq, 1);
        Assert.True(updatedRes.Success);
        var updated = updatedRes.Data!;

        Assert.Equal(120, updated.DurationMinutes);
        Assert.Equal(28000000m, updated.Price);
        Assert.Equal(48, updated.MinLeadTimeHours);
    }

    [Fact]
    public async Task ServiceManagement_MultipleServices_HaveDistinctDurationsWithoutGeneralConsultationCollapse()
    {
        // Arrange: 5 dịch vụ thuộc các chuyên khoa khác nhau có thời lượng riêng biệt
        using var db = CreateInMemoryDb(nameof(ServiceManagement_MultipleServices_HaveDistinctDurationsWithoutGeneralConsultationCollapse));
        var mockAudit = new Mock<IAuditLogService>();
        var mockLogger = new Mock<ILogger<AdminService>>();
        var adminService = new AdminService(db, mockAudit.Object, mockLogger.Object);

        var dept = new Department { DepartmentId = 2, Name = "Khoa Tổng Quát", Status = "Active" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var services = new List<(string Name, int Duration, string Type)>
        {
            ("Lấy cao răng & Đánh bóng", 20, "Treatment"),
            ("Khám tổng quát & Lập hồ sơ", 15, "Consultation"),
            ("Nhổ răng khôn mọc lệch hàm dưới", 60, "Surgery"),
            ("Chỉnh nha mắc cài kim loại", 45, "Treatment"),
            ("Tẩy trắng răng công nghệ Laser Whitening", 75, "Cosmetic")
        };

        foreach (var s in services)
        {
            await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
            {
                DepartmentId = 2,
                ServiceName = s.Name,
                DurationMinutes = s.Duration,
                Price = 500000m,
                ServiceType = s.Type
            }, 1);
        }

        // Act
        var result = await adminService.GetServicesAsync(departmentId: 2);
        Assert.True(result.Success);
        var list = result.Data!;

        // Assert
        Assert.Equal(5, list.Count);
        var durations = list.Select(r => r.DurationMinutes).Distinct().ToList();
        Assert.Equal(5, durations.Count); // Cả 5 dịch vụ đều có thời lượng hoàn toàn riêng biệt
        Assert.DoesNotContain(list, r => r.ServiceName != "Khám tổng quát & Lập hồ sơ" && r.DurationMinutes == 15);
    }

    // =========================================================================
    // 2. QUẢN LÝ NHÂN VIÊN (STAFF MANAGEMENT - 4 ROLES CHUẨN)
    // =========================================================================
    [Fact]
    public async Task StaffManagement_CreateValidRoles_DentistReceptionistAdmin_Succeeds()
    {
        // Arrange
        using var db = CreateInMemoryDb(nameof(StaffManagement_CreateValidRoles_DentistReceptionistAdmin_Succeeds));
        var mockAudit = new Mock<IAuditLogService>();
        var mockLogger = new Mock<ILogger<AdminService>>();
        var adminService = new AdminService(db, mockAudit.Object, mockLogger.Object);

        var dept = new Department { DepartmentId = 1, Name = "Khoa Răng Hàm Mặt", Status = "Active" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        // 1. Tạo Bác sĩ (Dentist)
        var dentistReq = new CreateStaffRequest
        {
            FullName = "Bác sĩ Trần Nha Khoa",
            Email = "bstran@dentalcare.com",
            Password = "Password@123",
            PhoneNumber = "0987654321",
            Role = UserRole.Dentist,
            Specialty = "Bác sĩ Chuyên khoa 1 Chỉnh nha",
            DepartmentId = 1
        };

        var dentistRes = await adminService.CreateStaffAccountAsync(dentistReq, 1);
        Assert.True(dentistRes.Success);
        var dentist = dentistRes.Data!;
        Assert.Equal(UserRole.Dentist, dentist.Role);
        Assert.Equal("Bác sĩ Chuyên khoa 1 Chỉnh nha", dentist.Specialty);

        // 2. Tạo Lễ tân (Receptionist)
        var recReq = new CreateStaffRequest
        {
            FullName = "Lễ tân Nguyễn Thu Ngân",
            Email = "letan@dentalcare.com",
            Password = "Password@123",
            PhoneNumber = "0912345678",
            Role = UserRole.Receptionist
        };

        var recRes = await adminService.CreateStaffAccountAsync(recReq, 1);
        Assert.True(recRes.Success);
        var receptionist = recRes.Data!;
        Assert.Equal(UserRole.Receptionist, receptionist.Role);

        // 3. Tạo Quản trị viên (SystemAdministrator)
        var adminReq = new CreateStaffRequest
        {
            FullName = "Quản trị viên Hệ thống",
            Email = "admin2@dentalcare.com",
            Password = "Password@123",
            Role = UserRole.SystemAdministrator
        };

        var adminRes = await adminService.CreateStaffAccountAsync(adminReq, 1);
        Assert.True(adminRes.Success);
        var adminUser = adminRes.Data!;
        Assert.Equal(UserRole.SystemAdministrator, adminUser.Role);
    }

    [Fact]
    public async Task StaffManagement_CreateDisallowedRole_FailsValidation()
    {
        // Arrange
        using var db = CreateInMemoryDb(nameof(StaffManagement_CreateDisallowedRole_FailsValidation));
        var mockAudit = new Mock<IAuditLogService>();
        var mockLogger = new Mock<ILogger<AdminService>>();
        var adminService = new AdminService(db, mockAudit.Object, mockLogger.Object);

        // Thử tạo role DepartmentManager cũ (đã loại bỏ khỏi mô hình 4 role)
        var invalidReq = new CreateStaffRequest
        {
            FullName = "Trưởng khoa cũ",
            Email = "manager_old@dentalcare.com",
            Password = "Password@123",
            Role = "DepartmentManager"
        };

        // Act
        var result = await adminService.CreateStaffAccountAsync(invalidReq, 1);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("không hợp lệ", result.Message);
    }

    // =========================================================================
    // 3. KIỂM DUYỆT ĐÁNH GIÁ BÁC SĨ (FEEDBACK MODERATION - KHÔNG SỬA SAO)
    // =========================================================================
    [Fact]
    public async Task FeedbackModeration_AdminHidesAndApproves_PreservesPatientStarRating()
    {
        // Arrange
        using var db = CreateInMemoryDb(nameof(FeedbackModeration_AdminHidesAndApproves_PreservesPatientStarRating));
        var feedbackService = new FeedbackService(db);

        var patientUser = new UserAccount
        {
            UserId = 10,
            Email = "patient10@example.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 10,
            PatientCode = "PAT-010",
            FullName = "Bệnh nhân Nguyễn Văn A",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = "Male",
            Address = "Hà Nội"
        };
        db.UserAccounts.Add(patientUser);
        db.PatientProfiles.Add(patientProfile);

        var appt = new Appointment
        {
            AppointmentId = 50,
            AppointmentCode = "APPT-050",
            PatientUserId = 10,
            PatientUser = patientProfile,
            Status = "Completed",
            BookingSource = "Online",
            ReasonForVisit = "Khám răng tổng quát",
            ScheduledStart = DateTime.UtcNow.AddDays(-1),
            ScheduledEnd = DateTime.UtcNow.AddDays(-1).AddMinutes(30)
        };
        db.Appointments.Add(appt);

        // Giả lập Appointment đã hoàn tất và Feedback từ bệnh nhân
        var feedback = new VisitFeedback
        {
            FeedbackId = 100,
            AppointmentId = 50,
            Appointment = appt,
            DentistRating = 4, // Bệnh nhân đánh giá 4 sao
            ClinicRating = 5,  // Phòng khám 5 sao
            Comment = "Bác sĩ tư vấn rất kỹ lưỡng, phòng khám sạch đẹp.",
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        };
        db.VisitFeedbacks.Add(feedback);
        await db.SaveChangesAsync();

        // Act 1: Admin kiểm duyệt ẩn đánh giá (Hidden) có lý do
        var hideReq = new ModerateFeedbackRequest
        {
            Status = "Hidden",
            AdminNotes = "Ẩn tạm thời để xác minh thông tin với khoa."
        };
        var hideResult = await feedbackService.ModerateFeedbackAsync(100, hideReq, 1);

        // Assert 1
        Assert.True(hideResult.Success);
        Assert.Equal("Hidden", hideResult.Data!.Status);
        Assert.Equal("Ẩn tạm thời để xác minh thông tin với khoa.", hideResult.Data.AdminNotes);
        Assert.Equal(4, hideResult.Data.DentistRating); // Số sao bảo toàn tuyệt đối 4 sao!
        Assert.Equal(5, hideResult.Data.ClinicRating);  // Số sao bảo toàn tuyệt đối 5 sao!

        // Act 2: Admin phản hồi chính thức từ phòng khám
        var respondReq = new RespondFeedbackRequest
        {
            AdminResponse = "Cảm ơn quý khách đã tin tưởng và đánh giá dịch vụ tại DentalCare."
        };
        var respResult = await feedbackService.RespondFeedbackAsync(100, respondReq, 1);

        // Assert 2
        Assert.True(respResult.Success);
        Assert.Equal("Cảm ơn quý khách đã tin tưởng và đánh giá dịch vụ tại DentalCare.", respResult.Data!.AdminResponse);
        Assert.Equal(4, respResult.Data.DentistRating); // Không thể bị sửa thành 5
    }

    // =========================================================================
    // 4. KIỂM DUYỆT BLOG & CMS (RECEPTIONIST VIẾT, ADMIN DUYỆT / TỪ CHỐI CÓ LÝ DO)
    // =========================================================================
    [Fact]
    public async Task BlogModeration_AdminApprovesOrRejectsWithReason_Successfully()
    {
        // Arrange
        using var db = CreateInMemoryDb(nameof(BlogModeration_AdminApprovesOrRejectsWithReason_Successfully));
        var mockAudit = new Mock<IAuditLogService>();
        var mockStorage = new Mock<IMediaStorageService>();
        var mockValidator = new Mock<IBlogMediaFileValidator>();
        var mockLogger = new Mock<ILogger<BlogService>>();
        var blogService = new BlogService(db, mockAudit.Object, mockStorage.Object, mockValidator.Object, mockLogger.Object);

        var authorUser = new UserAccount
        {
            UserId = 5,
            Email = "receptionist5@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        var staffProfile = new StaffProfile
        {
            UserId = 5,
            EmployeeCode = "EMP-005",
            FullName = "Lễ tân Viết Blog",
            EmploymentStatus = "Active"
        };
        db.UserAccounts.Add(authorUser);
        db.StaffProfiles.Add(staffProfile);

        var category = new BlogCategory { CategoryId = 1, Name = "Kiến thức Chăm sóc Răng", Slug = "kien-thuc-cham-soc-rang" };
        db.BlogCategories.Add(category);

        // Bài 1: Chờ duyệt
        var post1 = new BlogPost
        {
            BlogPostId = 201,
            Title = "Cách đánh răng đúng chuẩn nha khoa",
            Slug = "cach-danh-rang-dung-chuan-nha-khoa",
            Content = "<p>Nội dung hướng dẫn chải răng 45 độ theo phương pháp Bass...</p>",
            Status = "PendingReview",
            PostType = "Blog",
            AuthorUserId = 5,
            AuthorUser = authorUser,
            CategoryId = 1,
            Category = category,
            CreatedAt = DateTime.UtcNow
        };

        // Bài 2: Chờ duyệt để từ chối
        var post2 = new BlogPost
        {
            BlogPostId = 202,
            Title = "Mẹo làm trắng răng cấp tốc tại nhà",
            Slug = "meo-lam-trang-rang-cap-toc-tai-nha",
            Content = "<p>Dùng chanh và muối chà trực tiếp lên men răng...</p>",
            Status = "PendingReview",
            PostType = "Blog",
            AuthorUserId = 5,
            AuthorUser = authorUser,
            CategoryId = 1,
            Category = category,
            CreatedAt = DateTime.UtcNow
        };

        db.BlogPosts.AddRange(post1, post2);
        await db.SaveChangesAsync();

        // Act 1: Admin phê duyệt bài 1 (Published)
        var approveReq = new ReviewBlogPostRequest
        {
            Status = "Published",
            IsFeatured = true,
            ReviewNotes = "Nội dung chuẩn y khoa, duyệt đăng lên trang chủ."
        };
        var approveResult = await blogService.ReviewPostAsync(201, 1, approveReq);

        // Assert 1
        Assert.True(approveResult.Success);
        Assert.Equal("Published", approveResult.Data!.Status);
        Assert.True(approveResult.Data.IsFeatured);

        var savedPost1 = await db.BlogPosts.FindAsync(201L);
        Assert.Equal("Published", savedPost1!.Status);
        Assert.Equal(1, savedPost1.ApprovedByUserId);
        Assert.NotNull(savedPost1.ApprovedAt);

        // Act 2: Admin từ chối bài 2 (Rejected) kèm RejectionReason
        var rejectReq = new ReviewBlogPostRequest
        {
            Status = "Rejected",
            RejectionReason = "Phương pháp dùng chanh làm mòn men răng nghiêm trọng, phản khoa học.",
            ReviewNotes = "Từ chối duyệt bài vi phạm chuyên môn."
        };
        var rejectResult = await blogService.ReviewPostAsync(202, 1, rejectReq);

        // Assert 2
        Assert.True(rejectResult.Success);
        Assert.Equal("Rejected", rejectResult.Data!.Status);

        var savedPost2 = await db.BlogPosts.FindAsync(202L);
        Assert.Equal("Rejected", savedPost2!.Status);
        Assert.Equal("Phương pháp dùng chanh làm mòn men răng nghiêm trọng, phản khoa học.", savedPost2.RejectionReason);
    }
}
