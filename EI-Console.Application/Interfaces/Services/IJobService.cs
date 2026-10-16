using _04_EI_Console.Shared.Application.Contracts;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Services
{
    public interface IJobService
    {
        Task<List<JobSummaryDto>> ListAsync(CancellationToken ct = default);
        Task<JobDto?> GetAsync(int id, CancellationToken ct = default);
        Task<JobDto> CreateAsync(CreateJobRequest request, CancellationToken ct = default);
        Task<JobDto?> UpdateAsync(int id, UpdateJobRequest request, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
        Task<JobDto?> SavePipelineAsync(int id, SavePipelineRequest request, CancellationToken ct = default);
        Task<JobDto?> SetStatusAsync(int id, string status, CancellationToken ct = default);
        Task<JobDto?> PublishAsync(int id, CancellationToken ct = default);
    }
}
