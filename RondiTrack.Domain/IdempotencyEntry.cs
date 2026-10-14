// One remembered Idempotency-Key. Request and response are stored as JSON so the
// store can replay the original response and detect a key reused with a different body.
// The key is the primary key: two concurrent requests with the same key cannot both insert.
namespace RondiTrack.Persistence.Entities;

public class IdempotencyEntry
{
    public string Key { get; private set; } = "";
    public string RequestJson { get; private set; } = "";
    public string ResponseJson { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }

    private IdempotencyEntry() { }

    public IdempotencyEntry(string key, string requestJson, string responseJson, DateTime createdAtUtc)
    {
        Key = key;
        RequestJson = requestJson;
        ResponseJson = responseJson;
        CreatedAtUtc = createdAtUtc;
    }
}