using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Services;

using Microsoft.AspNetCore.Mvc;

namespace ei_console_api.Controllers;

[ApiController]
[Route("api/ei/chat/sessions")]
public sealed class ChatController : ControllerBase
{
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOpts = new(System.Text.Json.JsonSerializerDefaults.Web);

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
    public async Task Send(Guid id, [FromBody] SendChatMessageRequest request, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        // 1. Stream tokens
        var result = await _service.StreamMessageAsync(id, request, async (token) =>
        {
            // Use _jsonOpts here!
            var payload = System.Text.Json.JsonSerializer.Serialize(new { type = "token", value = token }, _jsonOpts);
            await Response.WriteAsync($"data: {payload}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }, ct);

        // 2. Send final result
        if (result != null)
        {
            // Use _jsonOpts here!
            var payload = System.Text.Json.JsonSerializer.Serialize(new { type = "result", value = result }, _jsonOpts);
            await Response.WriteAsync($"data: {payload}\n\n", ct);
        }

        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _service.DeleteSessionAsync(id, ct) ? NoContent() : NotFound();


}