// What a caller sends to create or update a Stokvel.
namespace RondiTrack.Dtos;

public record StokvelRequest(string Name, decimal ContributionAmount);