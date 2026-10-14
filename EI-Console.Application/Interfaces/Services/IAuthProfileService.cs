using _04_EI_Console.Shared.Application.Contracts;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Services
{
    public interface IAuthProfileService
    {
        Task<List<AuthProfileDto>> ListAsync(CancellationToken ct = default);
        Task<AuthProfileDto?> GetAsync(int id, CancellationToken ct = default);
        Task<AuthProfileDto> CreateAsync(CreateAuthProfileRequest request, CancellationToken ct = default);
        Task<AuthProfileDto?> UpdateAsync(int id, UpdateAuthProfileRequest request, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
        Task<TestAuthResultDto> TestAsync(int id, CancellationToken ct = default);
    }
}
