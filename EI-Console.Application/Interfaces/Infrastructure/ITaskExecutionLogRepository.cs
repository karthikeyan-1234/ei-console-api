using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public interface ITaskExecutionLogRepository
    {
        Task<List<TaskExecutionLog>> GetForExecutionAsync(Guid executionId, CancellationToken ct = default);
        void Add(TaskExecutionLog log);
    }
}
