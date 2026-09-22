// Hand-written mapping from the Contribution entity to its response DTO.
// Takes stokvelId separately since Contribution itself doesn't know which stokvel it belongs to.
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Mapping;

public static class ContributionMapping
{
    public static ContributionResponse ToResponse(this Contribution contribution, Guid stokvelId)
        => new(contribution.Id, stokvelId, contribution.UserId, contribution.Cycle, contribution.Amount, contribution.RecordedAt);
}