using EI_Console.Application.Pipeline;

using Microsoft.AspNetCore.SignalR;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Pipeline
{
    public sealed class SignalRRealtimePublisher : IRealtimePublisher
    {
        private readonly IHubContext<Hub> _hub;

        public SignalRRealtimePublisher(IHubContext<Hub> hub) => _hub = hub;

        public Task ExecutionStartedAsync(Guid executionId, int tenantId, string jobName, CancellationToken ct) =>
            _hub.Clients.All.SendAsync("executionStarted",
                new { executionId, tenantId, jobName, at = DateTimeOffset.UtcNow }, ct);

        public Task TaskCompletedAsync(Guid executionId, int tenantId, int taskId, string taskName, string status, int durationMs, CancellationToken ct) =>
            _hub.Clients.All.SendAsync("taskCompleted",
                new { executionId, tenantId, taskId, taskName, status, durationMs, at = DateTimeOffset.UtcNow }, ct);

        public Task ExecutionCompletedAsync(Guid executionId, int tenantId, string status, int successCount, int failureCount, CancellationToken ct) =>
            _hub.Clients.All.SendAsync("executionCompleted",
                new { executionId, tenantId, status, successCount, failureCount, at = DateTimeOffset.UtcNow }, ct);
    }
}
