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

        public JobService(
            IJobRepository jobs,
            ITenantRepository tenants,
            IJobScheduler scheduler,
            IUnitOfWork uow,
            ILogger<JobService> log)
        {
            _jobs = jobs;
            _tenants = tenants;
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

        // -----------------------------------------------------------------
        // Delete
        // -----------------------------------------------------------------

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var job = await _jobs.GetByIdAsync(id, ct);
            if (job is null) return false;

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

            var version = job.Versions
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();

            if (version is null)
                throw new InvalidOperationException($"Job {id} has no version.");

            // 1. Load all existing tasks for this version from the database.
            var existingTasks = await _jobs.GetTasksForVersionAsync(version.VersionId, ct);

            // 2. Delete children (tasks with a parent) before parents. The FK
            //    from job_tasks.parent_task_id is ON DELETE RESTRICT, so ordering
            //    matters. Max depth in v0.1 is two levels, but we sort by depth
            //    anyway to remain correct if nesting grows.
            var orderedForDeletion = OrderTasksByDepthDescending(existingTasks);
            if (orderedForDeletion.Count > 0)
                _jobs.RemoveTasks(orderedForDeletion);

            // 3. Clear the in-memory navigation so EF Core doesn't try to
            //    reconcile the deleted tasks with the incoming ones.
            version.Tasks.Clear();

            // 4. Persist the deletes. Doing this separately from the inserts
            //    keeps the transaction small and makes failures easier to reason
            //    about: if the insert fails, the delete is already committed,
            //    which is a harmless state (an empty pipeline) rather than a
            //    half-deleted tree.
            await _uow.SaveChangesAsync(ct);

            // 5. Re-query the job to get a fresh tracking state with no orphaned
            //    tasks hanging around in memory.
            job = await _jobs.GetWithPipelineAsync(id, ct);
            version = job!.Versions.OrderByDescending(v => v.VersionNumber).First();

            // 6. Build and insert the new tree.
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
                "Pipeline saved for job {Id} — {RootCount} root task(s)",
                id, request.Tasks.Count);

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
