namespace DentalClinic.Application.Features.Blog.DTOs;

public class UpdateBlogPostRequest
{
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Status { get; set; }
}
