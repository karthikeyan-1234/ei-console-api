using EI_Console.AI.Agents;
using EI_Console.AI.Configuration;
using EI_Console.AI.Orchestration;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenAI;

using System.ClientModel;

namespace EI_Console.AI.DependencyInjection;

public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddEiAi(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AiOptions>(config.GetSection(AiOptions.SectionName));

        services.AddChatClient(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;

            var openAi = new OpenAIClient(
                new ApiKeyCredential(opts.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(opts.Endpoint) });

            return openAi.GetChatClient(opts.Model).AsIChatClient();
        });

        services.AddSingleton<IConversationStore, InMemoryConversationStore>(); // must be singleton
        services.AddSingleton<IMcpKernelPluginBuilder, McpKernelPluginBuilder>();
        services.AddSingleton<IMcpToolRegistry, McpToolRegistry>();
        services.AddScoped<IEiAgentFactory, EiAgentFactory>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();


        return services;
    }
}