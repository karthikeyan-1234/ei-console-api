using System.ClientModel;

using EI_Console.AI.Configuration;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenAI;

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

        return services;
    }
}