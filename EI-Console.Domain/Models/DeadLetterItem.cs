using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class DeadLetterItem
    {
        public long DlqId { get; set; }
        public Guid ExecutionId { get; set; }
        public int? TaskId { get; set; }
        public long? ScatterItemId { get; set; }
        public string Reason { get; set; } = null!;
        public string FailedPayload { get; set; } = null!;
        public string? ExceptionDetails { get; set; }
        public int FailureCount { get; set; }
        public DeadLetterStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastFailedAt { get; set; }
        public DateTimeOffset? ReplayedAt { get; set; }
        public int? ReplayedByUserId { get; set; }

        public JobExecution Execution { get; set; } = null!;
        public JobTask? Task { get; set; }
        public ScatterGatherItem? ScatterItem { get; set; }
        public User? ReplayedByUser { get; set; }
    }
}
