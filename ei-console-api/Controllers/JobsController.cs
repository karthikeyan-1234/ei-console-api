using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ei_console_api.Controllers
{
    [ApiController]
    [Route("api/ei/jobs")]
    public sealed class JobsController : ControllerBase
    {
        private readonly IJobService _service;

        public JobsController(IJobService service) => _service = service;

        [HttpGet]
        public Task<List<JobSummaryDto>> List(CancellationToken ct) =>
            _service.ListAsync(ct);

        [HttpGet("{id:int}")]
        public async Task<ActionResult<JobDto>> Get(int id, CancellationToken ct)
        {
            var dto = await _service.GetAsync(id, ct);
            return dto is null ? NotFound() : dto;
        }

        [HttpPost]
        public async Task<ActionResult<JobDto>> Create(
            [FromBody] CreateJobRequest request, CancellationToken ct)
        {
            var dto = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<JobDto>> Update(
            int id, [FromBody] UpdateJobRequest request, CancellationToken ct)
        {
            var dto = await _service.UpdateAsync(id, request, ct);
            return dto is null ? NotFound() : dto;
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var ok = await _service.DeleteAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }

        [HttpPut("{id:int}/pipeline")]
        public async Task<ActionResult<JobDto>> SavePipeline(
            int id, [FromBody] SavePipelineRequest request, CancellationToken ct)
        {
            var dto = await _service.SavePipelineAsync(id, request, ct);
            return dto is null ? NotFound() : dto;
        }
    }
}
