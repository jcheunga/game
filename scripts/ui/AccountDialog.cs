using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// The Account page: a full-screen painted page with the King's Road behind the game's title and a framed panel
/// for the nickname, the sign-in buttons, email and Play as guest. A new caravan meets it once on the home map
/// (<see cref="ShowWelcome"/>), and Settings and the storehouse open it to sign in or out (<see cref="Show"/>).
/// </summary>
public partial class AccountDialog : RoyalScreen
{
    private Label _status;
    private LineEdit _nickname, _email, _code;
    private Button _emailButton, _verifyButton, _appleButton, _googleButton, _platformButton, _devButton, _suggestButton, _guestButton, _signOutButton, _doneButton;
    private RoyalButton _back;
    private Panel _panel;
    private Control _content, _nicknameCaption, _signInCaption, _signedInCaption, _divider, _rule, _accountCard;
    private RoyalLabel _accountName, _accountDetail;
    private TextureRect _accountMark;
    private Panel _accountTile;
    private string _challenge = "", _secret = "";
    private bool _busy, _closed, _emailAvailable, _appleAvailable, _googleAvailable, _platformAvailable, _devAvailable, _codeSent, _signedIn;
    private JsonElement _providers;
    private readonly bool _welcome;

    // The panel on the right of the canvas; the title and the caravan keep the left.
    private const float PanelX = 660, PanelWidth = 576, Pad = 28;
    private const float ContentX = PanelX + Pad, ContentWidth = PanelWidth - 2 * Pad;
    private const float FieldHeight = 56, ButtonHeight = 56, StatusHeight = 30;
    private static readonly Color Soft = new("d9ccb2"), Engraved = new("fbe7bb"), Error = new("f0a493");

    /// <summary>
    /// Apple and Google sign-in run in the browser. On iPhone that means leaving the game for Safari, which App Review
    /// has rejected, so the iPhone offers Game Center and email instead; the browser sign-ins stay on Android and desktop.
    /// </summary>
    internal static bool BrowserSignIn => OS.GetName() != "iOS";
    private AccountDialog(bool welcome)
    {
        _welcome = welcome;
        Veil = new Color(0, 0, 0, 0);
    }

    /// <summary>Opens the page over <paramref name="host"/> to sign in or out; it closes back to the host.</summary>
    public static void Show(Control host) => Open(host, false, null);

    /// <summary>
    /// A new caravan's first stop: choose a nickname, then sign in or play as a guest. There is no close button;
    /// <paramref name="done"/> runs once the player has chosen.
    /// </summary>
    public static void ShowWelcome(Control host, Action done) => Open(host, true, done);

    private static void Open(Control host, bool welcome, Action done)
    {
        // Its own layer, so the page covers the canvas whatever scale the host's modal draws at.
        var layer = new CanvasLayer { Layer = 90, Name = "AccountLayer" };
        host.AddChild(layer);
        var page = new AccountDialog(welcome) { Name = "AccountDialog" };
        page.Closed = () =>
        {
            if (page._closed) return;
            page._closed = true;
            page.QueueFree(); layer.QueueFree();
            done?.Invoke();
        };
        layer.AddChild(page);
    }

