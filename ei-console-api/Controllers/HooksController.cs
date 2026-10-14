using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Pipeline;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.Text.Json;

using Wolverine;

namespace ei_console_api.Controllers
{
    [ApiController]
    [Route("api/ei/hooks")]
    public sealed class HooksController : ControllerBase
    {
        private const string IdempotencyHeader = "X-Idempotency-Key";

        private readonly IJobRepository _jobs;
        private readonly IExecutionService _executions;
        private readonly IMessageBus _bus;

        public HooksController(
            IJobRepository jobs,
            IExecutionService executions,
            IMessageBus bus)
        {
            _jobs = jobs;
            _executions = executions;
            _bus = bus;
        }

        [HttpPost("{slug}")]
        public async Task<IActionResult> Receive(
            string slug,
            CancellationToken ct,
            [FromBody] JsonElement? body = null)
        {
            var job = await _jobs.GetBySlugAsync(slug, ct);
            if (job is null) return NotFound(new { error = $"No job with slug '{slug}'" });

            var idempotencyKey = Request.Headers[IdempotencyHeader].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existing = await _executions.FindExecutionByIdempotencyKeyAsync(
                    idempotencyKey, ct);

                if (existing is Guid existingId)
                {
                    return Conflict(new
                    {
                        error = "Duplicate request",
                        executionId = existingId.ToString(),
                        statusUrl = $"/api/ei/executions/{existingId}",
                    });
                }
            }

            var payloadJson = body.HasValue
                ? JsonSerializer.Serialize(body.Value)
                : null;

            var executionId = await _executions.TriggerAsync(
                job.JobId,
                triggerSource: "Webhook",
                idempotencyKey: idempotencyKey,
                payload: payloadJson,
                ct);

            await _bus.PublishAsync(new RunJobCommand(executionId, payloadJson));

            return Accepted(new
            {
                executionId = executionId.ToString(),
                statusUrl = $"/api/ei/executions/{executionId}",
            });
        }
    }
}

