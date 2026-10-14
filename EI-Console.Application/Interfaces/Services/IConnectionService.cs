using _04_EI_Console.Shared.Application.Contracts;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Services
{
    public interface IConnectionService
    {
        Task<List<ConnectionDto>> ListAsync(CancellationToken ct = default);
        Task<ConnectionDto?> GetAsync(int id, CancellationToken ct = default);
        Task<ConnectionDto> CreateAsync(CreateConnectionRequest request, CancellationToken ct = default);
        Task<ConnectionDto?> UpdateAsync(int id, UpdateConnectionRequest request, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
        Task<TestConnectionResultDto> TestAsync(int id, CancellationToken ct = default);
    }
}
