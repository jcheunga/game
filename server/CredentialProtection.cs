using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CrownroadServer;

/// <summary>
/// Encrypts the sign-in provider credentials the server must keep (Apple refresh tokens, needed to
/// revoke Sign in with Apple when a player deletes their account) with AES-256-GCM. The key is
/// ACCOUNT_TOKEN_KEY, 32 random bytes in base64, set inline or as a file through
/// ACCOUNT_TOKEN_KEY_PATH. A database copy alone never holds a usable token.
/// </summary>
public static class CredentialProtection
{
    private const string Version = "v1.";
    private const int NonceSize = 12, TagSize = 16;
    private static string Env(string name) => Environment.GetEnvironmentVariable(name)?.Trim() ?? "";

    public static bool Configured => TryKey(out _);

    private static bool TryKey(out byte[] key)
    {
        key = [];
        var text = Env("ACCOUNT_TOKEN_KEY");
        if (text.Length == 0 && Env("ACCOUNT_TOKEN_KEY_PATH") is { Length: > 0 } path && File.Exists(path)) text = File.ReadAllText(path).Trim();
        try { key = Convert.FromBase64String(text); }
        catch (FormatException) { return false; }
        return key.Length == 32;
    }

    private static byte[] Key() => TryKey(out var key) ? key
        : throw new InvalidOperationException("ACCOUNT_TOKEN_KEY must be 32 random bytes in base64 (openssl rand -base64 32).");

    /// <summary>Seals a credential. The purpose is authenticated with it, so a value sealed for one use cannot be read as another.</summary>
    public static string Protect(string plaintext, string purpose)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);
        var sealedBytes = new byte[NonceSize + data.Length + TagSize];
        var nonce = sealedBytes.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(Key(), TagSize);
        aes.Encrypt(nonce, data, sealedBytes.AsSpan(NonceSize, data.Length), sealedBytes.AsSpan(NonceSize + data.Length), Encoding.UTF8.GetBytes(purpose));
        return Version + Base64Url.EncodeToString(sealedBytes);
    }

    /// <summary>Opens a sealed credential; throws when the key, the purpose or the bytes do not match.</summary>
    public static string Unprotect(string sealedText, string purpose)
    {
        if (!sealedText.StartsWith(Version, StringComparison.Ordinal)) throw new CryptographicException("Unknown credential format.");
        var sealedBytes = Base64Url.DecodeFromChars(sealedText.AsSpan(Version.Length));
        if (sealedBytes.Length < NonceSize + TagSize) throw new CryptographicException("Truncated credential.");
        var length = sealedBytes.Length - NonceSize - TagSize;
        var data = new byte[length];
        using var aes = new AesGcm(Key(), TagSize);
        aes.Decrypt(sealedBytes.AsSpan(0, NonceSize), sealedBytes.AsSpan(NonceSize, length), sealedBytes.AsSpan(NonceSize + length), data, Encoding.UTF8.GetBytes(purpose));
        return Encoding.UTF8.GetString(data);
    }
}
