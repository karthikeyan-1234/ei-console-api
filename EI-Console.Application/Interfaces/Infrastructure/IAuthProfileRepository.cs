using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public interface IAuthProfileRepository
    {
        Task<List<AuthProfile>> GetAllAsync(CancellationToken ct = default);
        Task<AuthProfile?> GetByIdAsync(int id, CancellationToken ct = default);
        void Add(AuthProfile profile);
        void Remove(AuthProfile profile);
        Task<AuthProfile?> GetWithSubtypesAsync(int id, CancellationToken ct = default);
    }
}
