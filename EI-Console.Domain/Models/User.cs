using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string KeycloakSubject { get; set; } = null!;
        public string? Email { get; set; }
        public string DisplayName { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
        public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    }
}
