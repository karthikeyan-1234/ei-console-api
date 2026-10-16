using _04_EI_Console.Shared.Application.Contracts;
using _04_EI_Console.Shared.Application.Contracts.Jobs;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using Microsoft.Extensions.Logging;

using System.Text.Json;

namespace EI_Console.Application.Pipeline.Executors
{
    public sealed class ApiPushExecutor : ITaskExecutor
    {
        private readonly IConnectionRepository _connections;
        private readonly ICredentialResolver _credentials;
        private readonly IApiRequestBodyBuilder _bodyBuilder;
        private readonly IHttpClientFactory _http;
        private readonly ITemplateResolver _templates;
        private readonly ILogger<ApiPushExecutor> _log;

        public ApiPushExecutor(
            IConnectionRepository connections,
            ICredentialResolver credentials,
            IApiRequestBodyBuilder bodyBuilder,
            IHttpClientFactory http,
            ITemplateResolver templates,
            ILogger<ApiPushExecutor> log)
        {
            _connections = connections;
            _credentials = credentials;
            _bodyBuilder = bodyBuilder;
            _http = http;
            _templates = templates;
            _log = log;
        }

        public TaskType Handles => TaskType.ApiPush;

        public async Task<TaskExecutionOutcome> ExecuteAsync(
            JobTask task, PipelineContext context, CancellationToken ct)
        {
            if (task.ConnectionId is not int connId)
                return TaskExecutionOutcome.Fail("ApiPush task has no connection.");

            var conn = await _connections.GetWithAuthProfileAsync(connId, ct);
            if (conn is null)
                return TaskExecutionOutcome.Fail($"Connection {connId} not found.");

            var cfg = JsonSerializer.Deserialize<TaskConfigLite>(task.ConfigurationJson ?? "{}");

            var rawUrl = string.IsNullOrWhiteSpace(cfg?.Url)
                ? conn.BaseUrl
                : CombineUrl(conn.BaseUrl, cfg!.Url!);

            var url = _templates.Resolve(rawUrl, context);

            var method = string.IsNullOrWhiteSpace(cfg?.Method)
                ? "POST"
                : cfg!.Method!.ToUpperInvariant();

            var authHeaders = await _credentials.ResolveHeadersAsync(conn.AuthProfile, ct);
            var mergedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var h in authHeaders)
                mergedHeaders[h.Key] = h.Value;

            if (cfg?.Headers is { Count: > 0 })
            {
                foreach (var h in cfg.Headers)
                {
                    if (string.IsNullOrWhiteSpace(h.Key)) continue;
                    mergedHeaders[h.Key] = _templates.Resolve(h.Value, context);
                }
            }

            var content = await _bodyBuilder.BuildAsync(
                bodyKind: cfg?.BodyKind,
                bodyJsonata: cfg?.Body,
                rawContentType: cfg?.RawContentType,
                formFields: cfg?.FormFields,
                context: context,
                ct: ct);

            using var client = _http.CreateClient("EiExecutor");
            client.Timeout = TimeSpan.FromSeconds(task.TimeoutSeconds ?? conn.TimeoutSeconds);

            using var req = new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = content,
            };

            foreach (var h in mergedHeaders)
                req.Headers.TryAddWithoutValidation(h.Key, h.Value);

            try
            {
                using var resp = await client.SendAsync(req, ct);
                var responseBody = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning(
                        "ApiPush HTTP {Status} for {Method} {Url} — body: {Body}",
                        (int)resp.StatusCode, method, url, Truncate(responseBody, 500));

                    return TaskExecutionOutcome.Fail(
                        $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(responseBody, 300)}",
                        (int)resp.StatusCode);
                }

                try
                {
                    var json = JsonDocument.Parse(responseBody);
                    return TaskExecutionOutcome.Ok(
                        json.RootElement.Clone(),
                        (int)resp.StatusCode);
                }
                catch (JsonException)
                {
                    return TaskExecutionOutcome.Ok(
                        JsonSerializer.SerializeToElement(responseBody),
                        (int)resp.StatusCode);
                }
            }
            catch (TaskCanceledException)
            {
                return TaskExecutionOutcome.Fail("Request timed out.");
            }
            catch (HttpRequestException ex)
            {
                return TaskExecutionOutcome.Fail($"HTTP request failed: {ex.Message}");
            }
        }

        private static string CombineUrl(string baseUrl, string path) =>
            baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');

        private static string Truncate(string v, int max) =>
            v.Length <= max ? v : v[..max] + "…";

        private sealed class TaskConfigLite
        {
            public string? Url { get; set; }
            public string? Method { get; set; }
            public string? Body { get; set; }
            public string? BodyKind { get; set; }
            public string? RawContentType { get; set; }
            public List<FormFieldDto>? FormFields { get; set; }
            public List<HeaderDto>? Headers { get; set; }
        }
    }
}