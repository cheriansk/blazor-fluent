using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorEnterprise.Shared.Common;

/// <summary>
/// Universal JSON serialization conventions ensuring parity across Client WebApp and Cloud API.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions StandardOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = false
    };

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, StandardOptions);
    public static T? FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, StandardOptions);
    public static byte[] ToUtf8Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, StandardOptions);
    public static T? FromUtf8Bytes<T>(byte[] utf8Bytes) => JsonSerializer.Deserialize<T>(utf8Bytes, StandardOptions);
}
