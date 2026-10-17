using EI_Console.Domain.Models;

namespace EI_Console.Application.Interfaces.Infrastructure;

public interface IChatRepository
{
    Task<List<ChatSession>> ListAsync(int tenantId, int userId, CancellationToken ct = default);
    Task<ChatSession?> GetAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<ChatMessage>> GetMessagesAsync(Guid sessionId, CancellationToken ct = default);
    void AddSession(ChatSession session);
    void RemoveSession(ChatSession session);
    void AddMessage(ChatMessage message);
}