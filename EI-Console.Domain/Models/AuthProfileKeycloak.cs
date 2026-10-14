using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class AuthProfileKeycloak
    {
        public int AuthProfileId { get; set; }
        public AuthType AuthType { get; set; } = AuthType.KeycloakAuthCodeExchange;
        public string BaseUrl { get; set; } = null!;
        public string Realm { get; set; } = null!;
        public string ClientId { get; set; } = null!;
        public string? Audience { get; set; }
        public string? Scopes { get; set; }
        public string ClientSecretRef { get; set; } = null!;
        public AuthProfile AuthProfile { get; set; } = null!;
    }
}
