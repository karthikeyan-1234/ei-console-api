namespace EI_Console.AI.Orchestration;

public interface IAgentOrchestrator
{
    /// <param name="conversationId">
    /// Stable per-session identifier. In SignalR Hubs use Context.ConnectionId.
    /// In REST controllers use a header/query GUID created by the client.
    /// Drives shared ChatHistory so context survives domain switches within a session.
    /// </param>
    IAsyncEnumerable<string> StreamAsync(
        string userMessage,
        string conversationId,
        CancellationToken ct = default);
}