using System.Text.Json;
using System.Text.RegularExpressions;

using EI_Console.Application.Pipeline;

using Microsoft.Extensions.Logging;

namespace EI_Console.Infrastructure.Pipeline;

public sealed class TemplateResolver : ITemplateResolver
{
    private static readonly Regex Pattern =
        new(@"\{\{\s*([^}]+?)\s*\}\}", RegexOptions.Compiled);

    private readonly ILogger<TemplateResolver> _log;

    public TemplateResolver(ILogger<TemplateResolver> log) => _log = log;

    public string Resolve(string? template, PipelineContext context)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        if (!template.Contains("{{")) return template;

        return Pattern.Replace(template, match =>
        {
            var path = match.Groups[1].Value;

            JsonElement? el;
            try { el = context.Resolve(path); }
            catch { el = null; }

            if (el is null)
            {
                _log.LogWarning(
                    "Template placeholder could not be resolved: {Path}",
                    path);
                return string.Empty;
            }

            return el.Value.ValueKind switch
            {
                JsonValueKind.String => el.Value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Undefined => string.Empty,
                _ => el.Value.GetRawText(),
            };
        });
    }
}