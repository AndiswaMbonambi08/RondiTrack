// Hand-written mapping from the Contribution entity to its response DTO.
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Mapping;

public static class ContributionMapping
{
    public static ContributionResponse ToResponse(this Contribution contribution, Guid stokvelId)
        => new(contribution.Id, stokvelId, contribution.UserId, contribution.ContributionCycleId, contribution.Amount, contribution.RecordedAt);
}