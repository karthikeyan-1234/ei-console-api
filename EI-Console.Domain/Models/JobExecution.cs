using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class JobExecution
    {
        public Guid ExecutionId { get; set; }
        public int JobId { get; set; }
        public int VersionId { get; set; }
        public string CorrelationId { get; set; } = null!;
        public string? IdempotencyKey { get; set; }
        public TriggerSource TriggerSource { get; set; }
        public ExecutionStatus Status { get; set; }
        public int ItemCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public string? ErrorSummary { get; set; }
        public string? WorkerInstanceId { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? PayloadJson { get; set; }

        public IntegrationJob Job { get; set; } = null!;
        public JobVersion Version { get; set; } = null!;
        public ICollection<TaskExecutionLog> TaskLogs { get; set; } = new List<TaskExecutionLog>();
        public ICollection<ScatterGatherItem> ScatterItems { get; set; } = new List<ScatterGatherItem>();
        public ICollection<DeadLetterItem> DeadLetters { get; set; } = new List<DeadLetterItem>();
    }
}
