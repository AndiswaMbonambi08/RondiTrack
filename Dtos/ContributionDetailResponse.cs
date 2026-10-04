namespace RondiTrack.Dtos;

public record ContributionDetailResponse(Guid Id, Guid UserId, string UserFullName, decimal Amount, DateTime RecordedAt);