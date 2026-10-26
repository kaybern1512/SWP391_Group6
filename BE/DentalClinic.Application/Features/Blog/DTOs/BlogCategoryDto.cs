using System;

namespace DentalClinic.Application.Features.Blog.DTOs;

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
