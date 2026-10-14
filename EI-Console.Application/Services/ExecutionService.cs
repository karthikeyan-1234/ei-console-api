using _04_EI_Console.Shared.Application;
using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Mappings;
using EI_Console.Application.Pipeline;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;
using EI_Console.Shared.Application;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;


namespace EI_Console.Application.Services
{
    public sealed class ExecutionService : IExecutionService
    {
        private readonly IExecutionRepository _executions;
        private readonly IJobRepository _jobs;
        private readonly ITaskExecutionLogRepository _logs;
        private readonly ICurrentUser _currentUser;
        private readonly IRealtimePublisher _realtime;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<ExecutionService> _log;

        public ExecutionService(
            IExecutionRepository executions,
            IJobRepository jobs,
            ITaskExecutionLogRepository logs,
            ICurrentUser currentUser,
            IRealtimePublisher realtime,
            IUnitOfWork uow,
            ILogger<ExecutionService> log)
        {
            _executions = executions;
            _jobs = jobs;
            _logs = logs;
            _currentUser = currentUser;
            _realtime = realtime;
            _uow = uow;
            _log = log;
        }

        // -----------------------------------------------------------------
        // Read
        // -----------------------------------------------------------------

        public async Task<List<ExecutionSummaryDto>> ListAsync(int take = 50, CancellationToken ct = default)
        {
            var all = await _executions.GetRecentAsync(take, ct);

            // Load job names in one pass
            var jobIds = all.Select(e => e.JobId).Distinct().ToList();
            var jobs = new Dictionary<int, IntegrationJob>();
            foreach (var id in jobIds)
            {
                var j = await _jobs.GetByIdAsync(id, ct);
                if (j is not null) jobs[id] = j;
            }

            return all.Select(e => e.ToSummaryDto(
                jobs.TryGetValue(e.JobId, out var j) ? j.JobName : "(unknown)",
                jobVersion: 1)).ToList();
        }

        public async Task<ExecutionDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
        {
            var exec = await _executions.GetByIdAsync(id, ct);
            if (exec is null) return null;

            var job = await _jobs.GetByIdAsync(exec.JobId, ct);
            var logs = await _logs.GetForExecutionAsync(id, ct);

            // Resolve task names in one pass
            var jobWithPipeline = await _jobs.GetWithPipelineAsync(exec.JobId, ct);
            var taskNames = jobWithPipeline?.Versions
                .SelectMany(v => v.Tasks)
                .ToDictionary(t => t.TaskId, t => (t.TaskName, t.TaskType.ToString()))
                ?? new Dictionary<int, (string, string)>();

            var logDtos = logs.Select(l =>
            {
                taskNames.TryGetValue(l.TaskId, out var name);
                return l.ToDto(name.Item1 ?? "(task)", name.Item2 ?? "Unknown");
            }).ToList();

            return exec.ToDetailDto(job?.JobName ?? "(unknown)", jobVersion: 1, logDtos);
        }

        // -----------------------------------------------------------------
        // Trigger
        // -----------------------------------------------------------------

        public async Task<Guid> TriggerAsync(
            int jobId,
            string triggerSource = "Manual",
            string? idempotencyKey = null,
            string? payload = null,
            CancellationToken ct = default)
        {
            var job = await _jobs.GetWithPipelineAsync(jobId, ct);
            if (job is null)
                throw new InvalidOperationException($"Job {jobId} not found.");

            var version = job.Versions
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();

            if (version is null)
                throw new InvalidOperationException($"Job {jobId} has no version.");

            var source = Enum.TryParse<TriggerSource>(triggerSource, ignoreCase: true, out var s)
    ? s : TriggerSource.Manual;

            var now = DateTimeOffset.UtcNow;
            var correlationId = Guid.NewGuid().ToString();
            var workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

            var exec = new JobExecution
            {
                JobId = jobId,
                VersionId = version.VersionId,
                CorrelationId = correlationId,
                IdempotencyKey = idempotencyKey,
                TriggerSource = source,
                Status = ExecutionStatus.Running,
                ItemCount = 0,
                SuccessCount = 0,
                FailureCount = 0,
                WorkerInstanceId = workerId,
                StartedAt = now,
            };

            _executions.Add(exec);
            await _uow.SaveChangesAsync(ct);

            // SignalR: announce start
            await _realtime.ExecutionStartedAsync(exec.ExecutionId, job.TenantId, job.JobName, ct);

            _log.LogInformation(
                "Execution {ExecutionId} started for job {JobName} ({JobId}) via {Source}",
                exec.ExecutionId, job.JobName, jobId, triggerSource);

            return exec.ExecutionId;
        }

        public async Task<Guid?> FindExecutionByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            var since = DateTimeOffset.UtcNow.AddHours(-24);
            var exists = await _executions.IdempotencyKeyExistsAsync(key, since, ct);
            if (!exists) return null;

            var recent = await _executions.GetRecentAsync(100, ct);
            var match = recent.FirstOrDefault(e =>
                e.IdempotencyKey == key && e.StartedAt >= since);

            return match?.ExecutionId;
        }
    }
}
