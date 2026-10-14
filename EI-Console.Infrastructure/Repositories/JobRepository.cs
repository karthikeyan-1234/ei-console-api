using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Domain.Models;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Repositories
{
    public sealed class JobRepository : IJobRepository, IJobRepositoryBridge
    {
        private readonly EiDbContext _db;

        public JobRepository(EiDbContext db) => _db = db;

        public Task<List<IntegrationJob>> GetAllAsync(CancellationToken ct = default) =>
            _db.IntegrationJobs.AsNoTracking()
                .OrderBy(j => j.JobName)
                .ToListAsync(ct);

        public Task<List<IntegrationJob>> GetByTenantAsync(int tenantId, CancellationToken ct = default) =>
            _db.IntegrationJobs.AsNoTracking()
                .Where(j => j.TenantId == tenantId)
                .OrderBy(j => j.JobName)
                .ToListAsync(ct);

        public Task<IntegrationJob?> GetByIdAsync(int id, CancellationToken ct = default) =>
            _db.IntegrationJobs.FirstOrDefaultAsync(j => j.JobId == id, ct);

        public Task<IntegrationJob?> GetWithPipelineAsync(int id, CancellationToken ct = default) =>
            _db.IntegrationJobs
                .Include(j => j.Schedule)
                .Include(j => j.Versions)
                    .ThenInclude(v => v.Tasks)
                .FirstOrDefaultAsync(j => j.JobId == id, ct);

        public Task<IntegrationJob?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            _db.IntegrationJobs
                .Include(j => j.Versions)
                    .ThenInclude(v => v.Tasks)
                .FirstOrDefaultAsync(j => j.EndpointSlug == slug, ct);

        public void Add(IntegrationJob job) => _db.IntegrationJobs.Add(job);

        public void Remove(IntegrationJob job) => _db.IntegrationJobs.Remove(job);

        public Task<List<JobTask>> GetTasksForVersionAsync(int versionId, CancellationToken ct = default) =>
    _db.JobTasks
        .Where(t => t.VersionId == versionId)
        .OrderBy(t => t.TaskId)
        .ToListAsync(ct);

        public void RemoveTasks(IEnumerable<JobTask> tasks) =>
            _db.JobTasks.RemoveRange(tasks);

        public async Task<IReadOnlyList<ScheduledJobEntry>> GetScheduledJobsAsync(CancellationToken ct = default)
        {
            return await _db.IntegrationJobs
                .AsNoTracking()
                .Where(j => j.TriggerType == EI_Console.Domain.Enums.TriggerType.Scheduled
                         && j.Status == EI_Console.Domain.Enums.JobStatus.Active
                         && j.Schedule != null)
                .Select(j => new ScheduledJobEntry(
                    j.JobId,
                    j.Schedule!.CronExpression,
                    j.Schedule!.Timezone))
                .ToListAsync(ct);
        }
    }
}
