using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Helpers;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
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
        var db = CreateDbContext(dbName ?? Guid.NewGuid().ToString());
        var mockAudit = new Mock<IAuditLogService>();
        var mockLogger = new Mock<ILogger<BlogService>>();

        var service = new BlogService(db, mockAudit.Object, mockLogger.Object);
        return (service, db, mockAudit);
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
}
