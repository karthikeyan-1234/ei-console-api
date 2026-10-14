using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class AuthProfileMtls
    {
        public int AuthProfileId { get; set; }
        public AuthType AuthType { get; set; } = AuthType.MutualTls;
        public string ClientCertRef { get; set; } = null!;
        public string PrivateKeyRef { get; set; } = null!;
        public string? CaCertRef { get; set; }
        public string? PassphraseRef { get; set; }
        public string? Thumbprint { get; set; }
        public AuthProfile AuthProfile { get; set; } = null!;
    }
}
