using System.Text.Json;

using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using Microsoft.Extensions.Logging;

namespace EI_Console.Application.Pipeline.Executors
{
    public sealed class ApiPushExecutor : ITaskExecutor
    {
        private readonly IConnectionRepository _connections;
        private readonly ICredentialResolver _credentials;
        private readonly IApiRequestBodyBuilder _bodyBuilder;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<ApiPushExecutor> _log;

        public ApiPushExecutor(
            IConnectionRepository connections,
            ICredentialResolver credentials,
            IApiRequestBodyBuilder bodyBuilder,
            IHttpClientFactory http,
            ILogger<ApiPushExecutor> log)
        {
            _connections = connections;
            _credentials = credentials;
            _bodyBuilder = bodyBuilder;
            _http = http;
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

            var url = string.IsNullOrWhiteSpace(cfg?.Url)
                ? conn.BaseUrl
                : CombineUrl(conn.BaseUrl, cfg!.Url!);

            var method = string.IsNullOrWhiteSpace(cfg?.Method)
                ? "POST"
                : cfg!.Method!.ToUpperInvariant();

            var headers = await _credentials.ResolveHeadersAsync(conn.AuthProfile, ct);

            // Build the body via the shared builder. Returns null for None.
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

            foreach (var h in headers)
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
                        $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(responseBody, 300)}");
                }

                try
                {
                    var json = JsonDocument.Parse(responseBody);
                    return TaskExecutionOutcome.Ok(json.RootElement.Clone());
                }
                catch (JsonException)
                {
                    return TaskExecutionOutcome.Ok(JsonSerializer.SerializeToElement(responseBody));
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
        }
    }
}