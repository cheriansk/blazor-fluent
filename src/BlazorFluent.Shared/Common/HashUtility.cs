using System.Security.Cryptography;

namespace BlazorFluent.Shared.Common;

/// <summary>
/// High-performance zero-allocation SHA-256 hashing utility.
/// </summary>
public static class HashUtility
{
    public static string ComputeSha256(Stream stream, bool leaveOpen = true)
    {
        var position = stream.CanSeek ? stream.Position : 0;
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        if (stream.CanSeek && leaveOpen) stream.Position = position;
        return Convert.ToHexStringLower(hash);
    }

    public static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    public static string ComputeSha256(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return ComputeSha256(bytes);
    }
}
