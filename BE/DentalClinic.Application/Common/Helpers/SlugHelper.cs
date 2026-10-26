using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DentalClinic.Application.Common.Helpers;

public static class SlugHelper
{
    private static readonly Regex NonAlphanumericRegex = new(@"[^a-z0-9\s-]", RegexOptions.Compiled);
    private static readonly Regex MultipleHyphensRegex = new(@"[\s-]+", RegexOptions.Compiled);

    public static string GenerateSlug(string? text, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Replace Vietnamese 'đ' / 'Đ' which are not decomposed by FormD
        var normalized = text.Trim()
            .Replace("đ", "d")
            .Replace("Đ", "d");

        // Decompose diacritics
        var formD = normalized.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var ch in formD)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        var formC = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        // Remove non-alphanumeric characters except space and hyphen
        var clean = NonAlphanumericRegex.Replace(formC, " ");

        // Replace multiple spaces/hyphens with a single hyphen
        var slug = MultipleHyphensRegex.Replace(clean, "-").Trim('-');

        if (slug.Length > maxLength)
        {
            slug = slug.Substring(0, maxLength).Trim('-');
        }

        return string.IsNullOrEmpty(slug) ? "bai-viet" : slug;
    }
}
