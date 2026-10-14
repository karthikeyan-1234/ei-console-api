using EI_Console.Shared.Application;

using EiConsole.Infrastructure.Persistence;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Persistence
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly EiDbContext _db;

        public UnitOfWork(EiDbContext db) => _db = db;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _db.SaveChangesAsync(cancellationToken);
    }
}
