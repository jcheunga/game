using System;
using System.Linq;
using Godot;

public partial class AchievementsPanel : VBoxContainer
{
    private GridContainer _grid;
    private Label _summary, _status, _pageLabel;
    private Button _previous, _next;
    private int _page;
    private string _category = "All";
    private const int PageSize = 6;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 12);
        _summary = RealmUi.Label("", 18, true); AddChild(_summary); _summary.Hide();
        var categories = new[] { "All", "Campaign", "Combat", "Endless", "Collection", "Mastery" };
        var tabs = RealmUi.Tabs(this, index => { _category = categories[index]; _page = 0; Refresh(); }, categories); RealmModal.Polish(tabs);
        var stack = RealmUi.Scroll(this);
        _grid = new GridContainer { Columns = 3 }; _grid.AddThemeConstantOverride("h_separation", 14); _grid.AddThemeConstantOverride("v_separation", 14); stack.AddChild(_grid);
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 10); AddChild(footer);
        _previous = HomeMapUi.IconButton("back", "Previous achievement page", () => { _page--; Refresh(); }); footer.AddChild(_previous);
        _pageLabel = RealmUi.Label("", 18, true); _pageLabel.CustomMinimumSize = new Vector2(72, 0); _pageLabel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter; _pageLabel.HorizontalAlignment = HorizontalAlignment.Center; _pageLabel.VerticalAlignment = VerticalAlignment.Center; _pageLabel.AutowrapMode = TextServer.AutowrapMode.Off; footer.AddChild(_pageLabel);
        _next = HomeMapUi.IconButton("arrow", "Next achievement page", () => { _page++; Refresh(); }); footer.AddChild(_next);
        ModalUi.StyleButton(_previous); ModalUi.StyleButton(_next);
        _status = RealmUi.Label("", 18, true); _status.VerticalAlignment = VerticalAlignment.Center; footer.AddChild(_status);
        Refresh();
    }

    private void Refresh()
    {
        var state = GameState.Instance;
        RealmModal.UpdateHeading(this, subtitle: $"{state.GetUnlockedAchievementCount()}/{AchievementCatalog.GetAll().Count} complete · {state.GetUnclaimedAchievementRewardCount()} rewards ready");
        _summary.Text = $"{state.GetUnlockedAchievementCount()}/{AchievementCatalog.GetAll().Count} completed · {state.GetUnclaimedAchievementRewardCount()} rewards ready";
        var entries = AchievementCatalog.GetAll().Where(a => _category == "All" || a.Category.Equals(_category, StringComparison.OrdinalIgnoreCase)).ToArray();
        var pages = Math.Max(1, (entries.Length + PageSize - 1) / PageSize); _page = Math.Clamp(_page, 0, pages - 1);
        _previous.Disabled = _page == 0; _next.Disabled = _page >= pages - 1; _pageLabel.Text = $"{_page + 1} of {pages}";
        RealmUi.Clear(_grid);
        foreach (var entry in entries.Skip(_page * PageSize).Take(PageSize))
        {
            bool done = state.IsAchievementUnlocked(entry.Id), claimed = state.HasClaimedAchievementReward(entry.Id);
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 176), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var accent = entry.Category.ToLowerInvariant() switch { "campaign" => new Color("6387b9"), "combat" => new Color("bb6671"), "endless" => new Color("bc7b46"), "collection" => new Color("8265b1"), _ => new Color("599b7d") };
            panel.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Inset, 12)); _grid.AddChild(panel);
            var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 8); panel.AddChild(stack);
            var cap = new PanelContainer(); cap.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Tab, 8, accent, done)); stack.AddChild(cap);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); cap.AddChild(row);
            row.AddChild(new TextureRect { Texture = HomeMapArt.Icon(entry.Category.ToLowerInvariant() switch { "campaign" => "star", "combat" => "sword", "endless" => "flame", "collection" => "book", _ => "hammer" }), CustomMinimumSize = new Vector2(42,42), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = done ? Colors.White : new Color(.7f,.75f,.74f) });
            var title = RealmUi.Heading(entry.Title, 18); title.VerticalAlignment = VerticalAlignment.Center; row.AddChild(title);
            title.AddThemeColorOverride("font_color", done ? new Color("ffe3a1") : ModalUi.Cream);
            var description = RealmUi.Label(entry.Description, 18, true); description.AddThemeFontSizeOverride("font_size", 18); description.SizeFlagsVertical = SizeFlags.ExpandFill; stack.AddChild(description);
            var (value, target) = Progress(entry.Id);
            var progress = new ProgressBar { MaxValue = target, Value = done ? target : value, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) }; ModalUi.StyleProgress(progress, done ? new Color("8dd274") : accent.Lightened(.25f)); stack.AddChild(progress);
            var reward = AchievementRewardCatalog.GetForAchievement(entry.Id);
            var amount = reward == null ? "" : $"+{reward.RewardAmount:N0}";
            var text = claimed ? $"Claimed · {amount}" : done ? "Claim " + amount : target > 1 ? $"{value}/{target} · {amount}" : amount;
            var button = RealmUi.Button(done && !claimed ? "gift" : claimed ? "star" : "lock", text, () => { state.TryClaimAchievementReward(entry.Id, out var message); _status.Text = message; Refresh(); }, done && !claimed);
            if (reward != null)
            {
                button.Icon = UiArtLoader.TryLoadRewardIcon(reward.RewardType, reward.RewardItemId);
                button.SetMeta("painted_resource_icon", true);
                button.AddThemeConstantOverride("icon_max_width", 24);
                button.AccessibilityName = $"{entry.Title}, {reward.RewardLabel}, {(claimed ? "claimed" : done ? "ready to claim" : "in progress")}";
                button.TooltipText = button.AccessibilityName;
            }
            button.CustomMinimumSize = new Vector2(0, 44); button.Disabled = !done || claimed || reward == null; ModalUi.StyleButton(button, done && !claimed); stack.AddChild(button);
            if (claimed) button.AddThemeStyleboxOverride("disabled", new ModalSurface(ModalMaterial.Tab, 8, new Color("5e9971"), true));
            RealmModal.Polish(description);
        }
    }

    private static (int Value, int Target) Progress(string id)
    {
        var s = GameState.Instance;
        (int, int) result = id switch {
            "campaign_complete" => (CampaignPlanCatalog.GetAll().Count(d => s.IsDistrictCleared(d.Id)), CampaignPlanCatalog.GetAll().Count),
            "all_stars" => (Enumerable.Range(1, s.MaxStage).Count(stage => s.GetStageStars(stage) >= 3), 40),
            "endless_30" => (s.BestEndlessWave, 30), "endless_60" => (s.BestEndlessWave, 60), "endless_90" => (s.BestEndlessWave, 90),
            "relic_collector" => (s.GetOwnedEquipment().Count, 6), "full_armory" => (s.GetOwnedEquipment().Count, 12),
            "full_roster" => (GameData.GetPlayerUnits().Count(unit => s.IsUnitOwned(unit.Id)), GameData.PlayerRosterIds.Length),
            "all_spells" => (GameData.GetPlayerSpells().Count(spell => s.IsSpellOwned(spell.Id)), GameData.PlayerSpellIds.Length),
            "codex_10" => (s.DiscoveredCodexCount, 10), "codex_complete" => (s.DiscoveredCodexCount, CodexCatalog.TotalEntries),
            "tower_25" => (s.TowerHighestFloor, 25), "tower_50" => (s.TowerHighestFloor, 50), "tower_100" => (s.TowerHighestFloor, 100),
            "expedition_10" => (s.TotalExpeditionsCompleted, 10), "hard_mode_10" => (s.HardModeClearedCount, 10),
            _ => (s.IsAchievementUnlocked(id) ? 1 : 0, 1) };
        return (Math.Min(result.Item1, result.Item2), result.Item2);
    }
}
