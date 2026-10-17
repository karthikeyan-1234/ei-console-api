using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Models;
using EI_Console.Infrastructure.Persistence;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace EI_Console.Infrastructure.Repositories;

public sealed class ChatRepository : IChatRepository
{
    private readonly EiDbContext _db;
    public ChatRepository(EiDbContext db) => _db = db;

    public Task<List<ChatSession>> ListAsync(int tenantId, int userId, CancellationToken ct = default) =>
        _db.ChatSessions
            .Where(s => s.TenantId == tenantId && s.UserId == userId)
            .OrderByDescending(s => s.LastMessageAt)
            .ToListAsync(ct);

    public Task<ChatSession?> GetAsync(Guid sessionId, CancellationToken ct = default) =>
        _db.ChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

    public Task<List<ChatMessage>> GetMessagesAsync(Guid sessionId, CancellationToken ct = default) =>
        _db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync(ct);

    public void AddSession(ChatSession session) => _db.ChatSessions.Add(session);
    public void RemoveSession(ChatSession session) => _db.ChatSessions.Remove(session);
    public void AddMessage(ChatMessage message) => _db.ChatMessages.Add(message);
}