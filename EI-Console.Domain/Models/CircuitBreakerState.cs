using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class CircuitBreakerState
    {
        public int CircuitId { get; set; }
        public int ConnectionId { get; set; }
        public CircuitBreakerStatus State { get; set; } = CircuitBreakerStatus.Closed;
        public int FailureCount { get; set; }
        public DateTimeOffset? LastFailureAt { get; set; }
        public DateTimeOffset? OpenedAt { get; set; }
        public DateTimeOffset? HalfOpenAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        // Navigation
        public Connection Connection { get; set; } = null!;
    }
}
