using System;
using System.Collections.Generic;
using System.Text;

namespace _04_EI_Console.Shared.Application.Contracts
{
    public sealed record ScatterGatherItemDto(
        long ItemId,
        Guid ExecutionId,
        int TaskId,
        string TaskName,            // new
        int ParentTaskId,
        int ItemIndex,
        string? ItemKey,
        string Status,
        int? HttpStatusCode,
        int AttemptCount,
        string? InputPayload,
        string? OutputPayload,
        string? ErrorMessage,
        DateTimeOffset? DispatchedAt,
        DateTimeOffset? CompletedAt,
        long? DurationMs);          // new
}
