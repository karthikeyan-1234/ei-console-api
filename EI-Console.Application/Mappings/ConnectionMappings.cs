using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Mappings
{
    public static class ConnectionMappings
    {
        public static ConnectionDto ToDto(this Connection c, string? authProfileName = null)
        {
            return new ConnectionDto(
                Id: c.ConnectionId.ToString(),
                Name: c.Name,
                Provider: c.ProviderName ?? "Unknown",
                Protocol: c.Protocol.ToString(),
                BaseUrl: c.BaseUrl,
                Port: c.Port,
                TenantId: (c.TenantId ?? 0).ToString(),
                AuthProfileId: c.AuthProfileId.ToString(),
                AuthProfileName: authProfileName,
                Status: c.IsActive ? "Active" : "Inactive",
                Timeout: c.TimeoutSeconds,
                Headers: c.DefaultHeadersJson ?? string.Empty);
        }

        public static void Apply(this Connection c, CreateConnectionRequest r)
        {
            c.Name = r.Name.Trim();
            c.ProviderName = r.Provider.Trim();
            c.Protocol = Enum.Parse<Domain.Enums.Protocol>(r.Protocol, ignoreCase: true);
            c.BaseUrl = r.BaseUrl.Trim();
            c.Port = r.Port;
            c.TenantId = int.Parse(r.TenantId);
            c.AuthProfileId = string.IsNullOrWhiteSpace(r.AuthProfileId) ? null : int.Parse(r.AuthProfileId);
            c.TimeoutSeconds = r.Timeout;
            c.DefaultHeadersJson = r.Headers;
        }

        public static void Apply(this Connection c, UpdateConnectionRequest r)
        {
            c.Name = r.Name.Trim();
            c.ProviderName = r.Provider.Trim();
            c.Protocol = Enum.Parse<Domain.Enums.Protocol>(r.Protocol, ignoreCase: true);
            c.BaseUrl = r.BaseUrl.Trim();
            c.Port = r.Port;
            c.AuthProfileId = string.IsNullOrWhiteSpace(r.AuthProfileId) ? null: int.Parse(r.AuthProfileId);
            c.TimeoutSeconds = r.Timeout;
            c.DefaultHeadersJson = r.Headers;
        }
    }
}
