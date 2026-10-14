using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Mappings;
    public static class ExecutionMappings
    {
        public static ExecutionSummaryDto ToSummaryDto(this JobExecution e, string jobName, int jobVersion)
        {
            var elapsed = e.CompletedAt.HasValue
                ? (int)(e.CompletedAt.Value - e.StartedAt).TotalSeconds
                : (int)(DateTimeOffset.UtcNow - e.StartedAt).TotalSeconds;

            return new ExecutionSummaryDto(
                Id: e.ExecutionId.ToString(),
                JobId: e.JobId.ToString(),
                Job: jobName,
                TenantId: e.Job?.TenantId.ToString() ?? "—",
                Status: e.Status.ToString(),
                Started: e.StartedAt.Time(),
                ItemsDone: e.SuccessCount,
                ItemsTotal: e.ItemCount,
                Failures: e.FailureCount,
                ElapsedSec: elapsed,
                CorrelationId: e.CorrelationId,
                TriggerSource: e.TriggerSource.ToString(),
                CoreEvent: null,
                WorkerInstance: e.WorkerInstanceId,
                JobVersion: jobVersion,
                IdempotencyKey: e.IdempotencyKey,
                HasBranches: false);
        }

        public static ExecutionDetailDto ToDetailDto(
            this JobExecution e,
            string jobName,
            int jobVersion,
            IReadOnlyList<TaskLogEntryDto> taskLogs)
        {
            var summary = e.ToSummaryDto(jobName, jobVersion);
            return new ExecutionDetailDto(
                Id: summary.Id,
                JobId: summary.JobId,
                Job: summary.Job,
                TenantId: summary.TenantId,
                Status: summary.Status,
                Started: summary.Started,
                ItemsDone: summary.ItemsDone,
                ItemsTotal: summary.ItemsTotal,
                Failures: summary.Failures,
                ElapsedSec: summary.ElapsedSec,
                CorrelationId: summary.CorrelationId,
                TriggerSource: summary.TriggerSource,
                CoreEvent: summary.CoreEvent,
                WorkerInstance: summary.WorkerInstance,
                JobVersion: summary.JobVersion,
                IdempotencyKey: summary.IdempotencyKey,
                HasBranches: summary.HasBranches,
                TaskLogs: taskLogs);
        }

        public static TaskLogEntryDto ToDto(this TaskExecutionLog l, string taskName, string taskType)
        {
            return new TaskLogEntryDto(
                ExecutionId: l.ExecutionId.ToString(),
                TaskId: l.TaskId.ToString(),
                ParentTaskId: l.Task?.ParentTaskId?.ToString(),
                OrderIndex: l.OrderIndex,
                Name: taskName,
                Type: taskType,
                Status: l.Status.ToString(),
                Attempt: l.AttemptNumber,
                Started: l.StartedAt.Time(),
                Completed: l.CompletedAt.Time(),
                DurationMs: (int)(l.DurationMs ?? 0),
                Summary: l.OutputSummary ?? "{}");
        }
    }
