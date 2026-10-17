using System.ComponentModel;

using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Pipeline;

using ModelContextProtocol.Server;

using Wolverine;

namespace ei_console_api.Mcp;

[McpServerToolType]
public sealed class JobsMcpTools
{
    private readonly IJobService _jobs;
    private readonly IExecutionService _executions;
    private readonly IMessageBus _bus;

    public JobsMcpTools(IJobService jobs, IExecutionService executions, IMessageBus bus)
    {
        _jobs = jobs;
        _executions = executions;
        _bus = bus;
    }

    [McpServerTool, Description("List all jobs")]
    public Task<List<JobSummaryDto>> ListJobs(CancellationToken ct = default)
        => _jobs.ListAsync(ct);

    [McpServerTool, Description("Get a job by ID")]
    public Task<JobDto?> GetJob(int id, CancellationToken ct = default)
        => _jobs.GetAsync(id, ct);

    [McpServerTool, Description("Create a new job")]
    public Task<JobDto> CreateJob(
        [Description("Name of the job")] string name,
        [Description("Tenant ID (default '1')")] string tenantId,
        [Description("Trigger type (Manual, Scheduled, Webhook)")] string trigger,
        [Description("Cron expression (required if Scheduled)")] string? cron,
        [Description("Description")] string? description,
        CancellationToken ct = default)
    {
        var req = new CreateJobRequest(name, tenantId, trigger, cron, description);
        return _jobs.CreateAsync(req, ct);
    }

    [McpServerTool, Description("Save the pipeline tasks for a job")]
    public Task<JobDto?> SavePipeline(
        int id,
        [Description("The array of pipeline tasks")] List<PipelineTaskDto> tasks,
        CancellationToken ct = default)
    {
        var req = new SavePipelineRequest(tasks);
        return _jobs.SavePipelineAsync(id, req, ct);
    }

    [McpServerTool, Description("Publish a job to make it active")]
    public Task<JobDto?> PublishJob(int id, CancellationToken ct = default)
        => _jobs.PublishAsync(id, ct);

    [McpServerTool, Description("Trigger a job manually and start execution")]
    public async Task<string> TriggerJob(int id, CancellationToken ct = default)
    {
        var executionId = await _executions.TriggerAsync(id, "Manual", null, null, ct);
        await _bus.PublishAsync(new RunJobCommand(executionId, Payload: null));
        return executionId.ToString();
    }
}