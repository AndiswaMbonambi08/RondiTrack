// Hand-written mapping from the Contribution entity to its response DTO.
// No longer needs a separate stokvelId parameter — Contribution carries its
// own StokvelId now.
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Mapping;

public static class ContributionMapping
{
    public static ContributionResponse ToResponse(this Contribution contribution)
        => new(contribution.Id, contribution.StokvelId, contribution.UserId, contribution.ContributionCycleId, contribution.Amount, contribution.RecordedAt);
}