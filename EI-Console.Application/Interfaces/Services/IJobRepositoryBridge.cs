using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Services
{
    public sealed record ScheduledJobEntry(int JobId, string CronExpression, string Timezone);

    public interface IJobRepositoryBridge
    {
        Task<IReadOnlyList<ScheduledJobEntry>> GetScheduledJobsAsync(CancellationToken ct = default);
    }
}
