using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class ChatSession
    {
        public Guid SessionId { get; set; }
        public int TenantId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = null!;
        public DateTimeOffset LastMessageAt { get; set; }
        public string? LastMessagePreview { get; set; }
        public int MessageCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public Tenant Tenant { get; set; } = null!;
        public User User { get; set; } = null!;
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
