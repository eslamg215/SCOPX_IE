using System.Security.Cryptography;
using System.Text;

namespace SCOPX.Api.Media;

/// <summary>
/// Versioned authenticated-encryption envelope for medical media.
/// The 256-bit key must come from a managed secret store or protected environment
/// configuration; never commit it or place it in the database.
/// </summary>
public sealed class MediaContentProtector(IConfiguration configuration)
{
    private static readonly byte[] Magic = "SCX1"u8.ToArray();
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = 4 + NonceSize + TagSize;

    public byte[] Encrypt(Guid hospitalId, Guid mediaId, ReadOnlySpan<byte> plaintext)
    {
        var key = ReadKey();
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(hospitalId, mediaId));

            var envelope = new byte[HeaderSize + ciphertext.Length];
            Magic.CopyTo(envelope, 0);
            nonce.CopyTo(envelope, 4);
            tag.CopyTo(envelope, 4 + NonceSize);
            ciphertext.CopyTo(envelope, HeaderSize);
            return envelope;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public byte[] Decrypt(Guid hospitalId, Guid mediaId, ReadOnlySpan<byte> envelope)
    {
        if (envelope.Length < HeaderSize || !envelope[..4].SequenceEqual(Magic))
            throw new CryptographicException("Unsupported or malformed media envelope.");

        var key = ReadKey();
        var plaintext = new byte[envelope.Length - HeaderSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                envelope.Slice(4, NonceSize),
                envelope[HeaderSize..],
                envelope.Slice(4 + NonceSize, TagSize),
                plaintext,
                AssociatedData(hospitalId, mediaId));
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

    private byte[] ReadKey()
    {
        var encoded = configuration["MediaStorage:EncryptionKeyBase64"];
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidOperationException("Media encryption key is not configured.");

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

    private static byte[] AssociatedData(Guid hospitalId, Guid mediaId) =>
        Encoding.UTF8.GetBytes($"SCOPX.I.E|media|v1|{hospitalId:N}|{mediaId:N}");
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
