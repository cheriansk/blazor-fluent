namespace BlazorFluent.Shared.Security;

/// <summary>
/// Data transfer object holding an AES-256-GCM AEAD encrypted envelope.
/// </summary>
public record EncryptedEnvelopeDto(
    string NonceBase64,
    string TagBase64,
    string CiphertextBase64
)
{
    public static EncryptedEnvelopeDto FromBytes(byte[] nonce, byte[] tag, byte[] ciphertext) =>
        new(
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(ciphertext)
        );

    public (byte[] Nonce, byte[] Tag, byte[] Ciphertext) ToBytes() =>
        (
            Convert.FromBase64String(NonceBase64),
            Convert.FromBase64String(TagBase64),
            Convert.FromBase64String(CiphertextBase64)
        );
}
