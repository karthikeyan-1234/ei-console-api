using EI_Console.Application.Interfaces.Services;

using Microsoft.Extensions.Logging;

using Quartz;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Scheduling
{
    public sealed class QuartzJobScheduler : IJobScheduler
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IJobRepositoryBridge _jobs;
        private readonly ILogger<QuartzJobScheduler> _log;

        public QuartzJobScheduler(
            ISchedulerFactory schedulerFactory,
            IJobRepositoryBridge jobs,
            ILogger<QuartzJobScheduler> log)
        {
            _schedulerFactory = schedulerFactory;
            _jobs = jobs;
            _log = log;
        }

        public async Task ScheduleAsync(int jobId, string cronExpression, string timezone, CancellationToken ct = default)
        {
            var scheduler = await _schedulerFactory.GetScheduler(ct);

            var jobKey = new JobKey($"ei-job-{jobId}", "ei-scheduled");
            var existing = await scheduler.GetJobDetail(jobKey, ct);
            if (existing is not null)
            {
                _log.LogDebug("Quartz job {JobKey} already exists — skipping schedule", jobKey);
                return;
            }

            var tz = string.IsNullOrWhiteSpace(timezone)
                ? TimeZoneInfo.Utc
                : TimeZoneInfo.FindSystemTimeZoneById(timezone);

            var job = JobBuilder.Create<ScheduledJobTrigger>()
                .WithIdentity(jobKey)
                .UsingJobData(ScheduledJobTrigger.JobIdKey, jobId)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"ei-trigger-{jobId}", "ei-scheduled")
                .WithCronSchedule(cronExpression, x => x.InTimeZone(tz))
                .Build();

            await scheduler.ScheduleJob(job, trigger);
            _log.LogInformation("Scheduled Quartz job {JobId} with CRON '{Cron}'", jobId, cronExpression);
        }

        public async Task UnscheduleAsync(int jobId, CancellationToken ct = default)
        {
            var scheduler = await _schedulerFactory.GetScheduler(ct);
            var jobKey = new JobKey($"ei-job-{jobId}", "ei-scheduled");
            var existing = await scheduler.GetJobDetail(jobKey, ct);
            if (existing is not null)
            {
                await scheduler.DeleteJob(jobKey, ct);
                _log.LogInformation("Removed Quartz schedule for job {JobId}", jobId);
            }
        }

        public async Task RescheduleAsync(int jobId, string cronExpression, string timezone, CancellationToken ct = default)
        {
            await UnscheduleAsync(jobId, ct);
            await ScheduleAsync(jobId, cronExpression, timezone, ct);
        }

        public async Task ReconcileStartupAsync(CancellationToken ct = default)
        {
            var scheduler = await _schedulerFactory.GetScheduler(ct);
            var scheduled = await _jobs.GetScheduledJobsAsync(ct);

            foreach (var entry in scheduled)
            {
                await ScheduleAsync(entry.JobId, entry.CronExpression, entry.Timezone, ct);
            }

            _log.LogInformation("Reconciled {Count} scheduled job(s) at startup", scheduled.Count);
        }
    }
}
