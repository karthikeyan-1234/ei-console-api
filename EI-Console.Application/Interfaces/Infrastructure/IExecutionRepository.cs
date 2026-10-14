using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public interface IExecutionRepository
    {
        Task<List<JobExecution>> GetRecentAsync(int take, CancellationToken ct = default);
        Task<List<JobExecution>> GetByJobAsync(int jobId, int take, CancellationToken ct = default);
        Task<JobExecution?> GetByIdAsync(Guid executionId, CancellationToken ct = default);
        Task<bool> IdempotencyKeyExistsAsync(string key, DateTimeOffset since, CancellationToken ct = default);
        void Add(JobExecution execution);
    }
}
