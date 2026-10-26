namespace DentalClinic.Application.Features.Blog.DTOs;

public class ReviewBlogPostRequest
{
    /// <summary>
    /// Target review status: "Published", "Archived", or "Draft" (reject / return for edit)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    public bool? IsFeatured { get; set; }

    public string? ReviewNotes { get; set; }
}
