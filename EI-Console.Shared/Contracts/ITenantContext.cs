using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Shared.Contracts
{
    public interface ITenantContext
    {
        int? TenantId { get; }
        bool IsUnrestrictedAccess { get; }
    }
}
