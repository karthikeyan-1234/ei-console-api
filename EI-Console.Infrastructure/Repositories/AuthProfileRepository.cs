using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Models;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Repositories
{
    public sealed class AuthProfileRepository : IAuthProfileRepository
    {
        private readonly EiDbContext _db;

        public AuthProfileRepository(EiDbContext db) => _db = db;

        public Task<List<AuthProfile>> GetAllAsync(CancellationToken ct = default) =>
            _db.AuthProfiles
                .AsNoTracking()
                .Include(p => p.Keycloak)
                .Include(p => p.OAuth2)
                .Include(p => p.ApiKey)
                .Include(p => p.WsSecurity)
                .Include(p => p.Mtls)
                .Include(p => p.SqlServer)
                .Include(p => p.Ftp)
                .Include(p => p.Sftp)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

        public Task<AuthProfile?> GetByIdAsync(int id, CancellationToken ct = default) =>
            _db.AuthProfiles
                .Include(p => p.Keycloak)
                .Include(p => p.OAuth2)
                .Include(p => p.ApiKey)
                .Include(p => p.WsSecurity)
                .Include(p => p.Mtls)
                .Include(p => p.SqlServer)
                .Include(p => p.Ftp)
                .Include(p => p.Sftp)
                .FirstOrDefaultAsync(p => p.AuthProfileId == id, ct);

        public Task<AuthProfile?> GetWithSubtypesAsync(int id, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);

        public void Add(AuthProfile profile) => _db.AuthProfiles.Add(profile);

        public void Remove(AuthProfile profile) => _db.AuthProfiles.Remove(profile);
    }
}
