using System.Net;
using System.Text;
using System.Text.Json;

namespace CrownroadServer.Tests;

/// <summary>
/// Nicknames: the shared filter (scripts/core/Nickname.cs), what the server does with a blocked name a device sends,
/// and support's rename, which a device that still has the old name can't undo.
/// </summary>
public static partial class ServerTests
{
    private static Task TestNicknameFilter()
    {
        string[] blocked =
        {
            "fuck", "F U C K", "fvvvck", "Sh1t", "BigAss", "Big-Ass", "xxniggerxx", "fuсk" /* Cyrillic с */, "ｆｕｃｋ",
            "Fück", "PeaCock", "Admin", "TheModerator", "Crownroad Dev", "Player 1488", "a s s",
        };
        string[] allowed =
        {
            "Lantern", "Ser Rowan-2", "Glass Cannon", "Assassin", "Peacock", "Scunthorpe", "Therapist", "Classic Bass",
            "Dickens", "Cassandra", "Grapevine", "Niger Delta", "Shiitake", "Analyst", "Hancock", "Milford", "Sussex",
            "Spicy", "Raccoon", "Cocktail", "Anonymous", "Ironwake", "Emberline", "Caravan", "Host", "Runner",
            "Ashen Warden", "Thorn Rider", "Hollow Squire", "Raven Vow",
        };
        foreach (var name in blocked) Assert(Nickname.IsBlocked(name), $"expected '{name}' to be blocked");
        foreach (var name in allowed) Assert(!Nickname.IsBlocked(name), $"expected '{name}' to be allowed");

        Assert(Nickname.Normalize("  Iron<script>Wake!!  ") == "IronscriptWake", "expected only letters, numbers, spaces, hyphens and underscores");
        Assert(Nickname.Normalize(new string('x', 40)).Length == Nickname.MaxLength, "expected names cut to the maximum length");
        Assert(Nickname.Normalize("!!!") == Nickname.Default, "expected an empty name to fall back to the default");
        Assert(Nickname.Clean("Shitlord") == Nickname.Default, "expected a blocked name to be cleaned to the default");
        return Task.CompletedTask;
    }

    private static async Task TestNicknameSupportRename(HttpClient client)
    {
        const string profile = "TEST-NICKNAME";
        async Task<string> Sync(string callsign)
        {
            using var synced = await Post(client, "/player-profile", new { profile = new { playerProfileId = profile, playerCallsign = callsign } }, profile);
            SessionTokens[profile] = synced.RootElement.GetProperty("sessionToken").GetString() ?? "";
            return synced.RootElement.GetProperty("playerCallsign").GetString() ?? "";
        }
        async Task<(HttpStatusCode Status, string Text)> Rename(object body, bool authorized = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/support/rename")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            if (authorized) request.Headers.TryAddWithoutValidation("X-Admin-Api-Key", TestAdminApiKey);
            using var response = await client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        // A blocked name from an old or altered game never reaches the players table.
        Assert(await Sync("Shitlord") == Nickname.Default && ReadCallsign(profile) == Nickname.Default,
            "expected a blocked name on a new player to be replaced with the default");
        Assert(await Sync("Ironwake") == "Ironwake", "expected an ordinary name to be accepted");
        Assert(await Sync("Fuckface") == "Ironwake" && ReadCallsign(profile) == "Ironwake",
            "expected a blocked name to leave the player's current name in place");
        Assert(await Sync("Gravelord<>") == "Gravelord", "expected the server to drop characters a nickname can't have");

        // Support takes away a name the filter missed.
        Assert((await Rename(new { profileId = profile }, authorized: false)).Status == HttpStatusCode.Unauthorized,
            "expected a rename without the admin key to be refused");
        Assert((await Rename(new { profileId = "CVY-NOBODY" })).Status == HttpStatusCode.NotFound, "expected an unknown player to be reported");
        Assert((await Rename(new { profileId = profile, callsign = "Bitchface" })).Status == HttpStatusCode.BadRequest,
            "expected support to be stopped from setting a blocked name");
        var renamed = await Rename(new { profileId = profile });
        Assert(renamed.Status == HttpStatusCode.OK && ReadCallsign(profile) == Nickname.Default,
            $"expected the rename to set the default name, got {renamed.Status} {renamed.Text}");

        // The device still has the old name and sends it at its next sync: it gets the new one back.
        Assert(await Sync("Gravelord") == Nickname.Default && await Sync("gravelord") == Nickname.Default,
            "expected the taken-away name to stay gone");
        using (var room = await Post(client, "/challenge-room-create", new
        {
            room = new { boardCode = "TEST-BOARD-NAME", boardTitle = "Test Board", playerProfileId = profile, playerCallsign = "Gravelord", region = "test" }
        }, profile))
        {
            Assert(room.RootElement.GetProperty("hostCallsign").GetString() == Nickname.Default,
                "expected a room to refuse the taken-away name too");
        }
        // The player can still choose any other name.
        Assert(await Sync("Stonewarden") == "Stonewarden", "expected a new name to be accepted after a rename");

        using var lookup = await GetAdmin(client, $"/admin/support/player?profileId={profile}");
        Assert(lookup.RootElement.GetProperty("player").GetProperty("blockedCallsign").GetString() == "Gravelord",
            "expected the lookup to show the name support took away");
    }
}