    protected override void Build()
    {
        // The King's Road with the player's caravan and a few of the warband beside it, facing up the road.
        AddChild(new RoadVista
        {
            Name = "Vista", Caravan = new Vector2(392, 364), CaravanScale = 1.3f,
            Escort = new[] { ("player_paladin", 490f, 56f), ("player_longbowman", 530f, 52f), ("player_stormcaller", 570f, 54f) }
        });
        AddShades();
        AddTitle();
        _content = Layer("Content");
        _content.MouseFilter = MouseFilterEnum.Ignore;
        _panel = RoyalKit.Frame("ach-card", new Rect2(PanelX, 0, PanelWidth, 0), 14);
        _panel.Name = "Panel";
        _content.AddChild(_panel);

        // The status line is the page's first Label, which the server end-to-end review reads.
        _status = RoyalText.Paragraph("", 18, Soft, 600, HorizontalAlignment.Center);
        _status.Name = "Status";
        _status.CustomMinimumSize = new Vector2(ContentWidth, 0);
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.ClipText = true;
        _content.AddChild(_status);

        _signedIn = GameState.Instance.AccountProvider.Length > 0;
        _nicknameCaption = Caption("Nickname");
        _signInCaption = Caption("Save your progress");
        _signedInCaption = Caption("Signed in");

        // A new account keeps this nickname; signing in to an existing account brings back its own.
        _nickname = Field("Nickname", GameState.MaxCallsignLength, "AccountNickname");
        _nickname.Text = _welcome && !GameState.Instance.HasChosenCallsign ? NicknameDialog.Suggest() : GameState.Instance.PlayerCallsign;
        _nickname.TooltipText = "Nickname";
        _nickname.TextChanged += _ => { if (_status.Text.StartsWith("Nickname")) Say(""); };
        _suggestButton = Square("SuggestNickname", "Suggest a nickname", "icon-refresh", () => { _nickname.Text = NicknameDialog.Suggest(); Say(""); });

        // Sign-in buttons follow their companies' button rules (SignInButton), one size each so none outranks another.
        // The phone's own game account comes first: it needs no typing and no browser.
        var platform = PlatformSignIn.Provider == PlatformSignIn.PlayGames ? SignInButton.Brand.PlayGames : SignInButton.Brand.GameCenter;
        _platformButton = Brand(platform, "PlatformSignIn", () => _ = Platform());
        _platformButton.Visible = PlatformSignIn.Provider.Length > 0;
        // Apple asks that Sign in with Apple sits beside any other third-party sign-in, at least as prominently, without scrolling.
        _appleButton = Brand(SignInButton.Brand.Apple, "AppleSignIn", () => _ = Browser("apple"));
        _appleButton.Visible = BrowserSignIn;
        _googleButton = Brand(SignInButton.Brand.Google, "GoogleSignIn", () => _ = Browser("google"));
        _googleButton.Visible = BrowserSignIn;
        _divider = Divider();

        _email = Field("Email address", 254, "AccountEmail");
        _email.TextSubmitted += text => _ = SendCode();
        _emailButton = Kit("Send code", "SendCode", "icon-scroll-gold", () => _ = SendCode(), RoyalBoard.Kind.Navy, 178);
        _code = Field("6-digit code", 6, "AccountCode");
        _code.TextSubmitted += text => _ = Verify();
        _verifyButton = Kit("Sign in", "VerifyCode", null, () => _ = Verify(), RoyalBoard.Kind.Gold, 178);

        _rule = Rule();
        _guestButton = Kit("Play as guest", "PlayAsGuest", "icon-start", PlayAsGuest, RoyalBoard.Kind.Navy, ContentWidth);
        _signOutButton = Kit("Sign out", "SignOut", "icon-quit", () => _ = SignOut(), RoyalBoard.Kind.Navy, (ContentWidth - 14) / 2);
        _doneButton = Kit(_welcome ? "Continue" : "Done", "Done", null, Finish, RoyalBoard.Kind.Gold, (ContentWidth - 14) / 2);
        _accountCard = AccountCard();

        // Opened from a screen, the page returns to it with the boards' Back plaque; the welcome waits for a choice.
        if (!_welcome)
        {
            _back = RoyalBoard.ActionButton(new Rect2(40, 648, 168, 52), "Back", "icon-back-chevron", Close, RoyalBoard.Kind.Navy, size: 17);
            _back.Text = "Back";
            AddChild(_back);
        }
        // Developer mode in a debug build, against a development server: sign in to the typed email without a code.
        _devButton = RoyalBoard.ActionButton(new Rect2(_welcome ? 40 : 224, 648, 300, 52), "Developer sign-in", "icon-tools", () => _ = DeveloperSignIn(), RoyalBoard.Kind.Slate,
            size: 16, name: "DeveloperSignIn");
        _devButton.Text = "Developer sign-in (no code)";
        AddChild(_devButton);
        Say("");
        Layout();
        Busy(false);
        _ = LoadProviders();
    }

    public override void _Process(double delta)
    {
        // Play Games may sign a new player in by itself while the welcome is open.
        if (!_closed && _signedIn != GameState.Instance.AccountProvider.Length > 0) Layout();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        // The welcome waits for a choice; elsewhere Escape closes the page as it closes any screen.
        if (!_welcome) base._UnhandledInput(input);
    }

    // ---- Layout ---------------------------------------------------------------------------------------------

