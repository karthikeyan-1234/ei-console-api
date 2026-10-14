using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class ScatterGatherItem
    {
        public long ItemId { get; set; }
        public Guid ExecutionId { get; set; }
        public int TaskId { get; set; }
        public int ParentTaskId { get; set; }
        public int ItemIndex { get; set; }
        public string? ItemKey { get; set; }
        public ScatterItemStatus Status { get; set; }
        public int? HttpStatusCode { get; set; }
        public int AttemptCount { get; set; }
        public string InputPayload { get; set; } = null!;
        public string? OutputPayload { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTimeOffset? DispatchedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }

        public JobExecution Execution { get; set; } = null!;
        public JobTask Task { get; set; } = null!;
        public JobTask ParentTask { get; set; } = null!;
        public ICollection<DeadLetterItem> DeadLetters { get; set; } = new List<DeadLetterItem>();
    }
}
