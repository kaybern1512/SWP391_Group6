using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Blog;

public class BlogIndexViewModel
{
    public BlogPostPagedResultDto PagedResult { get; set; } = new();
    public List<BlogCategoryDto> Categories { get; set; } = new();
    public string? CurrentCategorySlug { get; set; }
    public string? CurrentKeyword { get; set; }
    public int CurrentPage { get; set; } = 1;
}

public class BlogDetailViewModel
{
    public BlogPostDto Post { get; set; } = new();
    public List<BlogCategoryDto> Categories { get; set; } = new();
}

public class DentistBlogIndexViewModel
{
    public BlogPostPagedResultDto PagedResult { get; set; } = new();
    public List<BlogCategoryDto> Categories { get; set; } = new();
    public string? CurrentStatus { get; set; }
    public string? CurrentKeyword { get; set; }
    public int CurrentPage { get; set; } = 1;
}

public class DentistBlogEditViewModel
{
    public long? BlogPostId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn chuyên mục.")]
    [Display(Name = "Chuyên mục")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
    [Display(Name = "Tiêu đề bài viết")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Tóm tắt không được vượt quá 1000 ký tự.")]
    [Display(Name = "Tóm tắt ngắn")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung bài viết.")]
    [Display(Name = "Nội dung bài viết")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Ảnh bìa bài viết")]
    public string? ThumbnailUrl { get; set; }

    [Display(Name = "Trạng thái lưu")]
    public string Status { get; set; } = "Draft";

    public List<BlogCategoryDto> Categories { get; set; } = new();
}

public class AdminBlogIndexViewModel
{
    public BlogPostPagedResultDto PagedResult { get; set; } = new();
    public List<BlogCategoryDto> Categories { get; set; } = new();
    public string? CurrentStatus { get; set; }
    public int? CurrentCategoryId { get; set; }
    public string? CurrentKeyword { get; set; }
    public int CurrentPage { get; set; } = 1;
}
