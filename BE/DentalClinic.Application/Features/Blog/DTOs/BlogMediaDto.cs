using System;

namespace DentalClinic.Application.Features.Blog.DTOs;

public class BlogMediaDto
{
    public long MediaId { get; set; }
    public long? BlogPostId { get; set; }
    public long UploadedByUserId { get; set; }
    public string MediaType { get; set; } = "Image";
    public string FileUrl { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UploadBlogMediaResponse
{
    public long MediaId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string MediaType { get; set; } = "Image";
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? DurationSeconds { get; set; }
}

public class UploadBlogMediaRequest
{
    public long? BlogPostId { get; set; }
    public string? MediaType { get; set; }
}
