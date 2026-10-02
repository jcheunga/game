using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

public partial class AccountDialog : AcceptDialog
{
    private Label _status;
    private LineEdit _email, _code;
    private Button _emailButton, _verifyButton, _googleButton;
    private string _challenge = "", _secret = "";
    private bool _busy, _closed, _emailAvailable, _googleAvailable;

    public static void Show(Control host)
    {
        var dialog = new AccountDialog { Title = "Account", Exclusive = true, Theme = host.Theme, MinSize = new Vector2I(380, 300) };
        host.AddChild(dialog); dialog.PopupCentered(new Vector2I(540, 430));
    }
    public override void _Ready()
    {
        GetOkButton().Text = "Close";
        Confirmed += Close; Canceled += Close;
        var scroll = new ScrollContainer { OffsetLeft = 18, OffsetTop = 12, OffsetRight = -18, OffsetBottom = -52, AnchorRight = 1, AnchorBottom = 1,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        var stack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 12); scroll.AddChild(stack);
        _status = RealmUi.Label(GameState.Instance.AccountProvider.Length > 0 ? $"Signed in · {GameState.Instance.AccountLabel}" : "Save your journey across devices.", 18, true);
        stack.AddChild(_status);
        _email = new LineEdit { PlaceholderText = "Email address", CustomMinimumSize = new Vector2(0, 48), MaxLength = 254 };
        stack.AddChild(_email);
        _emailButton = RealmUi.Button("arrow", "Send sign-in code", () => _ = SendCode()); stack.AddChild(_emailButton);
        var row = new HBoxContainer(); stack.AddChild(row);
        _code = new LineEdit { PlaceholderText = "6-digit code", MaxLength = 6, Visible = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 48) }; row.AddChild(_code);
        _verifyButton = RealmUi.Button("check", "Sign in", () => _ = Verify()); _verifyButton.Hide(); row.AddChild(_verifyButton);
        _googleButton = RealmUi.Button("people", "Continue with Google", () => _ = Google()); stack.AddChild(_googleButton);
        if (GameState.Instance.AccountProvider.Length > 0)
        {
            var signout = RealmUi.Button("back", "Sign out", () => _ = SignOut());
            stack.AddChild(signout);
        }
        _ = LoadProviders();
    }
    private void Close() { _closed = true; QueueFree(); }
    private void Busy(bool busy)
    { _busy = busy; _emailButton.Disabled = busy || !_emailAvailable; _verifyButton.Disabled = busy; _googleButton.Disabled = busy || !_googleAvailable; }
    private async Task LoadProviders()
    {
        Busy(true);
        try
        {
            var providers = await AccountSignIn.Request("/auth/providers"); if (_closed) return;
            _emailAvailable = providers.GetProperty("email").GetBoolean();
            _googleAvailable = providers.GetProperty("google").GetBoolean();
            Busy(false);
            if (_emailButton.Disabled && _googleButton.Disabled) _status.Text = "Sign-in needs to be enabled on the game server.";
        }
        catch (Exception e) { if (!_closed) _status.Text = e.Message; }
        finally { if (!_closed) Busy(false); }
    }
    private async Task SendCode()
    {
        if (_busy) return; Busy(true); _status.Text = "Sending…";
        try
        {
            var response = await AccountSignIn.Request("/auth/email/start", new { email = _email.Text }); if (_closed) return;
            _challenge = AccountSignIn.Text(response, "challengeId"); _secret = AccountSignIn.Text(response, "secret");
            _code.Show(); _verifyButton.Show(); _code.GrabFocus(); _status.Text = "Check your email. Code expires in 10 minutes.";
        }
        catch (Exception e) { if (!_closed) _status.Text = e.Message; }
        finally { if (!_closed) Busy(false); }
    }
    private async Task Verify()
    {
        if (_busy) return; Busy(true); _status.Text = "Signing in…";
        try { await Finish(await AccountSignIn.Request("/auth/email/verify", new { challengeId = _challenge, secret = _secret, code = _code.Text.Trim() })); }
        catch (Exception e) { if (!_closed) _status.Text = e.Message; }
        finally { if (!_closed) Busy(false); }
    }
    private async Task Google()
    {
        if (_busy) return; Busy(true); _status.Text = "Finish sign-in in your browser.";
        try
        {
            var response = await AccountSignIn.Request("/auth/google/start", new { }); if (_closed) return;
            var challenge = AccountSignIn.Text(response, "challengeId"); var secret = AccountSignIn.Text(response, "secret");
            OS.ShellOpen(AccountSignIn.Text(response, "url"));
            var deadline = DateTime.UtcNow.AddMinutes(10);
            while (!_closed && DateTime.UtcNow < deadline)
            {
                await Task.Delay(2000); if (_closed) return;
                var session = await AccountSignIn.Request("/auth/google/poll", new { challengeId = challenge, secret });
                if (AccountSignIn.Text(session, "status") == "pending") continue;
                await Finish(session); return;
            }
            if (!_closed) _status.Text = "Sign-in expired. Try again.";
        }
        catch (Exception e) { if (!_closed) _status.Text = e.Message; }
        finally { if (!_closed) Busy(false); }
    }
    private async Task SignOut()
    {
        if (_busy) return; Busy(true);
        try
        {
            try { await AccountSignIn.Request("/auth/signout", new { }); } catch { /* Local sign-out remains available offline. */ }
            GameState.Instance.StartAccountSave();
            if (!_closed) { Close(); SceneRouter.Instance.ReloadHome(); }
        }
        catch (Exception e) { if (!_closed) { _status.Text = e.Message; Busy(false); } }
    }
    private async Task Finish(JsonElement session)
    {
        if (_closed) return;
        await AccountSignIn.Apply(session);
        if (_closed) return;
        Close(); SceneRouter.Instance.ReloadHome();
    }
}
