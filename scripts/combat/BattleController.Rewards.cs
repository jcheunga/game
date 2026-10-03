using System.Linq;
using Godot;

public partial class BattleController
{
    private GameSaveData _battleRewardStart;
    private ScrollContainer _endReportScroll;
    private VBoxContainer _endContent;
    private MarginContainer _endPadding;

    private void PresentVictoryRewards()
    {
        var rewards = BattleRewardUi.Earned(_battleRewardStart, GameState.Instance.BuildSaveData());
        BattleSummaryData.Current = new BattleSummaryData {
            Won = true, StarsEarned = _endStarRating.Stars, Stage = _stage, BattleMode = _battleMode.ToString(),
            Rewards = rewards, GoldEarned = rewards.Where(reward => reward.Kind == "gold").Sum(reward => reward.Amount),
            FoodEarned = rewards.Where(reward => reward.Kind == "food").Sum(reward => reward.Amount),
            SeasonXPEarned = rewards.Where(reward => reward.Kind == "season_xp").Sum(reward => reward.Amount),
            MasteryXPPerUnit = rewards.Where(reward => reward.Kind == "mastery").ToDictionary(reward => reward.ItemId, reward => reward.Amount)
        };
        _endLabel.Text = "";
        _endReportScroll.Hide();
        var compact = MobilePresentation.Enabled;
        _endPanel.CustomMinimumSize = new Vector2(compact ? 680 : 760, BattleRewardUi.PanelHeight(rewards.Count, compact));
        _endPanel.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Wood, 0));
        _endContent.AddThemeConstantOverride("separation", compact ? 8 : 14);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            _endPadding.AddThemeConstantOverride("margin_" + side, compact ? 12 : 24);
        _endStarRating.CustomMinimumSize = new Vector2(132, compact ? 36 : 48);
        var scroll = new ScrollContainer { Name = "VictoryRewards", SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, compact ? 96 : 160) };
        _endContent.AddChild(scroll); _endContent.MoveChild(scroll, 1);
        scroll.AddChild(BattleRewardUi.Cards(rewards, compact));
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 12); _endContent.AddChild(actions);
        _endSecondaryButton.Reparent(actions);
        _endPrimaryButton.Reparent(actions);
        _endSecondaryButton.SizeFlagsHorizontal = _endPrimaryButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ModalUi.StyleButton(_endSecondaryButton, material: ModalMaterial.Gold);
        ModalUi.StyleButton(_endPrimaryButton, material: ModalMaterial.Steel);
    }
}