    /// <summary>Stacks the visible rows down the panel and centres the panel on the canvas.</summary>
    private void Layout()
    {
        _signedIn = GameState.Instance.AccountProvider.Length > 0;
        var guest = !_signedIn;
        var askNickname = guest || _welcome;
        foreach (var control in new Control[] { _signInCaption, _platformButton, _appleButton, _googleButton, _divider, _email, _emailButton, _code, _verifyButton, _guestButton })
            control.Visible = guest && Shown(control);
        foreach (var control in new Control[] { _nicknameCaption, _nickname, _suggestButton }) control.Visible = askNickname;
        foreach (var control in new Control[] { _signedInCaption, _accountCard, _signOutButton, _doneButton }) control.Visible = _signedIn;
        _devButton.Visible = guest && GameState.Instance.DeveloperToolsActive;
        if (_signedIn) RefreshAccountCard();

        var height = Flow(0, false);
        var top = Mathf.Round((RoyalArt.Canvas.Y - height - 2 * Pad) / 2);
        _panel.Position = new Vector2(PanelX, top);
        _panel.Size = new Vector2(PanelWidth, height + 2 * Pad);
        Flow(top + Pad, true);
    }

    /// <summary>A row is shown unless its provider or step rules it out (set by <see cref="LoadProviders"/> and the email steps).</summary>
    private bool Shown(Control control)
    {
        if (control == _platformButton) return PlatformSignIn.Provider.Length > 0;
        if (control == _appleButton) return BrowserSignIn && (_appleAvailable || !_providersLoaded);
        if (control == _googleButton) return BrowserSignIn && (_googleAvailable || !_providersLoaded);
        if (control == _divider) return _platformButton.Visible || _appleButton.Visible || _googleButton.Visible;
        if (control == _email || control == _emailButton) return !_codeSent;
        if (control == _code || control == _verifyButton) return _codeSent;
        return true;
    }

    private bool _providersLoaded;

    /// <summary>Lays the visible rows from <paramref name="y"/> down (or only measures them); returns the height used.</summary>
    private float Flow(float y, bool place)
    {
        var start = y;
        void At(Control control, float x, float width = 0, float height = 0)
        {
            if (!place) return;
            control.Position = new Vector2(x, y);
            if (width > 0) control.Size = new Vector2(width, height);
        }
        void Head(Control caption) { At(caption, ContentX); y += caption.Size.Y + 8; }

        if (_signedIn)
        {
            Head(_signedInCaption);
            At(_accountCard, ContentX); y += _accountCard.Size.Y + 18;
        }
        if (_nickname.Visible)
        {
            Head(_nicknameCaption);
            At(_nickname, ContentX, ContentWidth - FieldHeight - 12, FieldHeight);
            At(_suggestButton, ContentX + ContentWidth - FieldHeight);
            y += FieldHeight + 18;
        }
        if (!_signedIn)
        {
            Head(_signInCaption);
            foreach (var button in new[] { _platformButton, _appleButton, _googleButton })
            {
                if (!button.Visible) continue;
                At(button, ContentX, ContentWidth, SignInButton.Height);
                y += SignInButton.Height + 10;
            }
            if (_divider.Visible) { y += 2; At(_divider, ContentX); y += _divider.Size.Y + 12; }
            var field = _codeSent ? _code : _email;
            var send = _codeSent ? _verifyButton : _emailButton;
            At(field, ContentX, ContentWidth - send.Size.X - 12, FieldHeight);
            At(send, ContentX + ContentWidth - send.Size.X);
            y += FieldHeight + 10;
        }
        // Messages take the rule's place, so the panel keeps its height when one appears.
        At(_status, ContentX, ContentWidth, StatusHeight);
        y += (StatusHeight - _rule.Size.Y) / 2; At(_rule, ContentX); y += (StatusHeight + _rule.Size.Y) / 2 + 10;
        if (_signedIn)
        {
            At(_signOutButton, ContentX);
            At(_doneButton, ContentX + ContentWidth - _doneButton.Size.X);
        }
        else At(_guestButton, ContentX);
        y += ButtonHeight;
        if (place) _status.Size = new Vector2(ContentWidth, StatusHeight);
        return y - start;
    }

    // ---- Pieces ---------------------------------------------------------------------------------------------

