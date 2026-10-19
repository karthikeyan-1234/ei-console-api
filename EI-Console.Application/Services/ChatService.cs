using _04_EI_Console.Shared.Application;
using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.AI.Configuration;
using EI_Console.AI.Orchestration;
using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;
using EI_Console.Shared.Application;
using EI_Console.Shared.Contracts;

using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EI_Console.Application.Services;

public sealed class ChatService : IChatService
{
    private readonly IChatRepository _repo;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IUnitOfWork _uow;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly AiOptions _ai;

    public ChatService(
        IChatRepository repo,
        IAgentOrchestrator orchestrator,
        IUnitOfWork uow,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IOptions<AiOptions> ai)
    {
        _repo = repo;
        _orchestrator = orchestrator;
        _uow = uow;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _ai = ai.Value;
    }

    private int TenantId => _tenantContext.TenantId ?? 1;
    private int UserId => _currentUser.UserId != 0 ? _currentUser.UserId : 1;

    public async Task<List<ChatSessionDto>> ListSessionsAsync(CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(TenantId, UserId, ct);
        return rows.Select(ToSessionDto).ToList();
    }

    public async Task<ChatSessionDetailDto?> GetSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _repo.GetAsync(sessionId, ct);
        if (session is null) return null;

        var messages = await _repo.GetMessagesAsync(sessionId, ct);
        return new ChatSessionDetailDto(
            ToSessionDto(session),
            messages.Select(ToMessageDto).ToList());
    }

    public async Task<ChatSessionDto> CreateSessionAsync(CreateChatSessionRequest request, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSession
        {
            SessionId = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = UserId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "New chat" : request.Title.Trim(),
            MessageCount = 0,
            LastMessageAt = now,
            CreatedAt = now,
        };
        _repo.AddSession(session);
        await _uow.SaveChangesAsync(ct);
        return ToSessionDto(session);
    }

    public async Task<ChatSessionDto?> RenameSessionAsync(Guid sessionId, RenameChatSessionRequest request, CancellationToken ct = default)
    {
        var session = await _repo.GetAsync(sessionId, ct);
        if (session is null) return null;

        session.Title = request.Title;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        await _uow.SaveChangesAsync(ct);
        return ToSessionDto(session);
    }

    public async Task<SendChatMessageResponse?> StreamMessageAsync(
        Guid sessionId,
        SendChatMessageRequest request,
        Func<string, Task> onToken,
        CancellationToken ct = default)
    {
        var session = await _repo.GetAsync(sessionId, ct);
        if (session is null) return null;

        var history = await _repo.GetMessagesAsync(sessionId, ct);
        var nextSeq = history.Count == 0 ? 1 : history.Max(m => m.SequenceNumber) + 1;

        var userMessage = new ChatMessage
        {
            SessionId = sessionId,
            SequenceNumber = nextSeq,
            Role = ChatRole.User,
            Content = request.Content,
            Status = ChatMessageStatus.Sent,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _repo.AddMessage(userMessage);
        await _uow.SaveChangesAsync(ct);

        // Call the Orchestrator instead of raw IChatClient
        var sb = new StringBuilder();
        await foreach (var update in _orchestrator.StreamAsync(
            request.Content,
            sessionId.ToString(),   // conversationId: keys the shared ChatHistory
            ct))
        {
            if (!string.IsNullOrEmpty(update))
            {
                sb.Append(update);
                await onToken(update);
            }
        }

        var assistantMessage = new ChatMessage
        {
            SessionId = sessionId,
            SequenceNumber = nextSeq + 1,
            Role = ChatRole.Assistant,
            Content = sb.ToString(),
            Model = _ai.Model,
            Status = ChatMessageStatus.Sent,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _repo.AddMessage(assistantMessage);

        session.MessageCount += 2;
        session.LastMessageAt = DateTimeOffset.UtcNow;
        session.LastMessagePreview = sb.ToString().Length > 120
            ? sb.ToString()[..120] + "…"
            : sb.ToString();

        await _uow.SaveChangesAsync(ct);

        return new SendChatMessageResponse(
            ToMessageDto(userMessage),
            ToMessageDto(assistantMessage),
            ToSessionDto(session));
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _repo.GetAsync(sessionId, ct);
        if (session is null) return false;
        _repo.RemoveSession(session);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    private static ChatSessionDto ToSessionDto(ChatSession s) => new(
        s.SessionId.ToString(), s.Title, s.MessageCount,
        s.LastMessageAt.ToString("o"), s.LastMessagePreview, s.CreatedAt.ToString("o"));

    private static ChatMessageDto ToMessageDto(ChatMessage m) => new(
        m.MessageId.ToString(), m.SessionId.ToString(), m.SequenceNumber,
        m.Role.ToString(), m.Content, m.Model, m.Status.ToString(), m.CreatedAt.ToString("o"));
}