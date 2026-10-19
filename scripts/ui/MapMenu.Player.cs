using Godot;

/// <summary>
/// The player's card at the top left of the home HUD: the level medallion (its ring is the XP bar, its badge
/// the rewards waiting) and the nickname. It opens the level track, which shows the XP in full, and a new
/// caravan is asked for its nickname here.
/// </summary>
public partial class MapMenu
{
    private static readonly Rect2 PlayerCardRect = new(19, 11, 330, 66);
    private const float MedallionSize = 66, NameLeft = 80, NameEndPadding = 26;
    private LevelMedallion _medallion;
    private RoyalLabel _playerName;
    private Panel _playerBar;
    private RoyalButton _playerCard;

    private void BuildPlayerCard()
    {
        // The resource strip's parchment, with the medallion overlapping its left end like a seal.
        _playerBar = new Panel { Name = "PlayerBar", Position = new Vector2(PlayerCardRect.Position.X + 30, 13), Size = new Vector2(PlayerCardRect.Size.X - 30, 62), MouseFilter = MouseFilterEnum.Ignore };
        _playerBar.AddThemeStyleboxOverride("panel", RoyalKit.Slice("res-bar", 30, 0, 30, 0));
        _hud.AddChild(_playerBar);
        _playerName = RoyalText.Serif("", 22, new Color("121210"), 700);
        _playerName.ShadowOffset = Vector2.Zero;
        // Centred on the parchment like the resource amounts (its middle sits at y 45.5).
        RoyalText.Place(_hud, _playerName, PlayerCardRect.Position.X + NameLeft, 32, PlayerCardRect.Size.X - NameLeft - NameEndPadding, 27);
        _medallion = new LevelMedallion { Name = "PlayerLevelMedallion", Position = new Vector2(PlayerCardRect.Position.X, 12), Size = new Vector2(MedallionSize, MedallionSize) };
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
        // The parchment hugs the nickname rather than leaving an empty run after a short name.
        var width = Mathf.Clamp(NameLeft + _playerName.TextWidth(_playerName.FontSize) + NameEndPadding, 200, PlayerCardRect.Size.X);
        _playerBar.Size = new Vector2(width - 30, _playerBar.Size.Y);
        _playerCard.Size = new Vector2(width, PlayerCardRect.Size.Y);
        var xp = state.IsMaxPlayerLevel ? "highest level" : $"{state.PlayerXPIntoLevel:N0} / {state.PlayerXPForNextLevel:N0} XP";
        _playerCard.TooltipText = $"{state.PlayerCallsign} · level {state.PlayerLevel} · {xp}{(ready > 0 ? $" · {ready} reward{(ready == 1 ? "" : "s")} to claim" : "")}";
    }

    public void OpenLevelTrack() => OpenHomeDestination("levels");

    /// <summary>Asks a new caravan for its nickname once the home map is up.</summary>
    private void PromptForNicknameIfNeeded()
    {
        if (!GameState.Instance.ShouldPromptForNickname) return;
        Callable.From(() =>
        {
            if (IsInsideTree() && GameState.Instance.ShouldPromptForNickname) NicknameDialog.Show(this, true, RefreshUi);
        }).CallDeferred();
    }
}
