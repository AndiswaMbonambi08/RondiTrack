// Database-backed idempotency store. SaveAsync only ADDS to the DbContext: SaveChangesMiddleware
// commits it in the same transaction as the contribution it protects, so a crash can never
// leave a recorded contribution without its key (or the reverse). Two concurrent requests
// with the same key collide on the primary key and the loser gets the central 409.
using System.Text.Json;
using RondiTrack.Dtos;
using RondiTrack.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace RondiTrack.Data;

public class EfIdempotencyStore : IIdempotencyStore
{
    private readonly RondiTrackDbContext _db;
    private readonly TimeProvider _time;

    public EfIdempotencyStore(RondiTrackDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<IdempotencyRecord?> GetAsync(string key)
    {
        var entry = await _db.IdempotencyEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Key == key);
        if (entry is null) return null;

        var request = JsonSerializer.Deserialize<RecordContributionRequest>(entry.RequestJson)!;
        var response = JsonSerializer.Deserialize<ContributionResponse>(entry.ResponseJson)!;
        return new IdempotencyRecord(request, response);
    }

    public Task SaveAsync(string key, IdempotencyRecord record)
    {
        _db.IdempotencyEntries.Add(new IdempotencyEntry(
            key,
            JsonSerializer.Serialize(record.Request),
            JsonSerializer.Serialize(record.Response),
            _time.GetUtcNow().UtcDateTime));
        return Task.CompletedTask;
    }
}