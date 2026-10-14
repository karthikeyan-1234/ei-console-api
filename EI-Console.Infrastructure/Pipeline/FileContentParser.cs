using EI_Console.Application.Interfaces.Infrastructure;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Infrastructure.Pipeline
{
    public sealed class FileContentParser : IFileContentParser
    {
        public JsonElement Parse(string content, string formatHint, string remotePath)
        {
            var format = ResolveFormat(formatHint, remotePath, content);

            return format switch
            {
                "Json" => ParseJson(content),
                "Csv" => ParseCsv(content),
                "Text" => JsonSerializer.SerializeToElement(content),
                _ => JsonSerializer.SerializeToElement(content),
            };
        }

        // -----------------------------------------------------------------
        // Format detection
        // -----------------------------------------------------------------

        private static string ResolveFormat(string hint, string path, string content)
        {
            if (!string.IsNullOrWhiteSpace(hint) &&
                !string.Equals(hint, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeFormat(hint);
            }

            // Try extension first.
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var extFormat = ext switch
            {
                ".json" => "Json",
                ".csv" => "Csv",
                ".txt" or ".log" or ".text" => "Text",
                _ => null,
            };
            if (extFormat is not null) return extFormat;

            // Fallback: sniff the content.
            var trimmed = content.TrimStart();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                // If it parses as JSON, treat it as JSON.
                try { using var _ = JsonDocument.Parse(content); return "Json"; }
                catch (JsonException) { /* fall through */ }
            }

            // Simple CSV sniff: first line has commas, more than one line.
            var firstLine = content.Split('\n', 2)[0];
            if (firstLine.Contains(',') && content.Contains('\n'))
                return "Csv";

            return "Text";
        }

        private static string NormalizeFormat(string hint) =>
            hint.Trim().ToLowerInvariant() switch
            {
                "json" => "Json",
                "csv" => "Csv",
                "text" => "Text",
                _ => "Text",
            };

        // -----------------------------------------------------------------
        // Parsers
        // -----------------------------------------------------------------

        private static JsonElement ParseJson(string content)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Malformed JSON — return it as a plain string rather than failing.
                return JsonSerializer.SerializeToElement(content);
            }
        }

        private static JsonElement ParseCsv(string content)
        {
            var lines = content.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return JsonSerializer.SerializeToElement(Array.Empty<object>());

            var headers = SplitCsvLine(lines[0]);
            var rows = new List<Dictionary<string, string?>>();

            for (var i = 1; i < lines.Length; i++)
            {
                var values = SplitCsvLine(lines[i]);
                var row = new Dictionary<string, string?>(headers.Length);
                for (var j = 0; j < headers.Length; j++)
                {
                    row[headers[j]] = j < values.Length ? values[j] : null;
                }
                rows.Add(row);
            }

            return JsonSerializer.SerializeToElement(rows);
        }

        /// <summary>
        /// Minimal CSV line splitter. Handles quoted values with embedded commas.
        /// Does not handle embedded newlines inside quotes — acceptable for v0.2.
        /// </summary>
        private static string[] SplitCsvLine(string line)
        {
            var values = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    // Doubled quotes inside a quoted field produce a literal quote.
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString().Trim());
            return values.ToArray();
        }
    }
}
