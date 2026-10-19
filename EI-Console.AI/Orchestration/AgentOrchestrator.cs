using EI_Console.AI.Configuration;

using Microsoft.Extensions.AI;

using ModelContextProtocol.Client;

using System.ClientModel;
using System.ComponentModel;

namespace EI_Console.AI.Orchestration;

public interface IAgentOrchestrator
{
    IAsyncEnumerable<string> StreamAsync(string userMessage, CancellationToken ct = default);
}

public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IChatClient _chat;
    private readonly AiOptions _options;

    public AgentOrchestrator(IChatClient chat, Microsoft.Extensions.Options.IOptions<AiOptions> options)
    {
        _chat = chat;
        _options = options.Value;
    }

    public async IAsyncEnumerable<string> StreamAsync(string userMessage, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. Connect to MCP Server and fetch tools
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(_options.McpEndpoint)
        });
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);
        var allTools = await client.ListToolsAsync(cancellationToken: ct);

        // 2. Define Agent Tool Subsets
        var builderTools = allTools.Where(t =>
            t.Name.ToUpper().Contains("Job".ToUpper()) || t.Name.ToUpper().Contains("Pipeline".ToUpper()) || t.Name.ToUpper().Contains("Jsonata".ToUpper()) || t.Name.ToUpper().Contains("Catalog".ToUpper())).ToList();

        var operatorTools = allTools.Where(t =>
            t.Name.ToUpper().Contains("Execution".ToUpper()) || t.Name.ToUpper().Contains("Trigger".ToUpper()) || t.Name.ToUpper().Contains("Scatter".ToUpper())).ToList();

        // 3. Route to Agent (The Classifier)
        var targetAgent = await ClassifyIntentAsync(userMessage, ct);

        // 4. Build the specific ChatOptions and Prompt for the chosen Agent
        // Notice the explicit casting to AITool, and the harsh anti-bash instructions
        List<AITool> toolsToUse;
        string systemPrompt;

        switch (targetAgent)
        {
            case "Builder":
                systemPrompt = "You are the EI Console Builder. You help users list, create, and configure jobs, pipelines, and tasks. " +
                               "CRITICAL: Do NOT output bash scripts, PowerShell, or markdown code blocks to run tools. " +
                               "You must natively invoke the provided tools to get data. " +
                               "When asked to list jobs, invoke the ListJobs tool immediately.";
                toolsToUse = builderTools.Cast<AITool>().ToList();
                break;

            case "Operator":
                systemPrompt = "You are the EI Console Operator. You help users trigger jobs, check execution status, and view logs. " +
                               "CRITICAL: Do NOT output bash scripts or fake commands. You must natively invoke the provided tools.";
                toolsToUse = operatorTools.Cast<AITool>().ToList();
                break;

            default:
                systemPrompt = "You are the EI Console Assistant. Answer general questions about the system.";
                toolsToUse = new List<AITool>();
                break;
        }

        var chatOptions = new ChatOptions
        {
            ModelId = _options.Model,
            MaxOutputTokens = _options.MaxOutputTokens,
            Tools = toolsToUse,
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage)
        };

        // 5. Execute the tool-calling loop
        var toolClient = _chat.AsBuilder().UseFunctionInvocation().Build();

        await foreach (var update in toolClient.GetStreamingResponseAsync(messages, chatOptions, ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }



    private async Task<string> ClassifyIntentAsync(string userMessage, CancellationToken ct)
    {
        var prompt = $"""
            Classify the user's intent into exactly one word: 'Builder', 'Operator', or 'General'.
            - Builder: Wants to list, view, create, edit, or configure jobs, pipelines, or tasks. Or generate a JSONata expression.
            - Operator: Wants to trigger/run a job, or check the status/logs of an execution.
            - General: Asking a general question about how the system works, or greeting.
            
            User: "{userMessage}"
            Output:
            """;

        var response = await _chat.GetResponseAsync(prompt, new ChatOptions { MaxOutputTokens = 10 }, ct);
        var intent = response.Text?.Trim() ?? "General";

        if (intent.Contains("Builder", StringComparison.OrdinalIgnoreCase)) return "Builder";
        if (intent.Contains("Operator", StringComparison.OrdinalIgnoreCase)) return "Operator";
        return "General";
    }
}