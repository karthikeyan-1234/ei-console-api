using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Shared
{
    public static class StringExtensions
    {
        /// <summary>
        /// Converts a human-readable string to a URL-safe slug. Lowercase,
        /// hyphenated, ASCII-safe. Matches the slugify function the Angular
        /// console uses so the two stay in sync.
        /// </summary>
        public static string ToSlug(this string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var cleaned = new string(value
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray());

            while (cleaned.Contains("--"))
                cleaned = cleaned.Replace("--", "-");

            return cleaned.Trim('-');
        }

        /// <summary>
        /// Truncates to a maximum length, appending an ellipsis when truncation
        /// occurs. Returns the original string when it already fits.
        /// </summary>
        public static string TruncateWithEllipsis(this string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length <= maxLength) return value;
            return value[..(maxLength - 1)] + "…";
        }
    }
}
