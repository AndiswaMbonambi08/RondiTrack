// Hand-written mapping from the Stokvel entity to its response DTO.
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Mapping;

public static class StokvelMapping
{
    public static StokvelResponse ToResponse(this Stokvel stokvel)
        => new(stokvel.Id, stokvel.Name, stokvel.ContributionAmount, stokvel.MemberIds.Count);
}