using EI_Console.Application.Interfaces.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Scheduling
{
    public sealed class SchedulerReconciliationService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SchedulerReconciliationService> _log;

        public SchedulerReconciliationService(
            IServiceScopeFactory scopeFactory,
            ILogger<SchedulerReconciliationService> log)
        {
            _scopeFactory = scopeFactory;
            _log = log;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
                await scheduler.ReconcileStartupAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Scheduler reconciliation failed");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
