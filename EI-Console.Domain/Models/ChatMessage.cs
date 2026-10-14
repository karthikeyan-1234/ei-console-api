using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class ChatMessage
    {
        public long MessageId { get; set; }
        public Guid SessionId { get; set; }
        public int SequenceNumber { get; set; }
        public ChatRole Role { get; set; }
        public string Content { get; set; } = null!;
        public string? Model { get; set; }
        public ChatMessageStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public ChatSession Session { get; set; } = null!;
    }
}
