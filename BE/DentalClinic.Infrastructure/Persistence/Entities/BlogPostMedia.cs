using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class BlogPostMedia
{
    public long MediaId { get; set; }

    public long? BlogPostId { get; set; }

    public long UploadedByUserId { get; set; }

    public string MediaType { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public string PublicId { get; set; } = null!;

    public string? ThumbnailUrl { get; set; }

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public int? DurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual BlogPost? BlogPost { get; set; }

    public virtual UserAccount UploadedByUser { get; set; } = null!;
}
