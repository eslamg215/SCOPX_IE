using System.Security.Cryptography;
using System.Text;

namespace SCOPX.Api.Media;

/// <summary>
/// Versioned authenticated-encryption envelope with key identifiers.
/// Keys are supplied by an external secret provider; never commit them or store them in PostgreSQL.
/// </summary>
public sealed class MediaContentProtector(IConfiguration configuration)
{
    private static readonly byte[] Magic = "SCX2"u8.ToArray();
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int PrefixSize = 5;
    private const int MaximumKeyIdBytes = 64;

    public byte[] Encrypt(Guid hospitalId, Guid mediaId, ReadOnlySpan<byte> plaintext)
    {
        var keyId = configuration["MediaStorage:ActiveKeyId"];
        if (!IsSafeKeyId(keyId))
            throw new InvalidOperationException("An active media encryption key ID is not configured.");

        var keyIdBytes = Encoding.ASCII.GetBytes(keyId!);
        var key = ReadKey(keyId!);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var headerSize = PrefixSize + keyIdBytes.Length + NonceSize + TagSize;

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(hospitalId, mediaId, keyId!));

            var envelope = new byte[headerSize + ciphertext.Length];
            Magic.CopyTo(envelope, 0);
            envelope[4] = checked((byte)keyIdBytes.Length);
            keyIdBytes.CopyTo(envelope, PrefixSize);
            nonce.CopyTo(envelope, PrefixSize + keyIdBytes.Length);
            tag.CopyTo(envelope, PrefixSize + keyIdBytes.Length + NonceSize);
            ciphertext.CopyTo(envelope, headerSize);
            return envelope;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public byte[] Decrypt(Guid hospitalId, Guid mediaId, ReadOnlySpan<byte> envelope)
    {
        if (envelope.Length < PrefixSize + 1 + NonceSize + TagSize ||
            !envelope[..4].SequenceEqual(Magic))
            throw new CryptographicException("Unsupported or malformed media envelope.");

        var keyIdLength = envelope[4];
        if (keyIdLength is < 1 or > MaximumKeyIdBytes)
            throw new CryptographicException("Invalid media encryption key identifier.");

        var headerSize = PrefixSize + keyIdLength + NonceSize + TagSize;
        if (envelope.Length < headerSize)
            throw new CryptographicException("Truncated media encryption envelope.");

        var keyId = Encoding.ASCII.GetString(envelope.Slice(PrefixSize, keyIdLength));
        if (!IsSafeKeyId(keyId))
            throw new CryptographicException("Invalid media encryption key identifier.");

        var key = ReadKey(keyId);
        var plaintext = new byte[envelope.Length - headerSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                envelope.Slice(PrefixSize + keyIdLength, NonceSize),
                envelope[headerSize..],
                envelope.Slice(PrefixSize + keyIdLength + NonceSize, TagSize),
                plaintext,
                AssociatedData(hospitalId, mediaId, keyId));
            return plaintext;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private byte[] ReadKey(string keyId)
    {
        var encoded = configuration["MediaStorage:Keys:" + keyId];
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidOperationException("The requested media encryption key is not configured.");

        byte[] key;
        try { key = Convert.FromBase64String(encoded); }
        catch (FormatException ex) { throw new InvalidOperationException("Media encryption key must be Base64.", ex); }

        if (key.Length != 32)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidOperationException("Media encryption key must decode to exactly 32 bytes.");
        }
        return key;
    }

    private static bool IsSafeKeyId(string? keyId) =>
        !string.IsNullOrWhiteSpace(keyId) && keyId.Length <= MaximumKeyIdBytes &&
        keyId.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-');

    private static byte[] AssociatedData(Guid hospitalId, Guid mediaId, string keyId) =>
        Encoding.UTF8.GetBytes($"SCOPX.I.E|media|v2|{hospitalId:N}|{mediaId:N}|{keyId}");
}

/// <summary>
/// Verifies file signatures instead of trusting the user-supplied extension or MIME header.
/// </summary>
public static class MediaFileSignatureValidator
{
    public static bool Matches(string extension, ReadOnlySpan<byte> bytes, out string mediaType, out string contentType)
    {
        mediaType = "";
        contentType = "";
        var ext = extension.ToLowerInvariant();

        var jpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var png = bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var webp = bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8);
        var mp4 = bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8);
        var webm = bytes.Length >= 4 && bytes[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 });

        (mediaType, contentType) = ext switch
        {
            ".jpg" or ".jpeg" when jpeg => ("image", "image/jpeg"),
            ".png" when png => ("image", "image/png"),
            ".webp" when webp => ("image", "image/webp"),
            ".mp4" when mp4 => ("video", "video/mp4"),
            ".webm" when webm => ("video", "video/webm"),
            _ => ("", "")
        };
        return mediaType.Length > 0;
    }
}

/// <summary>
/// A real malware scanner must be registered before media routes are exposed.
/// Implementations must fail closed when the scanning engine is unavailable.
/// </summary>
public interface IMediaMalwareScanner
{
    Task<bool> IsCleanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}

/// <summary>
/// Safe default: media uploads remain unavailable until a real malware scanner is configured.
/// </summary>
public sealed class UnavailableMediaMalwareScanner : IMediaMalwareScanner
{
    public Task<bool> IsCleanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No malware scanning engine is configured.");
}
