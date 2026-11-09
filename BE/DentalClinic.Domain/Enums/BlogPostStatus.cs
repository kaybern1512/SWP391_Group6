using System.Linq;

namespace DentalClinic.Domain.Enums;

public static class BlogPostStatus
{
    public const string Draft = "Draft";
    public const string PendingReview = "PendingReview";
    public const string Published = "Published";
    public const string Archived = "Archived";
    public const string Rejected = "Rejected";

    public static readonly string[] All = { Draft, PendingReview, Published, Archived, Rejected };

    public static bool IsValid(string? status) => !string.IsNullOrWhiteSpace(status) && All.Contains(status);
}
