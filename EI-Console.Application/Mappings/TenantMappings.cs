using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Mappings
{
    public static class TenantMappings
    {
        public static TenantDto ToDto(
            this Tenant t,
            int jobCount,
            int connectionCount,
            int authProfileCount,
            int watermarkCount)
        {
            return new TenantDto(
                Id: t.TenantId.ToString(),
                Code: t.TenantCode,
                Name: t.Name,
                CoreUrl: t.CoreApiBaseUrl,
                CoreDbRef: t.CoreDbConnectionSecretRef,
                Active: t.IsActive,
                Counts: new TenantCountsDto(
                    Jobs: jobCount,
                    Connections: connectionCount,
                    AuthProfiles: authProfileCount,
                    Watermarks: watermarkCount));
        }
    }
}
