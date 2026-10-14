using System;
using System.Collections.Generic;
using System.Text;

namespace _04_EI_Console.Shared.Application.Contracts
{
    public sealed record ExecutionSummaryDto(
       string Id,
       string JobId,
       string Job,
       string TenantId,
       string Status,
       string Started,
       int ItemsDone,
       int ItemsTotal,
       int Failures,
       int ElapsedSec,
       string? CorrelationId,
       string TriggerSource,
       string? CoreEvent,
       string? WorkerInstance,
       int JobVersion,
       string? IdempotencyKey,
       bool HasBranches);

    public sealed record ExecutionDetailDto(
        string Id,
        string JobId,
        string Job,
        string TenantId,
        string Status,
        string Started,
        int ItemsDone,
        int ItemsTotal,
        int Failures,
        int ElapsedSec,
        string? CorrelationId,
        string TriggerSource,
        string? CoreEvent,
        string? WorkerInstance,
        int JobVersion,
        string? IdempotencyKey,
        bool HasBranches,
        IReadOnlyList<TaskLogEntryDto> TaskLogs);

    public sealed record TaskLogEntryDto(
        string ExecutionId,
        string TaskId,
        string? ParentTaskId,
        int OrderIndex,
        string Name,
        string Type,
        string Status,
        int Attempt,
        string Started,
        string Completed,
        int DurationMs,
        string Summary);

    public sealed record TriggerJobResponse(string ExecutionId, string StatusUrl);

    public sealed record WebhookAcceptedResponse(string ExecutionId, string StatusUrl);
}
