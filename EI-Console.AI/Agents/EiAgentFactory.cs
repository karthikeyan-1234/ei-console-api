namespace EI_Console.AI.Agents;

using EI_Console.AI.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI; // OpenAIPromptExecutionSettings

using ModelContextProtocol.Client;                  // IMcpClient

public interface IEiAgentFactory
{
    Task<ChatCompletionAgent> CreateAsync(
        EiAgentDefinition definition,
        McpClient mcpClient,
        IEnumerable<string> toolNames,
        CancellationToken ct = default);
}

public sealed class EiAgentFactory : IEiAgentFactory
{
    private readonly IMcpKernelPluginBuilder _pluginBuilder;
    private readonly AiOptions _options;
    ILoggerFactory _loggerFactory;

    public EiAgentFactory(
        IMcpKernelPluginBuilder pluginBuilder,
        IOptions<AiOptions> options,
        ILoggerFactory loggerFactory)
    {
        _pluginBuilder = pluginBuilder;
        _options = options.Value;
        this._loggerFactory = loggerFactory;
    }

    public async Task<ChatCompletionAgent> CreateAsync(
        EiAgentDefinition definition,
        McpClient mcpClient,
        IEnumerable<string> toolNames,
        CancellationToken ct = default)
    {
        // Each agent gets its own isolated Kernel — plugin sets don't bleed across domains.
        //
        // Ollama exposes an OpenAI-compatible /v1 endpoint natively.
        // AddOpenAIChatCompletion is in Microsoft.SemanticKernel.Connectors.OpenAI,
        // which ships bundled inside Microsoft.SemanticKernel — no extra package needed.
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: _options.Model,
                endpoint: new Uri(_options.Endpoint),
                apiKey: _options.ApiKey)
            .Build();

        kernel.FunctionInvocationFilters.Add(
    new ToolLoggingFilter(_loggerFactory.CreateLogger<ToolLoggingFilter>()));

        var toolNamesList = toolNames.ToList();
        if (toolNamesList.Count > 0)
        {
            var plugin = await _pluginBuilder.BuildAsync(
                mcpClient,
                toolNamesList,
                pluginName: $"{definition.Domain}Plugin",
                ct);

            kernel.Plugins.Add(plugin);
        }

        return new ChatCompletionAgent
        {
            Name = definition.AgentName,
            Description = definition.Description,
            Instructions = definition.Instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(
                // OpenAIPromptExecutionSettings has MaxTokens; base class does not.
                new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    MaxTokens = _options.MaxOutputTokens
                })
        };
    }
}