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

        public ApiPullExecutor(
            IConnectionRepository connections,
            ICredentialResolver credentials,
            IHttpClientFactory http,
            IJsonataEvaluator jsonata)
        {
            _connections = connections;
            _credentials = credentials;
            _http = http;
            _jsonata = jsonata;
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

            // Config carries URL and method
            var cfg = JsonSerializer.Deserialize<TaskConfigLite>(task.ConfigurationJson ?? "{}");
            var url = string.IsNullOrWhiteSpace(cfg?.Url)
                ? conn.BaseUrl
                : CombineUrl(conn.BaseUrl, cfg!.Url!);

            var method = string.IsNullOrWhiteSpace(cfg?.Method) ? "GET" : cfg!.Method!.ToUpperInvariant();

            var headers = await _credentials.ResolveHeadersAsync(conn.AuthProfile, ct);

            using var client = _http.CreateClient("EiExecutor");
            client.Timeout = TimeSpan.FromSeconds(task.TimeoutSeconds ?? conn.TimeoutSeconds);

            using var req = new HttpRequestMessage(new HttpMethod(method), url);
            foreach (var h in headers)
                req.Headers.TryAddWithoutValidation(h.Key, h.Value);

            try
            {
                using var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                    return TaskExecutionOutcome.Fail(
                        $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(body, 300)}");

                // Try to parse the response as JSON. If it isn't, wrap it as a JSON string.
                try
                {
                    var json = JsonDocument.Parse(body);
                    return TaskExecutionOutcome.Ok(json.RootElement.Clone());
                }
                catch (JsonException)
                {
                    var wrapper = JsonSerializer.SerializeToElement(body);
                    return TaskExecutionOutcome.Ok(wrapper);
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
        }
    }
}
