using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Models;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Repositories
{
    public sealed class ScatterGatherItemRepository : IScatterGatherItemRepository
    {
        private readonly EiDbContext _db;

        public ScatterGatherItemRepository(EiDbContext db) => _db = db;

        public void Add(ScatterGatherItem item) => _db.ScatterGatherItems.Add(item);

        public Task<List<ScatterGatherItem>> GetByExecutionAsync(
            Guid executionId, CancellationToken ct = default) =>
            _db.ScatterGatherItems
                .Include(s => s.Task)
                .Where(s => s.ExecutionId == executionId)
                .OrderBy(s => s.ItemIndex)
                .ThenBy(s => s.TaskId)
                .ToListAsync(ct);
    }
}
