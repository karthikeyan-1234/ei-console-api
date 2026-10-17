using _04_EI_Console.Shared.Application.Contracts;

namespace EI_Console.Application.Interfaces.Services;

public interface IChatService
{
    Task<List<ChatSessionDto>> ListSessionsAsync(CancellationToken ct = default);
    Task<ChatSessionDetailDto?> GetSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<ChatSessionDto> CreateSessionAsync(CreateChatSessionRequest request, CancellationToken ct = default);
    Task<ChatSessionDto?> RenameSessionAsync(Guid sessionId, RenameChatSessionRequest request, CancellationToken ct = default);
    Task<SendChatMessageResponse?> SendMessageAsync(Guid sessionId, SendChatMessageRequest request, CancellationToken ct = default);
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken ct = default);
}