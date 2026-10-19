namespace EI_Console.AI.Agents;

using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

using ModelContextProtocol.Client;

public interface IMcpKernelPluginBuilder
{
    Task<KernelPlugin> BuildAsync(
        McpClient mcpClient,
        IEnumerable<string> toolNames,
        string pluginName,
        CancellationToken ct = default);
}

public sealed class McpKernelPluginBuilder : IMcpKernelPluginBuilder
{
    public async Task<KernelPlugin> BuildAsync(
        McpClient mcpClient,
        IEnumerable<string> toolNames,
        string pluginName,
        CancellationToken ct = default)
    {
        var allTools = await mcpClient.ListToolsAsync(cancellationToken: ct);
        var nameSet = toolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // mcpClient must stay alive while the agent runs: these functions call back through it.
        var functions = allTools
            .Where(t => nameSet.Contains(t.Name))
            .Select(Wrap)
            .ToList();

        return KernelPluginFactory.CreateFromFunctions(pluginName, functions);
    }

    private static KernelFunction Wrap(McpClientTool tool)
    {
        // Build SK parameter metadata from the tool's JSON schema so the model
        // sees the real parameter names, descriptions and types.
        var parameters = new List<KernelParameterMetadata>();
        var schema = tool.JsonSchema;

        if (schema.ValueKind == JsonValueKind.Object)
        {
            var required = new HashSet<string>(StringComparer.Ordinal);
            if (schema.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array)
                foreach (var r in req.EnumerateArray())
                    if (r.GetString() is { } name) required.Add(name);

            if (schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in props.EnumerateObject())
                {
                    parameters.Add(new KernelParameterMetadata(p.Name)
                    {
                        Description = p.Value.TryGetProperty("description", out var d) ? d.GetString() : null,
                        IsRequired = required.Contains(p.Name),
                        Schema = KernelJsonSchema.Parse(p.Value.GetRawText())
                    });
                }
            }
        }

        Func<KernelArguments, CancellationToken, Task<string>> invoke = async (args, token) =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var kv in args)
                if (kv.Value is not null) dict[kv.Key] = kv.Value;

            var result = await tool.InvokeAsync(new AIFunctionArguments(dict), token);

            return result switch
            {
                null => string.Empty,
                string s => s,
                JsonElement je => je.GetRawText(),
                _ => result.ToString() ?? string.Empty
            };
        };

        return KernelFunctionFactory.CreateFromMethod(
            method: invoke,
            functionName: tool.Name,
            description: tool.Description,
            parameters: parameters,
            returnParameter: new KernelReturnParameterMetadata { Description = "Tool result" });
    }
}