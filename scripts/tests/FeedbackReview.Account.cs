using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

public partial class FeedbackReview
{
    private async Task CheckAccountClient()
    {
        if (ReleaseBackendConfiguration.ApiBaseUrl.Length > 0) return; // Never exercise a production server from a smoke test.
        var state = GameState.Instance;
        var before = state.BuildSaveData();
        using var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start(); var number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{number}/"); listener.Start();
        var guestLinked = false; var downloadAuthorized = false; var failDownload = false;
        var guestToken = new string('g',64); var accountToken = new string('s',64);
        var profile = "ACC-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var serve = Task.Run(async () =>
        {
            try
            {
                while (listener.IsListening)
                {
                    var context = await listener.GetContextAsync();
                    object result;
                    switch (context.Request.Url.AbsolutePath)
                    {
                        case "/player-profile":
                            result = new { playerProfileId = before.PlayerProfileId, playerCallsign = before.PlayerCallsign, sessionToken = guestToken };
                            break;
                        case "/auth/email/start":
                            guestLinked = context.Request.Headers["Authorization"] == "Bearer " + guestToken && context.Request.Headers["X-Convoy-Profile"] == before.PlayerProfileId;
                            result = new { challengeId = new string('c',64), secret = new string('d',64) };
                            break;
                        case "/cloud-save/download":
                            downloadAuthorized = context.Request.Headers["Authorization"] == "Bearer " + accountToken && context.Request.Headers["X-Convoy-Profile"] == profile;
                            if (failDownload) { context.Response.StatusCode = 503; result = new { message = "Try again." }; }
                            else result = new { status = "empty" };
                            break;
                        default:
                            context.Response.StatusCode = 404; result = new { message = "Unexpected test request." }; break;
                    }
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(result);
                    context.Response.ContentType = "application/json"; context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
                }
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { }
        });
        try
        {
            state.SetPurchaseValidationEndpoint($"http://127.0.0.1:{number}");
            await AccountSignIn.Request("/auth/email/start", new { email = "fixture@example.com" });
            Check(guestLinked && state.PlayerProfileId == before.PlayerProfileId && state.Food == before.Food,
                "First sign-in registers an offline guest without changing its journey");
            using var session = JsonDocument.Parse(JsonSerializer.Serialize(new { playerProfileId = profile, playerCallsign = "Caravan", sessionToken = accountToken,
                provider = "email", accountLabel = "fixture@example.com", isNewAccount = true }));
            await AccountSignIn.Apply(session.RootElement);
            Check(downloadAuthorized && state.PlayerProfileId == profile && state.PlayerAuthToken == accountToken && state.AccountProvider == "email",
                "Account download uses the verified account session before changing local identity");
            Check(state.Food == before.Food && state.Gold == before.Gold && state.ActiveDeckUnitIds.SequenceEqual(before.ActiveDeckUnitIds),
                "New accounts retain local guest progress even when no cloud save exists");
            failDownload = true;
            using var another = JsonDocument.Parse(JsonSerializer.Serialize(new { playerProfileId = "ACC-another", sessionToken = accountToken, provider = "google", isNewAccount = false }));
            try { await AccountSignIn.Apply(another.RootElement); Check(false, "Failed account download is rejected"); }
            catch (InvalidOperationException)
            { Check(state.PlayerProfileId == profile && state.Food == before.Food && state.ActiveDeckUnitIds.SequenceEqual(before.ActiveDeckUnitIds), "Failed account downloads preserve the current account and progress"); }
        }
        finally
        {
            listener.Stop(); await serve;
            state.ApplyPlayerProfileSession(before.PlayerProfileId, before.PlayerCallsign, before.PlayerAuthToken, before.LastPlayerProfileSyncAtUnixSeconds);
            state.SetSignedInAccount(before.AccountProvider, before.AccountLabel); state.SetPurchaseValidationEndpoint(before.PurchaseValidationEndpoint);
            state.RestoreCloudSave(before);
        }
    }
}
