using System.Net.Http;

/// <summary>
/// Applies the anonymous-player bearer session to backend requests. Profile IDs
/// remain useful routing identifiers, but they are never authorization by
/// themselves.
/// </summary>
public static class PlayerSessionHttp
{
    public static void Apply(HttpRequestMessage message, string profileId)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            message.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId.Trim());
        }

        var token = GameState.Instance?.PlayerAuthToken?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(token))
        {
            message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        }
    }
}
