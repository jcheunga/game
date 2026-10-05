using System.Linq;
using Godot;

public partial class LoadoutMenu : Control
{
    private StageDefinition _stage;
    private Label _status;
    private Button _deployButton;
    public override void _Process(double delta) { if (_deployButton != null) _deployButton.Disabled = !GameState.Instance.CanStartCampaignBattle(_stage.StageNumber, out _); }

    public override void _Ready()
    {
        _stage = GameData.GetStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        BuildUi();
    }

    private void BuildUi()
    {
        if(MobilePresentation.Enabled) { BuildMobileUi(); return; }
        var route = RouteCatalog.Get(_stage.MapId);

        MedievalUi.Apply(this);
        RealmUi.Header(this, $"{route.Title} / Stage {_stage.StageNumber:00}", _stage.StageName, () => SceneRouter.Instance.GoToMap());
        var mission = RealmUi.Panel(this, new Rect2(28, 108, 438, 490), out _);
        mission.AddChild(RealmUi.Label($"{route.Title} · Stage {_stage.StageNumber} · {_stage.StageName}", 18, true));
        mission.AddChild(RealmUi.Heading(AdventureMapCatalog.Leader(_stage.StageNumber).Title, 25));
        mission.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        mission.AddChild(BuildVictoryRewards());

        var roster = RealmUi.Panel(this, new Rect2(486, 108, 766, 490), out _);
        var heading = new HBoxContainer();
        heading.AddChild(RealmUi.Heading("Your warband", 27));
        heading.AddChild(RealmUi.Button("sword", "Edit squad", () => SceneRouter.Instance.GoToShop()));
        roster.AddChild(heading);
        var cardScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled }; roster.AddChild(cardScroll);
        cardScroll.SetMeta("modal_min_height", 0);
        var cards = new HBoxContainer(); cardScroll.AddChild(cards);
        foreach (var unit in GameState.Instance.GetActiveDeckUnits())
        {
            var frame = new PanelContainer { CustomMinimumSize = new Vector2(180, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            frame.SetMeta("modal_min_width", 180);
            cards.AddChild(frame);
            var card = new VBoxContainer();
            frame.AddChild(card);
            card.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(140, RealmModal.Embedded(this) ? 96 : 120)));
            card.AddChild(RealmUi.Label(unit.DisplayName, 17));
            var statRow = new HBoxContainer();
            statRow.AddThemeConstantOverride("separation", 8);
            card.AddChild(statRow);
            statRow.AddChild(RealmUi.Label($"Level {GameState.Instance.GetUnitLevel(unit.Id)}", 12, true));
            AddStat(statRow, "bolt", $"{unit.Cost}", "Courage cost");
            card.AddChild(RealmUi.Button("eye", "Details", () => ModelShowcase.Show(this, GameState.Instance.GetActiveDeckUnits().ToArray(), unit.Id)));
        }
        var magic = new HBoxContainer();
        magic.AddThemeConstantOverride("separation", 8);

        foreach (var spell in GameState.Instance.GetActiveDeckSpells())
        {
            var button = RealmUi.IconButton(spell.EffectType.Contains("heal") ? "heart" : "bolt", spell.DisplayName,
                () => SpellShowcase.Show(this, spell));
            if (UiArtLoader.TryLoadSpellIcon(spell) is { } icon) { button.Icon = icon; button.SetMeta("painted_resource_icon", true); }
            button.AddThemeConstantOverride("icon_max_width", 28);
            button.CustomMinimumSize = new Vector2(56, 48);
            magic.AddChild(button);
        }
        var footer = RealmUi.Panel(this, new Rect2(28, 616, 1224, 78), out _);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        footer.AddChild(row);
        _status = RealmUi.Label("", 18, true);
        _status.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_status);
        row.AddChild(magic);
        var canDeploy = GameState.Instance.CanStartCampaignBattle(_stage.StageNumber, out var reason);
        var deploy = RealmUi.Button("flag", "Deploy", () =>
        {
            if (!GameState.Instance.TrySpendStageEntryFood(_stage.StageNumber, out var message)) { _status.Text = message; return; }
            GameState.Instance.PrepareCampaignBattle();
            SceneRouter.Instance.GoToBattle();
        }, true);
        HomeResourceUi.SetEntryCost(deploy, GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber));
        deploy.CustomMinimumSize = new Vector2(260, 50);
        deploy.Disabled = !canDeploy; _deployButton = deploy;
        if (!canDeploy) _status.Text = reason;
        row.AddChild(deploy);
    }

    private Control BuildVictoryRewards(bool compact = false)
    {
        if (compact)
        {
            var panel = new PanelContainer { Name = "VictoryRewards" };
            panel.SetMeta("modal_unframed", true);
            panel.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Inset, 6));
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); panel.AddChild(row);
            var title = RealmUi.Label("Victory rewards", 18, true); title.VerticalAlignment = VerticalAlignment.Center; row.AddChild(title);
            row.AddChild(HomeResourceUi.Amount("gold", $"+{_stage.RewardGold:N0}", $"Victory · {_stage.RewardGold:N0} gold"));
            if (_stage.RewardFood > 0) row.AddChild(HomeResourceUi.Amount("food", $"+{_stage.RewardFood:N0}", $"Victory · {_stage.RewardFood:N0} rations"));
            return panel;
        }
        var section = new VBoxContainer { Name = "VictoryRewards" };
        section.AddThemeConstantOverride("separation", 12);
        section.AddChild(RealmUi.SectionTitle("Victory rewards"));
        var rewards = new HBoxContainer();
        rewards.AddThemeConstantOverride("separation", 12);
        section.AddChild(rewards);
        void Reward(string icon, string name, int amount)
        {
            var card = new PanelContainer { Name = "VictoryReward" + icon, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            card.SetMeta("modal_unframed", true);
            card.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Inset, 12));
            rewards.AddChild(card);
            var stack = new VBoxContainer();
            stack.AddThemeConstantOverride("separation", 6);
            card.AddChild(stack);
            var value = HomeResourceUi.Amount(icon, $"+{amount:N0}", $"Victory · {amount:N0} {name.ToLowerInvariant()}", 48);
            value.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            value.GetChild<Label>(1).AddThemeFontSizeOverride("font_size", 30);
            value.GetChild<Label>(1).AddThemeColorOverride("font_color", ModalUi.Cream);
            stack.AddChild(value);
            var caption = RealmUi.Label(name, 18, true);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            stack.AddChild(caption);
        }
        Reward("gold", "Gold", _stage.RewardGold);
        if (_stage.RewardFood > 0) Reward("food", "Rations", _stage.RewardFood);
        return section;
    }

    private static void AddStat(HBoxContainer row, string icon, string value, string hint)
    {
        var metric = new HBoxContainer { TooltipText = hint, MouseFilter = MouseFilterEnum.Stop, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        metric.AddThemeConstantOverride("separation", 6);
        metric.AddChild(new TextureRect { Texture = RealmUi.Icon(icon), CustomMinimumSize = new Vector2(16, 16), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
        var number = RealmUi.Label(value, 18); number.AutowrapMode = TextServer.AutowrapMode.Off; metric.AddChild(number);
        row.AddChild(metric);
    }
}
