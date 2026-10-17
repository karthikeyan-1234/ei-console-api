using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Services;

using Microsoft.AspNetCore.Mvc;

namespace ei_console_api.Controllers;

[ApiController]
[Route("api/ei/chat/sessions")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatService _service;
    public ChatController(IChatService service) => _service = service;

    [HttpGet]
    public Task<List<ChatSessionDto>> List(CancellationToken ct) => _service.ListSessionsAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChatSessionDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var res = await _service.GetSessionAsync(id, ct);
        return res is null ? NotFound() : res;
    }

    [HttpPost]
    public Task<ChatSessionDto> Create([FromBody] CreateChatSessionRequest request, CancellationToken ct) =>
        _service.CreateSessionAsync(request, ct);

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ChatSessionDto>> Rename(Guid id, [FromBody] RenameChatSessionRequest request, CancellationToken ct)
    {
        var res = await _service.RenameSessionAsync(id, request, ct);
        return res is null ? NotFound() : res;
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<SendChatMessageResponse>> Send(
        Guid id, [FromBody] SendChatMessageRequest request, CancellationToken ct)
    {
        var res = await _service.SendMessageAsync(id, request, ct);
        return res is null ? NotFound() : res;
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _service.DeleteSessionAsync(id, ct) ? NoContent() : NotFound();
}