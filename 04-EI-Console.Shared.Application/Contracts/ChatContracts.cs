namespace _04_EI_Console.Shared.Application.Contracts;

public sealed record ChatSessionDto(
    string Id,
    string Title,
    int MessageCount,
    string LastMessageAt,
    string? LastMessagePreview,
    string CreatedAt);

public sealed record ChatMessageDto(
    string Id,
    string SessionId,
    int SequenceNumber,
    string Role,
    string Content,
    string? Model,
    string Status,
    string CreatedAt);

public sealed record CreateChatSessionRequest(string? Title);

public sealed record SendChatMessageRequest(string Content);

public sealed record SendChatMessageResponse(
    ChatMessageDto UserMessage,
    ChatMessageDto AssistantMessage,
    ChatSessionDto Session);

public sealed record ChatSessionDetailDto(
    ChatSessionDto Session,
    IReadOnlyList<ChatMessageDto> Messages);

public sealed record RenameChatSessionRequest(string Title);