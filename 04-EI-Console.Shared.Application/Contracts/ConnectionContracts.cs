using System;
using System.Collections.Generic;
using System.Text;

namespace _04_EI_Console.Shared.Application.Contracts
{
    public sealed record ConnectionDto(
       string Id,
       string Name,
       string Provider,
       string Protocol,
       string BaseUrl,
       int? Port,
       string TenantId,
       string? AuthProfileId,
       string? AuthProfileName,
       string Status,
       int Timeout,
       string Headers);

    public sealed record CreateConnectionRequest(
        string Name,
        string Provider,
        string Protocol,
        string BaseUrl,
        int? Port,
        string TenantId,
        string? AuthProfileId,
        int Timeout,
        string Headers);

    public sealed record UpdateConnectionRequest(
        string Name,
        string Provider,
        string Protocol,
        string BaseUrl,
        int? Port,
        string? AuthProfileId,
        int Timeout,
        string Headers);

    public sealed record TestConnectionResultDto(
        bool Ok,
        int LatencyMs,
        int Status,
        string? Message);
}
