using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public static class AccountSignIn
{
    private static readonly System.Net.Http.HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    public static string Origin
    {
        get
        {
            var value = ReleaseBackendConfiguration.ApiBaseUrl;
            if (value.Length == 0) value = GameState.Instance.PurchaseValidationEndpoint;
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback)) ? uri.GetLeftPart(UriPartial.Authority) : "";
        }
    }
    private sealed class RequestFailure : InvalidOperationException
    {
        public int StatusCode { get; }
        public RequestFailure(string message, int statusCode) : base(message) { StatusCode = statusCode; }
    }
    public static async Task<JsonElement> Request(string path, object body = null)
    {
        var state = GameState.Instance;
        if (state.AccountProvider.Length == 0 && path is "/auth/email/start" or "/auth/google/start")
        {
            // Establish an authenticated guest before linking an identity, including an offline-only journey.
            try
            {
                var guest = await SendRequest("/player-profile", new { profile = new { playerProfileId = state.PlayerProfileId, playerCallsign = state.PlayerCallsign } });
                if (Text(guest,"playerProfileId") != state.PlayerProfileId || Text(guest,"sessionToken").Length == 0)
                    throw new InvalidOperationException("Could not prepare your account. Try again.");
                state.ApplyPlayerProfileSession(state.PlayerProfileId, Text(guest,"playerCallsign",state.PlayerCallsign), Text(guest,"sessionToken"), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            catch (RequestFailure e) when (e.StatusCode == 401) { /* Existing identities can recover a guest whose device session expired. */ }
        }
        return await SendRequest(path, body);
    }
    private static async Task<JsonElement> SendRequest(string path, object body, string profile = "", string token = "")
    {
        if (Origin.Length == 0) throw new InvalidOperationException("Set the game server in Settings to sign in.");
        using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, Origin + path);
        if (profile.Length == 0) PlayerSessionHttp.Apply(request, GameState.Instance.PlayerProfileId);
        else
        {
            request.Headers.Add("X-Convoy-Profile", profile);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        try
        {
            using var response = await Client.SendAsync(request);
            if ((int)response.StatusCode == 429) throw new InvalidOperationException("Too many attempts. Wait a minute and try again.");
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!response.IsSuccessStatusCode) throw new RequestFailure(Text(result.RootElement,"message", "Could not sign in. Try again."), (int)response.StatusCode);
            return result.RootElement.Clone();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        { throw new InvalidOperationException("The game server is unavailable. Try again.", e); }
    }
    public static string Text(JsonElement json, string name, string fallback = "") => json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    public static async Task Apply(JsonElement session)
    {
        var state = GameState.Instance;
        var profile = Text(session, "playerProfileId");
        if (profile.Length == 0 || Text(session, "sessionToken").Length == 0) throw new InvalidOperationException("Invalid sign-in response.");
        var changedAccount = profile != state.PlayerProfileId;
        GameSaveData accountSave = null;
        if (changedAccount)
        {
            // Download before changing local identity so a failed request leaves current progress intact.
            var cloud = await SendRequest("/cloud-save/download?profileId=" + Uri.EscapeDataString(profile), null, profile, Text(session, "sessionToken"));
            if (Text(cloud, "status") != "empty" && Text(cloud, "saveData").Length > 0)
                accountSave = JsonSerializer.Deserialize<GameSaveData>(CloudSavePrivacy.Sanitize(Text(cloud, "saveData")),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            else if (state.AccountProvider.Length == 0 && session.TryGetProperty("isNewAccount", out var created) && created.ValueKind == JsonValueKind.True)
                accountSave = state.BuildSaveData();
        }
        if (changedAccount) state.StartAccountSave();
        state.ApplyPlayerProfileSession(profile, Text(session, "playerCallsign"), Text(session, "sessionToken"), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        state.SetSignedInAccount(Text(session, "provider"), Text(session, "accountLabel"));
        PlayerProfileSyncService.InvalidateFromState("Account signed in.");
        if (accountSave != null) state.RestoreCloudSave(accountSave);
    }
}
