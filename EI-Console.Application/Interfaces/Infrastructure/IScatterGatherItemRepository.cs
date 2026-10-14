using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public interface IScatterGatherItemRepository
    {
        void Add(ScatterGatherItem item);
        Task<List<ScatterGatherItem>> GetByExecutionAsync(
            Guid executionId, CancellationToken ct = default);
    }
}
