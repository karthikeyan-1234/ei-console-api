namespace EI_Console.AI.Agents;

using Microsoft.SemanticKernel.ChatCompletion;

using System.Collections.Concurrent;

public interface IConversationStore
{
    ChatHistory GetOrCreate(string conversationId);
    void Remove(string conversationId);
}

/// <summary>
/// In-memory store with TTL-based eviction.
/// Replace the backing store with IDistributedCache (Redis) for
/// multi-instance / scale-out deployments.
/// </summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, Slot> _slots = new();
    private readonly TimeSpan _ttl;

    public InMemoryConversationStore(TimeSpan? ttl = null) =>
        _ttl = ttl ?? TimeSpan.FromHours(2);

    public ChatHistory GetOrCreate(string conversationId)
    {
        var slot = _slots.GetOrAdd(conversationId, _ => new Slot(new ChatHistory()));
        slot.Touch();
        Evict();
        return slot.History;
    }

    public void Remove(string conversationId) =>
        _slots.TryRemove(conversationId, out _);

    private void Evict()
    {
        var cutoff = DateTimeOffset.UtcNow - _ttl;
        foreach (var key in _slots.Keys)
            if (_slots.TryGetValue(key, out var s) && s.LastAccessed < cutoff)
                _slots.TryRemove(key, out _);
    }

    private sealed class Slot(ChatHistory history)
    {
        public ChatHistory History { get; } = history;
        public DateTimeOffset LastAccessed { get; private set; } = DateTimeOffset.UtcNow;
        public void Touch() => LastAccessed = DateTimeOffset.UtcNow;
    }
}