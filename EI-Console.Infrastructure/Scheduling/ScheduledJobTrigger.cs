using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Pipeline;

using Microsoft.Extensions.Logging;

using Quartz;

using System;
using System.Collections.Generic;
using System.Text;

using Wolverine;

namespace EI_Console.Infrastructure.Scheduling
{
    /// <summary>
    /// Quartz job that fires on the CRON schedule stored in ei.job_schedules.
    /// Its only responsibility is to publish a RunJobCommand, which the
    /// ExecutionService's own pipeline picks up. Keeping the Quartz job thin
    /// ensures the actual work always happens through the same handler chain
    /// as a manual trigger.
    /// </summary>
    [DisallowConcurrentExecution]
    public sealed class ScheduledJobTrigger : IJob
    {
        public const string JobIdKey = "EiJobId";

        private readonly IExecutionServiceBridge _executions;
        private readonly IMessageBus _bus;
        private readonly ILogger<ScheduledJobTrigger> _log;

        public ScheduledJobTrigger(
            IExecutionServiceBridge executions,
            IMessageBus bus,
            ILogger<ScheduledJobTrigger> log)
        {
            _executions = executions;
            _bus = bus;
            _log = log;
        }

        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
        {
            var jobId = context.JobDetail.JobDataMap.GetInt(JobIdKey);
            _log.LogInformation("Scheduled trigger fired for job {JobId}", jobId);

            try
            {
                var executionId = await _executions.TriggerAsync(
                    jobId,
                    triggerSource: "Scheduled",
                    idempotencyKey: null,
                    payload: null,
                    cancellationToken);

                await _bus.PublishAsync(new RunJobCommand(executionId, Payload: null));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Scheduled trigger failed for job {JobId}", jobId);
                throw new JobExecutionException(ex);
            }
        }
    }
}
