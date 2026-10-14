// The service layer's contract: the two operations that involve a decision spanning
// more than one entity: adding a member, and recording a contribution idempotently.
using RondiTrack.Dtos;

namespace RondiTrack.Services;

public interface IStokvelService
{
    Task<StokvelResponse> AddMemberAsync(Guid stokvelId, Guid userId);

    // Kept so existing callers and unit tests compile unchanged. The key is scoped to request.UserId.
    Task<ContributionResponse> RecordContributionAsync(Guid stokvelId, RecordContributionRequest request, string idempotencyKey);

    // The key is scoped to the signed-in caller. Replayed is true when the stored response was returned.
    Task<(ContributionResponse Response, bool Replayed)> RecordContributionWithReplayAsync(
        Guid callerId, Guid stokvelId, RecordContributionRequest request, string idempotencyKey);
}