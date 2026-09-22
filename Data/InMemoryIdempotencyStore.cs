// In-memory implementation of the idempotency store. Same spirit as the repositories —
// not a database, just enough to make the guarantee real for this assignment.
using System.Collections.Concurrent;

namespace RondiTrack.Data;

public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _records = new();

    public Task<IdempotencyRecord?> GetAsync(string key)
        => Task.FromResult(_records.GetValueOrDefault(key));

    public Task SaveAsync(string key, IdempotencyRecord record)
    {
        _records[key] = record;
        return Task.CompletedTask;
    }
}