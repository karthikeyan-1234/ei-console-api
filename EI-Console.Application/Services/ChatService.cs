using _04_EI_Console.Shared.Application;
using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.AI.Configuration;
using EI_Console.Application.Interfaces;
using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;
using EI_Console.Shared.Application;
using EI_Console.Shared.Contracts;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

using System.Text;

namespace EI_Console.Application.Services;

using DomainChatMessage = EI_Console.Domain.Models.ChatMessage;
using ChatMessageAI = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Domain.Enums.ChatRole;
using ChatRoleAI = Microsoft.Extensions.AI.ChatRole;

public sealed class ChatService : IChatService
{
    private const int ContextWindowMessages = 10;

    private readonly IChatRepository _repo;
    private readonly IChatClient _chat;
    private readonly IUnitOfWork _uow;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly AiOptions _ai;

    public ChatService(
        IChatRepository repo,
        IChatClient chat,
        IUnitOfWork uow,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IOptions<AiOptions> ai)
    {
        _repo = repo;
        _chat = chat;
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
        return rows.Select(r => ToSessionDto(r)).ToList(); // Fix here
    }



    public async Task<List<ChatMessageDto>> GetMessagesAsync(Guid sessionId, CancellationToken ct = default)
    {
        var rows = await _repo.GetMessagesAsync(sessionId, ct);
        return rows.Select(m => ToMessageDto(m)).ToList(); // Fix here
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

    public async Task<SendChatMessageResponse?> SendMessageAsync(Guid sessionId, SendChatMessageRequest request, CancellationToken ct = default)
    {
        var session = await _repo.GetAsync(sessionId, ct);
        if (session is null) return null;

        var history = await _repo.GetMessagesAsync(sessionId, ct);
        var nextSeq = history.Count == 0 ? 1 : history.Max(m => m.SequenceNumber) + 1;

        var userMessage = new DomainChatMessage
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

        // Build rolling context for the model
        var chatMessages = new List<ChatMessageAI>();
        chatMessages.Add(new ChatMessageAI(ChatRoleAI.System,
            "You are the EI Console assistant. Be concise and precise."));
        foreach (var m in history.TakeLast(ContextWindowMessages))
            chatMessages.Add(new ChatMessageAI(MapRole(m.Role), m.Content));
        chatMessages.Add(new ChatMessageAI(ChatRoleAI.User, request.Content));

        var response = await _chat.GetResponseAsync(chatMessages, new ChatOptions
        {
            ModelId = _ai.Model,
            MaxOutputTokens = _ai.MaxOutputTokens,
        }, ct);

        var assistantText = response.Text ?? string.Empty;

        var assistantMessage = new DomainChatMessage
        {
            SessionId = sessionId,
            SequenceNumber = nextSeq + 1,
            Role = ChatRole.Assistant,
            Content = assistantText,
            Model = _ai.Model,
            Status = ChatMessageStatus.Sent,
            CreatedAt = DateTimeOffset.UtcNow,
        };


        _repo.AddMessage(assistantMessage);

        session.MessageCount += 2;
        session.LastMessageAt = DateTimeOffset.UtcNow;
        session.LastMessagePreview = Truncate(assistantText, 120);

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

    public async Task<ChatSessionDetailDto?> GetSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _repo.GetAsync(sessionId, ct);
        if (session is null) return null;

        var messages = await _repo.GetMessagesAsync(sessionId, ct);
        return new ChatSessionDetailDto(
            ToSessionDto(session),
            messages.Select(ToMessageDto).ToList());
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

    private static ChatRoleAI MapRole(ChatRole role) => role switch
    {
        ChatRole.User => ChatRoleAI.User,
        ChatRole.Assistant => ChatRoleAI.Assistant,
        _ => ChatRoleAI.System,
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static ChatSessionDto ToSessionDto(ChatSession s) => new(
        s.SessionId.ToString(), s.Title, s.MessageCount,
        s.LastMessageAt.ToString("o"), s.LastMessagePreview, s.CreatedAt.ToString("o"));

    private static ChatMessageDto ToMessageDto(DomainChatMessage m) => new(
       m.MessageId.ToString(), m.SessionId.ToString(), m.SequenceNumber,
       m.Role.ToString(), m.Content, m.Model, m.Status.ToString(), m.CreatedAt.ToString("o"));
}