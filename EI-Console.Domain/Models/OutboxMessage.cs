using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class OutboxMessage
    {
        public long OutboxId { get; set; }
        public int? TenantId { get; set; }
        public string AggregateType { get; set; } = null!;
        public string AggregateId { get; set; } = null!;
        public string EventType { get; set; } = null!;
        public string Topic { get; set; } = null!;
        public string? MessageKey { get; set; }
        public string PayloadJson { get; set; } = null!;
        public string? CorrelationId { get; set; }
        public OutboxStatus Status { get; set; }
        public int RetryCount { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? NextAttemptAt { get; set; }
        public DateTimeOffset? ProcessedAt { get; set; }

        public Tenant? Tenant { get; set; }
    }
}
