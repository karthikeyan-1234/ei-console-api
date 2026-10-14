using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Models;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Repositories
{
    public sealed class ConnectionRepository : IConnectionRepository
    {
        private readonly EiDbContext _db;

        public ConnectionRepository(EiDbContext db) => _db = db;

        public Task<List<Connection>> GetAllAsync(CancellationToken ct = default) =>
            _db.Connections.AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(ct);

        public Task<Connection?> GetByIdAsync(int id, CancellationToken ct = default) =>
            _db.Connections.FirstOrDefaultAsync(c => c.ConnectionId == id, ct);

        public Task<Connection?> GetWithAuthProfileAsync(int id, CancellationToken ct = default) =>
            _db.Connections
                .Include(c => c.AuthProfile)
                .FirstOrDefaultAsync(c => c.ConnectionId == id, ct);

        public void Add(Connection connection) => _db.Connections.Add(connection);

        public void Remove(Connection connection) => _db.Connections.Remove(connection);
    }
}
