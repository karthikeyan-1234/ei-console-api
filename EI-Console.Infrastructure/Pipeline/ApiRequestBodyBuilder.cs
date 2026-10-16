using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Pipeline;

using Microsoft.Extensions.Logging;

namespace EI_Console.Infrastructure.Pipeline;

public sealed class ApiRequestBodyBuilder : IApiRequestBodyBuilder
{
    private const string ExecutorHttpClientName = "EiExecutor";

    private readonly IJsonataEvaluator _jsonata;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ApiRequestBodyBuilder> _log;

    public ApiRequestBodyBuilder(
        IJsonataEvaluator jsonata,
        IHttpClientFactory httpFactory,
        ILogger<ApiRequestBodyBuilder> log)
    {
        _jsonata = jsonata;
        _httpFactory = httpFactory;
        _log = log;
    }

    public async Task<HttpContent?> BuildAsync(
        string? bodyKind,
        string? bodyJsonata,
        string? rawContentType,
        IReadOnlyList<FormFieldDto>? formFields,
        PipelineContext context,
        CancellationToken ct)
    {
        var kind = (bodyKind ?? "Json").Trim();

        switch (kind)
        {
            case "None":
                return null;

            case "Raw":
                {
                    var value = await EvaluateValueAsync(bodyJsonata, context, ct);
                    var contentType = string.IsNullOrWhiteSpace(rawContentType)
                        ? "text/plain"
                        : rawContentType!;
                    return new StringContent(value, Encoding.UTF8, contentType);
                }

            case "UrlEncoded":
                {
                    var pairs = new List<KeyValuePair<string, string>>();
                    foreach (var field in formFields ?? Array.Empty<FormFieldDto>())
                    {
                        if (string.Equals(field.ValueType, "File", StringComparison.OrdinalIgnoreCase))
                        {
                            _log.LogWarning(
                                "UrlEncoded does not support file parts — skipping field {Key}",
                                field.Key);
                            continue;
                        }
                        var value = await EvaluateValueAsync(field.Value, context, ct);
                        pairs.Add(new KeyValuePair<string, string>(field.Key, value));
                    }
                    return new FormUrlEncodedContent(pairs);
                }

            case "FormData":
                {
                    var multipart = new MultipartFormDataContent();
                    foreach (var field in formFields ?? Array.Empty<FormFieldDto>())
                    {
                        if (string.Equals(field.ValueType, "File", StringComparison.OrdinalIgnoreCase))
                        {
                            await AddFilePartAsync(multipart, field, context, ct);
                        }
                        else
                        {
                            var value = await EvaluateValueAsync(field.Value, context, ct);
                            multipart.Add(new StringContent(value, Encoding.UTF8), field.Key);
                        }
                    }
                    return multipart;
                }

            case "Json":
            default:
                {
                    var value = await EvaluateJsonBodyAsync(bodyJsonata, context, ct);
                    return new StringContent(value, Encoding.UTF8, "application/json");
                }
        }
    }

    // -----------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------

    /// <summary>
    /// JSON body — matches the original ApiPushExecutor behavior:
    ///   empty       → the current pipeline item, or "{}"
    ///   no '$'      → pass through as literal JSON
    ///   has '$'     → evaluate as JSONata, return the result's raw JSON
    /// </summary>
    private async Task<string> EvaluateJsonBodyAsync(
        string? expression, PipelineContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            var current = context.Current;
            return current.HasValue ? current.Value.GetRawText() : "{}";
        }

        if (!expression.Contains('$'))
            return expression;

        var result = await _jsonata.EvaluateAsync(expression, context.Current, ct);
        if (result is null) return "null";
        return result.Value.GetRawText();
    }

    /// <summary>
    /// Text value — for Raw bodies and form-field values.
    ///   no '$'      → literal string, sent as-is
    ///   has '$'     → JSONata; strings are unwrapped so `$.name` produces
    ///                 "Alice" rather than "\"Alice\""
    /// </summary>
    private async Task<string> EvaluateValueAsync(
        string? expression, PipelineContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(expression)) return string.Empty;
        if (!expression.Contains('$')) return expression;

        var result = await _jsonata.EvaluateAsync(expression, context.Current, ct);
        if (result is null) return string.Empty;

        var el = result.Value;
        return el.ValueKind == JsonValueKind.String
            ? (el.GetString() ?? string.Empty)
            : el.GetRawText();
    }

    private async Task AddFilePartAsync(
        MultipartFormDataContent multipart,
        FormFieldDto field,
        PipelineContext context,
        CancellationToken ct)
    {
        var url = await EvaluateValueAsync(field.Value, context, ct);
        if (string.IsNullOrWhiteSpace(url))
        {
            _log.LogWarning(
                "File field {Key} produced an empty URL — skipped",
                field.Key);
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _log.LogWarning(
                "File field {Key} produced an invalid URL '{Url}' — skipped",
                field.Key, url);
            return;
        }

        var client = _httpFactory.CreateClient(ExecutorHttpClientName);
        byte[] bytes;
        string? contentTypeFromServer = null;

        try
        {
            using var response = await client.GetAsync(uri, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "File field {Key} — download failed with {Status} from {Url}",
                    field.Key, (int)response.StatusCode, url);
                return;
            }

            contentTypeFromServer = response.Content.Headers.ContentType?.MediaType;
            bytes = await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "File field {Key} — download threw for {Url}",
                field.Key, url);
            return;
        }

        var fileName = !string.IsNullOrWhiteSpace(field.FileName)
            ? field.FileName!
            : System.IO.Path.GetFileName(uri.LocalPath);

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "file";

        var byteContent = new ByteArrayContent(bytes);

        var contentType = !string.IsNullOrWhiteSpace(field.ContentType)
            ? field.ContentType!
            : contentTypeFromServer ?? "application/octet-stream";

        byteContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        multipart.Add(byteContent, field.Key, fileName);

        _log.LogInformation(
            "File field {Key} — attached {Bytes} byte(s) as '{FileName}' ({ContentType})",
            field.Key, bytes.Length, fileName, contentType);
    }
}