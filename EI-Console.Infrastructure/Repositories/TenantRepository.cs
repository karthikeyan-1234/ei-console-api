using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Models;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Repositories
{
    public sealed class TenantRepository : ITenantRepository
    {
        private readonly EiDbContext _db;

        public TenantRepository(EiDbContext db) => _db = db;

        public Task<List<Tenant>> GetAllAsync(CancellationToken ct = default) =>
            _db.Tenants.AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync(ct);

        public Task<Tenant?> GetByIdAsync(int id, CancellationToken ct = default) =>
            _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == id, ct);

        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) =>
            _db.Tenants.FirstOrDefaultAsync(t => t.TenantCode == code, ct);

        public void Add(Tenant tenant) => _db.Tenants.Add(tenant);

        public void Remove(Tenant tenant) => _db.Tenants.Remove(tenant);
    }
}
