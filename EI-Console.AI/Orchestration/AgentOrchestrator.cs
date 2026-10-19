using EI_Console.AI.Agents;
using EI_Console.AI.Configuration;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Win32;

using ModelContextProtocol.Client;

using System.ClientModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EI_Console.AI.Orchestration;



public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IChatClient _chat;

    private readonly IMcpToolRegistry _registry;
    private readonly IEiAgentFactory _factory;
    private readonly IConversationStore _conversations;
    private readonly AiOptions _options;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IChatClient chat,
        IMcpToolRegistry registry,
        IEiAgentFactory factory,
        IConversationStore conversations,
        IOptions<AiOptions> options,
        ILogger<AgentOrchestrator> logger)
    {
        _chat = chat;
        _registry = registry;
        _factory = factory;
        _conversations = conversations;
        _options = options.Value;
        _logger = logger;
    }
    public async IAsyncEnumerable<string> StreamAsync(
            string userMessage,
            string conversationId,
            [EnumeratorCancellation] CancellationToken ct = default)
    {
        // ── 1. Shared history — survives domain switches ──────────────────
        var history = _conversations.GetOrCreate(conversationId);

        // ── 2. Semantic routing ───────────────────────────────────────────
        string context = BuildRoutingContext(userMessage, history);
        var selection = await _registry.GetRelevantToolsAsync(context, topK: 4, ct);
        var definition = EiAgentDefinition.ForDomain(selection.Domain);

        _logger.LogInformation(
            "[Conv={ConvId}] Domain={Domain} Tools=[{Tools}]",
            conversationId,
            selection.Domain,
            string.Join(", ", selection.ToolNames));

        // ── 3. Scoped MCP session — must outlive the streaming loop ───────
        //    McpClientTool instances (created inside BuildAsync) hold a reference
        //    to this client. Disposing early = dead tool handles = silent failures.
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        { Endpoint = new Uri(_options.McpEndpoint) });

        await using var mcpClient = await McpClient.CreateAsync(
            transport, cancellationToken: ct);

        // ── 4. Build the agent for this domain ────────────────────────────
        var agent = await _factory.CreateAsync(
            definition, mcpClient, selection.ToolNames, ct);

        // ── 5. Thread backed by shared history ───────────────────────────
        //    The thread reads from + appends to history, so the next agent
        //    invocation (even a different domain) sees all prior turns.
        var thread = new ChatHistoryAgentThread(history);

        // ── 6. Stream ─────────────────────────────────────────────────────
        //    SK's FunctionChoiceBehavior.Auto handles the tool-calling loop.
        //    mcpClient above must stay alive until this loop completes.
        await foreach (var item in agent.InvokeStreamingAsync(
            userMessage, thread, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(item.Message.Content))
                yield return item.Message.Content;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string BuildRoutingContext(
        string current,
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory history)
    {
        if (history.Count == 0) return current;

        var recent = history
            .Where(m => m.Role == AuthorRole.User)
            .TakeLast(2)
            .Select(m => m.Content ?? "");

        return string.Join(" ", recent) + " " + current;
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