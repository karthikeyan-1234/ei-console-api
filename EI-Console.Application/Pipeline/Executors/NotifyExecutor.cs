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

        public NotifyExecutor(
            IConnectionRepository connections,
            ICredentialResolver credentials,
            IHttpClientFactory http)
        {
            _connections = connections;
            _credentials = credentials;
            _http = http;
        }

        public TaskType Handles => TaskType.Notify;

        public async Task<TaskExecutionOutcome> ExecuteAsync(
            JobTask task, PipelineContext context, CancellationToken ct)
        {
            var cfg = JsonSerializer.Deserialize<TaskConfigLite>(task.ConfigurationJson ?? "{}");

            // Notify supports two targets: Kafka topic (deferred in v0.1) or webhook URL.
            if (!string.IsNullOrWhiteSpace(cfg?.KafkaTopic))
                return TaskExecutionOutcome.Fail(
                    "Kafka-based Notify is not supported in v0.1. Use a webhook URL.");

            if (string.IsNullOrWhiteSpace(cfg?.Url))
                return TaskExecutionOutcome.Fail("Notify has no webhook URL configured.");

            var url = cfg!.Url!;
            var headers = new Dictionary<string, string>();

            if (task.ConnectionId is int connId)
            {
                var conn = await _connections.GetWithAuthProfileAsync(connId, ct);
                if (conn is not null)
                {
                    url = string.IsNullOrWhiteSpace(cfg.Url)
                        ? conn.BaseUrl
                        : conn.BaseUrl.TrimEnd('/') + "/" + cfg.Url!.TrimStart('/');

                    var resolved = await _credentials.ResolveHeadersAsync(conn.AuthProfile, ct);
                    foreach (var h in resolved) headers[h.Key] = h.Value;
                }
            }

            var bodyJson = context.Current.HasValue
                ? JsonSerializer.Serialize(context.Current.Value)
                : "{}";

            using var client = _http.CreateClient("EiExecutor");
            client.Timeout = TimeSpan.FromSeconds(task.TimeoutSeconds ?? 15);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            foreach (var h in headers)
                req.Headers.TryAddWithoutValidation(h.Key, h.Value);

            try
            {
                using var resp = await client.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                    return TaskExecutionOutcome.Fail(
                        $"Notify HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");

                return TaskExecutionOutcome.Ok(context.Current);
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

        private sealed class TaskConfigLite
        {
            public string? Url { get; set; }
            public string? KafkaTopic { get; set; }
        }
    }
}
