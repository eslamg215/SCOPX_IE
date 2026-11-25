using System.Security.Cryptography;
using System.Text;
namespace SCOPX.Api.Authentication;

/// <summary>Generates high-entropy opaque tokens and hashes them before persistence.</summary>
internal static class SessionToken
{
    public static string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
