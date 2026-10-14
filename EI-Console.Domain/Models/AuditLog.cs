using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class AuditLog
    {
        public long AuditId { get; set; }
        public int? TenantId { get; set; }
        public string Action { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public string EntityId { get; set; } = null!;
        public int? PerformedByUserId { get; set; }
        public string? DetailsJson { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Tenant? Tenant { get; set; }
        public User? PerformedByUser { get; set; }
    }
}
