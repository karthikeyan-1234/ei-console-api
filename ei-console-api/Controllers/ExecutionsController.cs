using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Pipeline;

using Microsoft.AspNetCore.Mvc;

using Wolverine;

namespace ei_console_api.Controllers
{
    [ApiController]
    [Route("api/ei/executions")]
    public sealed class ExecutionsController : ControllerBase
    {
        private readonly IExecutionService _service;
        private readonly IMessageBus _bus;

        public ExecutionsController(IExecutionService service, IMessageBus bus)
        {
            _service = service;
            _bus = bus;
        }

        [HttpGet]
        public Task<List<ExecutionSummaryDto>> List([FromQuery] int take = 50, CancellationToken ct = default) =>
            _service.ListAsync(take, ct);

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ExecutionDetailDto>> Get(Guid id, CancellationToken ct)
        {
            var dto = await _service.GetAsync(id, ct);
            return dto is null ? NotFound() : dto;
        }

        [HttpPost("trigger/{jobId:int}")]
        public async Task<ActionResult<TriggerJobResponse>> Trigger(
            int jobId, CancellationToken ct)
        {
            var executionId = await _service.TriggerAsync(jobId, "Manual", null, null, ct);

            // Fire-and-forget via Wolverine's local queue.
            await _bus.PublishAsync(new RunJobCommand(executionId, Payload: null));

            return Accepted(new TriggerJobResponse(
                executionId.ToString(),
                $"/api/ei/executions/{executionId}"));
        }
    }
}
