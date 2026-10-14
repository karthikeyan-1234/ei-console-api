using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ei_console_api.Controllers
{
    [ApiController]
    [Route("api/ei/auth-profiles")]
    public sealed class AuthProfilesController : ControllerBase
    {
        private readonly IAuthProfileService _service;

        public AuthProfilesController(IAuthProfileService service) => _service = service;

        [HttpGet]
        public Task<List<AuthProfileDto>> List(CancellationToken ct) =>
            _service.ListAsync(ct);

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AuthProfileDto>> Get(int id, CancellationToken ct)
        {
            var dto = await _service.GetAsync(id, ct);
            return dto is null ? NotFound() : dto;
        }

        [HttpPost]
        public async Task<ActionResult<AuthProfileDto>> Create(
            [FromBody] CreateAuthProfileRequest request, CancellationToken ct)
        {
            var dto = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<AuthProfileDto>> Update(
            int id, [FromBody] UpdateAuthProfileRequest request, CancellationToken ct)
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

        [HttpPost("{id:int}/test")]
        public async Task<ActionResult<TestAuthResultDto>> Test(int id, CancellationToken ct)
        {
            var result = await _service.TestAsync(id, ct);
            return result;
        }
    }
}
