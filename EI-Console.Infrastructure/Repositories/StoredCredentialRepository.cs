using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Models;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Repositories
{
    public sealed class StoredCredentialRepository : IStoredCredentialRepository
    {
        private readonly EiDbContext _db;

        public StoredCredentialRepository(EiDbContext db) => _db = db;

        public Task<StoredCredential?> GetByIdAsync(int id, CancellationToken ct = default) =>
            _db.StoredCredentials.FirstOrDefaultAsync(c => c.StoredCredentialId == id, ct);
    }
}
