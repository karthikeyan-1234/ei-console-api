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

        public async Task<List<JobSummaryDto>> ListAsync(CancellationToken ct = default)
        {
            var all = await _jobs.GetAllAsync(ct);
            return all.Select(j => j.ToSummaryDto()).ToList();
        }

        public async Task<JobDto?> GetAsync(int id, CancellationToken ct = default)
        {
            var job = await _jobs.GetWithPipelineAsync(id, ct);
            return job?.ToDto();
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

            return (await _jobs.GetWithPipelineAsync(job.JobId, ct))!.ToDto();
        }

        // -----------------------------------------------------------------
        // Update
        // -----------------------------------------------------------------

        public async Task<JobDto?> UpdateAsync(int id, UpdateJobRequest request, CancellationToken ct = default)
        {
            var job = await _jobs.GetWithPipelineAsync(id, ct);
            if (job is null) return null;

            job.JobName = request.Name.Trim();
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
                            job.Schedule = new JobSchedule { CronExpression = request.Cron.Trim(), Timezone = "UTC" };
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
                await _scheduler.ScheduleAsync(id, job.Schedule.CronExpression, job.Schedule.Timezone, ct);
            }

            return (await _jobs.GetWithPipelineAsync(id, ct))?.ToDto();
        }


        public async Task<JobDto?> SetStatusAsync(int id, string status, CancellationToken ct = default)
        {
            var job = await _jobs.GetWithPipelineAsync(id, ct);
            if (job is null) return null;

            if (!Enum.TryParse<JobStatus>(status, ignoreCase: true, out var newStatus))
                throw new InvalidOperationException($"Unknown job status '{status}'.");

            // No-op if the status is already set. Prevents a spurious "Draft → Draft"
            // log line and avoids an unnecessary DB write.
            if (job.Status == newStatus) return job.ToDto();

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

            return (await _jobs.GetWithPipelineAsync(id, ct))?.ToDto();
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

            var currentVersion = job.Versions
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();

            if (currentVersion is null)
                throw new InvalidOperationException($"Job {id} has no version.");

            // If the job has any execution history, we cannot delete the existing
            // task rows — task_execution_log.task_id references them with RESTRICT.
            // Create a new version and leave the previous one intact. Executions
            // remain pinned to the version they ran under.
            var executions = await _executions.GetByJobAsync(id, take: 1, ct);
            var hasHistory = executions.Count > 0;

            JobVersion version;

            if (hasHistory)
            {
                var now = DateTimeOffset.UtcNow;

                // UQ_job_versions_published enforces at most one published version per
                // job. Retire the current one before publishing the new one. The old
                // version's task rows survive — the executions that ran against them
                // keep their audit trail. Only the is_published flag flips.
                currentVersion.IsPublished = false;

                version = new JobVersion
                {
                    VersionNumber = currentVersion.VersionNumber + 1,
                    PipelineJson = "{\"tasks\":[]}",
                    PipelineSchemaVersion = 1,
                    IsPublished = true,
                    PublishedAt = now,
                    CreatedAt = now,
                };
                job.Versions.Add(version);

                // Both changes — flip the old flag, insert the new row — go in one save
                // so the unique index never sees two published rows simultaneously.
                await _uow.SaveChangesAsync(ct);

                _log.LogInformation(
                    "Job {Id} pipeline updated — created version {Version}, retired version {Old}",
                    id, version.VersionNumber, currentVersion.VersionNumber);
            }
            else
            {
                // No history — safe to delete-and-replace the current version's tasks.
                var existingTasks = await _jobs.GetTasksForVersionAsync(currentVersion.VersionId, ct);

                var orderedForDeletion = OrderTasksByDepthDescending(existingTasks);
                if (orderedForDeletion.Count > 0)
                    _jobs.RemoveTasks(orderedForDeletion);

                currentVersion.Tasks.Clear();
                await _uow.SaveChangesAsync(ct);

                // Re-query to get a fresh tracking state with no orphaned tasks.
                job = await _jobs.GetWithPipelineAsync(id, ct);
                version = job!.Versions
                    .OrderByDescending(v => v.VersionNumber)
                    .First();
            }

            // Build and insert the new tree under the target version.
            var sequence = 0;
            foreach (var dto in request.Tasks)
            {
                var task = BuildTaskTree(
                    versionId: version.VersionId,
                    parentTaskId: null,
                    role: TaskRole.Root,
                    sequenceOrder: sequence++,
                    dto: dto);
                version.Tasks.Add(task);
            }

            await _uow.SaveChangesAsync(ct);

            _log.LogInformation(
                "Pipeline saved for job {Id} — {RootCount} root task(s) on version {Version}",
                id, request.Tasks.Count, version.VersionNumber);

            return (await _jobs.GetWithPipelineAsync(id, ct))?.ToDto();
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
