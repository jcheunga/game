using System.Linq;
using Godot;

public partial class MainMenu : Control
{
    private Label _summaryLabel = null!;

    public override void _Ready()
    {
        BuildUi();
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Color = new Color("1d2d44")
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(560, 620)
        };
        center.AddChild(panel);

        var content = new MarginContainer();
        content.AddThemeConstantOverride("margin_left", 24);
        content.AddThemeConstantOverride("margin_top", 24);
        content.AddThemeConstantOverride("margin_right", 24);
        content.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(content);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        content.AddChild(stack);

        var title = new Label
        {
            Text = "CROWNROAD: SIEGE OF ASH",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.AddChild(title);

        var subtitle = new Label
        {
            Text = "Medieval fantasy siege campaign\nBuild a warband, hold the lane, break the gate.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.AddChild(subtitle);

        _summaryLabel = new Label
        {
            Text = BuildProgressSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 136f)
        };
        stack.AddChild(_summaryLabel);

        var startButton = BuildButton(GameState.Instance.HighestUnlockedStage > 1 ? "Resume Campaign" : "Start Campaign");
        startButton.Pressed += () => SceneRouter.Instance.GoToMap();
        stack.AddChild(startButton);

        var shopButton = BuildButton("Caravan Armory");
        shopButton.Pressed += () => SceneRouter.Instance.GoToShop();
        stack.AddChild(shopButton);

        var endlessButton = BuildButton("Endless Run");
        endlessButton.Pressed += () => SceneRouter.Instance.GoToEndless();
        stack.AddChild(endlessButton);

        var multiplayerButton = BuildButton("Multiplayer Challenge");
        multiplayerButton.Pressed += () => SceneRouter.Instance.GoToMultiplayer();
        stack.AddChild(multiplayerButton);

        var settingsButton = BuildButton("Settings");
        settingsButton.Pressed += () => SceneRouter.Instance.GoToSettings();
        stack.AddChild(settingsButton);

        var resetButton = BuildButton("Reset Progress");
        resetButton.Pressed += () =>
        {
            GameState.Instance.ResetProgress();
            SceneRouter.Instance.GoToMap();
        };
        stack.AddChild(resetButton);

        var quitButton = BuildButton("Quit");
        quitButton.Pressed += () => GetTree().Quit();
        stack.AddChild(quitButton);
    }

    private static Button BuildButton(string text)
    {
        return new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 52)
        };
    }

    private string BuildProgressSummary()
    {
        var nextStage = GameData.GetStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        var totalStars = 0;

        foreach (var stage in GameData.Stages)
        {
            totalStars += GameState.Instance.GetStageStars(stage.StageNumber);
        }

        var ownedUnits = GameState.Instance.GetOwnedPlayerUnits().Count;
        var ownedSpells = GameState.Instance.GetOwnedPlayerSpells().Count;
        var hullLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.HullPlatingId);
        var pantryLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId);
        var dispatchLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.DispatchConsoleId);
        var relayLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId);
        var nextExploreLine = GameState.Instance.CanExploreNextStage(out var nextExploreStage, out _)
            ? $"Next exploration: Stage {nextExploreStage.StageNumber} for {GameState.Instance.GetStageExploreFoodCost(nextExploreStage.StageNumber)} food"
            : "Route exploration complete";

        var squadSummary = GameState.Instance.GetActiveDeckUnits()
            .Select(unit => $"{unit.DisplayName} Lv{GameState.Instance.GetUnitLevel(unit.Id)}");
        var squadLine = string.Join(", ", squadSummary);
        if (string.IsNullOrWhiteSpace(squadLine))
        {
            squadLine = "No active squad configured.";
        }

        var spellLine = GameState.Instance.GetActiveDeckSpells().Count == 0
            ? "No active magic prepared."
            : string.Join(", ", GameState.Instance.GetActiveDeckSpells().Select(spell => spell.DisplayName));

        var selectedChallenge = GameState.Instance.GetSelectedAsyncChallenge();
        var bestChallengeScore = GameState.Instance.GetAsyncChallengeBestScore(selectedChallenge.Code);

        return
            "Caravan status:\n" +
            $"Unlocked stages: {GameState.Instance.HighestUnlockedStage}/{GameState.Instance.MaxStage}  |  Stars: {totalStars}\n" +
            $"{CampaignPlanCatalog.BuildCampaignStatusSummary()}\n" +
            $"Resources: {GameState.Instance.Gold} gold  |  {GameState.Instance.Food} food  |  Owned units: {ownedUnits}/{GameData.PlayerRosterIds.Length}  |  Owned spells: {ownedSpells}/{GameData.PlayerSpellIds.Length}\n" +
            $"War wagon upgrades: Plating {hullLevel}/{GameState.Instance.MaxBaseUpgradeLevel}  |  Stores {pantryLevel}/{GameState.Instance.MaxBaseUpgradeLevel}  |  Drum {dispatchLevel}/{GameState.Instance.MaxBaseUpgradeLevel}  |  Beacon {relayLevel}/{GameState.Instance.MaxBaseUpgradeLevel}\n" +
            $"Best endless: wave {GameState.Instance.BestEndlessWave}  |  {GameState.Instance.BestEndlessTimeSeconds:0.0}s survived\n" +
            $"Selected challenge: {selectedChallenge.Code}  |  Best score {bestChallengeScore}\n" +
            $"Next deployment: {nextStage.MapName} - Stage {nextStage.StageNumber}: {nextStage.StageName}\n" +
            $"{nextExploreLine}\n" +
            $"Active squad: {squadLine}\n" +
            $"Active magic: {spellLine}\n" +
            $"Deck synergy: {GameState.Instance.BuildActiveDeckSynergyInlineSummary()}";
    }
}
