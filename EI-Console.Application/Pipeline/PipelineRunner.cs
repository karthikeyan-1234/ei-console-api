using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;
using EI_Console.Shared.Application;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private readonly IConnectionRepository _connections;
        private readonly SemaphoreSlim _dbLock = new(1, 1);
        private readonly IScatterGatherItemRepository _scatter;
        private readonly IServiceScopeFactory _scopeFactory;

        public PipelineRunner(
            IExecutionRepository executions,
            IJobRepository jobs,
            ITaskExecutionLogRepository logs,
            IConnectionRepository connections,
            IScatterGatherItemRepository scatter,
            ITaskExecutorFactory executors,
            IRealtimePublisher realtime,
            IUnitOfWork uow,
            IServiceScopeFactory scopeFactory,
            ILogger<PipelineRunner> log)
        {
            _executions = executions;
            _jobs = jobs;
            _logs = logs;
            _executors = executors;
            _realtime = realtime;
            _uow = uow;
            _log = log;
            _connections = connections;
            _scatter = scatter;
            _scopeFactory = scopeFactory;
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

            // Resolve connection protocol when the task has a connection.
            Protocol? protocol = null;
            if (task.ConnectionId is int connId)
            {
                var conn = await _connections.GetByIdAsync(connId, ct);
                protocol = conn?.Protocol;
            }

            var executor = ResolveExecutor(task, protocol);
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
                // Iterator fan-out — bounded concurrency.
                if (task.TaskType == TaskType.Transform &&
                    task.Iterator is not null &&
                    outcome.Output is { ValueKind: JsonValueKind.Array } arr)
                {
                    var cfg = JsonSerializer.Deserialize<IteratorConfigLite>(task.ConfigurationJson ?? "{}");

                    var subs = task.ChildTasks
                        .Where(c => c.TaskRole == TaskRole.IteratorSubtask)
                        .OrderBy(c => c.SequenceOrder)
                        .ToList();

                    var items = arr.EnumerateArray().ToList();
                    if (items.Count == 0) return true;

                    // Defaults: process every item, one at a time. Keeps the pre-2A behaviour
                    // exactly when the fields are absent.
                    var batchSize = cfg?.BatchSize is int bs and > 0 ? bs : int.MaxValue;
                    var maxConcurrency = cfg?.MaxConcurrency is int mc and > 0 ? mc : 1;

                    // Thread-safe order index for log entries. Interlocked over a shared box
                    // because captured locals are compiled to display-class fields; StrongBox
                    // makes the intent explicit and prevents accidental stack-local aliasing.
                    var orderBox = new StrongBox<int>(orderIndex + 1);

                    using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
                    var anyItemFailed = false;

                    for (var batchStart = 0; batchStart < items.Count; batchStart += batchSize)
                    {
                        ct.ThrowIfCancellationRequested();

                        var batch = items
                            .Skip(batchStart)
                            .Take(batchSize)
                            .ToList();

                        var itemTasks = batch.Select(async (item, localIndex) =>
                        {
                            await gate.WaitAsync(ct);
                            try
                            {
                                var globalIndex = batchStart + localIndex;
                                var inputJson = item.GetRawText();
                                var itemOk = true;

                                foreach (var sub in subs)
                                {
                                    var subOrder = Interlocked.Increment(ref orderBox.Value);

                                    // Fresh scope per (item, subtask). Each scope owns its own
                                    // DbContext, so parallel items don't trample each other's
                                    // change tracker.
                                    await using var scope = _scopeFactory.CreateAsyncScope();
                                    var innerRunner = scope.ServiceProvider
                                        .GetRequiredService<PipelineRunner>();

                                    var elementContext = new PipelineContext();
                                    elementContext.SetCurrent(item);

                                    var subOk = await innerRunner.RunItemSubtaskAsync(
                                        exec.ExecutionId,
                                        sub,
                                        elementContext,
                                        subOrder,
                                        inputJson,
                                        globalIndex,
                                        task.TaskId,
                                        ct);

                                    if (!subOk) itemOk = false;
                                }

                                return itemOk;
                            }
                            finally
                            {
                                gate.Release();
                            }
                        }).ToList();

                        var batchResults = await Task.WhenAll(itemTasks);
                        if (batchResults.Any(ok => !ok)) anyItemFailed = true;
                    }

                    if (anyItemFailed)
                    {
                        _log.LogWarning(
                            "Iterator on task {TaskId} had at least one failing item; execution continues",
                            task.TaskId);
                        return false;
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


        /// <summary>
        /// Runs one (item, subtask) pair in its own DI scope. Invoked by the
        /// iterator loop when maxConcurrency > 1 — each parallel item gets its own
        /// DbContext so EF Core's single-threaded constraint isn't violated.
        ///
        /// Writes a scatter_gather_items row before and after the subtask runs.
        /// </summary>
        public async Task<bool> RunItemSubtaskAsync(
            Guid executionId,
            JobTask sub,
            PipelineContext context,
            int orderIndex,
            string inputJson,
            int itemIndex,
            int parentTaskId,
            CancellationToken ct)
        {
            var scatterItem = new ScatterGatherItem
            {
                ExecutionId = executionId,
                TaskId = sub.TaskId,
                ParentTaskId = parentTaskId,
                ItemIndex = itemIndex,
                ItemKey = null,
                Status = ScatterItemStatus.Dispatched,
                AttemptCount = 1,
                InputPayload = inputJson,
                DispatchedAt = DateTimeOffset.UtcNow,
            };

            _scatter.Add(scatterItem);
            await _uow.SaveChangesAsync(ct);

            var exec = await _executions.GetByIdAsync(executionId, ct)
                ?? throw new InvalidOperationException($"Execution {executionId} not found.");

            var ok = await RunTaskAsync(exec, sub, context, orderIndex, ct);

            scatterItem.Status = ok ? ScatterItemStatus.Completed : ScatterItemStatus.Failed;
            scatterItem.CompletedAt = DateTimeOffset.UtcNow;
            scatterItem.OutputPayload = context.Current?.GetRawText();
            scatterItem.ErrorMessage = ok ? null : $"Subtask '{sub.TaskName}' failed.";
            await _uow.SaveChangesAsync(ct);

            return ok;
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

            // EF Core's DbContext is not thread-safe. When the iterator runs items
            // in parallel, two tasks can reach this point simultaneously. Serialize
            // the Add+Save pair so the change tracker and the DbContext stay coherent.
            await _dbLock.WaitAsync(ct);
            try
            {
                _logs.Add(entry);
                await _uow.SaveChangesAsync(ct);
            }
            finally
            {
                _dbLock.Release();
            }
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

        private ITaskExecutor? ResolveExecutor(JobTask task, Protocol? connectionProtocol)
        {
            return _executors.ResolveForConnection(task.TaskType, connectionProtocol);
        }

        private sealed class IteratorConfigLite
        {
            public int? BatchSize { get; set; }
            public int? MaxConcurrency { get; set; }
        }

        /// <summary>
        /// Best-effort extraction of an identifying key from an iterated item.
        /// Prefers common conventions ("id", "key", "code"), falls back to null.
        /// Purely informational for the console; nothing downstream depends on it.
        /// </summary>
        private static string? ExtractItemKey(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object) return null;

            foreach (var name in new[] { "id", "key", "code", "name" })
            {
                if (item.TryGetProperty(name, out var v) &&
                    (v.ValueKind == JsonValueKind.String || v.ValueKind == JsonValueKind.Number))
                {
                    return v.ToString();
                }
            }
            return null;
        }
    }
}
