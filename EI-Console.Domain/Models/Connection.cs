using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class Connection
    {
        public int ConnectionId { get; set; }
        public int? TenantId { get; set; }
        public string Name { get; set; } = null!;
        public string? ProviderName { get; set; }
        public Protocol Protocol { get; set; }
        public string BaseUrl { get; set; } = null!;
        public int? Port { get; set; }
        public string? DefaultHeadersJson { get; set; }
        public int TimeoutSeconds { get; set; }
        public int RetryCount { get; set; }
        public int? AuthProfileId { get; set; }
        public int? RateLimitPolicyId { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public Tenant? Tenant { get; set; }
        public AuthProfile? AuthProfile { get; set; }
        public RateLimitPolicy? RateLimitPolicy { get; set; }
        public CircuitBreakerState? CircuitBreaker { get; set; }
        public ICollection<JobTask> JobTasks { get; set; } = new List<JobTask>();
    }
}
