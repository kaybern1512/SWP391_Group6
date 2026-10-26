using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

public class BlogCategoryDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int PostCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBlogCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
}

public class UpdateBlogCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
}

public class BlogPostListItemDto
{
    public long BlogPostId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public long AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string? AuthorRole { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class BlogPostDto : BlogPostListItemDto
{
    public string Content { get; set; } = string.Empty;
    public List<BlogPostListItemDto> RelatedPosts { get; set; } = new();
}

public class BlogPostPagedResultDto
{
    public List<BlogPostListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public class CreateBlogPostRequest
{
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string Status { get; set; } = "Draft";
}

public class UpdateBlogPostRequest
{
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Status { get; set; }
}

public class ReviewBlogPostRequest
{
    public string Status { get; set; } = string.Empty;
    public bool? IsFeatured { get; set; }
    public string? ReviewNotes { get; set; }
}

public class BlogFilterRequest
{
    public string? CategorySlug { get; set; }
    public int? CategoryId { get; set; }
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public long? AuthorUserId { get; set; }
    public bool? IsFeatured { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}
