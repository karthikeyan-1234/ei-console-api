using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;
using EI_Console.Shared.Application;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Application.Pipeline
{
    public sealed class PipelineRunner
    {
        private readonly IExecutionRepository _executions;
        private readonly IJobRepository _jobs;
        private readonly ITaskExecutionLogRepository _logs;
        private readonly ITaskExecutorFactory _executors;
        private readonly IRealtimePublisher _realtime;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<PipelineRunner> _log;

        public PipelineRunner(
            IExecutionRepository executions,
            IJobRepository jobs,
            ITaskExecutionLogRepository logs,
            ITaskExecutorFactory executors,
            IRealtimePublisher realtime,
            IUnitOfWork uow,
            ILogger<PipelineRunner> log)
        {
            _executions = executions;
            _jobs = jobs;
            _logs = logs;
            _executors = executors;
            _realtime = realtime;
            _uow = uow;
            _log = log;
        }

        public async Task RunAsync(Guid executionId, string? payloadJson, CancellationToken ct)
        {
            var exec = await _executions.GetByIdAsync(executionId, ct);
            if (exec is null)
            {
                _log.LogWarning("Execution {ExecutionId} not found — aborting", executionId);
                return;
            }

            if (exec.Status != ExecutionStatus.Running)
            {
                _log.LogWarning("Execution {ExecutionId} is already {Status} — aborting",
                    executionId, exec.Status);
                return;
            }

            var job = await _jobs.GetWithPipelineAsync(exec.JobId, ct);
            if (job is null)
            {
                await FailExecutionAsync(exec, "Job not found", ct);
                return;
            }

            var version = job.Versions.FirstOrDefault(v => v.VersionId == exec.VersionId)
                       ?? job.Versions.OrderByDescending(v => v.VersionNumber).First();

            var rootTasks = version.Tasks
                .Where(t => t.ParentTaskId is null)
                .OrderBy(t => t.SequenceOrder)
                .ToList();

            var context = new PipelineContext();
            var orderIndex = 0;
            var succeeded = 0;
            var failed = 0;
            string? terminalError = null;

            if (!string.IsNullOrWhiteSpace(payloadJson))
            {
                try
                {
                    var doc = JsonDocument.Parse(payloadJson);
                    context.SetCurrent(doc.RootElement.Clone());
                }
                catch (JsonException ex)
                {
                    _log.LogWarning(ex, "Could not parse payload for execution {ExecutionId}", executionId);
                }
            }

            foreach (var task in rootTasks)
            {
                var ok = await RunTaskAsync(exec, task, context, orderIndex++, ct);
                if (ok) succeeded++;
                else
                {
                    failed++;
                    terminalError = $"Task '{task.TaskName}' failed.";
                    break; // sequential pipeline stops at first failure
                }
            }

            exec.SuccessCount = succeeded;
            exec.FailureCount = failed;
            exec.ItemCount = rootTasks.Count;

            if (failed == 0)
            {
                exec.Status = ExecutionStatus.Completed;
                exec.CompletedAt = DateTimeOffset.UtcNow;
                exec.ErrorSummary = null;
            }
            else
            {
                exec.Status = ExecutionStatus.Failed;
                exec.CompletedAt = DateTimeOffset.UtcNow;
                exec.ErrorSummary = terminalError;
            }

            await _uow.SaveChangesAsync(ct);

            await _realtime.ExecutionCompletedAsync(
                exec.ExecutionId, job.TenantId, exec.Status.ToString(),
                exec.SuccessCount, exec.FailureCount, ct);

            _log.LogInformation(
                "Execution {ExecutionId} finished with status {Status} ({Ok} ok, {Fail} failed)",
                executionId, exec.Status, succeeded, failed);
        }

        /// <summary>
        /// Runs one task, logs the attempt, and returns true on success.
        /// Handles iterator fan-out: if the task is a Transform with iterate=true,
        /// each child sub-task runs once per array element.
        /// </summary>
        private async Task<bool> RunTaskAsync(
            JobExecution exec,
            JobTask task,
            PipelineContext context,
            int orderIndex,
            CancellationToken ct)
        {
            var executor = _executors.Resolve(task.TaskType);
            if (executor is null)
            {
                await WriteLogAsync(exec, task, orderIndex, TaskExecutionStatus.Failed,
                    context.Snapshot(), null, $"No executor for task type {task.TaskType}",
                    attempt: 1, started: DateTimeOffset.UtcNow, completed: DateTimeOffset.UtcNow, ct);
                return false;
            }

            var started = DateTimeOffset.UtcNow;
            var inputSnapshot = context.Snapshot();

            try
            {
                var outcome = await executor.ExecuteAsync(task, context, ct);
                var completed = DateTimeOffset.UtcNow;

                if (!outcome.Success)
                {
                    await WriteLogAsync(exec, task, orderIndex, TaskExecutionStatus.Failed,
                        inputSnapshot, null, outcome.ErrorMessage,
                        attempt: 1, started, completed, ct);
                    return false;
                }

                var outputJson = outcome.Output.HasValue
                    ? JsonSerializer.Serialize(outcome.Output.Value)
                    : null;

                // Store output under the task's output key if it has one
                if (!string.IsNullOrWhiteSpace(task.OutputKey) && outcome.Output.HasValue)
                    context.Store(task.OutputKey!, outcome.Output.Value);

                // Store it as the flowing value
                context.SetCurrent(outcome.Output);

                await WriteLogAsync(exec, task, orderIndex, TaskExecutionStatus.Success,
                    inputSnapshot, outputJson, null,
                    attempt: 1, started, completed, ct);

                await _realtime.TaskCompletedAsync(
                    exec.ExecutionId, (int)exec.JobId, task.TaskId, task.TaskName,
                    "Success", (int)(completed - started).TotalMilliseconds, ct);

                // Iterator fan-out
                if (task.TaskType == TaskType.Transform &&
                    task.Iterator is not null &&
                    outcome.Output is { ValueKind: JsonValueKind.Array } arr)
                {
                    var subs = task.ChildTasks
                        .Where(c => c.TaskRole == TaskRole.IteratorSubtask)
                        .OrderBy(c => c.SequenceOrder)
                        .ToList();

                    var index = 0;
                    foreach (var element in arr.EnumerateArray())
                    {
                        var elementContext = new PipelineContext();
                        elementContext.SetCurrent(element);

                        var subIndex = 0;
                        foreach (var sub in subs)
                        {
                            var subOk = await RunTaskAsync(exec, sub, elementContext,
                                orderIndex + 1 + subIndex, ct);
                            if (!subOk) return false;
                            subIndex++;
                        }
                        index++;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Task {TaskId} threw during execution", task.TaskId);
                await WriteLogAsync(exec, task, orderIndex, TaskExecutionStatus.Failed,
                    inputSnapshot, null, ex.Message,
                    attempt: 1, started, DateTimeOffset.UtcNow, ct);
                return false;
            }
        }

        private async Task WriteLogAsync(
            JobExecution exec, JobTask task, int orderIndex, TaskExecutionStatus status,
            string? input, string? output, string? error, int attempt,
            DateTimeOffset started, DateTimeOffset completed, CancellationToken ct)
        {
            var entry = new TaskExecutionLog
            {
                ExecutionId = exec.ExecutionId,
                TaskId = task.TaskId,
                OrderIndex = orderIndex,
                Status = status,
                AttemptNumber = attempt,
                InputPayload = input,
                OutputPayload = output,
                OutputSummary = output is null ? null : Truncate(output, 500),
                ErrorDetails = error,
                StartedAt = started,
                CompletedAt = completed,
            };

            _logs.Add(entry);
            await _uow.SaveChangesAsync(ct);
        }

        private async Task FailExecutionAsync(JobExecution exec, string reason, CancellationToken ct)
        {
            exec.Status = ExecutionStatus.Failed;
            exec.CompletedAt = DateTimeOffset.UtcNow;
            exec.ErrorSummary = reason;
            await _uow.SaveChangesAsync(ct);
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max] + "…";
    }
}
