using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public interface ITenantRepository
    {
        Task<List<Tenant>> GetAllAsync(CancellationToken ct = default);
        Task<Tenant?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default);
        void Add(Tenant tenant);
        void Remove(Tenant tenant);
    }
}
