using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public interface IConnectionRepository
    {
        Task<List<Connection>> GetAllAsync(CancellationToken ct = default);
        Task<Connection?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Connection?> GetWithAuthProfileAsync(int id, CancellationToken ct = default);
        void Add(Connection connection);
        void Remove(Connection connection);
    }
}
