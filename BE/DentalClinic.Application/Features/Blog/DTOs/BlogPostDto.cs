using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.Blog.DTOs;

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
