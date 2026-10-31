using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    /// <summary>
    /// Settings → Account → Manage, opened like a player against a stand-in server that offers every sign-in. Measures
    /// the sign-in buttons against Google's and Apple's published button rules (art/signin/README.md lists them),
    /// captures the dialog, a held press and every brand on dark and light surroundings. Run with --mobile-preview
    /// for the phone-sized canvas.
    /// </summary>
    private async Task ReviewAccount()
    {
        _output = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--mobile-preview") ? "res://artifacts/account-phone" : "res://artifacts/account");
        System.IO.Directory.CreateDirectory(_output);
        if (ReleaseBackendConfiguration.ApiBaseUrl.Length > 0) { Check(false, "The account review never talks to a production server"); return; }
        var state = GameState.Instance;
        state.ResetProgress(); state.SetAnalyticsConsent(false); state.SetShowHints(false);
        var endpoint = state.PurchaseValidationEndpoint;
        using var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start(); var number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{number}/"); listener.Start();
        var serve = Task.Run(async () =>
        {
            try
            {
                while (listener.IsListening)
                {
                    var context = await listener.GetContextAsync();
                    var android = PlatformSignIn.ReviewProvider == PlatformSignIn.PlayGames;
                    object result = context.Request.Url.AbsolutePath == "/auth/providers"
                        ? new { email = true, google = true, apple = true, playGames = android, playGamesClientId = android ? "review" : "", gameCenter = false, devSignIn = false }
                        : new { message = "Not served in the account review." };
                    if (context.Request.Url.AbsolutePath != "/auth/providers") context.Response.StatusCode = 404;
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
            await ReviewSignInButtons();
        }
        finally
        {
            listener.Stop(); await serve;
            state.SetPurchaseValidationEndpoint(endpoint);
        }
    }

    private async Task ReviewSignInButtons()
    {
        await Open("MainMenu");
        await PressHint("Settings");
        var settings = Walk(GetTree().Root).OfType<SettingsMenu>().Single(page => !page.IsQueuedForDeletion());
        Button Named(string name) => Walk(settings).OfType<Button>().FirstOrDefault(button => button.IsVisibleInTree() && !button.IsQueuedForDeletion() && button.AccessibilityName == name && !button.HasMeta("royal_tab"));
        await TapModal(Walk(settings).OfType<Button>().First(button => button.IsVisibleInTree() && button.AccessibilityName == "Account" && button.HasMeta("royal_tab")));
        await TapModal(Named("Account") ?? throw new InvalidOperationException("The Account tab has no Manage row"));
        var dialog = Walk(GetTree().Root).OfType<AccountDialog>().Single(d => !d.IsQueuedForDeletion());
        var buttons = Walk(dialog).OfType<SignInButton>().Where(b => b.IsVisibleInTree()).ToList();
        var apple = buttons.SingleOrDefault(b => b.Kind == SignInButton.Brand.Apple);
        var google = buttons.SingleOrDefault(b => b.Kind == SignInButton.Brand.Google);
        for (var i = 0; i < 40 && (apple?.Disabled ?? false); i++) await Wait(.1);
        Check(apple != null && google != null && !apple.Disabled && !google.Disabled, "Account offers Apple and Google sign-in as branded buttons, enabled by the server");
        if (apple == null || google == null) return;

        // Size and prominence: Apple no smaller than any other sign-in and on screen without scrolling; Google at
        // least as prominent as the others.
        var canvas = new Rect2(Vector2.Zero, RoyalArt.Canvas);
        var pointsPerPixel = 390f / 720 * apple.GetGlobalTransformWithCanvas().Scale.Y;   // a 390 pt tall phone
        Check(buttons.All(b => b.Size == apple.Size), $"Every sign-in button is the same size ({apple.Size.X:0}x{apple.Size.Y:0} px)");
        Check(apple.Size.Y * pointsPerPixel >= 43, $"Sign-in buttons are 44 pt tall on a phone ({apple.Size.Y * pointsPerPixel:0.#} pt; Apple's minimum is 30)");
        Check(apple.Size.X * pointsPerPixel >= 140, $"Sign-in buttons are at least Apple's 140 pt wide ({apple.Size.X * pointsPerPixel:0} pt)");
        Check(canvas.Encloses(apple.GetGlobalRect()) && canvas.Encloses(google.GetGlobalRect()), "Apple and Google sign-in show without scrolling");
        Check(buttons.IndexOf(apple) < buttons.IndexOf(google), "Sign in with Apple comes before Google");
        // The panel behind the buttons holds them; everything else on it keeps Apple's clear space.
        var neighbours = Walk(dialog).OfType<Control>().Where(c => c.IsVisibleInTree() && c.GetParent() == apple.GetParent() && c != apple
            && !c.GetGlobalRect().Encloses(apple.GetGlobalRect())).ToList();
        var clearance = neighbours.Select(c => Gap(c.GetGlobalRect(), apple.GetGlobalRect())).DefaultIfEmpty(float.MaxValue).Min();
        Check(clearance >= apple.Size.Y / 10, $"Nothing comes within a tenth of the button's height of Sign in with Apple ({clearance:0} px)");

        // Titles, fonts and logos: one face, one title size and one column for every logo and every title.
        Check(buttons.All(b => b.Text == SignInButton.Title(b.Kind) && b.Text.StartsWith("Continue with ")), "Titles use the wording both companies allow: Continue with …");
        Check(SignInButton.TitleFont.ResourcePath.EndsWith("GoogleSans-Medium.ttf"), "Titles are set in Google Sans Medium, the font Google names");
        Check(buttons.All(b => b.TitleFontSize == Mathf.Round(SignInButton.TitleSize)),
            $"Every title is the spec's 14 points at that scale ({string.Join(", ", buttons.Select(b => b.TitleFontSize))} px)");
        Check(buttons.Select(b => b.GlobalPosition.X + b.TitleRect.Position.X).Distinct().Count() == 1, "Every title starts at the same column");
        Check(buttons.Select(b => b.GlobalPosition.X + SignInButton.TileRect.GetCenter().X).Distinct().Count() == 1, "Every logo tile sits in the same column");
        var g = google.LogoRect;
        Check(Mathf.IsEqualApprox(g.Size.X, g.Size.Y) && Mathf.IsEqualApprox(g.Size.Y * 20 / 22, SignInButton.LogoSize) && SignInButton.TileRect.Encloses(g),
            $"The Google G keeps its shape at 20 of the spec's {(OS.GetName() == "iOS" ? 44 : 40)} points ({SignInButton.LogoSize:0.#} px), on its white tile");
        if (SignInButton.HasLogo(SignInButton.Brand.Apple))
            Check(apple.LogoRect == SignInButton.TileRect, "Apple's logo-only file fills the tile, whole and uncropped");
        else GD.Print("PENDING Apple's Sign in with Apple logo file is not in assets/ui/signin yet (art/signin/README.md)");
        if (!SignInButton.HasLogo(SignInButton.Brand.GameCenter)) GD.Print("PENDING Apple's Game Center icon is not in assets/ui/signin yet (art/signin/README.md)");
        foreach (var button in buttons)
            Check(new Rect2(Vector2.Zero, button.Size).Encloses(SignInButton.TileRect) && new Rect2(Vector2.Zero, button.Size).Encloses(button.TitleRect)
                  && SignInButton.TileRect.End.X < button.TitleRect.Position.X, $"{button.Text}: logo tile and title sit inside the button, tile first");
        AuditText("Account");
        await Capture("account-sign-in");

        // A held press sinks the whole face (ButtonFeel), logo and title together.
        ButtonFeel.Hold(apple, true); for (var i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(ButtonFeel.Amount(apple) > .9f, "Holding Sign in with Apple presses it in");
        await Capture("account-apple-held");
        ButtonFeel.Hold(apple, false); for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var width = apple.Size.X;
        dialog.Close(); await Wait(.2);
        await ReviewAccountPages();

        // Every brand, live and disabled, on the dark dialog panel and on light parchment and white.
        foreach (var (name, colour) in new[] { ("dark", new Color("1d2a33")), ("parchment", new Color("efe4cc")), ("white", Colors.White) })
        {
            var layer = new CanvasLayer { Layer = 120 }; AddChild(layer);
            layer.AddChild(new ColorRect { Color = colour, AnchorRight = 1, AnchorBottom = 1 });
            // As wide as the dialog's buttons, so the Play Games and Game Center titles are measured as phones show them.
            var column = new VBoxContainer { Position = new Vector2(80, 60), Size = new Vector2(width, 600) };
            column.AddThemeConstantOverride("separation", 12);
            layer.AddChild(column);
            var second = new VBoxContainer { Position = new Vector2(1200 - width, 60), Size = new Vector2(width, 600) };
            second.AddThemeConstantOverride("separation", 12);
            layer.AddChild(second);
            foreach (var brand in Enum.GetValues<SignInButton.Brand>())
            {
                column.AddChild(SignInButton.Create(brand, SignInButton.Title(brand), null));
                var off = SignInButton.Create(brand, SignInButton.Title(brand), null); off.Disabled = true; second.AddChild(off);
            }
            await Wait(.2);
            if (name == "dark")
                foreach (var button in column.GetChildren().OfType<SignInButton>())
                    Check(button.TitleFontSize == Mathf.Round(SignInButton.TitleSize) && SignInButton.TileRect.End.X < button.TitleRect.Position.X,
                        $"{button.Text}: the title fits at its spec size after the logo tile");
            await Capture("sign-in-buttons-" + name);
            layer.QueueFree(); await Wait(.1);
        }
    }

    /// <summary>
    /// The page as a new caravan meets it on the home map (nickname, sign-in, Play as guest, no close button), and as
    /// a signed-in player sees it from Settings.
    /// </summary>
    private async Task ReviewAccountPages()
    {
        var state = GameState.Instance;
        var home = (Control)GetTree().CurrentScene;
        var chosen = false;
        AccountDialog.ShowWelcome(home, () => chosen = true);
        await Wait(1);
        var welcome = Walk(home).OfType<AccountDialog>().Single(d => !d.IsQueuedForDeletion());
        Button Visible(string text) => Walk(welcome).OfType<Button>().SingleOrDefault(b => b.IsVisibleInTree() && b.Text == text);
        var nickname = Walk(welcome).OfType<LineEdit>().Single(e => e.Name == "AccountNickname");
        Check(nickname.IsVisibleInTree() && nickname.Text.Length > 0 && Visible("Back") == null && Visible("Play as guest") != null,
            "The welcome suggests a nickname and offers Play as guest, with no close button");
        AuditText("Account welcome");
        await Capture("account-welcome");
        // Android adds Play Games above Apple and Google; the panel must still fit the canvas with room to spare.
        var panel = Walk(welcome).OfType<Panel>().Single(p => p.Name == "Panel");
        var tallest = panel.Size.Y + (PlatformSignIn.Provider.Length > 0 ? 0 : SignInButton.Height + 10);
        Check(tallest <= RoyalArt.Canvas.Y - 40, $"With every sign-in button the panel fits the canvas ({tallest:0} of {RoyalArt.Canvas.Y:0} px)");
        // A message (here the stand-in server refusing an email code) takes the rule's place without moving the rows.
        var guestTop = Visible("Play as guest").Position.Y;
        Walk(welcome).OfType<LineEdit>().Single(e => e.Name == "AccountEmail").Text = "rowan@example.com";
        Visible("Send code").EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.6);
        var status = Walk(welcome).OfType<Label>().Single(l => l.Name == "Status");
        Check(status.IsVisibleInTree() && status.Text.Length > 0 && Visible("Play as guest").Position.Y == guestTop, $"A sign-in message shows in place of the rule ({status.Text})");
        await Capture("account-message");
        nickname.Text = "Ser Rowan";
        Visible("Play as guest").EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.3);
        Check(chosen && state.PlayerCallsign == "Ser Rowan" && state.HasChosenCallsign && !IsInstanceValid(welcome),
            "Play as guest keeps the nickname and closes the welcome");

        state.SetSignedInAccount("google", "rowan@example.com");
        AccountDialog.Show(home); await Wait(.6);
        var signedIn = Walk(home).OfType<AccountDialog>().Single(d => !d.IsQueuedForDeletion());
        Check(Walk(signedIn).OfType<Button>().Any(b => b.IsVisibleInTree() && b.Text == "Sign out") && !Walk(signedIn).OfType<SignInButton>().Any(b => b.IsVisibleInTree()),
            "Signed in, the page shows the account and Sign out instead of the sign-in buttons");
        AuditText("Account signed in");
        await Capture("account-signed-in");
        signedIn.Close(); await Wait(.2);
        state.SetSignedInAccount("", "");

        // Android puts the phone's Play Games account first, above Apple and Google.
        PlatformSignIn.ReviewProvider = PlatformSignIn.PlayGames;
        try
        {
            AccountDialog.ShowWelcome(home, null); await Wait(1);
            var android = Walk(home).OfType<AccountDialog>().Single(d => !d.IsQueuedForDeletion());
            var brands = Walk(android).OfType<SignInButton>().Where(b => b.IsVisibleInTree()).OrderBy(b => b.GlobalPosition.Y).ToList();
            var canvas = new Rect2(Vector2.Zero, RoyalArt.Canvas).Grow(-16);
            Check(brands.Select(b => b.Kind).SequenceEqual(new[] { SignInButton.Brand.PlayGames, SignInButton.Brand.Apple, SignInButton.Brand.Google }) && brands.All(b => !b.Disabled)
                  && Walk(android).OfType<Control>().Where(c => c.IsVisibleInTree() && c is Button or LineEdit).All(c => canvas.Encloses(c.GetGlobalRect())),
                "On Android, Play Games comes first, then Apple and Google, and everything fits on the canvas");
            AuditText("Account Android");
            await Capture("account-android");
            android.Close(); await Wait(.2);
        }
        finally { PlatformSignIn.ReviewProvider = null; }
    }

    private static float Gap(Rect2 a, Rect2 b) =>
        Mathf.Max(Mathf.Max(b.Position.X - a.End.X, a.Position.X - b.End.X), Mathf.Max(b.Position.Y - a.End.Y, a.Position.Y - b.End.Y));
}
