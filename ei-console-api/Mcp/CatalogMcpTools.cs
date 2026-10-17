using System.ComponentModel;

using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Services;

using ModelContextProtocol.Server;

namespace ei_console_api.Mcp;

[McpServerToolType]
public sealed class CatalogMcpTools
{
    private readonly IConnectionService _connections;
    private readonly IAuthProfileService _authProfiles;

    public CatalogMcpTools(IConnectionService connections, IAuthProfileService authProfiles)
    {
        _connections = connections;
        _authProfiles = authProfiles;
    }

    [McpServerTool, Description("List all connections available in the system")]
    public Task<List<ConnectionDto>> ListConnections(CancellationToken ct = default)
        => _connections.ListAsync(ct);

    [McpServerTool, Description("List all auth profiles available in the system")]
    public Task<List<AuthProfileDto>> ListAuthProfiles(CancellationToken ct = default)
        => _authProfiles.ListAsync(ct);
}