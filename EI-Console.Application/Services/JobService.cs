using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Mappings;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;
using EI_Console.Shared;
using EI_Console.Shared.Application;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Services
{
    public sealed class JobService : IJobService
    {
        private readonly IJobRepository _jobs;
        private readonly ITenantRepository _tenants;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<JobService> _log;
        private readonly IJobScheduler _scheduler;
        private readonly IExecutionRepository _executions;


        public JobService(
            IJobRepository jobs,
            ITenantRepository tenants,
                IExecutionRepository executions,

            IJobScheduler scheduler,
            IUnitOfWork uow,
            ILogger<JobService> log)
        {
            _jobs = jobs;
            _tenants = tenants;
            _executions = executions;
            _scheduler = scheduler;
            _uow = uow;
            _log = log;
        }

        // -----------------------------------------------------------------
        // Read
        // -----------------------------------------------------------------

        public async Task<List<JobSummaryDto>> ListAsync(int filterCount, CancellationToken ct = default)
        {
            var all = await _jobs.GetAllAsync(filterCount, ct);
            var lastRuns = await _executions.GetLastStartedAtByJobAsync(
                all.Select(j => j.JobId).ToList(), ct);

            return all.Select(j => j.ToSummaryDto(
                lastRuns.TryGetValue(j.JobId, out var t) ? t : null)).ToList();
        }

        public async Task<List<JobSummaryDto>> ListAsync(CancellationToken ct = default)
        {
            var all = await _jobs.GetAllAsync(ct);
            var lastRuns = await _executions.GetLastStartedAtByJobAsync(
                all.Select(j => j.JobId).ToList(), ct);

            return all.Select(j => j.ToSummaryDto(
                lastRuns.TryGetValue(j.JobId, out var t) ? t : null)).ToList();
        }

        public async Task<JobDto?> GetAsync(int id, CancellationToken ct = default)
        {
            var job = await _jobs.GetWithPipelineAsync(id, ct);
            if (job is null) return null;

            var lastRuns = await _executions.GetLastStartedAtByJobAsync(new[] { id }, ct);
            var lastRun = lastRuns.TryGetValue(id, out var t) ? t : (DateTimeOffset?)null;

            return job.ToDto(lastRun);
        }

        public async Task<JobDto?> PublishAsync(int id, CancellationToken ct = default)
        {
            var job = await _jobs.GetWithPipelineAsync(id, ct);
            if (job is null) return null;

            var current = job.Versions
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();
            if (current is null)
                throw new InvalidOperationException($"Job {id} has no version.");

            if (current.IsPublished)
            {
                // Idempotent. Re-publishing an already-published job just ensures
                // the job is Active and returns.
                if (job.Status != JobStatus.Active)
                {
                    job.Status = JobStatus.Active;
                    job.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.SaveChangesAsync(ct);
                }
                return await GetAsync(id, ct);
            }

            var tasks = await _jobs.GetTasksForVersionAsync(current.VersionId, ct);
            if (tasks.Count == 0)
                throw new InvalidOperationException(
                    "Cannot publish — the current version has no tasks. " +
                    "Add at least one task in the builder first.");

            // Retire any other published version — the unique index
            // UQ_job_versions_published permits exactly one per job.
            foreach (var v in job.Versions.Where(v => v.IsPublished))
                v.IsPublished = false;

            current.IsPublished = true;
            current.PublishedAt = DateTimeOffset.UtcNow;

            job.Status = JobStatus.Active;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            await _uow.SaveChangesAsync(ct);

            await _scheduler.UnscheduleAsync(id, ct);
            if (job.TriggerType == TriggerType.Scheduled && job.Schedule is not null)
            {
                await _scheduler.ScheduleAsync(
                    id, job.Schedule.CronExpression, job.Schedule.Timezone, ct);
            }

            _log.LogInformation(
                "Job {Id} published v{Version} and set to Active",
                id, current.VersionNumber);

            return await GetAsync(id, ct);
        }
        
        
        
        // -----------------------------------------------------------------
        // Create
        // -----------------------------------------------------------------

        public async Task<JobDto> CreateAsync(CreateJobRequest request, CancellationToken ct = default)
        {
            var tenantId = int.Parse(request.TenantId);

            if (await _tenants.GetByIdAsync(tenantId, ct) is null)
                throw new InvalidOperationException($"Tenant '{request.TenantId}' not found.");

            var slug = request.Name.ToSlug();
            if (string.IsNullOrWhiteSpace(slug))
                throw new InvalidOperationException("Job name produced an empty slug.");

            var trigger = Enum.Parse<TriggerType>(request.Trigger, ignoreCase: true);
            var now = DateTimeOffset.UtcNow;

            var job = new IntegrationJob
            {
                TenantId = tenantId,
                JobName = request.Name.Trim(),
                EndpointSlug = slug,
                Description = request.Description,
                TriggerType = trigger,
                Status = JobStatus.Draft,
                CreatedAt = now,
            };

            if (trigger == TriggerType.Scheduled && !string.IsNullOrWhiteSpace(request.Cron))
            {
                job.Schedule = new JobSchedule
                {
                    CronExpression = request.Cron.Trim(),
                    Timezone = "UTC",
                };
            }

            var version = new JobVersion
            {
                VersionNumber = 1,
                PipelineJson = "{\"tasks\":[]}",
                PipelineSchemaVersion = 1,
                IsPublished = true,
                PublishedAt = now,
                CreatedAt = now,
            };
            job.Versions.Add(version);

            _jobs.Add(job);

            try
            {
                await _uow.SaveChangesAsync(ct);

                if (job.TriggerType == TriggerType.Scheduled && job.Schedule is not null)
                {
                    await _scheduler.ScheduleAsync(job.JobId, job.Schedule.CronExpression, job.Schedule.Timezone, ct);
                }
            }
            catch (Exception ex) when (
                (ex.InnerException?.Message ?? string.Empty).Contains("UQ_integration_jobs_slug") ||
                ex.Message.Contains("UQ_integration_jobs_slug"))
            {
                throw new InvalidOperationException(
                    $"A job with slug '{slug}' already exists for this tenant.");
            }

            _log.LogInformation("Job {Name} (slug {Slug}) created with id {Id}",
                job.JobName, job.EndpointSlug, job.JobId);

            return (await GetAsync(job.JobId, ct))!;
        }

        // -----------------------------------------------------------------
        // Update
        // -----------------------------------------------------------------

        public async Task<JobDto?> UpdateAsync(int id, UpdateJobRequest request, CancellationToken ct = default)
        {
            var job = await _jobs.GetWithPipelineAsync(id, ct);
            if (job is null) return null;

            if (!string.IsNullOrWhiteSpace(request.Name))
                job.JobName = request.Name.Trim();

            if (request.Description != null)
                job.Description = request.Description;

            job.UpdatedAt = DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Trigger))
            {
                var trigger = Enum.Parse<TriggerType>(request.Trigger, ignoreCase: true);
                job.TriggerType = trigger;

                if (trigger == TriggerType.Scheduled)
                {
                    if (!string.IsNullOrWhiteSpace(request.Cron))
                    {
                        if (job.Schedule is null)
                            job.Schedule = new JobSchedule
                            {
                                CronExpression = request.Cron.Trim(),
                                Timezone = "UTC",
                            };
                        else
                            job.Schedule.CronExpression = request.Cron.Trim();
                    }
                }
                else
                {
                    job.Schedule = null;
                }
            }

            await _uow.SaveChangesAsync(ct);

            await _scheduler.UnscheduleAsync(id, ct);
            if (job.TriggerType == TriggerType.Scheduled && job.Schedule is not null)
            {
                await _scheduler.ScheduleAsync(
                    id, job.Schedule.CronExpression, job.Schedule.Timezone, ct);
            }

            // If the caller supplied a pipeline, replace it through the same path
            // the dedicated /pipeline endpoint uses. This is how the builder
            // persists task-list edits — it does not know about the separate
            // pipeline endpoint.
            if (request.Pipeline is not null)
            {
                await SavePipelineAsync(id, new SavePipelineRequest(request.Pipeline), ct);
            }

            return await GetAsync(id, ct);
        }

        public async Task<JobDto?> SetStatusAsync(int id, string status, CancellationToken ct = default)
        {
            var job = await _jobs.GetWithPipelineAsync(id, ct);
            if (job is null) return null;

            if (!Enum.TryParse<JobStatus>(status, ignoreCase: true, out var newStatus))
                throw new InvalidOperationException($"Unknown job status '{status}'.");

            // No-op if the status is already set. Prevents a spurious "Draft → Draft"
            // log line and avoids an unnecessary DB write.
            if (job.Status == newStatus) return await GetAsync(id, ct);

            var previous = job.Status;
            job.Status = newStatus;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            await _uow.SaveChangesAsync(ct);

            // Reconcile the Quartz schedule against the new status. Activating a
            // scheduled job installs its trigger; deactivating removes it.
            if (newStatus == JobStatus.Active
                && job.TriggerType == TriggerType.Scheduled
                && job.Schedule is not null)
            {
                await _scheduler.ScheduleAsync(
                    job.JobId, job.Schedule.CronExpression, job.Schedule.Timezone, ct);
            }
            else if (previous == JobStatus.Active
                  && newStatus != JobStatus.Active)
            {
                await _scheduler.UnscheduleAsync(job.JobId, ct);
            }

            _log.LogInformation(
                "Job {Id} status changed {Previous} → {New}",
                id, previous, newStatus);

            return await GetAsync(id, ct);
        }


        // -----------------------------------------------------------------
        // Delete
        // -----------------------------------------------------------------

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var job = await _jobs.GetByIdAsync(id, ct);
            if (job is null) return false;

            // If the job has any execution history, archive it instead of hard-
            // deleting. task_execution_log rows reference task_id with RESTRICT, so
            // a hard delete would either fail or require cascading away the audit
            // trail. Archiving keeps the history intact and hides the job from the
            // default list.
            var executions = await _executions.GetByJobAsync(id, take: 1, ct);

            if (executions.Count > 0)
            {
                job.Status = JobStatus.Archived;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await _uow.SaveChangesAsync(ct);

                await _scheduler.UnscheduleAsync(job.JobId, ct);

                _log.LogWarning(
                    "Job {Name} (id {Id}) archived — has execution history",
                    job.JobName, job.JobId);
                return true;
            }

            // No history — safe to hard delete.
            _jobs.Remove(job);
            await _uow.SaveChangesAsync(ct);

            await _scheduler.UnscheduleAsync(job.JobId, ct);

            _log.LogWarning("Job {Name} (id {Id}) deleted", job.JobName, job.JobId);
            return true;
        }

        // -----------------------------------------------------------------
        // Pipeline save — delete-and-replace the current version's task tree
        // -----------------------------------------------------------------

        public async Task<JobDto?> SavePipelineAsync(int id, SavePipelineRequest request, CancellationToken ct = default)
        {
            var job = await _jobs.GetWithPipelineAsync(id, ct);
            if (job is null) return null;

            var current = job.Versions
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();
            if (current is null)
                throw new InvalidOperationException($"Job {id} has no version.");

            // A published version is frozen. Executions are pinned to it; its task
            // rows may be referenced by task_execution_log (FK RESTRICT). Editing
            // in place would break both. Fork a new draft on top.
            //
            // A draft version is editable in place — that is what keeps version
            // numbers stable across many edits. This is the fix: the signal is
            // "is the current version published?", not "does the job have any
            // execution history?".
            // Fork a new version whenever the current one cannot be edited in place:
            //   - it is published (executions pinned to it)
            //   - its tasks have execution-log rows (FK RESTRICT blocks delete)
            // A pure draft that has never been executed can be edited in place.
            var hasHistory = await _jobs.VersionHasExecutionHistoryAsync(current.VersionId, ct);

            if (current.IsPublished || hasHistory)
            {
                var reason = current.IsPublished
                    ? $"published v{current.VersionNumber} was frozen"
                    : $"v{current.VersionNumber} has execution history";

                var now = DateTimeOffset.UtcNow;
                var draft = new JobVersion
                {
                    VersionNumber = current.VersionNumber + 1,
                    PipelineJson = "{\"tasks\":[]}",
                    PipelineSchemaVersion = current.PipelineSchemaVersion,
                    IsPublished = false,
                    CreatedAt = now,
                };
                job.Versions.Add(draft);

                await _uow.SaveChangesAsync(ct);

                _log.LogInformation(
                    "Job {Id} — created draft v{Version} on top ({Reason})",
                    id, draft.VersionNumber, reason);

                current = draft;
            }

            // `current` is now a mutable draft. Replace its task tree atomically.
            // Delete-then-insert in a single SaveChanges — EF Core will batch the
            // whole operation into one MERGE and one transaction. If the insert
            // fails, the delete rolls back with it. No orphan-empty-draft possible.
            var existing = await _jobs.GetTasksForVersionAsync(current.VersionId, ct);
            if (existing.Count > 0)
            {
                _jobs.RemoveTasks(OrderTasksByDepthDescending(existing));
                current.Tasks.Clear();
            }

            var sequence = 0;
            foreach (var dto in request.Tasks)
            {
                current.Tasks.Add(BuildTaskTree(
                    versionId: current.VersionId,
                    parentTaskId: null,
                    role: TaskRole.Root,
                    sequenceOrder: sequence++,
                    dto: dto));
            }

            await _uow.SaveChangesAsync(ct);

            _log.LogInformation(
                "Pipeline saved for job {Id} — {RootCount} root task(s) on v{Version}",
                id, request.Tasks.Count, current.VersionNumber);

            return await GetAsync(id, ct);
        }
        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static List<JobTask> OrderTasksByDepthDescending(List<JobTask> tasks)
        {
            // Compute depth of each task by following ParentTaskId.
            var byId = tasks.ToDictionary(t => t.TaskId);
            var depths = new Dictionary<int, int>();

            int Depth(JobTask t)
            {
                if (depths.TryGetValue(t.TaskId, out var cached)) return cached;
                var d = t.ParentTaskId is int pid && byId.ContainsKey(pid)
                    ? Depth(byId[pid]) + 1
                    : 0;
                depths[t.TaskId] = d;
                return d;
            }

            return tasks.OrderByDescending(Depth).ToList();
        }

        private static JobTask BuildTaskTree(
            int versionId,
            int? parentTaskId,
            TaskRole role,
            int sequenceOrder,
            PipelineTaskDto dto)
        {
            var taskType = Enum.Parse<TaskType>(dto.Type, ignoreCase: true);

            var task = new JobTask
            {
                VersionId = versionId,
                ParentTaskId = parentTaskId,
                TaskRole = role,
                TaskType = taskType,
                TaskName = string.IsNullOrWhiteSpace(dto.Name) ? dto.Type : dto.Name!,
                SequenceOrder = sequenceOrder,
                ConnectionId = ParseNullableInt(dto.ConnectionId),
                AuthProfileId = ParseNullableInt(dto.AuthProfileId),
                TimeoutSeconds = dto.TimeoutSeconds,
                OutputKey = dto.OutputKey,
                ConfigurationJson = TaskConfigurationSerializer.Serialize(dto),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            // When the DTO says this Transform iterates, attach the iterator
            // configuration so the runner's entry condition (task.Iterator is not null)
            // passes and the per-item loop runs. BatchSize and MaxConcurrency are
            // also mirrored here for round-trip fidelity and so the console can read
            // them without parsing configuration_json.
            if (taskType == TaskType.Transform && dto.Iterate == true)
            {
                task.Iterator = new TaskIterator
                {
                    TaskType = TaskType.Transform,
                    // 0 means "no batching, process everything in one batch" and is
                    // the same default the runner uses when configuration_json lacks
                    // the field. 1 concurrency means strictly sequential.
                    BatchSize = dto.BatchSize ?? 0,
                    MaxConcurrency = dto.MaxConcurrency ?? 1,
                };
            }

            // Fork children — for a Branch task's children
            if (dto.Children is not null)
            {
                var seq = 0;
                foreach (var child in dto.Children)
                {
                    task.ChildTasks.Add(BuildTaskTree(
                        versionId,
                        parentTaskId: null,
                        role: TaskRole.ForkChild,
                        sequenceOrder: seq++,
                        dto: child));
                }
            }

            // Iterator sub-tasks — for an iterating Transform's children
            if (dto.Subtasks is not null)
            {
                var seq = 0;
                foreach (var sub in dto.Subtasks)
                {
                    task.ChildTasks.Add(BuildTaskTree(
                        versionId,
                        parentTaskId: null,
                        role: TaskRole.IteratorSubtask,
                        sequenceOrder: seq++,
                        dto: sub));
                }
            }

            return task;
        }

        private static int? ParseNullableInt(string? raw) =>
            int.TryParse(raw, out var v) ? v : null;
    }
}
