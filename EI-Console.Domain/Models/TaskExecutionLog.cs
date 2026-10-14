using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class TaskExecutionLog
    {
        public long LogId { get; set; }
        public Guid ExecutionId { get; set; }
        public int TaskId { get; set; }
        public long? ParentLogId { get; set; }
        public int OrderIndex { get; set; }
        public TaskExecutionStatus Status { get; set; }
        public int AttemptNumber { get; set; }
        public string? InputPayload { get; set; }
        public string? OutputPayload { get; set; }
        public string? OutputSummary { get; set; }
        public string? ErrorDetails { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public long? DurationMs { get; private set; } // computed in DB

        public JobExecution Execution { get; set; } = null!;
        public JobTask Task { get; set; } = null!;
        public TaskExecutionLog? ParentLog { get; set; }
        public ICollection<TaskExecutionLog> ChildLogs { get; set; } = new List<TaskExecutionLog>();
    }
}
