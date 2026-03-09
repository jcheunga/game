using System;
using System.Text;
using Godot;

public partial class MultiplayerMenu : Control
{
    private OptionButton _stageSelector = null!;
    private OptionButton _mutatorSelector = null!;
    private LineEdit _codeEdit = null!;
    private Label _summaryLabel = null!;
    private Label _recordLabel = null!;
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
        _codeEdit.Text = challenge.Code;
        _summaryLabel.Text =
            $"Challenge target: {stage.MapName} - Stage {stage.StageNumber}: {stage.StageName}\n" +
            $"{AsyncChallengeCatalog.BuildSummary(challenge)}\n\n" +
            $"{StageEncounterIntel.BuildCompactSummary(stage)}";
        _recordLabel.Text =
            $"Local records:\n" +
            $"Best for this code: {GameState.Instance.GetAsyncChallengeBestScore(challenge.Code)}\n" +
            $"Challenge runs logged: {GameState.Instance.ChallengeRuns}\n" +
            $"Route lock: {(challenge.Stage <= GameState.Instance.HighestUnlockedStage ? "ready" : $"explore stage {challenge.Stage} first")}";
        _historyLabel.Text = BuildHistoryText(challenge);
        _rulesLabel.Text =
            "Challenge rules:\n" +
            "- Runs use the exact same challenge code, stage, mutator, and seeded spawn pattern.\n" +
            "- No food is spent and no campaign stars or stage unlocks are awarded.\n" +
            "- Compare score, time, stars, and hull preservation on the same code.\n" +
            $"- Active mutator: {mutator.Title}.";

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
            Text = $"Active Squad ({GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit})"
        });

        foreach (var definition in GameState.Instance.GetActiveDeckUnits())
        {
            _squadStack.AddChild(BuildUnitPanel(definition));
        }

        _squadStack.AddChild(new Label
        {
            Text = StageEncounterIntel.BuildEncounterIntel(stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }

    private Control BuildUnitPanel(UnitDefinition definition)
    {
        var stats = GameState.Instance.BuildPlayerUnitStats(definition);
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
            Text = $"Lv{GameState.Instance.GetUnitLevel(definition.Id)}  {definition.DisplayName}"
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
                (stats.BusRepairAmount > 0.05f ? $"  |  Repair {stats.BusRepairAmount:0.#}" : ""),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

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
        var suffix = includeCode
            ? $" | {record.Code}"
            : $" | {record.StarsEarned}/3 stars | {record.EnemyDefeats} defeats";
        return $"{stamp} | {outcome} | {record.Score} pts | {record.ElapsedSeconds:0.0}s{suffix}";
    }
}
