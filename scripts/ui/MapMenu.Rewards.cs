using Godot;

public partial class MapMenu
{
    private HBoxContainer AddReward(string label, string icon, string amount, string hint)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var caption = SiteText(label, SiteInk);
        caption.AutowrapMode = TextServer.AutowrapMode.Off;
        caption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        caption.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(caption);
        row.AddChild(SiteAmount(icon, amount, hint));
        _rewards.AddChild(row);
        return row;
    }

    /// <summary>A painted resource icon and its amount in bold ink, as the card's reward rows show them.</summary>
    private static HBoxContainer SiteAmount(string icon, string amount, string hint)
    {
        var row = new HBoxContainer { TooltipText = hint, AccessibilityName = hint, MouseFilter = MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(new TextureRect { Texture = HomeMapArt.Icon(icon), CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
        var number = SiteText(amount, SiteInk, 700);
        number.AutowrapMode = TextServer.AutowrapMode.Off;
        number.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(number);
        return row;
    }

    private void AddEntryCost(int entry) => AddReward("Battle entry", "food", entry.ToString(), $"Battle entry · {entry} rations");

    private void ShowSiteRewards(bool known, bool leader, StageDefinition stage)
    {
        RealmUi.Clear(_rewards);
        if (!known) return;
        if (leader)
        {
            var victory = AddReward("Victory rewards", "gold", $"+{stage.RewardGold:N0}", $"Victory · {stage.RewardGold:N0} gold");
            if (stage.RewardFood > 0) victory.AddChild(SiteAmount("food", $"+{stage.RewardFood}", $"Victory · {stage.RewardFood} rations"));
        }
        else if (_selected.Kind == AdventureSiteKind.Gold) AddReward("Supplies", "gold", $"+{_selected.GoldReward:N0}", $"{_selected.GoldReward:N0} gold");
        else if (_selected.Kind == AdventureSiteKind.Food) AddReward("Supplies", "food", $"+{_selected.FoodReward}", $"{_selected.FoodReward} rations");
        if (leader) AddEntryCost(GameState.Instance.GetStageEntryFoodCost(_selected.Stage));
        if (!leader) _rewards.AddChild(SiteText("Opens surrounding tiles", SiteFaded));
    }

    private void ShowDiscoveryRewards()
    {
        RealmUi.Clear(_rewards);
        var survey = _selectedDiscovery.Kind == AdventureDiscoveryKind.Survey;
        AddReward(survey ? "Survey" : "Supplies", _selectedDiscovery.Icon,
            survey ? "Nearby land" : $"+{_selectedDiscovery.Amount:N0}", _selectedDiscovery.RewardText);
        _rewards.AddChild(SiteText(survey ? "Charts the plain land around it" : "Reveals the land around it", SiteFaded));
    }
}
