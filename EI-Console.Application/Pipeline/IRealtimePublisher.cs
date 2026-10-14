using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Pipeline
{
    public interface IRealtimePublisher
    {
        Task ExecutionStartedAsync(Guid executionId, int tenantId, string jobName, CancellationToken ct);
        Task TaskCompletedAsync(Guid executionId, int tenantId, int taskId, string taskName, string status, int durationMs, CancellationToken ct);
        Task ExecutionCompletedAsync(Guid executionId, int tenantId, string status, int successCount, int failureCount, CancellationToken ct);
    }
}
