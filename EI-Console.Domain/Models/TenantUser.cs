using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class TenantUser
    {
        public int TenantUserId { get; set; }
        public int TenantId { get; set; }
        public int UserId { get; set; }
        public TenantUserRole Role { get; set; }
        public int? GrantedByUserId { get; set; }
        public DateTimeOffset GrantedAt { get; set; }

        public Tenant Tenant { get; set; } = null!;
        public User User { get; set; } = null!;
        public User? GrantedByUser { get; set; }
    }
}
