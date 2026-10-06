using System.Linq;
using Godot;

/// <summary>
/// Prepare for battle, on the approved concept: the mission card (leader, battlefield, objective
/// and victory reward), the squad and spells going into battle, and Deploy with its food cost.
/// </summary>
public partial class LoadoutMenu : RoyalScreen
{
    private StageDefinition _stage;
    private RoyalButton _deployButton;

    public LoadoutMenu() { PlateName = "preparation"; Veil = Colors.Transparent; }

    private static RoyalSpec Spec => RoyalSpec.For("preparation");

    public override void _Process(double delta)
    {
        if (_deployButton != null) _deployButton.Disabled = !GameState.Instance.CanStartCampaignBattle(_stage.StageNumber, out _);
    }

    protected override void Build()
    {
        _stage = GameData.GetStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        var spec = Spec;
        var layer = Layer("Live");
        var state = GameState.Instance;
        layer.AddChild(spec.Label("title", "Prepare for battle", 400));
        layer.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", Close, 8));

        // Mission card.
        var ink = new Color("120a06");
        var leader = AdventureMapCatalog.Leader(_stage.StageNumber);
        var route = RouteCatalog.Get(_stage.MapId);
        var boss = state.IsAdventureBoss(_stage.StageNumber);
        var kicker = spec.Label("mission.kicker", boss ? "DEFEAT THE ZONE BOSS" : "CHALLENGE THE LEADER", 300, new Color("32271e"));
        kicker.ShadowOffset = Vector2.Zero; layer.AddChild(kicker);
        // The concept shows the rival standing on the parchment: the stage's toughest enemy.
        var portraitRect = spec.Rect("mission.portrait");
        var champion = _stage.Waves.SelectMany(wave => wave.Entries).Select(entry => GameData.TryGetUnit(entry.UnitId))
            .Where(unit => unit != null).OrderByDescending(unit => unit.MaxHealth).FirstOrDefault();
        if (champion != null)
        {
            var figure = new UnitFigure { Position = portraitRect.Position + new Vector2(portraitRect.Size.X * .42f, 4), Size = new Vector2(portraitRect.Size.X * .56f, portraitRect.Size.Y - 6) };
            figure.SetUnit(champion);
            figure.TooltipText = champion.DisplayName;
            layer.AddChild(figure);
        }
        var name = spec.Label("mission.leader", leader.Title, portraitRect.Position.X + portraitRect.Size.X * .42f - spec.Number("mission.leader", "pen_x", 87), ink);
        name.ShadowOffset = Vector2.Zero; layer.AddChild(name);
        layer.AddChild(RoyalKit.Image("prep-skull-shield", spec.Rect("mission.badge")));
        var picture = spec.Rect("mission.picture");
        layer.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true,
            Texture = MissionPicture(), Position = picture.Position, Size = picture.Size, MouseFilter = MouseFilterEnum.Ignore });
        layer.AddChild(RoyalKit.Image("icon-crossed-swords", spec.Rect("mission.objective.icon")));
        var objectiveTitle = spec.Label("mission.objective.title", boss ? $"DEFEAT {leader.Title.ToUpperInvariant()}" : "DESTROY THE ENEMY GATEHOUSE", 320, new Color("1e1710"));
        objectiveTitle.ShadowOffset = Vector2.Zero; layer.AddChild(objectiveTitle);
        var line = RoyalText.Paragraph($"{route.Title} · Stage {_stage.StageNumber} · {_stage.StageName}. Bring down the gatehouse and rout the enemy forces within.", 15, new Color("31261b"), 410);
        line.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        line.Position = new Vector2(spec.Number("mission.objective.line.1", "pen_x", 133), spec.Number("mission.objective.line.1", "baseline", 498) - 15);
        line.Size = new Vector2(spec.Rect("panel.mission").End.X - line.Position.X - 14, 40);
        line.AddThemeConstantOverride("line_spacing", -2);
        RoyalText.FitLines(line, 2, 12);
        layer.AddChild(line);
        layer.AddChild(RoyalKit.Image("coin-heap", spec.Rect("mission.reward.coins")));
        layer.AddChild(spec.Label("mission.reward.caption", "VICTORY REWARD", 200));
        var reward = _stage.RewardFood > 0 ? $"{_stage.RewardGold:N0} GOLD · {_stage.RewardFood} FOOD" : $"{_stage.RewardGold:N0} GOLD";
        layer.AddChild(spec.Label("mission.reward.value", reward, 230));

        // Warband.
        layer.AddChild(spec.Label("warband.title", "Your Warband", 240));
        var edit = RoyalButton.Over(spec.Rect("button.edit"), "Edit squad", () => SceneRouter.Instance.GoToShop(0), 6);
        edit.SetGlyph(RoyalKit.Texture("icon-quill"), new Rect2(spec.Rect("button.edit.icon").Position - spec.Rect("button.edit").Position, spec.Rect("button.edit.icon").Size));
        var editLabel = spec.Label("button.edit.label", "EDIT SQUAD", 120);
        editLabel.Position -= spec.Rect("button.edit").Position;
        edit.SetCaption(editLabel, new Rect2(editLabel.Position, editLabel.Size));
        layer.AddChild(edit);
        var units = state.GetActiveDeckUnits().ToArray();
        string[] tints = { "warm", "cool", "gold", "cool", "warm", "cool" };
        for (var i = 0; i < 6; i++)
        {
            var key = $"card.{i + 1}";
            var rect = spec.Rect(key);
            if (i >= units.Length) { layer.AddChild(EmptySlot(rect)); continue; }
            var unit = units[i];
            var card = RoyalButton.Over(rect, "View " + unit.DisplayName, null, 6);
            card.SetStates(RoyalKit.Slice("prep-card-" + tints[i], 10, 10, 10, 54), 6);
            card.TooltipText = $"{unit.DisplayName} · Level {state.GetUnitLevel(unit.Id)} · {unit.Cost} courage";
            var unitId = unit.Id;
            card.Pressed += () => ModelShowcase.Show(this, GameState.Instance.GetActiveDeckUnits().ToArray(), unitId);
            var art = spec.Rect(key + ".art");
            var figure = new UnitFigure { Position = art.Position - rect.Position + new Vector2(2, 4), Size = art.Size - new Vector2(4, 4) };
            figure.SetUnit(unit);
            card.AddChild(figure);
            var icon = spec.Rect(key + ".icon");
            card.AddChild(RoyalKit.Image("prep-class-sword", new Rect2(icon.Position - rect.Position, icon.Size)));
            card.AddChild(RoyalKit.Image(ShopIcon(unit), new Rect2(icon.Position - rect.Position + new Vector2(7, 7), icon.Size - new Vector2(14, 14))));
            var label = spec.Label(key + ".name", unit.DisplayName, rect.Size.X - 8);
            label.Position -= rect.Position;
            card.AddChild(label);
            layer.AddChild(card);
        }

        // Spells.
        layer.AddChild(RoyalKit.Image("icon-book-small", spec.Rect("spells.icon")));
        layer.AddChild(spec.Label("spells.label", "EQUIPPED SPELLS", 220));
        var spells = state.GetActiveDeckSpells().ToArray();
        var first = spec.Rect("spell.1");
        var pitch = spec.Has("spell.2") ? spec.Rect("spell.2").Position.X - first.Position.X : 80;
        for (var i = 0; i < 5; i++)
        {
            var rect = new Rect2(first.Position + new Vector2(i * pitch, 0), first.Size);
            var slot = RoyalButton.Over(rect, i < spells.Length ? spells[i].DisplayName : "Empty spell slot", null, 6);
            slot.SetStates(RoyalKit.Slice("prep-spell-slot", 10), 6);
            if (i < spells.Length)
            {
                var spell = spells[i];
                slot.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    Texture = ResourceLoader.Exists($"res://assets/ui/royal/items/{spell.Id}.png") ? RoyalArt.Load($"res://assets/ui/royal/items/{spell.Id}.png") : UiArtLoader.TryLoadSpellIcon(spell),
                    Position = new Vector2(6, 6), Size = rect.Size - new Vector2(12, 12), MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
                slot.Pressed += () => SpellShowcase.Show(this, spell);
            }
            else slot.Disabled = true;
            var pip = spec.Rect("spell.1.pip");
            slot.AddChild(RoyalKit.Image("prep-spell-pip", new Rect2(pip.Position - first.Position, pip.Size)));
            layer.AddChild(slot);
        }

        // Deploy.
        var deployRect = spec.Rect("button.deploy");
        var deploy = RoyalButton.Over(deployRect, "Deploy", () =>
        {
            if (!GameState.Instance.TrySpendStageEntryFood(_stage.StageNumber, out var message)) { RoyalToast.Show(this, message); return; }
            GameState.Instance.PrepareCampaignBattle();
            SceneRouter.Instance.GoToBattle();
        }, 8);
        deploy.SetGlyph(RoyalKit.Texture("icon-roast"), new Rect2(spec.Rect("button.deploy.icon").Position - deployRect.Position, spec.Rect("button.deploy.icon").Size));
        var deployLabel = spec.Label("button.deploy.label", "Deploy", 140);
        deployLabel.Ink = new Color("1a0e06"); deployLabel.ShadowInk = new Color(1, .95f, .8f, .3f);
        deployLabel.Position -= deployRect.Position;
        deploy.SetCaption(deployLabel, new Rect2(deployLabel.Position, deployLabel.Size));
        deploy.AddChild(RoyalKit.Image("icon-star-small", new Rect2(spec.Rect("button.deploy.cost.icon").Position - deployRect.Position, spec.Rect("button.deploy.cost.icon").Size)));
        var cost = spec.Label("button.deploy.cost", $"{state.GetStageEntryFoodCost(_stage.StageNumber)} FOOD", 120);
        cost.Ink = new Color("1e1107"); cost.ShadowInk = deployLabel.ShadowInk;
        cost.Position -= deployRect.Position;
        deploy.AddChild(cost);
        var canDeploy = state.CanStartCampaignBattle(_stage.StageNumber, out var reason);
        deploy.Disabled = !canDeploy;
        deploy.TooltipText = canDeploy ? $"Deploy · {state.GetStageEntryFoodCost(_stage.StageNumber)} food" : reason;
        _deployButton = deploy;
        layer.AddChild(deploy);
        if (!canDeploy) RoyalToast.Show(this, reason, 690);
    }

    private static string ShopIcon(UnitDefinition unit) => unit.Id switch
    {
        "player_shooter" or "player_ranger" or "player_marksman" or "player_ballista" => "class-ranged",
        "player_defender" or "player_lantern_guard" or "player_banner" => "class-shield",
        _ => "class-melee"
    };

    /// <summary>The battlefield the stage is fought on, from its zone's backdrop; the concept gatehouse otherwise.</summary>
    private Texture2D MissionPicture()
    {
        var path = $"res://assets/ui/royal/missions/{_stage.MapId}.png";
        return ResourceLoader.Exists(path) ? RoyalArt.Load(path) : null;
    }

    private static Control EmptySlot(Rect2 rect)
    {
        var panel = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", RoyalKit.Slice("prep-card-cool", 10, 10, 10, 54).Tinted(new Color(.45f, .45f, .5f)));
        panel.AddChild(RoyalKit.Image("empty-plus", new Rect2(rect.Size.X / 2 - 17, rect.Size.Y / 2 - 40, 34, 34)));
        return panel;
    }
}
