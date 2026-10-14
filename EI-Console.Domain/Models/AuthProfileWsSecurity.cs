using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class AuthProfileWsSecurity
    {
        public int AuthProfileId { get; set; }
        public AuthType AuthType { get; set; } = AuthType.WsSecurityUsernameToken;
        public string Username { get; set; } = null!;
        public string PasswordRef { get; set; } = null!;
        public string? EndpointUrl { get; set; }
        public AuthProfile AuthProfile { get; set; } = null!;
    }
}
