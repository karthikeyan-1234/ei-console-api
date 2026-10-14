using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class AuthProfileSftp
    {
        public int AuthProfileId { get; set; }
        public AuthType AuthType { get; set; } = AuthType.SftpKeyCredentials;
        public CredentialStorageMode CredentialStorageMode { get; set; }
        public string Username { get; set; } = null!;
        public string? PrivateKeySecretRef { get; set; }
        public string? PassphraseSecretRef { get; set; }
        public string? InlinePrivateKey { get; set; }
        public string? InlinePassphrase { get; set; }
        public int? StoredCredentialId { get; set; }
        public AuthProfile AuthProfile { get; set; } = null!;
        public StoredCredential? StoredCredential { get; set; }
    }
}
