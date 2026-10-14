using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class AuthProfileFtp
    {
        public int AuthProfileId { get; set; }
        public AuthType AuthType { get; set; } = AuthType.FtpCredentials;
        public CredentialStorageMode CredentialStorageMode { get; set; }
        public string Username { get; set; } = null!;
        public string? PasswordSecretRef { get; set; }
        public string? InlinePassword { get; set; }
        public int? StoredCredentialId { get; set; }
        public AuthProfile AuthProfile { get; set; } = null!;
        public StoredCredential? StoredCredential { get; set; }
    }
}
