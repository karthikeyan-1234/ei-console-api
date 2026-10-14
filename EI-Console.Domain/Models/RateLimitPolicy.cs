using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class RateLimitPolicy
    {
        public int RateLimitPolicyId { get; set; }
        public int? TenantId { get; set; }
        public string PolicyName { get; set; } = null!;
        public RateLimitScope Scope { get; set; }
        public int MaxRequestsPerSecond { get; set; }
        public int MaxConcurrentRequests { get; set; }
        public int BurstCapacity { get; set; }
        public string RedisBucketKey { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public Tenant? Tenant { get; set; }
        public ICollection<Connection> Connections { get; set; } = new List<Connection>();
    }
}
