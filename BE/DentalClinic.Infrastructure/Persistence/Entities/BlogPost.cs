using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class BlogPost
{
    public long BlogPostId { get; set; }

    public int CategoryId { get; set; }

    public long AuthorUserId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Summary { get; set; }

    public string Content { get; set; } = null!;

    public string? ThumbnailUrl { get; set; }

    public string? VideoUrl { get; set; }

    public string? EmbedVideoId { get; set; }

    public int ReadTimeMinutes { get; set; } = 5;

    public bool IsFeatured { get; set; }

    public int ViewCount { get; set; }

    public string PostType { get; set; } = "Blog";

    public string Status { get; set; } = "Draft";

    public string? RejectionReason { get; set; }

    public long? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual BlogCategory Category { get; set; } = null!;

    public virtual UserAccount AuthorUser { get; set; } = null!;

    public virtual UserAccount? ApprovedByUser { get; set; }
}