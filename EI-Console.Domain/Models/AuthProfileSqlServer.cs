using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class AuthProfileSqlServer
    {
        public int AuthProfileId { get; set; }
        public AuthType AuthType { get; set; } = AuthType.SqlServerConnectionString;
        public CredentialStorageMode CredentialStorageMode { get; set; }
        public string? ConnectionStringSecretRef { get; set; }
        public string? InlineConnectionString { get; set; }
        public int? StoredCredentialId { get; set; }
        public AuthProfile AuthProfile { get; set; } = null!;
        public StoredCredential? StoredCredential { get; set; }
    }
}
