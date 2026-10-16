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
    public sealed class NotifyExecutor : ITaskExecutor
    {
        private readonly IConnectionRepository _connections;
        private readonly ICredentialResolver _credentials;
        private readonly IHttpClientFactory _http;
        private readonly ITemplateResolver _templates;

        public NotifyExecutor(
            IConnectionRepository connections,
            ICredentialResolver credentials,
            IHttpClientFactory http,
            ITemplateResolver templates)
        {
            _connections = connections;
            _credentials = credentials;
            _http = http;
            _templates = templates;
        }

        public TaskType Handles => TaskType.Notify;

        public async Task<TaskExecutionOutcome> ExecuteAsync(
            JobTask task, PipelineContext context, CancellationToken ct)
        {
            var cfg = JsonSerializer.Deserialize<TaskConfigLite>(task.ConfigurationJson ?? "{}");

            if (!string.IsNullOrWhiteSpace(cfg?.KafkaTopic))
                return TaskExecutionOutcome.Fail(
                    "Kafka-based Notify is not supported in v0.1. Use a webhook URL.");

            if (string.IsNullOrWhiteSpace(cfg?.Url))
                return TaskExecutionOutcome.Fail("Notify has no webhook URL configured.");

            var rawUrl = cfg!.Url!;
            var mergedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (task.ConnectionId is int connId)
            {
                var conn = await _connections.GetWithAuthProfileAsync(connId, ct);
                if (conn is not null)
                {
                    rawUrl = string.IsNullOrWhiteSpace(cfg.Url)
                        ? conn.BaseUrl
                        : conn.BaseUrl.TrimEnd('/') + "/" + cfg.Url!.TrimStart('/');

                    var authHeaders = await _credentials.ResolveHeadersAsync(conn.AuthProfile, ct);
                    foreach (var h in authHeaders)
                        mergedHeaders[h.Key] = h.Value;
                }
            }

            if (cfg.Headers is { Count: > 0 })
            {
                foreach (var h in cfg.Headers)
                {
                    if (string.IsNullOrWhiteSpace(h.Key)) continue;
                    mergedHeaders[h.Key] = _templates.Resolve(h.Value, context);
                }
            }

            var url = _templates.Resolve(rawUrl, context);

            var bodyJson = context.Current.HasValue
                ? JsonSerializer.Serialize(context.Current.Value)
                : "{}";

            using var client = _http.CreateClient("EiExecutor");
            client.Timeout = TimeSpan.FromSeconds(task.TimeoutSeconds ?? 15);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
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

                return TaskExecutionOutcome.Ok(context.Current, (int)resp.StatusCode);
            }
            catch (TaskCanceledException)
            {
                return TaskExecutionOutcome.Fail("Notify timed out.");
            }
            catch (HttpRequestException ex)
            {
                return TaskExecutionOutcome.Fail($"Notify request failed: {ex.Message}");
            }
        }

        private static string Truncate(string v, int max) =>
            v.Length <= max ? v : v[..max] + "…";

        private sealed class TaskConfigLite
        {
            public string? Url { get; set; }
            public string? KafkaTopic { get; set; }
            public List<HeaderDto>? Headers { get; set; }
        }
    }
}