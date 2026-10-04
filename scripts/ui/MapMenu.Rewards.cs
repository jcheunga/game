using Godot;

public partial class MapMenu
{
    private HBoxContainer AddReward(string label, string icon, string amount, string hint)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var caption = RealmUi.Label(label, 20, true);
        caption.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(caption);
        row.AddChild(HomeResourceUi.Amount(icon, amount, hint));
        _rewards.AddChild(row);
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
            if (stage.RewardFood > 0) victory.AddChild(HomeResourceUi.Amount("food", $"+{stage.RewardFood}", $"Victory · {stage.RewardFood} rations"));
        }
        else if (_selected.Kind == AdventureSiteKind.Gold) AddReward("Supplies", "gold", $"+{_selected.GoldReward:N0}", $"{_selected.GoldReward:N0} gold");
        else if (_selected.Kind == AdventureSiteKind.Food) AddReward("Supplies", "food", $"+{_selected.FoodReward}", $"{_selected.FoodReward} rations");
        if (leader) AddEntryCost(GameState.Instance.GetStageEntryFoodCost(_selected.Stage));
        if (!leader) _rewards.AddChild(RealmUi.Label("Opens surrounding tiles", 18, true));
    }

    private void ShowDiscoveryRewards()
    {
        RealmUi.Clear(_rewards);
        var survey = _selectedDiscovery.Kind == AdventureDiscoveryKind.Survey;
        AddReward(survey ? "Survey" : "Supplies", _selectedDiscovery.Icon,
            survey ? "Nearby terrain" : $"+{_selectedDiscovery.Amount:N0}", _selectedDiscovery.RewardText);
        _rewards.AddChild(RealmUi.Label("Opens surrounding tiles", 18, true));
    }
}
