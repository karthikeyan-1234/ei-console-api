using EI_Console.Shared.Contracts;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Tenancy;
/// <summary>
/// Resolves the active tenant. Order of precedence:
///   1. X-Tenant-Id header (set by the console's tenant selector)
///   2. EI:DefaultTenantId config value (dev fallback)
///   3. null (unscoped)
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private const string HeaderName = "X-Tenant-Id";
    private readonly IHttpContextAccessor _accessor;
    private readonly int? _defaultTenantId;

    public TenantContext(IHttpContextAccessor accessor, IConfiguration config)
    {
        _accessor = accessor;
        var raw = config["EI:DefaultTenantId"];
        _defaultTenantId = int.TryParse(raw, out var v) ? v : null;
    }

    public int? TenantId
    {
        get
        {
            var header = _accessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
            if (int.TryParse(header, out var fromHeader)) return fromHeader;
            return _defaultTenantId;
        }
    }

    public bool IsUnrestrictedAccess => false;
}
