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

        public Task<List<JobExecution>> GetRecentAsync(int take, CancellationToken ct = default) =>
            _db.JobExecutions.AsNoTracking()
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
            _db.JobExecutions.FirstOrDefaultAsync(e => e.ExecutionId == executionId, ct);

        public Task<bool> IdempotencyKeyExistsAsync(string key, DateTimeOffset since, CancellationToken ct = default) =>
            _db.JobExecutions.AnyAsync(
                e => e.IdempotencyKey == key && e.StartedAt >= since, ct);

        public void Add(JobExecution execution) => _db.JobExecutions.Add(execution);
    }
}
