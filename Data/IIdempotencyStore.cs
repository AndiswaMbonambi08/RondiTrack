// Contract for the idempotency store: remembers which key produced which request/response
// pair, so a repeated request can be recognised instead of processed twice.
using RondiTrack.Dtos;

namespace RondiTrack.Data;

public record IdempotencyRecord(RecordContributionRequest Request, ContributionResponse Response);

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(string key);
    Task SaveAsync(string key, IdempotencyRecord record);
}