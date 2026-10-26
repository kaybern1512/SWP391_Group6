using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers;
using DentalClinic.Web.Controllers.Admin;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Blog;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class BlogWebControllerTests
{
    private readonly Mock<IBlogApiService> _mockBlogApi = new();

    private static TempDataDictionary CreateTempData()
    {
        return new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    // ==========================================
    // PUBLIC BLOG CONTROLLER
    // ==========================================

    [Fact]
    public async Task PublicBlog_Index_ReturnsViewWithModel()
    {
        _mockBlogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(ApiResult<List<BlogCategoryDto>>.Success(new List<BlogCategoryDto>
            {
                new() { CategoryId = 1, Name = "Niềng răng", Slug = "nieng-rang" }
            }));

        _mockBlogApi.Setup(x => x.GetPublicPostsAsync(It.IsAny<BlogFilterRequest>()))
            .ReturnsAsync(ApiResult<BlogPostPagedResultDto>.Success(new BlogPostPagedResultDto
            {
                Items = new List<BlogPostListItemDto>
                {
                    new() { BlogPostId = 1, Title = "Bài 1", Slug = "bai-1", Status = "Published" }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 9
            }));

        var controller = new BlogController(_mockBlogApi.Object);

        var result = await controller.Index(null, null, 1);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<BlogIndexViewModel>().Subject;
        vm.PagedResult.Items.Should().HaveCount(1);
        vm.Categories.Should().HaveCount(1);
    }

    [Fact]
    public async Task PublicBlog_Detail_ValidSlug_ReturnsView()
    {
        _mockBlogApi.Setup(x => x.GetPostBySlugAsync("bai-viet-hay"))
            .ReturnsAsync(ApiResult<BlogPostDto>.Success(new BlogPostDto
            {
                BlogPostId = 1,
                Title = "Bài viết hay",
                Slug = "bai-viet-hay",
                Content = "<p>Nội dung</p>",
                Status = "Published"
            }));

        _mockBlogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(ApiResult<List<BlogCategoryDto>>.Success(new List<BlogCategoryDto>()));

        var controller = new BlogController(_mockBlogApi.Object);

        var result = await controller.Detail("bai-viet-hay");

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<BlogDetailViewModel>().Subject;
        vm.Post.Title.Should().Be("Bài viết hay");
    }

    [Fact]
    public async Task PublicBlog_Detail_InvalidSlug_RedirectsToIndexWithAlert()
    {
        _mockBlogApi.Setup(x => x.GetPostBySlugAsync("khong-ton-tai"))
            .ReturnsAsync(ApiResult<BlogPostDto>.Failure("Không tìm thấy bài viết."));

        var controller = new BlogController(_mockBlogApi.Object)
        {
            TempData = CreateTempData()
        };

        var result = await controller.Detail("khong-ton-tai");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(BlogController.Index));
        controller.TempData["ErrorMessage"].Should().Be("Không tìm thấy bài viết.");
    }

    // ==========================================
    // DENTIST BLOG CONTROLLER
    // ==========================================

    [Fact]
    public async Task DentistBlog_Index_ReturnsViewWithMyPosts()
    {
        _mockBlogApi.Setup(x => x.GetMyPostsAsync(It.IsAny<BlogFilterRequest>()))
            .ReturnsAsync(ApiResult<BlogPostPagedResultDto>.Success(new BlogPostPagedResultDto
            {
                Items = new List<BlogPostListItemDto>
                {
                    new() { BlogPostId = 10, Title = "Bản nháp 1", Status = "Draft" }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 10
            }));

        _mockBlogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(ApiResult<List<BlogCategoryDto>>.Success(new List<BlogCategoryDto>()));

        var controller = new DentistBlogController(_mockBlogApi.Object);

        var result = await controller.Index(null, null, 1);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<DentistBlogIndexViewModel>().Subject;
        vm.PagedResult.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task DentistBlog_Create_ValidModel_RedirectsToIndexWithSuccess()
    {
        _mockBlogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(ApiResult<List<BlogCategoryDto>>.Success(new List<BlogCategoryDto>()));

        _mockBlogApi.Setup(x => x.CreatePostAsync(It.IsAny<CreateBlogPostRequest>()))
            .ReturnsAsync(ApiResult<BlogPostDto>.Success(new BlogPostDto { BlogPostId = 1 }));

        var controller = new DentistBlogController(_mockBlogApi.Object)
        {
            TempData = CreateTempData()
        };

        var model = new DentistBlogEditViewModel
        {
            CategoryId = 1,
            Title = "Bài viết mới của bác sĩ",
            Content = "<p>Nội dung</p>",
            Status = "PendingReview"
        };

        var result = await controller.Create(model, null);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(DentistBlogController.Index));
        controller.TempData["SuccessMessage"].Should().Be("Bài viết đã được gửi duyệt thành công!");
    }

    [Fact]
    public async Task DentistBlog_Delete_CallsApiAndRedirects()
    {
        _mockBlogApi.Setup(x => x.DeletePostAsync(5))
            .ReturnsAsync(ApiResult<bool>.Success(true, "Xóa bài viết thành công."));

        var controller = new DentistBlogController(_mockBlogApi.Object)
        {
            TempData = CreateTempData()
        };

        var result = await controller.Delete(5);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(DentistBlogController.Index));
        controller.TempData["SuccessMessage"].Should().Be("Xóa bài viết thành công!");
    }

    // ==========================================
    // ADMIN BLOG CONTROLLER
    // ==========================================

    [Fact]
    public async Task AdminBlog_Index_ReturnsViewWithAllPosts()
    {
        _mockBlogApi.Setup(x => x.GetAllPostsForAdminAsync(It.IsAny<BlogFilterRequest>()))
            .ReturnsAsync(ApiResult<BlogPostPagedResultDto>.Success(new BlogPostPagedResultDto
            {
                Items = new List<BlogPostListItemDto>
                {
                    new() { BlogPostId = 1, Title = "Bài chờ duyệt", Status = "PendingReview" }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 15
            }));

        _mockBlogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(ApiResult<List<BlogCategoryDto>>.Success(new List<BlogCategoryDto>()));

        var controller = new AdminBlogController(_mockBlogApi.Object);

        var result = await controller.Index(null, null, null, 1);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<AdminBlogIndexViewModel>().Subject;
        vm.PagedResult.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task AdminBlog_Review_ValidRequest_RedirectsWithSuccess()
    {
        _mockBlogApi.Setup(x => x.ReviewPostAsync(1, It.IsAny<ReviewBlogPostRequest>()))
            .ReturnsAsync(ApiResult<BlogPostDto>.Success(new BlogPostDto { BlogPostId = 1, Status = "Published" }, "Đã xuất bản bài viết."));

        var controller = new AdminBlogController(_mockBlogApi.Object)
        {
            TempData = CreateTempData()
        };

        var request = new ReviewBlogPostRequest
        {
            Status = "Published",
            IsFeatured = true,
            ReviewNotes = "Duyệt xuất sắc"
        };

        var result = await controller.Review(1, request);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminBlogController.Index));
        controller.TempData["SuccessMessage"].Should().Be("Đã xuất bản bài viết.");
    }

    [Fact]
    public async Task AdminBlog_CreateCategory_Success_RedirectsWithSuccess()
    {
        _mockBlogApi.Setup(x => x.CreateCategoryAsync(It.IsAny<CreateBlogCategoryRequest>()))
            .ReturnsAsync(ApiResult<BlogCategoryDto>.Success(new BlogCategoryDto { CategoryId = 3, Name = "Nha chu" }));

        var controller = new AdminBlogController(_mockBlogApi.Object)
        {
            TempData = CreateTempData()
        };

        var request = new CreateBlogCategoryRequest { Name = "Nha chu" };

        var result = await controller.CreateCategory(request);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminBlogController.Index));
        controller.TempData["SuccessMessage"].Should().Be("Thêm chuyên mục mới thành công!");
    }
}
