using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class StoredCredential
    {
        public int StoredCredentialId { get; set; }
        public int? TenantId { get; set; }
        public string Name { get; set; } = null!;
        public StoredCredentialKind Kind { get; set; }
        public string Username { get; set; } = null!;
        public string Secret { get; set; } = null!;
        public string? SecondarySecret { get; set; }
        public string? Notes { get; set; }
        public EncryptionState EncryptionState { get; set; }
        public DateTimeOffset? LastRotatedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public Tenant? Tenant { get; set; }
        public ICollection<AuthProfileFtp> FtpProfiles { get; set; } = new List<AuthProfileFtp>();
        public ICollection<AuthProfileSftp> SftpProfiles { get; set; } = new List<AuthProfileSftp>();
        public ICollection<AuthProfileSqlServer> SqlServerProfiles { get; set; } = new List<AuthProfileSqlServer>();
    }
}
