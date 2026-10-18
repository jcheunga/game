using Godot;

/// <summary>
/// The player's card in the home HUD, between the zone banner and the settings ring: the level medallion
/// (its ring is the XP bar), the nickname and the level line. It opens the level track, and a new caravan
/// is asked for its nickname here.
/// </summary>
public partial class MapMenu
{
    private static readonly Rect2 PlayerCardRect = new(864, 11, 324, 66);
    private LevelMedallion _medallion;
    private RoyalLabel _playerName, _playerLevel;
    private RoyalButton _playerCard;

    private void BuildPlayerCard()
    {
        // The resource strip's parchment, with the medallion overlapping its left end like a seal.
        var bar = new Panel { Name = "PlayerBar", Position = new Vector2(PlayerCardRect.Position.X + 30, 13), Size = new Vector2(PlayerCardRect.End.X - PlayerCardRect.Position.X - 30, 62), MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("panel", RoyalKit.Slice("res-bar", 30, 0, 30, 0));
        _hud.AddChild(bar);
        var ink = new Color("121210");
        _playerName = RoyalText.Serif("", 21, ink, 700);
        _playerName.ShadowOffset = Vector2.Zero;
        RoyalText.Place(_hud, _playerName, PlayerCardRect.Position.X + 80, 21, PlayerCardRect.End.X - PlayerCardRect.Position.X - 104, 26);
        _playerLevel = RoyalText.Caps("", 12, new Color("5b4a33"), 650);
        _playerLevel.ShadowOffset = Vector2.Zero;
        RoyalText.Place(_hud, _playerLevel, PlayerCardRect.Position.X + 80, 47, PlayerCardRect.End.X - PlayerCardRect.Position.X - 104, 18);
        _medallion = new LevelMedallion { Name = "PlayerLevelMedallion", Position = PlayerCardRect.Position, Size = new Vector2(66, 66) };
        _hud.AddChild(_medallion);
        _playerCard = RoyalButton.Over(PlayerCardRect, "Player level", OpenLevelTrack, 10);
        _playerCard.Name = "PlayerCard";
        _hud.AddChild(_playerCard);
    }

    private void RefreshPlayerCard()
    {
        var state = GameState.Instance;
        var ready = state.ClaimableLevelRewardCount;
        _medallion.Level = state.PlayerLevel;
        _medallion.Progress = state.IsMaxPlayerLevel ? 1f : state.PlayerXPIntoLevel / (float)Mathf.Max(1, state.PlayerXPForNextLevel);
        _medallion.Badge = ready;
        _medallion.Lit = ready > 0;
        _playerName.Text = state.PlayerCallsign;
        _playerLevel.Text = state.IsMaxPlayerLevel ? $"LEVEL {state.PlayerLevel} · MAX"
            : $"LEVEL {state.PlayerLevel} · {state.PlayerXPIntoLevel:N0} / {state.PlayerXPForNextLevel:N0} XP";
        _playerCard.TooltipText = ready > 0 ? $"{state.PlayerCallsign} · level {state.PlayerLevel} · {ready} reward{(ready == 1 ? "" : "s")} to claim"
            : $"{state.PlayerCallsign} · level {state.PlayerLevel}";
    }

    public void OpenLevelTrack() => OpenHomeDestination("levels");

    /// <summary>
    /// Once the home map is up: explains an account deleted elsewhere, then asks a new caravan for its nickname.
    /// </summary>
    private void ShowArrivalPrompts()
    {
        var state = GameState.Instance;
        if (!state.AccountDeletedNoticePending && !state.ShouldPromptForNickname) return;
        void AskNickname() { if (IsInsideTree() && GameState.Instance.ShouldPromptForNickname) NicknameDialog.Show(this, true, RefreshUi); }
        Callable.From(() =>
        {
            if (!IsInsideTree()) return;
            if (GameState.Instance.AccountDeletedNoticePending) ShowAccountDeletedNotice(AskNickname);
            else AskNickname();
        }).CallDeferred();
    }

    /// <summary>
    /// The device reset itself because its account was deleted on another device or by support. Deleting it in
    /// Settings needs no notice: the player just confirmed it.
    /// </summary>
    private void ShowAccountDeletedNotice(System.Action acknowledged)
    {
        var notice = MedievalUi.ShowNotice(this, "Account deleted",
            "Your account was deleted on another device or by support. You're playing as a new guest.",
            () => { GameState.Instance.AcknowledgeAccountDeletedNotice(); acknowledged(); });
        notice.Name = "AccountDeletedNotice";
    }
}
