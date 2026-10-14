using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Models;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Repositories
{
    public sealed class TaskExecutionLogRepository : ITaskExecutionLogRepository
    {
        private readonly EiDbContext _db;

        public TaskExecutionLogRepository(EiDbContext db) => _db = db;

        public Task<List<TaskExecutionLog>> GetForExecutionAsync(Guid executionId, CancellationToken ct = default) =>
            _db.TaskExecutionLogs.AsNoTracking()
                .Where(l => l.ExecutionId == executionId)
                .OrderBy(l => l.OrderIndex)
                .ToListAsync(ct);

        public void Add(TaskExecutionLog log) => _db.TaskExecutionLogs.Add(log);
    }
}
