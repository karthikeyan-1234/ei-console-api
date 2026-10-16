using _04_EI_Console.Shared.Application.Contracts;
using _04_EI_Console.Shared.Application.Contracts.Jobs;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Application.Pipeline.Executors
{
    public sealed class ApiPullExecutor : ITaskExecutor
    {
        private readonly IConnectionRepository _connections;
        private readonly ICredentialResolver _credentials;
        private readonly IHttpClientFactory _http;
        private readonly IJsonataEvaluator _jsonata;
        private readonly ITemplateResolver _templates;

        private readonly IApiRequestBodyBuilder _bodyBuilder;


        public ApiPullExecutor(
            IConnectionRepository connections,
            ICredentialResolver credentials,
            IHttpClientFactory http,
            IJsonataEvaluator jsonata,
            ITemplateResolver templates,
            IApiRequestBodyBuilder bodyBuilder)
        {
            _connections = connections;
            _credentials = credentials;
            _http = http;
            _jsonata = jsonata;
            _templates = templates;
            _bodyBuilder = bodyBuilder;
        }

        public TaskType Handles => TaskType.ApiPull;

        public async Task<TaskExecutionOutcome> ExecuteAsync(
            JobTask task, PipelineContext context, CancellationToken ct)
        {
            if (task.ConnectionId is not int connId)
                return TaskExecutionOutcome.Fail("ApiPull task has no connection.");

            var conn = await _connections.GetWithAuthProfileAsync(connId, ct);
            if (conn is null)
                return TaskExecutionOutcome.Fail($"Connection {connId} not found.");

            var cfg = JsonSerializer.Deserialize<TaskConfigLite>(task.ConfigurationJson ?? "{}");

            var rawUrl = string.IsNullOrWhiteSpace(cfg?.Url)
                ? conn.BaseUrl
                : CombineUrl(conn.BaseUrl, cfg!.Url!);

            // Resolve any {{...}} placeholders in the URL against the
            // pipeline context — upstream outputs or the current value.
            var url = _templates.Resolve(rawUrl, context);

            var method = string.IsNullOrWhiteSpace(cfg?.Method)
                ? "GET"
                : cfg!.Method!.ToUpperInvariant();

            // Merge connection-level auth headers with task-level headers.
            // On a key collision, the task's header wins (case-insensitive).
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

            using var client = _http.CreateClient("EiExecutor");
            client.Timeout = TimeSpan.FromSeconds(task.TimeoutSeconds ?? conn.TimeoutSeconds);

            // Build the request body via the shared builder. Returns null for None
            // (which is the default and also correct for GET/DELETE without a body).
            var content = await _bodyBuilder.BuildAsync(
                bodyKind: cfg?.BodyKind,
                bodyJsonata: cfg?.Body,
                rawContentType: cfg?.RawContentType,
                formFields: cfg?.FormFields,
                context: context,
                ct: ct);

            using var req = new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = content,
            };
            foreach (var h in mergedHeaders)
                req.Headers.TryAddWithoutValidation(h.Key, h.Value);

            try
            {
                using var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                    return TaskExecutionOutcome.Fail(
                        $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(body, 300)}",
                        (int)resp.StatusCode);

                try
                {
                    var json = JsonDocument.Parse(body);
                    return TaskExecutionOutcome.Ok(
                        json.RootElement.Clone(),
                        (int)resp.StatusCode);
                }
                catch (JsonException)
                {
                    var wrapper = JsonSerializer.SerializeToElement(body);
                    return TaskExecutionOutcome.Ok(wrapper, (int)resp.StatusCode);
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
            public List<HeaderDto>? Headers { get; set; }

            // NEW — same as ApiPush
            public string? Body { get; set; }
            public string? BodyKind { get; set; }
            public string? RawContentType { get; set; }
            public List<FormFieldDto>? FormFields { get; set; }
        }
    }
}