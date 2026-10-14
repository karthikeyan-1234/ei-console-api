using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Services
{
    public interface IJobScheduler
    {
        Task ScheduleAsync(int jobId, string cronExpression, string timezone, CancellationToken ct = default);
        Task UnscheduleAsync(int jobId, CancellationToken ct = default);
        Task RescheduleAsync(int jobId, string cronExpression, string timezone, CancellationToken ct = default);
        Task ReconcileStartupAsync(CancellationToken ct = default);
    }
}
