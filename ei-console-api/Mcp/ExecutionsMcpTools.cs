using System.ComponentModel;

using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;

using ModelContextProtocol.Server;

namespace ei_console_api.Mcp;

[McpServerToolType]
public sealed class ExecutionsMcpTools
{
    private readonly IExecutionService _executions;
    private readonly IScatterGatherItemRepository _scatterItems;

    public ExecutionsMcpTools(IExecutionService executions, IScatterGatherItemRepository scatterItems)
    {
        _executions = executions;
        _scatterItems = scatterItems;
    }

    [McpServerTool, Description("List recent executions")]
    public Task<List<ExecutionSummaryDto>> ListExecutions(
        [Description("Maximum number of executions to return")] int take = 50,
        CancellationToken ct = default)
        => _executions.ListAsync(take, ct);

    [McpServerTool, Description("Get execution details by ID")]
    public Task<ExecutionDetailDto?> GetExecution(Guid id, CancellationToken ct = default)
        => _executions.GetAsync(id, ct);

    [McpServerTool, Description("Get scatter/gather items for an execution")]
    public async Task<List<ScatterGatherItemDto>> GetScatterItems(Guid executionId, CancellationToken ct = default)
    {
        var rows = await _scatterItems.GetByExecutionAsync(executionId, ct);
        return rows.Select(r => new ScatterGatherItemDto(
            r.ItemId, r.ExecutionId, r.TaskId, r.Task?.TaskName ?? $"Task {r.TaskId}",
            r.ParentTaskId, r.ItemIndex, r.ItemKey, r.Status.ToString(), r.HttpStatusCode,
            r.AttemptCount, r.InputPayload, r.OutputPayload, r.ErrorMessage,
            r.DispatchedAt, r.CompletedAt,
            r.DispatchedAt.HasValue && r.CompletedAt.HasValue
                ? (long)(r.CompletedAt.Value - r.DispatchedAt.Value).TotalMilliseconds : null
        )).ToList();
    }
}