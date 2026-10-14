using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class Tenant
    {
        public int TenantId { get; set; }
        public string TenantCode { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? CoreApiBaseUrl { get; set; }
        public string? CoreDbConnectionSecretRef { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
        public ICollection<Connection> Connections { get; set; } = new List<Connection>();
        public ICollection<AuthProfile> AuthProfiles { get; set; } = new List<AuthProfile>();
        public ICollection<StoredCredential> StoredCredentials { get; set; } = new List<StoredCredential>();
        public ICollection<RateLimitPolicy> RateLimitPolicies { get; set; } = new List<RateLimitPolicy>();
        public ICollection<IntegrationJob> IntegrationJobs { get; set; } = new List<IntegrationJob>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public ICollection<OutboxMessage> OutboxMessages { get; set; } = new List<OutboxMessage>();
        public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    }
}
