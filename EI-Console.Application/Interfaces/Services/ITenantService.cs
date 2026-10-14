using _04_EI_Console.Shared.Application.Contracts;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Services
{
    public interface ITenantService
    {
        Task<List<TenantDto>> ListAsync(CancellationToken ct = default);
        Task<TenantDto?> GetAsync(int id, CancellationToken ct = default);
        Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);
        Task<TenantDto?> UpdateAsync(int id, UpdateTenantRequest request, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
        Task<TenantDto?> SetActiveAsync(int id, bool active, CancellationToken ct = default);
    }
}
