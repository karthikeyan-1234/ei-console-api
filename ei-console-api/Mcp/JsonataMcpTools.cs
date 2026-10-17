using System.ComponentModel;

using Microsoft.Extensions.AI;

using ModelContextProtocol.Server;

namespace ei_console_api.Mcp;

[McpServerToolType]
public sealed class JsonataMcpTools
{
    private readonly IChatClient _chat;

    public JsonataMcpTools(IChatClient chat)
    {
        _chat = chat;
    }

    [McpServerTool, Description("Generate a JSONata expression that transforms inputJson to targetJson")]
    public async Task<string> GenerateJsonata(
        [Description("The source JSON payload")] string inputJson,
        [Description("The target JSON payload")] string targetJson,
        CancellationToken ct = default)
    {
        var prompt = "You are an expert in JSONata. Write a JSONata expression that transforms the input JSON into the target JSON shape.\n\n" +
                     "Rules:\n" +
                     "- Output ONLY the JSONata expression. No markdown, no explanations.\n\n" +
                     "Example 1:\n" +
                     "Input: {\"first\": \"John\", \"last\": \"Doe\"}\n" +
                     "Target: {\"fullName\": \"John Doe\"}\n" +
                     "Output: first & \" \" & last\n\n" +
                     "Example 2:\n" +
                     "Input: {\"items\": [{\"id\": 1}, {\"id\": 2}]}\n" +
                     "Target: {\"ids\": [1, 2]}\n" +
                     "Output: items.id\n\n" +
                     "Example 3:\n" +
                     "Input: {\"user\": {\"name\": \"Alice\", \"age\": 30}}\n" +
                     "Target: {\"profile\": {\"name\": \"Alice\"}}\n" +
                     "Output: {\"profile\": {\"name\": user.name}}\n\n" +
                     "Now do this:\n" +
                     $"Input: {inputJson}\n" +
                     $"Target: {targetJson}\n" +
                     "Output:";

        var response = await _chat.GetResponseAsync(prompt, new ChatOptions { MaxOutputTokens = 500 }, ct);
        var expr = response.Text?.Trim() ?? "$";

        if (expr.StartsWith("```jsonata")) expr = expr.Substring(10);
        if (expr.StartsWith("```")) expr = expr.Substring(3);
        if (expr.EndsWith("```")) expr = expr.Substring(0, expr.Length - 3);

        return expr.Trim();
    }
}