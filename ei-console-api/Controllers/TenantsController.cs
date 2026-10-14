using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ei_console_api.Controllers
{
    [ApiController]
    [Route("api/ei/tenants")]
    public sealed class TenantsController : ControllerBase
    {
        private readonly ITenantService _service;

        public TenantsController(ITenantService service) => _service = service;

        [HttpGet]
        public Task<List<TenantDto>> List(CancellationToken ct) =>
            _service.ListAsync(ct);

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TenantDto>> Get(int id, CancellationToken ct)
        {
            var dto = await _service.GetAsync(id, ct);
            return dto is null ? NotFound() : dto;
        }

        [HttpPost]
        public async Task<ActionResult<TenantDto>> Create(
            [FromBody] CreateTenantRequest request, CancellationToken ct)
        {
            var dto = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<TenantDto>> Update(
            int id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
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

        [HttpPost("{id:int}/activate")]
        public async Task<ActionResult<TenantDto>> Activate(int id, CancellationToken ct)
        {
            var dto = await _service.SetActiveAsync(id, active: true, ct);
            return dto is null ? NotFound() : dto;
        }

        [HttpPost("{id:int}/deactivate")]
        public async Task<ActionResult<TenantDto>> Deactivate(int id, CancellationToken ct)
        {
            var dto = await _service.SetActiveAsync(id, active: false, ct);
            return dto is null ? NotFound() : dto;
        }
    }
}
