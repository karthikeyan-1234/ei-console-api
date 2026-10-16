using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;
using EI_Console.Shared.Application;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private readonly IJsonataEvaluator _jsonata;

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
            IJsonataEvaluator jsonata,
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
            _jsonata = jsonata;
        }

        // -----------------------------------------------------------------
        // Entry point
        // -----------------------------------------------------------------

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

            // orderBox drives log ordering across the whole execution. Sequential
            // tasks and fork lanes both draw from it via Interlocked, so log
            // indices stay unique even when lanes run concurrently.
            var orderBox = new StrongBox<int>(0);
            var walkIdx = 0;

            while (walkIdx < rootTasks.Count)
            {
                ct.ThrowIfCancellationRequested();
                var task = rootTasks[walkIdx];

                // ---- Fork: consecutive Branch tasks ----------------------
                if (task.TaskType == TaskType.Branch)
                {
                    var branches = new List<JobTask>();
                    while (walkIdx < rootTasks.Count &&
                           rootTasks[walkIdx].TaskType == TaskType.Branch)
                    {
                        branches.Add(rootTasks[walkIdx]);
                        walkIdx++;
                    }

                    var forkOk = await ExecuteForkAsync(exec, branches, context, orderBox, ct);
                    if (forkOk) succeeded += branches.Count;
                    else
                    {
                        failed += branches.Count;
                        terminalError = $"Fork starting at '{branches[0].TaskName}' failed.";
                        break;
                    }

                    // Optional JoinPoint immediately after the fork
                    if (walkIdx < rootTasks.Count &&
                        rootTasks[walkIdx].TaskType == TaskType.JoinPoint)
                    {
                        var join = rootTasks[walkIdx];
                        walkIdx++;
                        var joinOrder = orderBox.Value;
                        orderBox.Value++;

                        var joinOk = await ExecuteJoinAsync(exec, join, context, joinOrder, ct);
                        if (joinOk) succeeded++;
                        else
                        {
                            failed++;
                            terminalError = $"Await barrier '{join.TaskName}' failed.";
                            break;
                        }
                    }
                    continue;
                }

                // ---- Regular task ---------------------------------------
                var taskOrder = orderBox.Value;
                orderBox.Value++;

                var ok = await RunTaskAsync(exec, task, context, taskOrder, ct);
                walkIdx++;

                if (ok) succeeded++;
                else
                {
                    failed++;
                    terminalError = $"Task '{task.TaskName}' failed.";
                    break;
                }
            }

            // Default: counts reflect the top-level task walk.
            exec.SuccessCount = succeeded;
            exec.FailureCount = failed;
            exec.ItemCount = rootTasks.Count;

            // If the pipeline produced scatter items (any iterator ran), the
            // operator cares about rows processed, not tasks walked. Recompute
            // from the actual item outcomes so a Scatter Test execution shows
            // "100/100", not "2/2".
            var scatterRows = await _scatter.GetByExecutionAsync(exec.ExecutionId, ct);
            if (scatterRows.Count > 0)
            {
                exec.ItemCount = scatterRows.Count;
                exec.SuccessCount = scatterRows.Count(s => s.Status == ScatterItemStatus.Completed);
                exec.FailureCount = scatterRows.Count(s => s.Status == ScatterItemStatus.Failed);
            }

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
                executionId, exec.Status, exec.SuccessCount, exec.FailureCount);
        }

        // -----------------------------------------------------------------
        // Fork — run consecutive Branch tasks as parallel lanes
        // -----------------------------------------------------------------

        private async Task<bool> ExecuteForkAsync(
            JobExecution exec,
            IReadOnlyList<JobTask> branches,
            PipelineContext parentContext,
            StrongBox<int> orderBox,
            CancellationToken ct)
        {
            if (branches.Count == 0) return true;

            var preForkContext = parentContext.Clone();

            // Every lane runs in its own DI scope so parallel lanes don't
            // share a DbContext.
            var laneTasks = branches.Select(async (branch, _) =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var inner = scope.ServiceProvider.GetRequiredService<PipelineRunner>();
                return await inner.ExecuteLaneAsync(
                    exec.ExecutionId, branch, preForkContext, orderBox, ct);
            }).ToList();

            LaneResult[] results;
            try
            {
                results = await Task.WhenAll(laneTasks);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Fork execution threw for execution {ExecutionId}", exec.ExecutionId);
                return false;
            }

            var aggregate = new Dictionary<string, object?>(branches.Count);
            var meta = new Dictionary<string, int>
            {
                ["success"] = 0,
                ["skipped"] = 0,
                ["failed"] = 0,
                ["total"] = branches.Count,
            };

            for (var i = 0; i < branches.Count; i++)
            {
                var branch = branches[i];
                var key = string.IsNullOrWhiteSpace(branch.TaskName)
                    ? $"Branch {i + 1}"
                    : branch.TaskName;

                var r = results[i];
                aggregate[key] = r.Output;

                switch (r.Status)
                {
                    case "Success": meta["success"]++; break;
                    case "Skipped": meta["skipped"]++; break;
                    default: meta["failed"]++; break;
                }
            }
            aggregate["_meta"] = meta;

            parentContext.SetCurrent(JsonSerializer.SerializeToElement(aggregate));

            return results.All(r => r.Success);
        }

        /// <summary>
        /// Runs one branch as a lane: evaluates its condition, then runs its
        /// children sequentially within a private context. Called by
        /// ExecuteForkAsync from within a per-lane DI scope.
        /// </summary>
        public async Task<LaneResult> ExecuteLaneAsync(
            Guid executionId,
            JobTask branch,
            PipelineContext preForkContext,
            StrongBox<int> orderBox,
            CancellationToken ct)
        {
            var exec = await _executions.GetByIdAsync(executionId, ct)
                ?? throw new InvalidOperationException($"Execution {executionId} not found.");

            var started = DateTimeOffset.UtcNow;

            await _realtime.TaskStartedAsync(
                exec.ExecutionId, (int)exec.JobId, branch.TaskId, branch.TaskName, ct);

            var laneOrder = Interlocked.Increment(ref orderBox.Value);

            var cfg = JsonSerializer.Deserialize<BranchConfigLite>(branch.ConfigurationJson ?? "{}");
            var condition = cfg?.Condition;

            _log.LogInformation(
                "Lane '{Branch}' condition='{Condition}'",
                branch.TaskName, condition ?? "(null)");

            // Three paths:
            //   empty               → run (default)
            //   literal "true"      → run
            //   literal "false"     → skip
            //   anything else       → JSONata
            //
            // The literal shortcut exists because jsonata('false') evaluates to an
            // empty object rather than the boolean false. JSONata parses the bare
            // token as a path lookup on the input, not a literal. Bypassing the
            // engine for the two most common cases avoids the pitfall entirely.
            bool shouldRun;

            if (string.IsNullOrWhiteSpace(condition))
            {
                shouldRun = true;
            }
            else if (string.Equals(condition.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                shouldRun = true;
            }
            else if (string.Equals(condition.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            {
                shouldRun = false;
            }
            else
            {
                try
                {
                    var eval = await _jsonata.EvaluateAsync(condition!, preForkContext.Current, ct);

                    _log.LogInformation(
                        "Lane '{Branch}' — jsonata('{Expr}') returned '{Value}' (kind {Kind})",
                        branch.TaskName, condition,
                        eval?.GetRawText() ?? "(null)",
                        eval?.ValueKind.ToString() ?? "(null)");

                    shouldRun = IsTruthy(eval);
                }
                catch (Exception ex)
                {
                    var crashedAt = DateTimeOffset.UtcNow;
                    await WriteLogAsync(exec, branch, laneOrder, TaskExecutionStatus.Failed,
                        preForkContext.Snapshot(), null,
                        $"Condition evaluation failed: {ex.Message}",
                        1, started, crashedAt, ct);

                    // Pair with TaskStartedAsync above — no orphans in runningTaskIds.
                    await _realtime.TaskCompletedAsync(
                        exec.ExecutionId, (int)exec.JobId, branch.TaskId, branch.TaskName,
                        "Failed", (int)(crashedAt - started).TotalMilliseconds, ct);

                    return new LaneResult(false, null, "Failed");
                }
            }

            _log.LogInformation(
                "Lane '{Branch}' — shouldRun={ShouldRun}",
                branch.TaskName, shouldRun);

            if (!shouldRun)
            {
                var skippedAt = DateTimeOffset.UtcNow;
                await WriteLogAsync(exec, branch, laneOrder, TaskExecutionStatus.Skipped,
                    preForkContext.Snapshot(), null,
                    $"Condition '{condition}' was not satisfied",
                    1, started, skippedAt, ct);

                await _realtime.TaskCompletedAsync(
                    exec.ExecutionId, (int)exec.JobId, branch.TaskId, branch.TaskName,
                    "Skipped", (int)(skippedAt - started).TotalMilliseconds, ct);

                return new LaneResult(true, null, "Skipped");
            }

            // ---- Lane runs: children execute in this lane's private context -----
            var laneContext = preForkContext.Clone();

            var children = branch.ChildTasks
                .Where(c => c.TaskRole == TaskRole.ForkChild)
                .OrderBy(c => c.SequenceOrder)
                .ToList();

            var anyFailed = false;
            foreach (var child in children)
            {
                var childOrder = Interlocked.Increment(ref orderBox.Value);
                var ok = await RunTaskAsync(exec, child, laneContext, childOrder, ct);
                if (!ok)
                {
                    anyFailed = true;
                    break;
                }
            }

            var completed = DateTimeOffset.UtcNow;

            await WriteLogAsync(exec, branch, laneOrder,
                anyFailed ? TaskExecutionStatus.Failed : TaskExecutionStatus.Success,
                preForkContext.Snapshot(),
                laneContext.Current?.GetRawText(),
                anyFailed ? $"Lane '{branch.TaskName}' had a failing child" : null,
                1, started, completed, ct);

            // Pair with TaskStartedAsync. Status reflects the lane's actual
            // outcome, not a skip.
            await _realtime.TaskCompletedAsync(
                exec.ExecutionId, (int)exec.JobId, branch.TaskId, branch.TaskName,
                anyFailed ? "Failed" : "Success",
                (int)(completed - started).TotalMilliseconds, ct);

            return new LaneResult(
                !anyFailed,
                laneContext.Current,
                anyFailed ? "Failed" : "Success");
        }

        // -----------------------------------------------------------------
        // JoinPoint — barrier after a fork
        // -----------------------------------------------------------------

        private async Task<bool> ExecuteJoinAsync(
            JobExecution exec,
            JobTask join,
            PipelineContext context,
            int orderIndex,
            CancellationToken ct)
        {
            var started = DateTimeOffset.UtcNow;
            await _realtime.TaskStartedAsync(
                exec.ExecutionId, (int)exec.JobId, join.TaskId, join.TaskName, ct);
            var snapshot = context.Snapshot();

            var cfg = JsonSerializer.Deserialize<JoinConfigLite>(join.ConfigurationJson ?? "{}");
            var mode = cfg?.JoinMode ?? "WaitAll";
            var threshold = cfg?.JoinThreshold ?? 1;

            var current = context.Current;
            if (!current.HasValue || current.Value.ValueKind != JsonValueKind.Object)
            {
                var failedAt = DateTimeOffset.UtcNow;
                await WriteLogAsync(exec, join, orderIndex, TaskExecutionStatus.Failed,
                    snapshot, null, "JoinPoint found no upstream fork output",
                    1, started, failedAt, ct);

                await _realtime.TaskCompletedAsync(
                    exec.ExecutionId, (int)exec.JobId, join.TaskId, join.TaskName,
                    "Failed", (int)(failedAt - started).TotalMilliseconds, ct);

                return false;
            }

            // The fork left a _meta block with per-status counts.
            int success = 0, total = 0, skipped = 0, failedLanes = 0;
            if (current.Value.TryGetProperty("_meta", out var m) &&
                m.ValueKind == JsonValueKind.Object)
            {
                if (m.TryGetProperty("success", out var s) && s.TryGetInt32(out var sv)) success = sv;
                if (m.TryGetProperty("skipped", out var sk) && sk.TryGetInt32(out var skv)) skipped = skv;
                if (m.TryGetProperty("failed", out var f) && f.TryGetInt32(out var fv)) failedLanes = fv;
                if (m.TryGetProperty("total", out var t) && t.TryGetInt32(out var tv)) total = tv;
            }

            bool ok;
            string reason;
            switch (mode)
            {
                case "WaitAny":
                    ok = success >= 1;
                    reason = ok
                        ? $"WaitAny satisfied — {success} lane(s) succeeded"
                        : "WaitAny not satisfied — no lane succeeded";
                    break;

                case "WaitN":
                    ok = success >= threshold;
                    reason = ok
                        ? $"WaitN({threshold}) satisfied — {success} lane(s) succeeded"
                        : $"WaitN({threshold}) not satisfied — {success} of {total} succeeded";
                    break;

                case "WaitAll":
                default:
                    // Skipped lanes are settled by policy — they were never meant to run.
                    // WaitAll requires every lane that was supposed to run to succeed.
                    ok = (success + skipped) == total && failedLanes == 0;
                    reason = ok
                        ? $"WaitAll satisfied — {success}/{total} succeeded, {skipped} skipped"
                        : $"WaitAll not satisfied — {success}/{total} succeeded, " +
                          $"{skipped} skipped, {failedLanes} failed";
                    break;
            }

            var completed = DateTimeOffset.UtcNow;

            await WriteLogAsync(exec, join, orderIndex,
                ok ? TaskExecutionStatus.Success : TaskExecutionStatus.Failed,
                snapshot, current.Value.GetRawText(),
                ok ? null : reason,
                1, started, completed, ct);

            await _realtime.TaskCompletedAsync(
                exec.ExecutionId, (int)exec.JobId, join.TaskId, join.TaskName,
                ok ? "Success" : "Failed",
                (int)(completed - started).TotalMilliseconds, ct);

            return ok;
        }

        // -----------------------------------------------------------------
        // Existing task runner
        // -----------------------------------------------------------------

        private async Task<bool> RunTaskAsync(
            JobExecution exec,
            JobTask task,
            PipelineContext context,
            int orderIndex,
            CancellationToken ct,
            Action<int?>? httpStatusSink = null)
        {
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

            await _realtime.TaskStartedAsync(
                exec.ExecutionId, (int)exec.JobId, task.TaskId, task.TaskName, ct);

            var started = DateTimeOffset.UtcNow;
            var inputSnapshot = context.Snapshot();

            try
            {
                var outcome = await executor.ExecuteAsync(task, context, ct);
                var completed = DateTimeOffset.UtcNow;

                // Hand the HTTP status (when the executor set one) to the caller.
                // Used by the iterator to populate scatter_gather_items.http_status_code.
                httpStatusSink?.Invoke(outcome.HttpStatusCode);

                if (!outcome.Success)
                {
                    await WriteLogAsync(exec, task, orderIndex, TaskExecutionStatus.Failed,
                        inputSnapshot, null, outcome.ErrorMessage,
                        attempt: 1, started, completed, ct);

                    await _realtime.TaskCompletedAsync(
                        exec.ExecutionId, (int)exec.JobId, task.TaskId, task.TaskName,
                        "Failed", (int)(completed - started).TotalMilliseconds, ct);

                    return false;
                }

                var outputJson = outcome.Output.HasValue
                    ? JsonSerializer.Serialize(outcome.Output.Value)
                    : null;

                if (!string.IsNullOrWhiteSpace(task.OutputKey) && outcome.Output.HasValue)
                    context.Store(task.OutputKey!, outcome.Output.Value);

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
                    var cfg = JsonSerializer.Deserialize<IteratorConfigLite>(task.ConfigurationJson ?? "{}");

                    var subs = task.ChildTasks
                        .Where(c => c.TaskRole == TaskRole.IteratorSubtask)
                        .OrderBy(c => c.SequenceOrder)
                        .ToList();

                    var items = arr.EnumerateArray().ToList();
                    if (items.Count == 0) return true;

                    var batchSize = cfg?.BatchSize is int bs and > 0 ? bs : int.MaxValue;
                    var maxConcurrency = cfg?.MaxConcurrency is int mc and > 0 ? mc : 1;

                    var orderBox = new StrongBox<int>(orderIndex + 1);
                    using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
                    var anyItemFailed = false;

                    for (var batchStart = 0; batchStart < items.Count; batchStart += batchSize)
                    {
                        ct.ThrowIfCancellationRequested();

                        var batch = items.Skip(batchStart).Take(batchSize).ToList();

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

                                    await using var scope = _scopeFactory.CreateAsyncScope();
                                    var innerRunner = scope.ServiceProvider
                                        .GetRequiredService<PipelineRunner>();

                                    var elementContext = new PipelineContext();
                                    elementContext.SetCurrent(item);

                                    var subOk = await innerRunner.RunItemSubtaskAsync(
                                        exec.ExecutionId, sub, elementContext, subOrder,
                                        inputJson, globalIndex, task.TaskId, ct);

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
                        var doneAt = DateTimeOffset.UtcNow;
                        await _realtime.TaskCompletedAsync(
                            exec.ExecutionId, (int)exec.JobId, task.TaskId, task.TaskName,
                            "Failed", (int)(doneAt - started).TotalMilliseconds, ct);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Task {TaskId} threw during execution", task.TaskId);
                var crashedAt = DateTimeOffset.UtcNow;
                await WriteLogAsync(exec, task, orderIndex, TaskExecutionStatus.Failed,
                    inputSnapshot, null, ex.Message,
                    attempt: 1, started, crashedAt, ct);

                await _realtime.TaskCompletedAsync(
                    exec.ExecutionId, (int)exec.JobId, task.TaskId, task.TaskName,
                    "Failed", (int)(crashedAt - started).TotalMilliseconds, ct);

                return false;
            }
        }

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

            // Capture the HTTP status from the outcome. Null for non-HTTP
            // executors (SQL, JSONata, etc.).
            int? httpStatus = null;
            var ok = await RunTaskAsync(
                exec, sub, context, orderIndex, ct,
                s => httpStatus = s);

            scatterItem.Status = ok ? ScatterItemStatus.Completed : ScatterItemStatus.Failed;
            scatterItem.CompletedAt = DateTimeOffset.UtcNow;
            scatterItem.OutputPayload = context.Current?.GetRawText();
            scatterItem.ErrorMessage = ok ? null : $"Subtask '{sub.TaskName}' failed.";
            scatterItem.HttpStatusCode = httpStatus;
            await _uow.SaveChangesAsync(ct);

            return ok;
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static bool IsTruthy(JsonElement? el)
        {
            if (el is null) return false;
            return el.Value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => false,
                JsonValueKind.Undefined => false,
                JsonValueKind.Number => el.Value.GetDouble() != 0,
                JsonValueKind.String => !string.IsNullOrEmpty(el.Value.GetString()),
                JsonValueKind.Array => el.Value.GetArrayLength() > 0,
                JsonValueKind.Object => true,
                _ => false,
            };
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
            => _executors.ResolveForConnection(task.TaskType, connectionProtocol);

        // -----------------------------------------------------------------
        // Config mirror types (deserialized from configuration_json)
        // -----------------------------------------------------------------

        public sealed record LaneResult(bool Success, JsonElement? Output, string Status);

        private sealed class IteratorConfigLite
        {
            public int? BatchSize { get; set; }
            public int? MaxConcurrency { get; set; }
        }

        private sealed class BranchConfigLite
        {
            public string? Condition { get; set; }
            public string? ExecutionMode { get; set; }
        }

        private sealed class JoinConfigLite
        {
            public string? JoinMode { get; set; }
            public int? JoinThreshold { get; set; }
            public int? JoinTimeoutSeconds { get; set; }
            public string? JoinTimeoutAction { get; set; }
        }
    }
}