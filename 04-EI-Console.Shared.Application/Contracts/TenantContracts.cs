using System;
using System.Collections.Generic;
using System.Text;

namespace _04_EI_Console.Shared.Application.Contracts
{
    public sealed record TenantDto(
        string Id,
        string Code,
        string Name,
        string? CoreUrl,
        string? CoreDbRef,
        bool Active,
        TenantCountsDto Counts);

    public sealed record TenantCountsDto(
        int Jobs,
        int Connections,
        int AuthProfiles,
        int Watermarks);

    public sealed record CreateTenantRequest(
        string Code,
        string Name,
        string? CoreUrl,
        string? CoreDbRef,
        bool Active);

    public sealed record UpdateTenantRequest(
        string Name,
        string? CoreUrl,
        string? CoreDbRef,
        bool Active);
}
