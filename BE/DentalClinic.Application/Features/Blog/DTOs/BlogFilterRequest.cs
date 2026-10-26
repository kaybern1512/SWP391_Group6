namespace DentalClinic.Application.Features.Blog.DTOs;

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
