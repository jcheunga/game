using System;
using Godot;

/// <summary>
/// Asks for the nickname other players see. A new caravan meets it once on the home map (with no way to
/// skip, but a suggested name already filled in); the profile and level track reopen it to rename.
/// </summary>
public static class NicknameDialog
{
    private static readonly string[] Firsts = { "Ashen", "Iron", "Gilded", "Wandering", "Ember", "Grey", "Thorn", "Oaken", "Silver", "Raven", "Stone", "Lantern", "Hollow", "Bright" };
    private static readonly string[] Seconds = { "Warden", "Banner", "Herald", "Lance", "Wolf", "Crown", "Hearth", "Squire", "Marshal", "Falcon", "Shield", "Rider", "Road", "Vow" };

    public static string Suggest()
    {
        var name = $"{Firsts[GD.RandRange(0, Firsts.Length - 1)]} {Seconds[GD.RandRange(0, Seconds.Length - 1)]}";
        return name.Length <= GameState.MaxCallsignLength ? name : name.Split(' ')[1];
    }

    /// <summary>Opens the dialog over <paramref name="host"/>; <paramref name="chosen"/> runs once a name is saved.</summary>
    public static void Show(Control host, bool firstRun, Action chosen = null)
    {
        var state = GameState.Instance;
        var stack = MedievalUi.CreateModal(host, new Vector2(480f, 0f), 14, out var veil, out var center, out var panel);
        center.Name = "NicknameDialog";
        void Close() { veil.QueueFree(); center.QueueFree(); }

        var heading = RealmUi.Heading(firstRun ? "Choose a nickname" : "Change nickname", 26);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(heading);
        // Leaderboards show anonymous tags, so only players racing you can see the nickname.
        var seen = LaunchFeatures.InternetRooms ? "Players racing you in rooms and LAN races see it." : "Players racing you over your local network see it.";
        var body = RealmUi.Label(firstRun ? seen + " You can change it later." : seen, 18, true);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(body);

        var field = new LineEdit
        {
            Name = "NicknameField", PlaceholderText = "Nickname", MaxLength = GameState.MaxCallsignLength,
            Text = firstRun && !state.HasChosenCallsign ? Suggest() : state.PlayerCallsign,
            CustomMinimumSize = new Vector2(0, 50), Alignment = HorizontalAlignment.Center, SelectAllOnFocus = true
        };
        stack.AddChild(field);
        var error = RealmUi.Label("", 16);
        error.Name = "NicknameError";
        error.HorizontalAlignment = HorizontalAlignment.Center;
        error.AddThemeColorOverride("font_color", new Color("f0a493"));
        error.Visible = false;
        stack.AddChild(error);

        void Confirm()
        {
            if (!state.TryChooseCallsign(field.Text, out var reason))
            {
                error.Text = reason; error.Visible = true;
                field.GrabFocus();
                return;
            }
            AudioDirector.Instance?.PlayUpgradeConfirm();
            Close();
            chosen?.Invoke();
        }
        field.TextSubmitted += _ => Confirm();
        field.TextChanged += _ => error.Visible = false;

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 10);
        stack.AddChild(row);
        if (!firstRun) row.AddChild(RealmUi.Button("close", "Cancel", Close));
        row.AddChild(RealmUi.Button("star", "Suggest", () => { field.Text = Suggest(); error.Visible = false; }));
        row.AddChild(RealmUi.Button("check", "Confirm", Confirm, true).With(b => b.Name = "ConfirmNickname"));

        panel.Modulate = new Color(1f, 1f, 1f, 0f);
        panel.PivotOffset = panel.CustomMinimumSize * 0.5f;
        host.CreateTween().TweenProperty(panel, "modulate:a", 1f, 0.18f);
        Callable.From(() => { if (GodotObject.IsInstanceValid(field)) { field.GrabFocus(); field.SelectAll(); } }).CallDeferred();
    }
}
