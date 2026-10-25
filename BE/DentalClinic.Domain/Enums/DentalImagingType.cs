using System;
using System.Linq;

namespace DentalClinic.Domain.Enums;

/// <summary>
/// Defines the standardized clinical classifications for dental medical imaging.
/// Supported categories: Panorama, Periapical, Cephalometric, Intraoral.
/// </summary>
public static class DentalImagingType
{
    public const string Panorama = "Panorama";
    public const string Periapical = "Periapical";
    public const string Cephalometric = "Cephalometric";
    public const string Intraoral = "Intraoral";

    public static readonly string[] All =
    {
        Panorama,
        Periapical,
        Cephalometric,
        Intraoral
    };

    /// <summary>
    /// Checks whether the given imaging type string matches one of the four supported categories.
    /// Case-insensitive.
    /// </summary>
    public static bool IsValid(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        return All.Any(c => string.Equals(c, type.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Normalizes the input type string to the standard canonical casing if valid; otherwise returns null.
    /// </summary>
    public static string? Normalize(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        return All.FirstOrDefault(c => string.Equals(c, type.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns a human-readable display name in Vietnamese for UI and clinical reports.
    /// </summary>
    public static string GetDisplayName(string? type)
    {
        var normalized = Normalize(type);
        return normalized switch
        {
            Panorama => "Chụp toàn cảnh (Panorama)",
            Periapical => "Chụp cận chóp (Periapical)",
            Cephalometric => "Chụp sọ nghiêng (Cephalometric)",
            Intraoral => "Ảnh chụp trong miệng (Intraoral)",
            _ => "Không xác định"
        };
    }
}
