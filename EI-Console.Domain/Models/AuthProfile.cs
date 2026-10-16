using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class AuthProfile
    {
        public int AuthProfileId { get; set; }
        public int? TenantId { get; set; }
        public string Name { get; set; } = null!;
        public AuthType AuthType { get; set; }
        public CredentialStorageMode CredentialStorageMode { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public Tenant? Tenant { get; set; }
        public AuthProfileApiKey? ApiKey { get; set; }
        public AuthProfileFtp? Ftp { get; set; }
        public AuthProfileSftp? Sftp { get; set; }
        public AuthProfileSqlServer? SqlServer { get; set; }
        public AuthProfileOAuth2? OAuth2 { get; set; }
        public AuthProfileKeycloak? Keycloak { get; set; }
        public AuthProfileMtls? Mtls { get; set; }
        public AuthProfileWsSecurity? WsSecurity { get; set; }

        public AuthProfileSubscriptionToken? SubscriptionToken { get; set; }
        public ICollection<Connection> Connections { get; set; } = new List<Connection>();
        public ICollection<JobTask> JobTasks { get; set; } = new List<JobTask>();
    }
}
