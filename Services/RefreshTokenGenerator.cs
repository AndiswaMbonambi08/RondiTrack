using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace RondiTrack.Services;

public static class RefreshTokenGenerator
{
    // 32 random bytes = 256 bits. Not a JWT: the token means nothing by itself, it's only a key into the table.
    public static string Generate() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    // A plain SHA-256 is enough here: the input is 256 bits of randomness, so there is nothing to
    // brute-force, and a plain hash lets us look the token up by an indexed column.
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}