    /// <summary>Darkens the scene behind the title and the panel so both read over the painting.</summary>
    private void AddShades()
    {
        Texture2D Gradient(Vector2 from, Vector2 to, float alpha, GradientTexture2D.FillEnum fill = GradientTexture2D.FillEnum.Linear) => new GradientTexture2D
        {
            Gradient = new Gradient { Colors = new[] { new Color(.02f, .03f, .05f, alpha), new Color(.02f, .03f, .05f, 0) }, Offsets = new[] { 0f, 1f } },
            Fill = fill, FillFrom = from, FillTo = to, Width = 256, Height = 256
        };
        void Shade(Texture2D texture, Rect2 rect) => AddChild(new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore });
        // A soft pool behind the title, the right of the canvas behind the panel, and the top and foot of the scene.
        Shade(Gradient(new Vector2(.5f, .5f), new Vector2(1f, .5f), .8f, GradientTexture2D.FillEnum.Radial), new Rect2(-110, -150, 890, 520));
        Shade(Gradient(new Vector2(1, 0), new Vector2(0, 0), .72f), new Rect2(520, 0, 760, 720));
        Shade(Gradient(new Vector2(0, 1), new Vector2(0, 0), .55f), new Rect2(0, 560, 1280, 160));
        Shade(Gradient(new Vector2(0, 0), new Vector2(0, 1), .35f), new Rect2(0, 0, 1280, 120));
    }

    /// <summary>The game's name, set like the result board's banner title, with the promise of signing in below it.</summary>
    private void AddTitle()
    {
        const float centre = PanelX / 2, top = 40;
        var title = RoyalText.Title("Crownroad", 82);
        Place(title, new Rect2(centre - 320, top, 640, 104), HorizontalAlignment.Center);
        var subtitle = RoyalText.Caps("Siege of Ash", 24, Engraved, 700);
        var width = subtitle.TextWidth(subtitle.FontSize);
        Place(subtitle, new Rect2(centre - width / 2 - 4, top + 104, width + 8, 34), HorizontalAlignment.Center);
        foreach (var side in new[] { -1, 1 })
        {
            var inner = centre + side * (width / 2 + 22);
            var outer = centre + side * (width / 2 + 120);
            AddChild(new ColorRect { Color = new Color(.93f, .77f, .45f, .55f), Position = new Vector2(Mathf.Min(inner, outer), top + 120), Size = new Vector2(98, 1.5f),
                MouseFilter = MouseFilterEnum.Ignore });
            AddChild(RoyalKit.Image("result-diamond", new Rect2(outer - 7, top + 113, 14, 15)));
        }
        var promise = RoyalText.Serif(_welcome ? "Sign in to keep your caravan on every device." : "Your caravan, kept safe on every device.", 22, RoyalText.Cream, 600);
        Place(promise, new Rect2(centre - 300, top + 150, 600, 34), HorizontalAlignment.Center);
    }

    private T Place<T>(T label, Rect2 rect, HorizontalAlignment align) where T : RoyalLabel
    {
        label.Align = align; label.Position = rect.Position; label.Size = rect.Size;
        AddChild(label);
        return label;
    }

    private Control Caption(string text)
    {
        var caption = RoyalText.Caps(text, 16, Engraved, 700);
        caption.Size = new Vector2(ContentWidth, 24);
        _content.AddChild(caption);
        return caption;
    }

    private LineEdit Field(string placeholder, int maxLength, string name)
    {
        var field = new LineEdit { Name = name, PlaceholderText = placeholder, MaxLength = maxLength, AccessibilityName = placeholder, CaretBlink = true };
        var box = RoyalKit.Slice("season-locked", 10);
        box.ContentMarginLeft = 18; box.ContentMarginRight = 14;
        field.AddThemeStyleboxOverride("normal", box);
        field.AddThemeStyleboxOverride("read_only", box);
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color(1f, .84f, .5f, .75f), AntiAliasing = true };
        focus.SetBorderWidthAll(2); focus.SetCornerRadiusAll(6);
        field.AddThemeStyleboxOverride("focus", focus);
        field.AddThemeFontOverride("font", RoyalFonts.Body(600));
        field.AddThemeFontSizeOverride("font_size", 23);
        field.AddThemeColorOverride("font_color", RoyalText.Cream);
        field.AddThemeColorOverride("font_uneditable_color", RoyalText.Muted);
        field.AddThemeColorOverride("font_placeholder_color", new Color("8f8676"));
        field.AddThemeColorOverride("caret_color", RealmUi.Gold);
        field.AddThemeColorOverride("selection_color", new Color(.85f, .66f, .3f, .35f));
        _content.AddChild(field);
        return field;
    }

    private SignInButton Brand(SignInButton.Brand brand, string name, Action action)
    {
        var button = SignInButton.Create(brand, SignInButton.Title(brand), action);
        button.Name = name;
        button.Size = new Vector2(ContentWidth, SignInButton.Height);
        _content.AddChild(button);
        return button;
    }

    /// <summary>A kit action button at its final size; layout only moves it.</summary>
    private RoyalButton Kit(string caption, string name, string glyph, Action run, RoyalBoard.Kind kind, float width)
    {
        var button = RoyalBoard.ActionButton(new Rect2(0, 0, width, ButtonHeight), caption, glyph, run, kind, size: 17);
        button.Name = name;
        // Reviews and the server end-to-end run press buttons by their text.
        button.Text = caption;
        _content.AddChild(button);
        return button;
    }

    private RoyalButton Square(string name, string hint, string glyph, Action run)
    {
        var button = RoyalButton.Over(new Rect2(0, 0, FieldHeight, FieldHeight), hint, run, 6);
        button.Name = name; button.Text = hint;
        button.SetStates(RoyalKit.Slice("button-slate", 14), 6);
        button.SetGlyph(RoyalKit.Texture(glyph), new Rect2(15, 15, FieldHeight - 30, FieldHeight - 30));
        _content.AddChild(button);
        return button;
    }

    /// <summary>"OR" between two gold hairlines, between the sign-in buttons and email.</summary>
    private Control Divider()
    {
        var row = new Control { Name = "Or", Size = new Vector2(ContentWidth, 22), MouseFilter = MouseFilterEnum.Ignore };
        var word = RoyalText.Caps("or", 16, Soft, 700);
        var width = word.TextWidth(word.FontSize) + 28;
        word.Align = HorizontalAlignment.Center; word.Position = new Vector2((ContentWidth - width) / 2, 0); word.Size = new Vector2(width, 22);
        row.AddChild(word);
        foreach (var x in new[] { 0f, (ContentWidth + width) / 2 })
            row.AddChild(new ColorRect { Color = new Color(.93f, .77f, .45f, .32f), Position = new Vector2(x, 11), Size = new Vector2((ContentWidth - width) / 2, 1),
                MouseFilter = MouseFilterEnum.Ignore });
        _content.AddChild(row);
        return row;
    }

    /// <summary>A hairline gold rule with a diamond at its centre.</summary>
    private Control Rule()
    {
        var row = new Control { Name = "Rule", Size = new Vector2(ContentWidth, 8), MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(new ColorRect { Color = new Color(.93f, .77f, .45f, .32f), Position = new Vector2(0, 4), Size = new Vector2(ContentWidth, 1), MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(RoyalKit.Image("result-diamond", new Rect2(ContentWidth / 2 - 7, -3, 14, 15)));
        _content.AddChild(row);
        return row;
    }

    /// <summary>Who the device is signed in as: the provider's mark, the account and the nickname.</summary>
    private Control AccountCard()
    {
        var card = new Panel { Name = "AccountCard", Size = new Vector2(ContentWidth, 96), MouseFilter = MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", RoyalKit.Slice("tile", 9));
        // A company's logo sits on the same white tile as on its sign-in button.
        _accountTile = new Panel { Position = new Vector2(16, 18), Size = new Vector2(SignInButton.TileSize, SignInButton.TileSize), MouseFilter = MouseFilterEnum.Ignore };
        var white = new StyleBoxFlat { BgColor = Colors.White, AntiAliasing = true };
        white.SetCornerRadiusAll(6);
        _accountTile.AddThemeStyleboxOverride("panel", white);
        card.AddChild(_accountTile);
        _accountMark = new TextureRect { Position = new Vector2(24, 26), Size = new Vector2(44, 44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        card.AddChild(_accountMark);
        _accountName = RoyalText.Serif("", 24, RoyalText.Cream, 700);
        _accountName.Position = new Vector2(92, 14); _accountName.Size = new Vector2(ContentWidth - 110, 36);
        card.AddChild(_accountName);
        _accountDetail = RoyalText.Serif("", 18, Soft, 500);
        _accountDetail.Position = new Vector2(92, 50); _accountDetail.Size = new Vector2(ContentWidth - 110, 30);
        card.AddChild(_accountDetail);
        _content.AddChild(card);
        return card;
    }

    private void RefreshAccountCard()
    {
        var state = GameState.Instance;
        var provider = PlatformSignIn.ProviderName(state.AccountProvider);
        _accountName.Text = state.AccountLabel.Length > 0 ? state.AccountLabel : state.PlayerCallsign;
        _accountDetail.Text = $"{provider} · {state.PlayerCallsign}";
        var logo = state.AccountProvider switch
        {
            "google" => SignInButton.LogoArt(SignInButton.Brand.Google),
            "apple" => SignInButton.LogoArt(SignInButton.Brand.Apple),
            PlatformSignIn.PlayGames => SignInButton.LogoArt(SignInButton.Brand.PlayGames),
            PlatformSignIn.GameCenter => SignInButton.LogoArt(SignInButton.Brand.GameCenter),
            _ => null
        };
        _accountTile.Visible = logo != null;
        _accountMark.Texture = logo ?? RoyalKit.Texture("icon-account");
    }

    private void Say(string text, bool error = false)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? Error : Soft);
        _status.Visible = text.Length > 0;
        _rule.Visible = text.Length == 0;
    }

    // ---- Sign-in ----------------------------------------------------------------------------------------------

    private void Busy(bool busy)
    {
        _busy = busy;
        // While the server is asked what it offers, only the sign-ins wait: the nickname and Play as guest never do.
        var wait = busy || _loading;
        _emailButton.Disabled = wait || !_emailAvailable; _verifyButton.Disabled = busy; _googleButton.Disabled = wait || !_googleAvailable;
        _appleButton.Disabled = wait || !_appleAvailable;
        _platformButton.Disabled = wait || !_platformAvailable;
        _devButton.Disabled = wait || !_devAvailable;
        _nickname.Editable = !busy; _suggestButton.Disabled = busy;
        _guestButton.Disabled = _signOutButton.Disabled = _doneButton.Disabled = busy;
    }

    private bool _loading;

    private async Task LoadProviders()
    {
        _loading = true; Busy(_busy);
        try
        {
            var providers = await AccountSignIn.Request("/auth/providers"); if (_closed) return;
            _providers = providers;
            _emailAvailable = providers.GetProperty("email").GetBoolean();
            _googleAvailable = providers.GetProperty("google").GetBoolean();
            _appleAvailable = providers.TryGetProperty("apple", out var apple) && apple.ValueKind == JsonValueKind.True;
            _platformAvailable = PlatformSignIn.Offered(providers);
            _devAvailable = providers.TryGetProperty("devSignIn", out var dev) && dev.ValueKind == JsonValueKind.True;
            // A browser sign-in the server doesn't offer is left out, not shown greyed out.
            _providersLoaded = true; _loading = false;
            Layout();
            Busy(_busy);
            if (!_signedIn && _emailButton.Disabled && _appleButton.Disabled && _googleButton.Disabled && _platformButton.Disabled)
                Say("Sign-in needs to be enabled on the game server.");
        }
        // Offline (or with no server), a guest can still play; the welcome says so calmly rather than as an error.
        catch (Exception) { if (!_closed && !_signedIn) Say("Sign-in is offline. You can sign in later in Settings."); }
        finally { _loading = false; if (!_closed) Busy(_busy); }
    }

    /// <summary>Saves the nickname field before an identity is linked, so the new account is created with it.</summary>
    private bool ApplyNickname()
    {
        if (!_nickname.Visible) return true;
        // The welcome always records the choice, even when the player keeps the name already shown.
        if (!_welcome && _nickname.Text.Trim() == GameState.Instance.PlayerCallsign) return true;
        if (GameState.Instance.TryChooseCallsign(_nickname.Text, out var reason)) return true;
        Say($"Nickname: {reason}", true);
        _nickname.GrabFocus();
        return false;
    }

    private void PlayAsGuest()
    {
        if (_busy || !ApplyNickname()) return;
        AudioDirector.Instance?.PlayUpgradeConfirm();
        Close();
    }

    private void Finish()
    {
        if (_busy || !ApplyNickname()) return;
        Close();
    }

    private async Task SendCode()
    {
        if (_busy || !ApplyNickname()) return; Busy(true); Say("Sending…");
        try
        {
            var response = await AccountSignIn.Request("/auth/email/start", new AccountSignIn.EmailStartRequest { Email = _email.Text }, ServerJson.Default.EmailStartRequest); if (_closed) return;
            _challenge = AccountSignIn.Text(response, "challengeId"); _secret = AccountSignIn.Text(response, "secret");
            _codeSent = true; Layout();
            _code.GrabFocus(); Say("Check your email. The code expires in 10 minutes.");
        }
        catch (Exception e) { if (!_closed) Say(e.Message, true); }
        finally { if (!_closed) Busy(false); }
    }

    private async Task Verify()
    {
        if (_busy || !ApplyNickname()) return; Busy(true); Say("Signing in…");
        try { await Complete(await AccountSignIn.Request("/auth/email/verify",
            new AccountSignIn.EmailVerifyRequest { ChallengeId = _challenge, Secret = _secret, Code = _code.Text.Trim() }, ServerJson.Default.EmailVerifyRequest)); }
        catch (Exception e) { if (!_closed) Say(e.Message, true); }
        finally { if (!_closed) Busy(false); }
    }

    /// <summary>Apple and Google sign in through the browser while the game polls with its own secret.</summary>
    private async Task Browser(string provider)
    {
        if (_busy || !ApplyNickname()) return; Busy(true); Say("Finish signing in in your browser.");
        try
        {
            var response = await AccountSignIn.Request($"/auth/{provider}/start", new AccountSignIn.EmptyRequest(), ServerJson.Default.EmptyRequest); if (_closed) return;
            var challenge = AccountSignIn.Text(response, "challengeId"); var secret = AccountSignIn.Text(response, "secret");
            OS.ShellOpen(AccountSignIn.Text(response, "url"));
            var deadline = DateTime.UtcNow.AddMinutes(10);
            while (!_closed && DateTime.UtcNow < deadline)
            {
                await Task.Delay(2000); if (_closed) return;
                var session = await AccountSignIn.Request($"/auth/{provider}/poll",
                    new AccountSignIn.PollRequest { ChallengeId = challenge, Secret = secret }, ServerJson.Default.PollRequest);
                if (AccountSignIn.Text(session, "status") == "pending") continue;
                await Complete(session); return;
            }
            if (!_closed) Say("Sign-in expired. Try again.", true);
        }
        catch (Exception e) { if (!_closed) Say(e.Message, true); }
        finally { if (!_closed) Busy(false); }
    }

    private async Task Platform()
    {
        if (_busy || !ApplyNickname()) return; Busy(true); Say("Signing in…");
        try { await Complete(await PlatformSignIn.SignIn(_providers, interactive: true)); }
        catch (Exception e) { if (!_closed) Say(e.Message, true); }
        finally { if (!_closed) Busy(false); }
    }

    private async Task DeveloperSignIn()
    {
        if (_busy || !ApplyNickname()) return; Busy(true); Say("Signing in…");
        var email = _email.Text.Trim().Length > 0 ? _email.Text.Trim() : "developer@example.com";
        try { await Complete(await AccountSignIn.Request("/auth/dev/sign-in", new AccountSignIn.DevSignInRequest { Email = email }, ServerJson.Default.DevSignInRequest)); }
        catch (Exception e) { if (!_closed) Say(e.Message, true); }
        finally { if (!_closed) Busy(false); }
    }

    private async Task SignOut()
    {
        if (_busy) return; Busy(true);
        try
        {
            try { await AccountSignIn.Request("/auth/signout", new AccountSignIn.EmptyRequest(), ServerJson.Default.EmptyRequest); }
            // An account deleted meanwhile is forgotten by the device, not kept as a backup.
            catch (AccountSignIn.AccountDeletedException) { if (!_closed) Close(); return; }
            catch { /* Local sign-out remains available offline. */ }
            GameState.Instance.StartAccountSave();
            GameState.Instance.PauseAutoSignIn();
            if (!_closed) { Close(); SceneRouter.Instance.ReloadHome(); }
        }
        catch (Exception e) { if (!_closed) { Say(e.Message, true); Busy(false); } }
    }

    private async Task Complete(JsonElement session)
    {
        if (_closed) return;
        await AccountSignIn.Apply(session);
        if (_closed) return;
        Close(); SceneRouter.Instance.ReloadHome();
    }
}
