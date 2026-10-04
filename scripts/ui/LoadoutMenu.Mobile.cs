using System.Linq;
using Godot;

public partial class LoadoutMenu
{
    private void BuildMobileUi() => BuildMobilePreparation();

    private void BuildMobilePreparation()
    {
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 8);
        AddChild(stack); stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(BuildVictoryRewards(compact: true));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto };
        stack.AddChild(scroll);
        var cards = new HBoxContainer(); cards.AddThemeConstantOverride("separation", 10); scroll.AddChild(cards);
        var roster = GameState.Instance.GetActiveDeckUnits().ToArray();
        foreach (var unit in roster)
        {
            var button = new RealmButton { CustomMinimumSize = new Vector2(140, 108),
                TooltipText = $"{unit.DisplayName} · Level {GameState.Instance.GetUnitLevel(unit.Id)} · {unit.Cost} courage",
                AccessibilityName = $"View {unit.DisplayName}" };
            button.Pressed += () => ModelShowcase.Show(this, roster, unit.Id);
            ModalUi.StyleButton(button); cards.AddChild(button);
            var padding = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(padding); padding.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            foreach (var side in new[] { "left", "right" }) padding.AddThemeConstantOverride("margin_" + side, 8);
            foreach (var side in new[] { "top", "bottom" }) padding.AddThemeConstantOverride("margin_" + side, 4);
            var card = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; padding.AddChild(card);
            card.AddChild(new TextureRect { Texture = UiArtLoader.TryLoadUnitIcon(unit), CustomMinimumSize = new Vector2(80, 64),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore });
            var name = RealmUi.Label(unit.DisplayName, 17); name.AddThemeFontSizeOverride("font_size", RealmUi.ButtonFontSize); name.HorizontalAlignment = HorizontalAlignment.Center;
            name.AutowrapMode = TextServer.AutowrapMode.Off; name.ClipText = true;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.MouseFilter = MouseFilterEnum.Ignore; card.AddChild(name);
        }
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 12); stack.AddChild(footer);
        var edit = RealmUi.Button("sword", "Warband", () => SceneRouter.Instance.GoToShop()); footer.AddChild(edit);
        _status = RealmUi.Label("", 16, true); _status.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(_status);
        _deployButton = RealmUi.Button("flag", "Deploy", () => {
            if (!GameState.Instance.TrySpendStageEntryFood(_stage.StageNumber, out var reason)) { _status.Text = reason; return; }
            GameState.Instance.PrepareCampaignBattle(); SceneRouter.Instance.GoToBattle();
        }, true);
        HomeResourceUi.SetEntryCost(_deployButton, GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber)); footer.AddChild(_deployButton);
        _deployButton.CustomMinimumSize = new Vector2(190, 48);
        RealmModal.Polish(stack);
    }

}
