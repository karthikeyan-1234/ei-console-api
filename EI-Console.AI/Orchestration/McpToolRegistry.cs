namespace EI_Console.AI.Orchestration;

using EI_Console.AI.Configuration;

using Microsoft.Extensions.Options;

using ModelContextProtocol.Client;

public sealed record ToolSelection(string Domain, IReadOnlyList<string> ToolNames);

public interface IMcpToolRegistry
{
    Task<ToolSelection> GetRelevantToolsAsync(string context, int topK, CancellationToken ct = default);
}

public sealed class McpToolRegistry(IOptions<AiOptions> options) : IMcpToolRegistry
{
    private readonly AiOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<(string Name, string Text)>? _cache;

    private static readonly string[] OperatorWords =
        ["trigger", "run ", "execute", "status", "log", "replay", "dlq", "dead-letter", "dead letter", "retry"];
    private static readonly string[] BuilderWords =
        ["list", "show", "create", "edit", "update", "delete", "configure", "job", "pipeline", "task", "catalog", "jsonata"];

    public async Task<ToolSelection> GetRelevantToolsAsync(string context, int topK, CancellationToken ct = default)
    {
        var lower = context.ToLowerInvariant();
        var domain =
            OperatorWords.Any(lower.Contains) ? "Operator" :
            BuilderWords.Any(lower.Contains) ? "Builder" : "General";

        if (domain == "General")
            return new ToolSelection(domain, []);

        var tools = await LoadAsync(ct);
        var tokens = lower.Split([' ', ',', '.', '?', '!', '"'], StringSplitOptions.RemoveEmptyEntries)
                          .Where(t => t.Length > 2).ToHashSet();

        var picked = tools
            .Select(t => (t.Name, Score: tokens.Count(k => t.Text.Contains(k, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Name)
            .ToList();

        return new ToolSelection(domain, picked);
    }

    private async Task<List<(string Name, string Text)>> LoadAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;
        await _gate.WaitAsync(ct);
        try
        {
            if (_cache is not null) return _cache;
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            { Endpoint = new Uri(_options.McpEndpoint) });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);
            var tools = await client.ListToolsAsync(cancellationToken: ct);
            return _cache = tools.Select(t => (t.Name, $"{t.Name} {t.Description}")).ToList();
        }
        finally { _gate.Release(); }
    }
}