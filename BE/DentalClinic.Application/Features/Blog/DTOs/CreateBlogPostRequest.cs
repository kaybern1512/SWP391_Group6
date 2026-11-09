namespace DentalClinic.Application.Features.Blog.DTOs;

public class CreateBlogPostRequest
{
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? VideoUrl { get; set; }
    /// <summary>
    /// Initial status: "Draft" or "PendingReview" (Admin may set "Published")
    /// </summary>
    public string Status { get; set; } = "Draft";
    public string PostType { get; set; } = "Blog"; // Blog, ClinicNews, Announcement
}
