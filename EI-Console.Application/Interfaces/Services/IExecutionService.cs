using _04_EI_Console.Shared.Application.Contracts;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Services
{
    public interface IExecutionService : IExecutionServiceBridge
    {
        Task<List<ExecutionSummaryDto>> ListAsync(int take = 50, CancellationToken ct = default);
        Task<ExecutionDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
        // TriggerAsync is inherited from IExecutionServiceBridge — remove the duplicate declaration
        Task<Guid?> FindExecutionByIdempotencyKeyAsync(string key, CancellationToken ct = default);

    }
}
