namespace EI_Console.AI.Agents;

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

public sealed class ToolLoggingFilter(ILogger<ToolLoggingFilter> logger) : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var name = $"{context.Function.PluginName}.{context.Function.Name}";
        var args = string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}"));
        logger.LogInformation("Tool call {Tool}({Args})", name, args);

        try
        {
            await next(context);
            var result = context.Result?.ToString() ?? "<null>";
            logger.LogInformation("Tool result {Tool}: {Result}",
                name, result.Length > 500 ? result[..500] + "…" : result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool {Tool} failed", name);
            throw;
        }
    }
}