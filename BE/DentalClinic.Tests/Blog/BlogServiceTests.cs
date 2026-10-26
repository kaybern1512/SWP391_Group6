using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Helpers;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Validation;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Blog;

public class BlogServiceTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static (BlogService service, DentalClinicDbContext db, Mock<IAuditLogService> mockAudit) CreateService(string? dbName = null)
    {
        var (service, db, mockAudit, _, _) = CreateFullService(dbName);
        return (service, db, mockAudit);
    }

    private static (BlogService service, DentalClinicDbContext db, Mock<IAuditLogService> mockAudit, Mock<IMediaStorageService> mockStorage, BlogMediaFileValidator validator) CreateFullService(string? dbName = null)
    {
        var db = CreateDbContext(dbName ?? Guid.NewGuid().ToString());
        var mockAudit = new Mock<IAuditLogService>();
        var mockLogger = new Mock<ILogger<BlogService>>();
        var mockStorage = new Mock<IMediaStorageService>();
        var validator = new BlogMediaFileValidator();

        var service = new BlogService(db, mockAudit.Object, mockStorage.Object, validator, mockLogger.Object);
        return (service, db, mockAudit, mockStorage, validator);
    }

    private async Task SeedDataAsync(DentalClinicDbContext db)
    {
        var category1 = new BlogCategory
        {
            CategoryId = 1,
            Name = "Niềng răng - Chỉnh nha",
            Slug = "nieng-rang-chinh-nha",
            CreatedAt = DateTime.UtcNow
        };
        var category2 = new BlogCategory
        {
            CategoryId = 2,
            Name = "Cấy ghép Implant",
            Slug = "cay-ghep-implant",
            CreatedAt = DateTime.UtcNow
        };
        db.BlogCategories.AddRange(category1, category2);

        var dentist1 = new UserAccount
        {
            UserId = 10,
            Email = "dentist1@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var dentistStaff1 = new StaffProfile
        {
            UserId = 10,
            EmployeeCode = "DENT-010",
            FullName = "Nguyễn Văn A",
            EmploymentStatus = "Active"
        };

        var dentist2 = new UserAccount
        {
            UserId = 20,
            Email = "dentist2@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var dentistStaff2 = new StaffProfile
        {
            UserId = 20,
            EmployeeCode = "DENT-020",
            FullName = "Trần Thị B",
            EmploymentStatus = "Active"
        };

        var admin = new UserAccount
        {
            UserId = 99,
            Email = "admin@dentalcare.com",
            Role = UserRole.SystemAdministrator,
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.UserAccounts.AddRange(dentist1, dentist2, admin);
        db.StaffProfiles.AddRange(dentistStaff1, dentistStaff2);

        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("Niềng răng có đau không?", "nieng-rang-co-dau-khong")]
    [InlineData("Cấy ghép Implant 2026: Chi phí & Quy trình!", "cay-ghep-implant-2026-chi-phi-quy-trinh")]
    [InlineData("Đặc điểm của răng khôn mọc lệch", "dac-diem-cua-rang-khon-moc-lech")]
    [InlineData("   Trắng sáng tự nhiên với bọc răng sứ   ", "trang-sang-tu-nhien-voi-boc-rang-su")]
    public void GenerateSlug_VietnameseDiacritics_GeneratesCorrectSlug(string input, string expected)
    {
        var slug = SlugHelper.GenerateSlug(input);
        Assert.Equal(expected, slug);
    }

    [Fact]
    public async Task CreatePost_DuplicateTitle_GeneratesUniqueSlugWithCounter()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var request1 = new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Niềng răng có đau không?",
            Content = "<p>Nội dung bài 1</p>"
        };
        var res1 = await service.CreatePostAsync(10, request1);
        Assert.True(res1.Success);
        Assert.Equal("nieng-rang-co-dau-khong", res1.Data!.Slug);

        var request2 = new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Niềng răng có đau không?",
            Content = "<p>Nội dung bài 2</p>"
        };
        var res2 = await service.CreatePostAsync(10, request2);
        Assert.True(res2.Success);
        Assert.Equal("nieng-rang-co-dau-khong-2", res2.Data!.Slug);
    }

    [Fact]
    public async Task GetPublicPosts_OnlyReturnsPublishedPosts()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        // Add 4 posts with different statuses
        db.BlogPosts.AddRange(
            new BlogPost { CategoryId = 1, AuthorUserId = 10, Title = "Post 1", Slug = "post-1", Content = "content", Status = BlogPostStatus.Draft },
            new BlogPost { CategoryId = 1, AuthorUserId = 10, Title = "Post 2", Slug = "post-2", Content = "content", Status = BlogPostStatus.PendingReview },
            new BlogPost { CategoryId = 1, AuthorUserId = 10, Title = "Post 3", Slug = "post-3", Content = "content", Status = BlogPostStatus.Published, PublishedAt = DateTime.UtcNow },
            new BlogPost { CategoryId = 1, AuthorUserId = 10, Title = "Post 4", Slug = "post-4", Content = "content", Status = BlogPostStatus.Archived }
        );
        await db.SaveChangesAsync();

        var result = await service.GetPublicPostsAsync(new BlogFilterRequest());

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("post-3", result.Data.Items[0].Slug);
        Assert.Equal(BlogPostStatus.Published, result.Data.Items[0].Status);
    }

    [Fact]
    public async Task GetPostBySlug_PublishedPost_IncrementsViewCount()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var post = new BlogPost
        {
            CategoryId = 1,
            AuthorUserId = 10,
            Title = "Cách chăm sóc răng sau niềng",
            Slug = "cach-cham-soc-rang-sau-nieng",
            Content = "<p>Chi tiết chăm sóc răng</p>",
            Status = BlogPostStatus.Published,
            ViewCount = 10,
            PublishedAt = DateTime.UtcNow
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var result = await service.GetPostBySlugAsync("cach-cham-soc-rang-sau-nieng", incrementView: true);

        Assert.True(result.Success);
        Assert.Equal(11, result.Data!.ViewCount);

        var savedPost = await db.BlogPosts.FirstAsync(p => p.Slug == "cach-cham-soc-rang-sau-nieng");
        Assert.Equal(11, savedPost.ViewCount);
    }

    [Fact]
    public async Task GetPostBySlug_DraftOrArchived_ReturnsFail()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var post = new BlogPost
        {
            CategoryId = 1,
            AuthorUserId = 10,
            Title = "Nháp bài viết",
            Slug = "nhap-bai-viet",
            Content = "<p>Bản nháp</p>",
            Status = BlogPostStatus.Draft
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var result = await service.GetPostBySlugAsync("nhap-bai-viet");
        Assert.False(result.Success);
        Assert.Contains("không tồn tại hoặc chưa được xuất bản", result.Message);
    }

    [Fact]
    public async Task CreatePost_SanitizesHtmlContentAgainstXss()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var dangerousContent = "<p>Nội dung an toàn</p><script>alert('xss attack!')</script><img src='https://example.com/pic.jpg' onerror='alert(1)'>";
        var request = new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Bài viết kiểm tra bảo mật XSS",
            Content = dangerousContent
        };

        var result = await service.CreatePostAsync(10, request);

        Assert.True(result.Success);
        Assert.DoesNotContain("<script>", result.Data!.Content);
        Assert.DoesNotContain("alert('xss attack!')", result.Data.Content);
        Assert.DoesNotContain("onerror", result.Data.Content);
        Assert.Contains("<p>Nội dung an toàn</p>", result.Data.Content);
    }

    [Fact]
    public async Task UpdatePost_AuthorCanUpdateOwnPost()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var post = new BlogPost
        {
            CategoryId = 1,
            AuthorUserId = 10,
            Title = "Tiêu đề ban đầu",
            Slug = "tieu-de-ban-dau",
            Content = "<p>Nội dung ban đầu</p>",
            Status = BlogPostStatus.Draft
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var updateRequest = new UpdateBlogPostRequest
        {
            CategoryId = 2,
            Title = "Tiêu đề đã chỉnh sửa",
            Content = "<p>Nội dung mới</p>",
            Status = BlogPostStatus.PendingReview
        };

        var result = await service.UpdatePostAsync(post.BlogPostId, 10, isAdmin: false, updateRequest);

        Assert.True(result.Success);
        Assert.Equal("Tiêu đề đã chỉnh sửa", result.Data!.Title);
        Assert.Equal(2, result.Data.CategoryId);
        Assert.Equal(BlogPostStatus.PendingReview, result.Data.Status);
        Assert.Equal("tieu-de-da-chinh-sua", result.Data.Slug);
    }

    [Fact]
    public async Task UpdatePost_OtherDentistCannotUpdate_ReturnsForbiddenFail()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var post = new BlogPost
        {
            CategoryId = 1,
            AuthorUserId = 10, // Created by Dentist 1 (10)
            Title = "Bài của bác sĩ 1",
            Slug = "bai-cua-bac-si-1",
            Content = "<p>Nội dung</p>",
            Status = BlogPostStatus.Draft
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var updateRequest = new UpdateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Cố tình sửa bởi bác sĩ 2",
            Content = "<p>Hacked</p>"
        };

        // Dentist 2 (20) attempts update
        var result = await service.UpdatePostAsync(post.BlogPostId, 20, isAdmin: false, updateRequest);

        Assert.False(result.Success);
        Assert.Contains("Bạn không có quyền chỉnh sửa bài viết này", result.Message);
    }

    [Fact]
    public async Task UpdatePost_AdminCanUpdateAnyPost()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var post = new BlogPost
        {
            CategoryId = 1,
            AuthorUserId = 10, // Created by Dentist 1
            Title = "Bài viết gốc",
            Slug = "bai-viet-goc",
            Content = "<p>Nội dung</p>",
            Status = BlogPostStatus.Draft
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var updateRequest = new UpdateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Admin chỉnh sửa bài viết",
            Content = "<p>Admin updated</p>",
            Status = BlogPostStatus.Published
        };

        // Admin (99) updates post
        var result = await service.UpdatePostAsync(post.BlogPostId, 99, isAdmin: true, updateRequest);

        Assert.True(result.Success);
        Assert.Equal("Admin chỉnh sửa bài viết", result.Data!.Title);
        Assert.Equal(BlogPostStatus.Published, result.Data.Status);
    }

    [Fact]
    public async Task DeletePost_OtherDentistCannotDelete_ReturnsForbiddenFail()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var post = new BlogPost
        {
            CategoryId = 1,
            AuthorUserId = 10,
            Title = "Bài cần xóa",
            Slug = "bai-can-xoa",
            Content = "<p>Nội dung</p>",
            Status = BlogPostStatus.Draft
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var result = await service.DeletePostAsync(post.BlogPostId, 20, isAdmin: false);

        Assert.False(result.Success);
        Assert.Contains("Bạn không có quyền xóa bài viết này", result.Message);
    }

    [Fact]
    public async Task DeletePost_AuthorCanDeleteOwnPost_AndLogsAudit()
    {
        var (service, db, mockAudit) = CreateService();
        await SeedDataAsync(db);

        var post = new BlogPost
        {
            CategoryId = 1,
            AuthorUserId = 10,
            Title = "Bài cần xóa của tác giả",
            Slug = "bai-can-xoa-cua-tac-gia",
            Content = "<p>Nội dung</p>",
            Status = BlogPostStatus.Draft
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var result = await service.DeletePostAsync(post.BlogPostId, 10, isAdmin: false);

        Assert.True(result.Success);
        Assert.Null(await db.BlogPosts.FirstOrDefaultAsync(p => p.BlogPostId == post.BlogPostId));

        mockAudit.Verify(a => a.LogAsync(
            10,
            "DeleteBlogPost",
            "BlogPost",
            post.BlogPostId.ToString(),
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewPost_AdminApproves_SetsPublishedAndPublishedAtAndLogsAudit()
    {
        var (service, db, mockAudit) = CreateService();
        await SeedDataAsync(db);

        var post = new BlogPost
        {
            CategoryId = 1,
            AuthorUserId = 10,
            Title = "Bài chờ duyệt",
            Slug = "bai-cho-duyet",
            Content = "<p>Nội dung chờ duyệt</p>",
            Status = BlogPostStatus.PendingReview
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var reviewRequest = new ReviewBlogPostRequest
        {
            Status = BlogPostStatus.Published,
            IsFeatured = true,
            ReviewNotes = "Bài viết chất lượng, duyệt ngay"
        };

        var result = await service.ReviewPostAsync(post.BlogPostId, 99, reviewRequest);

        Assert.True(result.Success);
        Assert.Equal(BlogPostStatus.Published, result.Data!.Status);
        Assert.True(result.Data.IsFeatured);
        Assert.NotNull(result.Data.PublishedAt);

        mockAudit.Verify(a => a.LogAsync(
            99,
            "ReviewBlogPost",
            "BlogPost",
            post.BlogPostId.ToString(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCategory_NewCategory_GeneratesSlugAndLogsAudit()
    {
        var (service, db, mockAudit) = CreateService();
        await SeedDataAsync(db);

        var request = new CreateBlogCategoryRequest
        {
            Name = "Nha khoa Trẻ em"
        };

        var result = await service.CreateCategoryAsync(99, request);

        Assert.True(result.Success);
        Assert.Equal("nha-khoa-tre-em", result.Data!.Slug);
        Assert.Equal("Nha khoa Trẻ em", result.Data.Name);

        mockAudit.Verify(a => a.LogAsync(
            99,
            "CreateBlogCategory",
            "BlogCategory",
            result.Data.CategoryId.ToString(),
            null,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCategory_CategoryWithPosts_ReturnsFail()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        db.BlogPosts.Add(new BlogPost
        {
            CategoryId = 1,
            AuthorUserId = 10,
            Title = "Bài thuộc danh mục 1",
            Slug = "bai-thuoc-danh-muc-1",
            Content = "<p>content</p>",
            Status = BlogPostStatus.Published
        });
        await db.SaveChangesAsync();

        var result = await service.DeleteCategoryAsync(1, 99);

        Assert.False(result.Success);
        Assert.Contains("Không thể xóa chuyên mục đang có bài viết", result.Message);
    }

    [Fact]
    public async Task DeleteCategory_EmptyCategory_SuccessAndLogsAudit()
    {
        var (service, db, mockAudit) = CreateService();
        await SeedDataAsync(db);

        var result = await service.DeleteCategoryAsync(2, 99);

        Assert.True(result.Success);
        Assert.Null(await db.BlogCategories.FirstOrDefaultAsync(c => c.CategoryId == 2));

        mockAudit.Verify(a => a.LogAsync(
            99,
            "DeleteBlogCategory",
            "BlogCategory",
            "2",
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==========================================
    // SUPPLEMENT TESTS: MAGIC BYTES & MEDIA VALIDATION
    // ==========================================

    [Fact]
    public void ValidateMedia_ValidJpeg_PassesValidation()
    {
        var validator = new BlogMediaFileValidator();
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
        using var stream = new MemoryStream(jpegBytes);

        var result = validator.Validate(stream, "photo.jpg", "image/jpeg", jpegBytes.Length);

        Assert.True(result.IsValid);
        Assert.Equal("Image", result.MediaType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateMedia_ValidPng_PassesValidation()
    {
        var validator = new BlogMediaFileValidator();
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        using var stream = new MemoryStream(pngBytes);

        var result = validator.Validate(stream, "graphic.png", "image/png", pngBytes.Length);

        Assert.True(result.IsValid);
        Assert.Equal("Image", result.MediaType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateMedia_ValidWebp_PassesValidation()
    {
        var validator = new BlogMediaFileValidator();
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        using var stream = new MemoryStream(webpBytes);

        var result = validator.Validate(stream, "image.webp", "image/webp", webpBytes.Length);

        Assert.True(result.IsValid);
        Assert.Equal("Image", result.MediaType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateMedia_ValidMp4_PassesValidation()
    {
        var validator = new BlogMediaFileValidator();
        var mp4Bytes = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32 };
        using var stream = new MemoryStream(mp4Bytes);

        var result = validator.Validate(stream, "clip.mp4", "video/mp4", mp4Bytes.Length);

        Assert.True(result.IsValid);
        Assert.Equal("Video", result.MediaType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateMedia_ValidWebm_PassesValidation()
    {
        var validator = new BlogMediaFileValidator();
        var webmBytes = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0xF7, 0x81 };
        using var stream = new MemoryStream(webmBytes);

        var result = validator.Validate(stream, "intro.webm", "video/webm", webmBytes.Length);

        Assert.True(result.IsValid);
        Assert.Equal("Video", result.MediaType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateMedia_DisguisedExe_RejectedWithClearErrorMessage()
    {
        var validator = new BlogMediaFileValidator();
        // MZ DOS header disguised with .mp4 extension
        var fakeExeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(fakeExeBytes);

        var result = validator.Validate(stream, "malicious.mp4", "video/mp4", fakeExeBytes.Length);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("thực thi", result.ErrorMessage);
    }

    [Fact]
    public void ValidateMedia_DisguisedJpgExe_RejectedWithClearErrorMessage()
    {
        var validator = new BlogMediaFileValidator();
        // MZ DOS header disguised with .jpg extension
        var fakeExeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(fakeExeBytes);

        var result = validator.Validate(stream, "virus.jpg", "image/jpeg", fakeExeBytes.Length);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("thực thi", result.ErrorMessage);
    }

    [Fact]
    public void ValidateMedia_UnsupportedExtension_Rejected()
    {
        var validator = new BlogMediaFileValidator();
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        using var stream = new MemoryStream(bytes);

        var result = validator.Validate(stream, "document.pdf", "application/pdf", bytes.Length);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("không được hỗ trợ", result.ErrorMessage);
    }

    // ==========================================
    // SUPPLEMENT TESTS: SANITIZATION & DOMAIN RESTRICTION
    // ==========================================

    [Fact]
    public async Task SanitizeHtml_UntrustedDomainImage_StripsImageTag()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var request = new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Kiểm tra XSS Hình Ảnh Độc Hại",
            Content = "<p>Nội dung an toàn <img src=\"https://evil.com/malicious.png\" alt=\"evil\" /></p>",
            Status = "Published"
        };

        var result = await service.CreatePostAsync(10, request);

        Assert.True(result.Success);
        // The evil image should be removed because its domain is not res.cloudinary.com
        Assert.DoesNotContain("evil.com", result.Data!.Content);
        Assert.DoesNotContain("<img", result.Data.Content);
        Assert.Contains("Nội dung an toàn", result.Data.Content);
    }

    [Fact]
    public async Task SanitizeHtml_CloudinaryImage_PreservesImageTag()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var request = new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Kiểm tra Ảnh Hợp Lệ Cloudinary",
            Content = "<p>Ảnh răng đẹp: <img src=\"https://res.cloudinary.com/dental-demo/image/upload/v123/smile.jpg\" alt=\"smile\" /></p>",
            Status = "Published"
        };

        var result = await service.CreatePostAsync(10, request);

        Assert.True(result.Success);
        Assert.Contains("https://res.cloudinary.com/dental-demo/image/upload/v123/smile.jpg", result.Data!.Content);
        Assert.Contains("<img", result.Data.Content);
    }

    [Fact]
    public async Task SanitizeHtml_IframeAndScripts_AreCompletelyRemoved()
    {
        var (service, db, _) = CreateService();
        await SeedDataAsync(db);

        var request = new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Kiểm tra Chặn Iframe và Script",
            Content = "<p>Bài viết</p><iframe src=\"https://evil.com/embed\"></iframe><script>alert(1)</script><img src=\"data:image/png;base64,abc\" onerror=\"alert(2)\" />",
            Status = "Published"
        };

        var result = await service.CreatePostAsync(10, request);

        Assert.True(result.Success);
        Assert.DoesNotContain("<iframe", result.Data!.Content);
        Assert.DoesNotContain("<script", result.Data.Content);
        Assert.DoesNotContain("onerror", result.Data.Content);
    }

    // ==========================================
    // SUPPLEMENT TESTS: MEDIA UPLOAD & DELETION
    // ==========================================

    [Fact]
    public async Task UploadMedia_ValidImage_SucceedsAndLogsAudit()
    {
        var (service, db, mockAudit, mockStorage, _) = CreateFullService();
        await SeedDataAsync(db);

        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
        using var stream = new MemoryStream(jpegBytes);

        mockStorage.Setup(s => s.UploadImageAsync(
            It.IsAny<Stream>(),
            "tooth.jpg",
            "image/jpeg",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaUploadResult
            {
                Url = "https://res.cloudinary.com/demo/image/upload/tooth_123.jpg",
                PublicId = "tooth_123",
                MediaType = "Image",
                ContentType = "image/jpeg",
                SizeBytes = jpegBytes.Length
            });

        var result = await service.UploadMediaAsync(10, stream, "tooth.jpg", "image/jpeg", declaredMediaType: "Image");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Image", result.Data.MediaType);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/tooth_123.jpg", result.Data.Url);

        var savedMedia = await db.BlogPostMedia.FirstOrDefaultAsync(m => m.PublicId == "tooth_123");
        Assert.NotNull(savedMedia);
        Assert.Equal(10, savedMedia.UploadedByUserId);
        Assert.Equal("tooth_123", savedMedia.PublicId);

        mockAudit.Verify(a => a.LogAsync(
            10,
            "UploadBlogMedia",
            "BlogPostMedia",
            savedMedia.MediaId.ToString(),
            null,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadMedia_DisguisedExe_RejectedBeforeStorage()
    {
        var (service, db, _, mockStorage, _) = CreateFullService();
        await SeedDataAsync(db);

        var fakeExeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(fakeExeBytes);

        var result = await service.UploadMediaAsync(10, stream, "teaser.mp4", "video/mp4", declaredMediaType: "Video");

        Assert.False(result.Success);
        Assert.Contains("thực thi", result.Message);
        mockStorage.Verify(s => s.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteMedia_AuthorUser_DeletesFromStorageAndDb()
    {
        var (service, db, mockAudit, mockStorage, _) = CreateFullService();
        await SeedDataAsync(db);

        var media = new BlogPostMedia
        {
            MediaId = 100,
            UploadedByUserId = 10,
            MediaType = "Image",
            FileUrl = "https://res.cloudinary.com/demo/image/upload/pic1.jpg",
            PublicId = "pic1",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            CreatedAt = DateTime.UtcNow
        };
        db.BlogPostMedia.Add(media);
        await db.SaveChangesAsync();

        mockStorage.Setup(s => s.DeleteMediaAsync("pic1", "Image", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await service.DeleteMediaAsync(100, userId: 10, isAdmin: false);

        Assert.True(result.Success);
        Assert.Null(await db.BlogPostMedia.FirstOrDefaultAsync(m => m.MediaId == 100));
        mockStorage.Verify(s => s.DeleteMediaAsync("pic1", "Image", It.IsAny<CancellationToken>()), Times.Once);

        mockAudit.Verify(a => a.LogAsync(
            10,
            "DeleteBlogMedia",
            "BlogPostMedia",
            "100",
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMedia_OtherNonAdminUser_ForbiddenFail()
    {
        var (service, db, _, mockStorage, _) = CreateFullService();
        await SeedDataAsync(db);

        var media = new BlogPostMedia
        {
            MediaId = 101,
            UploadedByUserId = 10, // Uploaded by user 10
            MediaType = "Image",
            FileUrl = "https://res.cloudinary.com/demo/image/upload/pic2.jpg",
            PublicId = "pic2",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            CreatedAt = DateTime.UtcNow
        };
        db.BlogPostMedia.Add(media);
        await db.SaveChangesAsync();

        // User 20 tries to delete user 10's media
        var result = await service.DeleteMediaAsync(101, userId: 20, isAdmin: false);

        Assert.False(result.Success);
        Assert.Contains("Bạn không có quyền xóa", result.Message);
        Assert.NotNull(await db.BlogPostMedia.FirstOrDefaultAsync(m => m.MediaId == 101));
        mockStorage.Verify(s => s.DeleteMediaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupOrphanMedia_DeletesOrphansOlderThan24Hours()
    {
        var (service, db, _, mockStorage, _) = CreateFullService();
        await SeedDataAsync(db);

        // Orphan 1: > 24h old, BlogPostId == null -> SHOULD be deleted
        var oldOrphan = new BlogPostMedia
        {
            MediaId = 201,
            BlogPostId = null,
            UploadedByUserId = 10,
            MediaType = "Image",
            FileUrl = "https://res.cloudinary.com/demo/image/upload/old_orphan.jpg",
            PublicId = "old_orphan",
            ContentType = "image/jpeg",
            SizeBytes = 500,
            CreatedAt = DateTime.UtcNow.AddHours(-30)
        };

        // Orphan 2: fresh (< 24h old), BlogPostId == null -> SHOULD NOT be deleted
        var freshOrphan = new BlogPostMedia
        {
            MediaId = 202,
            BlogPostId = null,
            UploadedByUserId = 10,
            MediaType = "Image",
            FileUrl = "https://res.cloudinary.com/demo/image/upload/fresh_orphan.jpg",
            PublicId = "fresh_orphan",
            ContentType = "image/jpeg",
            SizeBytes = 500,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };

        // Attached media: > 24h old, but attached to a post -> SHOULD NOT be deleted
        var attached = new BlogPostMedia
        {
            MediaId = 203,
            BlogPostId = 1,
            UploadedByUserId = 10,
            MediaType = "Image",
            FileUrl = "https://res.cloudinary.com/demo/image/upload/attached.jpg",
            PublicId = "attached",
            ContentType = "image/jpeg",
            SizeBytes = 500,
            CreatedAt = DateTime.UtcNow.AddHours(-30)
        };

        db.BlogPostMedia.AddRange(oldOrphan, freshOrphan, attached);
        await db.SaveChangesAsync();

        mockStorage.Setup(s => s.DeleteMediaAsync("old_orphan", "Image", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await service.CleanupOrphanMediaAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.Data);
        Assert.Null(await db.BlogPostMedia.FirstOrDefaultAsync(m => m.MediaId == 201));
        Assert.NotNull(await db.BlogPostMedia.FirstOrDefaultAsync(m => m.MediaId == 202));
        Assert.NotNull(await db.BlogPostMedia.FirstOrDefaultAsync(m => m.MediaId == 203));
    }
}
