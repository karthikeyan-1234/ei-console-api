using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class AuthProfileApiKey
    {
        public int AuthProfileId { get; set; }
        public AuthType AuthType { get; set; } = AuthType.ApiKey;
        public string HeaderName { get; set; } = null!;
        public string ApiKeyRef { get; set; } = null!;
        public AuthProfile AuthProfile { get; set; } = null!;
    }
}
