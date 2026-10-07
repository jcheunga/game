using System.Linq;
using Godot;

public partial class BattleController
{
    private GameSaveData _battleRewardStart;
    private ScrollContainer _endReportScroll;
    private VBoxContainer _endContent;
    private MarginContainer _endPadding;

    private static readonly Color VictoryInk = new("ffe3a1"), DefeatInk = new("e2a597");

    // Every outcome shares one card: a title, this run's stars, what was actually earned,
    // and the two actions side by side. The detailed report stays out of the way.
    private RoyalResult _royalResult;

    private void PresentResult(bool won, string title, string detail = "")
    {
        var rewards = BattleRewardUi.Earned(_battleRewardStart, GameState.Instance.BuildSaveData());
        if (!IsLanRaceMode && !IsOnlineRoomMode)
        {
            PresentRoyalResult(won, title, detail, rewards);
            return;
        }
        if (won)
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
        var height = rewards.Count > 0 ? BattleRewardUi.PanelHeight(rewards.Count, compact) : compact ? 280 : 330;
        if (detail.Length > 0) height += compact ? 24 : 30;
        _endPanel.CustomMinimumSize = new Vector2(compact ? 680 : 880, Mathf.Min(680, height));
        _endPanel.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Wood, 12));
        _endContent.AddThemeConstantOverride("separation", compact ? 8 : 14);
        var inset = compact ? 12 : 24;
        foreach (var side in new[] { "left", "right" }) _endPadding.AddThemeConstantOverride("margin_" + side, inset - ModalSurface.MinimumSideInset);
        foreach (var side in new[] { "top", "bottom" }) _endPadding.AddThemeConstantOverride("margin_" + side, inset - ModalSurface.MinimumEndInset);
        _endStarRating.CustomMinimumSize = new Vector2(132, compact ? 36 : 48);

        var heading = RealmUi.Heading(title.ToUpperInvariant(), compact ? 26 : 48);
        heading.Name = "ResultTitle"; heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.AddThemeColorOverride("font_color", won ? VictoryInk : DefeatInk);
        _endContent.AddChild(heading); _endContent.MoveChild(heading, 0);
        var next = _endStarRating.GetIndex() + 1;
        if (detail.Length > 0)
        {
            var line = RealmUi.Label(detail, 18, true);
            line.Name = "ResultDetail"; line.HorizontalAlignment = HorizontalAlignment.Center;
            _endContent.AddChild(line); _endContent.MoveChild(line, next++);
        }
        Control body;
        if (rewards.Count > 0)
        {
            var scroll = new ScrollContainer { Name = "VictoryRewards", SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, compact ? 96 : 160) };
            scroll.AddChild(BattleRewardUi.Cards(rewards, compact));
            body = scroll;
        }
        else
        {
            var none = RealmUi.Label("No rewards this time.", 18, true);
            none.Name = "NoRewards"; none.HorizontalAlignment = HorizontalAlignment.Center;
            none.VerticalAlignment = VerticalAlignment.Center; none.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            body = none;
        }
        _endContent.AddChild(body); _endContent.MoveChild(body, next);

        // Leave on the left, try again on the right; the likelier next step carries the gold finish.
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 12); _endContent.AddChild(actions);
        _endSecondaryButton.Reparent(actions);
        _endPrimaryButton.Reparent(actions);
        _endSecondaryButton.SizeFlagsHorizontal = _endPrimaryButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ModalUi.StyleButton(_endSecondaryButton, material: won ? ModalMaterial.Gold : ModalMaterial.Steel);
        ModalUi.StyleButton(_endPrimaryButton, material: won ? ModalMaterial.Steel : ModalMaterial.Gold);
    }

    /// <summary>The approved victory board, used for every result except LAN and online rooms.</summary>
    private void PresentRoyalResult(bool won, string title, string detail, System.Collections.Generic.List<BattleReward> rewards)
    {
        if (won)
            BattleSummaryData.Current = new BattleSummaryData {
                Won = true, StarsEarned = _endStarRating.Stars, Stage = _stage, BattleMode = _battleMode.ToString(),
                Rewards = rewards, GoldEarned = rewards.Where(reward => reward.Kind == "gold").Sum(reward => reward.Amount),
                FoodEarned = rewards.Where(reward => reward.Kind == "food").Sum(reward => reward.Amount),
                SeasonXPEarned = rewards.Where(reward => reward.Kind == "season_xp").Sum(reward => reward.Amount),
                MasteryXPPerUnit = rewards.Where(reward => reward.Kind == "mastery").ToDictionary(reward => reward.ItemId, reward => reward.Amount)
            };
        _royalResult?.QueueFree();
        _royalResult = new RoyalResult
        {
            Won = won, Title = title, Detail = detail, Stars = won ? _endStarRating.Stars : 0, Rewards = rewards,
            LeaveText = _endSecondaryButton.Text, RetryText = IsCampaignMode ? "Restart" : _endPrimaryButton.Text,
            RetryFoodCost = IsCampaignMode ? GameState.Instance.GetStageEntryFoodCost(_stage) : 0,
            Leave = HandleEndPanelSecondaryAction, Retry = HandleEndPanelPrimaryAction
        };
        _endCenter.GetParent().AddChild(_royalResult);
        _endPanel.Visible = false;
        // The result board stands alone over the battlefield, as in the concept.
        foreach (var hud in new Control[] { _topHudPanel, _goldFrame, _hudSettingsButton, _cardDock, _battleFollowButton })
            if (IsInstanceValid(hud)) hud.Visible = false;
    }
}
