using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Models;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Repositories
{
    public sealed class ExecutionRepository : IExecutionRepository
    {
        private readonly EiDbContext _db;

        public ExecutionRepository(EiDbContext db) => _db = db;

        public async Task<Dictionary<int, DateTimeOffset>> GetLastStartedAtByJobAsync(
    IReadOnlyCollection<int> jobIds, CancellationToken ct = default)
        {
            if (jobIds.Count == 0) return new();
            return await _db.JobExecutions
                .Where(e => jobIds.Contains(e.JobId))
                .GroupBy(e => e.JobId)
                .Select(g => new { JobId = g.Key, Last = g.Max(e => e.StartedAt) })
                .ToDictionaryAsync(x => x.JobId, x => x.Last, ct);
        }

        public Task<List<JobExecution>> GetRecentAsync(int take, CancellationToken ct = default) =>
            _db.JobExecutions
                .AsNoTracking()
                .Include(e => e.Job)
                .OrderByDescending(e => e.StartedAt)
                .Take(take)
                .ToListAsync(ct);

        public Task<List<JobExecution>> GetByJobAsync(int jobId, int take, CancellationToken ct = default) =>
            _db.JobExecutions.AsNoTracking()
                .Where(e => e.JobId == jobId)
                .OrderByDescending(e => e.StartedAt)
                .Take(take)
                .ToListAsync(ct);

        public Task<JobExecution?> GetByIdAsync(Guid executionId, CancellationToken ct = default) =>
       _db.JobExecutions
           .Include(e => e.Job)
           .FirstOrDefaultAsync(e => e.ExecutionId == executionId, ct);

        public Task<bool> IdempotencyKeyExistsAsync(string key, DateTimeOffset since, CancellationToken ct = default) =>
            _db.JobExecutions.AnyAsync(
                e => e.IdempotencyKey == key && e.StartedAt >= since, ct);

        public void Add(JobExecution execution) => _db.JobExecutions.Add(execution);
    }
}
