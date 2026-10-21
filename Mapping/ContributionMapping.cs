// Hand-written mapping from the Contribution entity to its response DTO.
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Mapping;

public static class ContributionMapping
{
    public static ContributionResponse ToResponse(this Contribution c)
        => new(c.Id, c.StokvelId, c.UserId, c.ContributionCycleId, c.Amount, c.RecordedAt,
               (long)decimal.Round(c.Amount * 100m, MidpointRounding.AwayFromZero), c.Status);
}