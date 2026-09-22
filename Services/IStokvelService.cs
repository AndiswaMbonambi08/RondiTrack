// The service layer's contract: the two operations that involve a decision spanning
// more than one entity — adding a member, and recording a contribution idempotently.
using RondiTrack.Dtos;

namespace RondiTrack.Services;

public interface IStokvelService
{
    Task<StokvelResponse> AddMemberAsync(Guid stokvelId, Guid userId);
    Task<ContributionResponse> RecordContributionAsync(Guid stokvelId, RecordContributionRequest request, string idempotencyKey);
}