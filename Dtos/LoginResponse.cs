namespace RondiTrack.Dtos;

public record LoginResponse(string AccessToken, int ExpiresInSeconds);