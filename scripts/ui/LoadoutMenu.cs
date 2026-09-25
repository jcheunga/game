using System.Linq;
using Godot;

public partial class LoadoutMenu : Control
{
    private StageDefinition _stage;
    private VBoxContainer _briefing;
    private Label _status;

    public override void _Ready()
    {
        _stage = GameState.Instance.BuildConfiguredCampaignStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        BuildUi();
    }

    private void BuildUi()
    {
        var route = RouteCatalog.Get(_stage.MapId);
        MenuBackdropComposer.AddSolidBackdrop(this, "loadout", new Color("101d26"), route.Id);
        RealmUi.Header(this, $"{route.Title} / Stage {_stage.StageNumber:00}", _stage.StageName, () => SceneRouter.Instance.GoToMap());
        var mission = RealmUi.Panel(this, new Rect2(28, 108, 438, 490), out _);
        mission.AddChild(RealmUi.Label("CHALLENGE THE LEADER", 12, true));
        mission.AddChild(RealmUi.Heading(AdventureMapCatalog.Leader(_stage.StageNumber).Title, 25));
        var rewards = new HBoxContainer();
        rewards.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", $"+{_stage.RewardGold}", new Vector2(26,26)));
        rewards.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", $"+{_stage.RewardFood}", new Vector2(26,26)));
        mission.AddChild(rewards);
        RealmUi.Tabs(mission, ShowBriefing, "Goals", "Foes", "Field", "Brief");
        _briefing = RealmUi.Scroll(mission);
        ShowBriefing(0);

        var roster = RealmUi.Panel(this, new Rect2(486, 108, 766, 490), out _);
        var heading = new HBoxContainer();
        heading.AddChild(RealmUi.Heading("Your warband", 27));
        heading.AddChild(RealmUi.Button("sword", "Edit squad", () => SceneRouter.Instance.GoToShop()));
        roster.AddChild(heading);
        var cards = new HBoxContainer();
        roster.AddChild(cards);
        foreach (var unit in GameState.Instance.GetActiveDeckUnits())
        {
            var frame = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cards.AddChild(frame);
            var card = new VBoxContainer();
            frame.AddChild(card);
            card.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(150, 140)));
            card.AddChild(RealmUi.Label(unit.DisplayName, 17));
            card.AddChild(RealmUi.Label($"Level {GameState.Instance.GetUnitLevel(unit.Id)} · {SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag)}", 12, true));
            var stats = GameState.Instance.BuildPlayerUnitStats(unit);
            var statRow = new HBoxContainer();
            card.AddChild(statRow);
            AddStat(statRow, "heart", $"{stats.MaxHealth:0}", "Health");
            AddStat(statRow, "sword", $"{stats.AttackDamage:0}", "Attack");
            AddStat(statRow, "bolt", $"{unit.Cost}", "Courage cost");
            card.AddChild(RealmUi.Button("eye", "Details", () => RealmUi.Details(this, unit.DisplayName,
                $"{GameState.Instance.BuildUnitDoctrineInlineText(unit.Id)}\nHealth {stats.MaxHealth:0} · Attack {stats.AttackDamage:0.#} · Gate damage {stats.BaseDamage}\nSpeed {stats.Speed:0.#} · Range {stats.AttackRange:0.#} · Attack interval {stats.AttackCooldown:0.##}s\nDeploy recovery {GameState.Instance.ApplyPlayerDeployCooldownUpgrade(unit.DeployCooldown):0.#}s\n{UnitStatText.BuildInlineTraits(stats)}")));
        }
        var magic = new HBoxContainer();
        roster.AddChild(magic);

        foreach (var spell in GameState.Instance.GetActiveDeckSpells())
        {
            var button = RealmUi.Button(spell.EffectType.Contains("heal") ? "heart" : "bolt", spell.DisplayName,
                () => RealmUi.Details(this, spell.DisplayName, SpellText.BuildInlineSummary(spell)));
            magic.AddChild(button);
        }
        if (!GameState.Instance.GetActiveDeckSpells().Any()) magic.AddChild(RealmUi.Label("Equip magic in the armory", 13, true));
        var synergy = RealmUi.Button("people", "Squad bonuses", () => RealmUi.Details(this, "Warband bonuses",
            GameState.Instance.BuildActiveDeckSynergySummary() + "\n\n" + GameState.Instance.BuildCampaignReadinessDetailedSummary(_stage.StageNumber)));
        magic.AddChild(synergy);
        var footer = RealmUi.Panel(this, new Rect2(28, 616, 1224, 78), out _);
        var row = new HBoxContainer();
        footer.AddChild(row);
        _status = RealmUi.Label("Select a card, then click the battlefield to deploy.", 14, true);
        row.AddChild(_status);
        var canDeploy = GameState.Instance.CanStartCampaignBattle(_stage.StageNumber, out var reason);
        var deploy = RealmUi.Button("flag", $"Deploy  ·  {GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber)} food", () =>
        {
            if (!GameState.Instance.TrySpendStageEntryFood(_stage.StageNumber, out var message)) { _status.Text = message; return; }
            GameState.Instance.PrepareCampaignBattle();
            SceneRouter.Instance.GoToBattle();
        }, true);
        deploy.CustomMinimumSize = new Vector2(260, 50);
        deploy.Disabled = !canDeploy;
        if (!canDeploy) _status.Text = reason;
        row.AddChild(deploy);
    }

    private void ShowBriefing(int tab)
    {
        RealmUi.Clear(_briefing);
        var text = tab switch
        {
            0 => StageObjectives.BuildSummaryText(_stage, GameState.Instance.GetStageStars(_stage.StageNumber)),
            1 => StageEncounterIntel.BuildCampaignEncounterIntel(_stage),
            2 => StageModifiers.BuildSummaryText(_stage) + "\n\n" + StageHazards.BuildSummaryText(_stage) + "\n\n" + GameState.Instance.BuildCampaignDirectiveStatusText(_stage.StageNumber),
            _ => AdventureMapCatalog.Leader(_stage.StageNumber).Description + "\n\n" + _stage.Description + "\n\n" + StageMissionEvents.BuildCampaignSummaryText(_stage)
        };
        _briefing.AddChild(RealmUi.Label(text, 15));
    }

    private static void AddStat(HBoxContainer row, string icon, string value, string hint)
    {
        var metric = new HBoxContainer { TooltipText = hint, MouseFilter = MouseFilterEnum.Stop, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        metric.AddThemeConstantOverride("separation", 6);
        metric.AddChild(new TextureRect { Texture = RealmUi.Icon(icon), CustomMinimumSize = new Vector2(16, 16), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
        metric.AddChild(RealmUi.Label(value, 13));
        row.AddChild(metric);
    }
}
