using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class MultiplayerMenu : Control
{
    private OptionButton _stageSelector = null!;
    private OptionButton _mutatorSelector = null!;
    private LineEdit _codeEdit = null!;
    private Label _summaryLabel = null!;
    private Label _recordLabel = null!;
    private Label _tapeLabel = null!;
    private Label _historyLabel = null!;
    private Label _rulesLabel = null!;
    private Label _statusLabel = null!;
    private VBoxContainer _squadStack = null!;
    private Button _startButton = null!;

    private int _selectedStage = 1;
    private string _selectedMutatorId = AsyncChallengeCatalog.PressureSpikeId;

    public override void _Ready()
    {
        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        _selectedStage = challenge.Stage;
        _selectedMutatorId = challenge.MutatorId;
        BuildUi();
        RefreshUi();
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Color = new Color("1f2041")
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var titlePanel = new PanelContainer
        {
            Position = new Vector2(24f, 20f),
            Size = new Vector2(1232f, 82f)
        };
        AddChild(titlePanel);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 16);
        titlePanel.AddChild(titleRow);

        titleRow.AddChild(new Label
        {
            Text = "Multiplayer Challenge",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        });

        titleRow.AddChild(new Label
        {
            Text = $"Gold: {GameState.Instance.Gold}  |  Food: {GameState.Instance.Food}",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        var missionPanel = new PanelContainer
        {
            Position = new Vector2(24f, 122f),
            Size = new Vector2(520f, 520f)
        };
        AddChild(missionPanel);

        var missionPadding = new MarginContainer();
        missionPadding.AddThemeConstantOverride("margin_left", 18);
        missionPadding.AddThemeConstantOverride("margin_right", 18);
        missionPadding.AddThemeConstantOverride("margin_top", 18);
        missionPadding.AddThemeConstantOverride("margin_bottom", 18);
        missionPanel.AddChild(missionPadding);

        var missionStack = new VBoxContainer();
        missionStack.AddThemeConstantOverride("separation", 12);
        missionPadding.AddChild(missionStack);

        missionStack.AddChild(new Label
        {
            Text = "Challenge stage"
        });

        _stageSelector = new OptionButton
        {
            CustomMinimumSize = new Vector2(240f, 0f)
        };
        _stageSelector.ItemSelected += OnStageSelected;
        missionStack.AddChild(_stageSelector);

        for (var stageNumber = 1; stageNumber <= GameState.Instance.MaxStage; stageNumber++)
        {
            var stage = GameData.GetStage(stageNumber);
            var index = _stageSelector.ItemCount;
            _stageSelector.AddItem($"{stage.MapName} - S{stage.StageNumber} {stage.StageName}");
            _stageSelector.SetItemMetadata(index, stage.StageNumber);
        }

        missionStack.AddChild(new Label
        {
            Text = "Challenge mutator"
        });

        _mutatorSelector = new OptionButton
        {
            CustomMinimumSize = new Vector2(240f, 0f)
        };
        _mutatorSelector.ItemSelected += OnMutatorSelected;
        missionStack.AddChild(_mutatorSelector);

        foreach (var mutator in AsyncChallengeCatalog.GetAll())
        {
            var index = _mutatorSelector.ItemCount;
            _mutatorSelector.AddItem(mutator.Title);
            _mutatorSelector.SetItemMetadata(index, mutator.Id);
        }

        missionStack.AddChild(new Label
        {
            Text = "Challenge code"
        });

        var codeRow = new HBoxContainer();
        codeRow.AddThemeConstantOverride("separation", 8);
        missionStack.AddChild(codeRow);

        _codeEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "CH-04-PRS-4821"
        };
        codeRow.AddChild(_codeEdit);

        var loadCodeButton = new Button
        {
            Text = "Load",
            CustomMinimumSize = new Vector2(92f, 0f)
        };
        loadCodeButton.Pressed += LoadCode;
        codeRow.AddChild(loadCodeButton);

        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 8);
        missionStack.AddChild(actionRow);

        var rollButton = new Button
        {
            Text = "Roll Code",
            CustomMinimumSize = new Vector2(0f, 42f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        rollButton.Pressed += GenerateChallengeCode;
        actionRow.AddChild(rollButton);

        var copyButton = new Button
        {
            Text = "Copy Code",
            CustomMinimumSize = new Vector2(0f, 42f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        copyButton.Pressed += () =>
        {
            DisplayServer.ClipboardSet(_codeEdit.Text);
            _statusLabel.Text = $"Status:\nCopied {_codeEdit.Text} to the clipboard.";
        };
        actionRow.AddChild(copyButton);

        _summaryLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 132f)
        };
        missionStack.AddChild(_summaryLabel);

        _recordLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        missionStack.AddChild(_recordLabel);

        _tapeLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 88f)
        };
        missionStack.AddChild(_tapeLabel);

        _historyLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 108f)
        };
        missionStack.AddChild(_historyLabel);

        _rulesLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        missionStack.AddChild(_rulesLabel);

        _statusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 72f)
        };
        missionStack.AddChild(_statusLabel);

        var squadPanel = new PanelContainer
        {
            Position = new Vector2(568f, 122f),
            Size = new Vector2(688f, 520f)
        };
        AddChild(squadPanel);

        var squadPadding = new MarginContainer();
        squadPadding.AddThemeConstantOverride("margin_left", 18);
        squadPadding.AddThemeConstantOverride("margin_right", 18);
        squadPadding.AddThemeConstantOverride("margin_top", 18);
        squadPadding.AddThemeConstantOverride("margin_bottom", 18);
        squadPanel.AddChild(squadPadding);

        var squadScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        squadPadding.AddChild(squadScroll);

        _squadStack = new VBoxContainer();
        _squadStack.AddThemeConstantOverride("separation", 12);
        squadScroll.AddChild(_squadStack);

        var bottomPanel = new PanelContainer
        {
            Position = new Vector2(24f, 660f),
            Size = new Vector2(1232f, 56f)
        };
        AddChild(bottomPanel);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 12);
        bottomPanel.AddChild(bottomRow);

        var backButton = new Button
        {
            Text = "Back To Title",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        backButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
        bottomRow.AddChild(backButton);

        var shopButton = new Button
        {
            Text = "Convoy Shop",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        shopButton.Pressed += () => SceneRouter.Instance.GoToShop();
        bottomRow.AddChild(shopButton);

        var settingsButton = new Button
        {
            Text = "Settings",
            CustomMinimumSize = new Vector2(150f, 0f)
        };
        settingsButton.Pressed += () => SceneRouter.Instance.GoToSettings();
        bottomRow.AddChild(settingsButton);

        var lanButton = new Button
        {
            Text = "LAN Race",
            CustomMinimumSize = new Vector2(160f, 0f)
        };
        lanButton.Pressed += () => SceneRouter.Instance.GoToLanRace();
        bottomRow.AddChild(lanButton);

        bottomRow.AddChild(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        _startButton = new Button
        {
            Text = "Start Challenge",
            CustomMinimumSize = new Vector2(240f, 0f)
        };
        _startButton.Pressed += StartChallenge;
        bottomRow.AddChild(_startButton);
    }

    private void RefreshUi()
    {
        SyncStageSelector();
        SyncMutatorSelector();

        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
        var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage));
        var previewDeck = GameState.Instance.GetSelectedAsyncChallengeDeckUnits();
        var ghostRun = GameState.Instance.GetChallengeGhostRun(challenge.Code, GameState.Instance.HasSelectedAsyncChallengeLockedDeck);
        var deckModeLabel = GameState.Instance.HasSelectedAsyncChallengeLockedDeck
            ? $"Locked featured squad: {string.Join(", ", previewDeck.Select(unit => unit.DisplayName))}"
            : $"Active squad: {string.Join(", ", previewDeck.Select(unit => unit.DisplayName))}";
        _codeEdit.Text = challenge.Code;
        _summaryLabel.Text =
            $"Challenge target: {stage.MapName} - Stage {stage.StageNumber}: {stage.StageName}\n" +
            $"{AsyncChallengeCatalog.BuildSummary(challenge)}\n\n" +
            $"{deckModeLabel}\n" +
            $"{GameState.Instance.BuildSelectedAsyncChallengeDeckSynergyInlineSummary()}\n\n" +
            $"{StageEncounterIntel.BuildCompactSummary(stage)}";
        _recordLabel.Text =
            $"Local records:\n" +
            $"Best for this code: {GameState.Instance.GetAsyncChallengeBestScore(challenge.Code)}\n" +
            $"Best tier: {AsyncChallengeCatalog.ResolveMedalLabel(challenge, GameState.Instance.GetAsyncChallengeBestScore(challenge.Code))}\n" +
            $"Challenge runs logged: {GameState.Instance.ChallengeRuns}\n" +
            $"Route lock: {(challenge.Stage <= GameState.Instance.HighestUnlockedStage ? "ready" : $"explore stage {challenge.Stage} first")}\n" +
            $"Deck mode: {(GameState.Instance.HasSelectedAsyncChallengeLockedDeck ? "featured locked squad" : "player active squad")}\n" +
            $"{GameState.Instance.BuildChallengeGhostSummary(ghostRun)}\n" +
            $"{AsyncChallengeCatalog.BuildTargetSummary(challenge, GameState.Instance.GetAsyncChallengeBestScore(challenge.Code))}";
        _tapeLabel.Text = GameState.Instance.BuildChallengeRunTapeSummary(GameState.Instance.GetLatestChallengeRun(challenge.Code));
        _historyLabel.Text = BuildHistoryText(challenge);
        _rulesLabel.Text =
            "Challenge rules:\n" +
            "- Runs use the exact same challenge code, stage, mutator, and seeded spawn pattern.\n" +
            "- No food is spent and no campaign stars or stage unlocks are awarded.\n" +
            "- Compare score, time, stars, and hull preservation on the same code.\n" +
            "- Daily featured boards rotate from the unlocked campaign range.\n" +
            "- Featured boards can also lock the convoy to the same 3-card squad for fairer score races.\n" +
            "- Pinned codes stay saved until you clear them from the board.\n" +
            $"- Active mutator: {mutator.Title}.\n\n" +
            $"{AsyncChallengeCatalog.BuildScoringGuide(challenge)}\n\n" +
            $"{AsyncChallengeCatalog.BuildTargetSummary(challenge)}";

        RebuildSquadPanels(stage);

        var canStart = GameState.Instance.CanStartAsyncChallenge(out var readinessMessage);
        _statusLabel.Text = $"Status:\n{readinessMessage}";
        _startButton.Disabled = !canStart;
        _startButton.Text = canStart ? $"Start {challenge.Code}" : "Challenge Not Ready";
    }

    private void RebuildSquadPanels(StageDefinition stage)
    {
        foreach (var child in _squadStack.GetChildren())
        {
            child.QueueFree();
        }

        _squadStack.AddChild(new Label
        {
            Text = $"Daily Featured Queue ({FeaturedChallengeCatalog.GetDailyRotationStamp()})"
        });

        foreach (var featured in FeaturedChallengeCatalog.GetDailyRotation(
                     GameState.Instance.HighestUnlockedStage,
                     GameState.Instance.MaxStage))
        {
            _squadStack.AddChild(BuildFeaturedChallengePanel(featured));
        }

        _squadStack.AddChild(new Label
        {
            Text = $"Pinned Codes ({GameState.Instance.GetPinnedChallengeCodes().Count})"
        });

        var pinnedCodes = GameState.Instance.GetPinnedChallengeCodes();
        if (pinnedCodes.Count == 0)
        {
            _squadStack.AddChild(new Label
            {
                Text = "Pin any challenge code to keep it on the board for rematches.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }
        else
        {
            foreach (var code in pinnedCodes)
            {
                _squadStack.AddChild(BuildPinnedChallengePanel(code));
            }
        }

        var previewDeck = GameState.Instance.GetSelectedAsyncChallengeDeckUnits();
        _squadStack.AddChild(new Label
        {
            Text = GameState.Instance.HasSelectedAsyncChallengeLockedDeck
                ? $"Featured Squad Lock ({previewDeck.Count}/{GameState.Instance.DeckSizeLimit})"
                : $"Active Squad ({previewDeck.Count}/{GameState.Instance.DeckSizeLimit})"
        });

        _squadStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildSelectedAsyncChallengeDeckSynergySummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        foreach (var definition in previewDeck)
        {
            _squadStack.AddChild(BuildUnitPanel(definition, previewDeck));
        }

        _squadStack.AddChild(new Label
        {
            Text = StageEncounterIntel.BuildEncounterIntel(stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }

    private Control BuildUnitPanel(UnitDefinition definition, IReadOnlyList<UnitDefinition> deckUnits)
    {
        var stats = GameState.Instance.BuildPlayerUnitStatsForDeck(definition, deckUnits);
        var deployCooldown = GameState.Instance.ApplyPlayerDeployCooldownUpgrade(definition.DeployCooldown);
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 108f)
        };

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 14);
        padding.AddThemeConstantOverride("margin_right", 14);
        padding.AddThemeConstantOverride("margin_top", 12);
        padding.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text =
                $"Lv{GameState.Instance.GetUnitLevel(definition.Id)}  {definition.DisplayName}  |  " +
                $"{SquadSynergyCatalog.GetTagDisplayName(definition.SquadTag)}"
        });

        stack.AddChild(new Label
        {
            Text =
                $"Cost {definition.Cost}  |  HP {Mathf.RoundToInt(stats.MaxHealth)}  |  ATK {stats.AttackDamage:0.#}  |  Base {stats.BaseDamage}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text =
                $"Range {stats.AttackRange:0.#}  |  Move {stats.Speed:0.#}  |  Deploy CD {deployCooldown:0.#}s" +
                UnitStatText.BuildInlineTraits(stats),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        return panel;
    }

    private Control BuildFeaturedChallengePanel(FeaturedChallengeDefinition featured)
    {
        var challenge = featured.Challenge;
        var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage));
        var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
        var best = GameState.Instance.GetAsyncChallengeBestScore(challenge.Code);
        var isPinned = GameState.Instance.IsChallengeCodePinned(challenge.Code);
        var lockedLabel = challenge.Stage <= GameState.Instance.HighestUnlockedStage
            ? "Ready"
            : $"Locked until stage {challenge.Stage}";
        var deckSummary = string.Join(
            ", ",
            featured.LockedDeckUnitIds.Select(unitId => GameData.GetUnit(unitId).DisplayName));

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 148f)
        };

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 14);
        padding.AddThemeConstantOverride("margin_right", 14);
        padding.AddThemeConstantOverride("margin_top", 12);
        padding.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = $"{featured.Title}  |  {stage.MapName} S{stage.StageNumber}"
        });

        stack.AddChild(new Label
        {
            Text =
                $"{challenge.Code}  |  {mutator.Title}  |  Best {best} ({AsyncChallengeCatalog.ResolveMedalLabel(challenge, best)})\n" +
                $"{featured.Summary}\n" +
                $"Locked squad: {deckSummary}\n" +
                $"Status: {lockedLabel}\n" +
                $"{AsyncChallengeCatalog.BuildTargetSummary(challenge)}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);

        var loadButton = new Button
        {
            Text = "Load Featured",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        loadButton.Pressed += () => LoadFeaturedChallenge(featured);
        row.AddChild(loadButton);

        var pinButton = new Button
        {
            Text = isPinned ? "Unpin" : "Pin",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        pinButton.Pressed += () => TogglePinnedChallenge(challenge.Code);
        row.AddChild(pinButton);

        return panel;
    }

    private Control BuildPinnedChallengePanel(string code)
    {
        if (!AsyncChallengeCatalog.TryParse(code, out var challenge, out _))
        {
            return new Label
            {
                Text = code,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
        }

        var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage));
        var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
        var best = GameState.Instance.GetAsyncChallengeBestScore(challenge.Code);
        var recent = GameState.Instance.GetRecentChallengeHistory(1, challenge.Code);
        var recentLine = recent.Count > 0
            ? $"Latest: {FormatHistoryEntry(recent[0], false)}"
            : "Latest: no local attempts yet.";

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 136f)
        };

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 14);
        padding.AddThemeConstantOverride("margin_right", 14);
        padding.AddThemeConstantOverride("margin_top", 12);
        padding.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = $"{challenge.Code}  |  {stage.MapName} S{stage.StageNumber}  |  {mutator.Title}"
        });

        stack.AddChild(new Label
        {
            Text =
                $"Pinned rematch board  |  Best {best} ({AsyncChallengeCatalog.ResolveMedalLabel(challenge, best)})\n" +
                $"{recentLine}\n" +
                $"{AsyncChallengeCatalog.BuildTargetSummary(challenge)}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);

        var loadButton = new Button
        {
            Text = "Load Pinned",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        loadButton.Pressed += () => LoadChallengeCode(challenge.Code, $"Loaded pinned code {challenge.Code}.");
        row.AddChild(loadButton);

        var removeButton = new Button
        {
            Text = "Remove Pin",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        removeButton.Pressed += () => TogglePinnedChallenge(challenge.Code);
        row.AddChild(removeButton);

        return panel;
    }

    private void OnStageSelected(long index)
    {
        if (index < 0 || index >= _stageSelector.ItemCount)
        {
            return;
        }

        _selectedStage = (int)_stageSelector.GetItemMetadata((int)index).AsInt32();
        GenerateChallengeCode();
    }

    private void OnMutatorSelected(long index)
    {
        if (index < 0 || index >= _mutatorSelector.ItemCount)
        {
            return;
        }

        _selectedMutatorId = _mutatorSelector.GetItemMetadata((int)index).AsString();
        GenerateChallengeCode();
    }

    private void GenerateChallengeCode()
    {
        GameState.Instance.GenerateAsyncChallenge(_selectedStage, _selectedMutatorId);
        _statusLabel.Text = $"Status:\nRolled {GameState.Instance.SelectedAsyncChallengeCode}.";
        RefreshUi();
    }

    private void LoadCode()
    {
        if (!GameState.Instance.TrySetSelectedAsyncChallengeCode(_codeEdit.Text, out var message))
        {
            _statusLabel.Text = $"Status:\n{message}";
            return;
        }

        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        _selectedStage = challenge.Stage;
        _selectedMutatorId = challenge.MutatorId;
        _statusLabel.Text = $"Status:\n{message}";
        RefreshUi();
    }

    private void LoadChallengeCode(string code, string successPrefix)
    {
        if (!GameState.Instance.TrySetSelectedAsyncChallengeCode(code, out var message))
        {
            _statusLabel.Text = $"Status:\n{message}";
            return;
        }

        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        _selectedStage = challenge.Stage;
        _selectedMutatorId = challenge.MutatorId;
        _statusLabel.Text = $"Status:\n{successPrefix}";
        RefreshUi();
    }

    private void LoadFeaturedChallenge(FeaturedChallengeDefinition featured)
    {
        GameState.Instance.SetSelectedFeaturedChallenge(featured);
        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        _selectedStage = challenge.Stage;
        _selectedMutatorId = challenge.MutatorId;
        _statusLabel.Text = $"Status:\nLoaded featured board {featured.Title} with its locked squad.";
        RefreshUi();
    }

    private void TogglePinnedChallenge(string code)
    {
        if (!GameState.Instance.TogglePinnedChallengeCode(code, out _, out var message))
        {
            _statusLabel.Text = $"Status:\n{message}";
            return;
        }

        _statusLabel.Text = $"Status:\n{message}";
        RefreshUi();
    }

    private void StartChallenge()
    {
        if (!GameState.Instance.PrepareAsyncChallenge(_codeEdit.Text, out var message))
        {
            _statusLabel.Text = $"Status:\n{message}";
            return;
        }

        SceneRouter.Instance.GoToBattle();
    }

    private void SyncStageSelector()
    {
        for (var i = 0; i < _stageSelector.ItemCount; i++)
        {
            if ((int)_stageSelector.GetItemMetadata(i).AsInt32() != _selectedStage)
            {
                continue;
            }

            _stageSelector.Select(i);
            return;
        }
    }

    private void SyncMutatorSelector()
    {
        for (var i = 0; i < _mutatorSelector.ItemCount; i++)
        {
            if (!_mutatorSelector.GetItemMetadata(i).AsString().Equals(_selectedMutatorId))
            {
                continue;
            }

            _mutatorSelector.Select(i);
            return;
        }
    }

    private string BuildHistoryText(AsyncChallengeDefinition challenge)
    {
        var sameCodeHistory = GameState.Instance.GetRecentChallengeHistory(3, challenge.Code);
        var recentHistory = GameState.Instance.GetRecentChallengeHistory(5);
        var builder = new StringBuilder();
        builder.AppendLine("Recent challenge history:");

        if (sameCodeHistory.Count == 0)
        {
            builder.AppendLine($"This code has no local attempts yet: {challenge.Code}");
        }
        else
        {
            builder.AppendLine("This code:");
            foreach (var entry in sameCodeHistory)
            {
                builder.AppendLine(FormatHistoryEntry(entry, false));
            }
        }

        if (recentHistory.Count == 0)
        {
            builder.Append("Recent queue: no challenge runs logged yet.");
        }
        else
        {
            builder.AppendLine("Recent queue:");
            foreach (var entry in recentHistory)
            {
                builder.AppendLine(FormatHistoryEntry(entry, true));
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatHistoryEntry(ChallengeRunRecord record, bool includeCode)
    {
        var stamp = record.PlayedAtUnixSeconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(record.PlayedAtUnixSeconds).ToLocalTime().ToString("MM-dd HH:mm")
            : "--";
        var outcome = record.Retreated ? "RET" : record.Won ? "WIN" : "FAIL";
        var medal = "No Medal";
        if (AsyncChallengeCatalog.TryParse(record.Code, out var challenge, out _))
        {
            medal = AsyncChallengeCatalog.ResolveMedalLabel(challenge, record.Score);
        }

        var suffix = includeCode
            ? $" | {record.Code}"
            : $" | {record.StarsEarned}/3 stars | {record.EnemyDefeats} defeats";
        var rawScore = Math.Max(0, record.RawScore);
        if (rawScore == 0)
        {
            rawScore = Math.Max(0, record.Score);
        }

        var multiplier = record.ScoreMultiplier > 0f ? record.ScoreMultiplier : 1f;
        var hullPercent = Mathf.RoundToInt(Mathf.Clamp(record.BusHullRatio, 0f, 1f) * 100f);
        var deckMode = record.UsedLockedDeck ? "Locked deck" : "Player deck";
        return
            $"{stamp} | {outcome} | {record.Score} pts | {medal} | {record.ElapsedSeconds:0.0}s{suffix}\n" +
            $"  Raw {rawScore} x{multiplier:0.##} | Hull {hullPercent}% | Deploys {record.PlayerDeployments} | {deckMode}";
    }
}
