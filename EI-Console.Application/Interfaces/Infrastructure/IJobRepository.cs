using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public interface IJobRepository
    {
        Task<List<IntegrationJob>> GetAllAsync(CancellationToken ct = default);
        Task<List<IntegrationJob>> GetByTenantAsync(int tenantId, CancellationToken ct = default);
        Task<IntegrationJob?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IntegrationJob?> GetWithPipelineAsync(int id, CancellationToken ct = default);
        Task<IntegrationJob?> GetBySlugAsync(string slug, CancellationToken ct = default);
        void Add(IntegrationJob job);
        void Remove(IntegrationJob job);

        Task<List<JobTask>> GetTasksForVersionAsync(int versionId, CancellationToken ct = default);
        void RemoveTasks(IEnumerable<JobTask> tasks);
        Task<bool> VersionHasExecutionHistoryAsync(int versionId, CancellationToken ct = default);
    }
}
