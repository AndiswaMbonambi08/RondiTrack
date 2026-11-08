namespace RondiTrack.Domain;

// Remembers one close request by its Idempotency-Key (scoped to caller and cycle), so a repeat gets the first result.
public class CloseRequest
{
    public string Key { get; private set; } = "";
    public Guid CycleId { get; private set; }
    public string ResponseJson { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }

    private CloseRequest() { }

    public CloseRequest(string key, Guid cycleId, string responseJson, DateTime createdAtUtc)
    {
        Key = key;
        CycleId = cycleId;
        ResponseJson = responseJson;
        CreatedAtUtc = createdAtUtc;
    }
}