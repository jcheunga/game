using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class LanRaceMenu : Control
{
	private MenuBackdropSet _menuBackdrop = null!;
	private HBoxContainer _resourcesRow = null!;
	private Label _challengeCodeLabel = null!;
	private Label _boardLabel = null!;
	private HBoxContainer _boardDeckRow = null!;
	private HBoxContainer _boardSpellRow = null!;
	private Label _roomLabel = null!;
	private Label _readinessLabel = null!;
	private Label _monitorLabel = null!;
	private Label _scoreboardLabel = null!;
	private Label _sessionLabel = null!;
	private Label _statusLabel = null!;
	private LineEdit _addressEdit = null!;
	private Button _hostButton = null!;
	private Button _refreshButton = null!;
	private Button _joinButton = null!;
	private Button _closeButton = null!;
	private Button _readyButton = null!;
	private Button _launchButton = null!;

	private readonly List<Control> _entrancePanels = new();

	public override void _Ready()
	{
		BuildUi();
		if (LanChallengeService.Instance != null)
		{
			LanChallengeService.Instance.StateChanged += OnLanStateChanged;
		}

		RefreshUi();
		AnimateEntrance();
	}

	private void AnimateEntrance()
	{
		for (var i = 0; i < _entrancePanels.Count; i++)
		{
			var panel = _entrancePanels[i];
			panel.Modulate = new Color(1f, 1f, 1f, 0f);
			var delay = 0.06f + (i * 0.05f);
			var tween = CreateTween();
			tween.TweenProperty(panel, "modulate:a", 1f, 0.22f)
				.SetDelay(delay)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		}
	}

	public override void _ExitTree()
	{
		if (LanChallengeService.Instance != null)
		{
			LanChallengeService.Instance.StateChanged -= OnLanStateChanged;
		}
	}

	private void BuildUi()
	{
		_menuBackdrop = MenuBackdropComposer.AddSolidBackdrop(this, "lan_race", new Color("14213d"));

		var titlePanel = new PanelContainer
		{
			Position = new Vector2(24f, 20f),
			Size = new Vector2(1232f, 82f)
		};
		AddChild(titlePanel);
		_entrancePanels.Add(titlePanel);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		titlePanel.AddChild(titleRow);

		titleRow.AddChild(new Label
		{
			Text = "LAN Challenge Race",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});
		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_resourcesRow);

		_challengeCodeLabel = new Label
		{
			Text = string.Empty,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		titleRow.AddChild(_challengeCodeLabel);

		var boardPanel = new PanelContainer
		{
			Position = new Vector2(24f, 122f),
			Size = new Vector2(540f, 520f)
		};
		AddChild(boardPanel);
		_entrancePanels.Add(boardPanel);

		var boardPadding = new MarginContainer();
		boardPadding.AddThemeConstantOverride("margin_left", 18);
		boardPadding.AddThemeConstantOverride("margin_right", 18);
		boardPadding.AddThemeConstantOverride("margin_top", 18);
		boardPadding.AddThemeConstantOverride("margin_bottom", 18);
		boardPanel.AddChild(boardPadding);

		var boardStack = new VBoxContainer();
		boardStack.AddThemeConstantOverride("separation", 12);
		boardPadding.AddChild(boardStack);

		boardStack.AddChild(new Label
		{
			Text = "Armed Board"
		});

		_boardLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0f, 210f)
		};
		boardStack.AddChild(_boardLabel);
		_boardDeckRow = new HBoxContainer();
		_boardDeckRow.AddThemeConstantOverride("separation", 8);
		boardStack.AddChild(_boardDeckRow);
		_boardSpellRow = new HBoxContainer();
		_boardSpellRow.AddThemeConstantOverride("separation", 8);
		boardStack.AddChild(_boardSpellRow);

		boardStack.AddChild(new Label
		{
			Text = "Join Host IP"
		});

		_addressEdit = new LineEdit
		{
			PlaceholderText = "192.168.0.12",
			Text = "127.0.0.1"
		};
		boardStack.AddChild(_addressEdit);

		var controlRow = new HBoxContainer();
		controlRow.AddThemeConstantOverride("separation", 8);
		boardStack.AddChild(controlRow);

		_hostButton = new Button
		{
			Text = "Host Current Board",
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_hostButton.Pressed += HostBoard;
		controlRow.AddChild(_hostButton);

		_refreshButton = new Button
		{
			Text = "Broadcast Update",
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_refreshButton.Pressed += RefreshHostedBoard;
		controlRow.AddChild(_refreshButton);

		var joinRow = new HBoxContainer();
		joinRow.AddThemeConstantOverride("separation", 8);
		boardStack.AddChild(joinRow);

		_joinButton = new Button
		{
			Text = "Join Host",
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_joinButton.Pressed += JoinHost;
		joinRow.AddChild(_joinButton);

		_closeButton = new Button
		{
			Text = "Close Room",
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_closeButton.Pressed += CloseRoom;
		joinRow.AddChild(_closeButton);

		_statusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0f, 118f)
		};
		boardStack.AddChild(_statusLabel);

		var roomPanel = new PanelContainer
		{
			Position = new Vector2(588f, 122f),
			Size = new Vector2(668f, 520f)
		};
		AddChild(roomPanel);
		_entrancePanels.Add(roomPanel);

		var roomPadding = new MarginContainer();
		roomPadding.AddThemeConstantOverride("margin_left", 18);
		roomPadding.AddThemeConstantOverride("margin_right", 18);
		roomPadding.AddThemeConstantOverride("margin_top", 18);
		roomPadding.AddThemeConstantOverride("margin_bottom", 18);
		roomPanel.AddChild(roomPadding);

		var roomScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		roomPadding.AddChild(roomScroll);

		var roomStack = new VBoxContainer();
		roomStack.AddThemeConstantOverride("separation", 12);
		roomScroll.AddChild(roomStack);

		roomStack.AddChild(new Label
		{
			Text = "Room"
		});

		_roomLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0f, 132f)
		};
		roomStack.AddChild(_roomLabel);

		roomStack.AddChild(new Label
		{
			Text = "Launch Readiness"
		});

		_readinessLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0f, 136f)
		};
		roomStack.AddChild(_readinessLabel);

		roomStack.AddChild(new Label
		{
			Text = "Race Monitor"
		});

		_monitorLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0f, 148f)
		};
		roomStack.AddChild(_monitorLabel);

		roomStack.AddChild(new Label
		{
			Text = "Scoreboard"
		});

		_scoreboardLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0f, 136f)
		};
		roomStack.AddChild(_scoreboardLabel);

		roomStack.AddChild(new Label
		{
			Text = "Session Standings"
		});

		_sessionLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0f, 156f)
		};
		roomStack.AddChild(_sessionLabel);

		var bottomPanel = new PanelContainer
		{
			Position = new Vector2(24f, 660f),
			Size = new Vector2(1232f, 56f)
		};
		AddChild(bottomPanel);
		_entrancePanels.Add(bottomPanel);

		var bottomRow = new HBoxContainer();
		bottomRow.AddThemeConstantOverride("separation", 12);
		bottomPanel.AddChild(bottomRow);

		var backButton = new Button
		{
			Text = "Back To Multiplayer",
			CustomMinimumSize = new Vector2(200f, 0f)
		};
		backButton.Pressed += () => SceneRouter.Instance.GoToMultiplayer();
		bottomRow.AddChild(backButton);

		var settingsButton = new Button
		{
			Text = "Settings",
			CustomMinimumSize = new Vector2(140f, 0f)
		};
		settingsButton.Pressed += () => SceneRouter.Instance.GoToSettings();
		bottomRow.AddChild(settingsButton);

		bottomRow.AddChild(new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		});

		_readyButton = new Button
		{
			Text = "Ready Up",
			CustomMinimumSize = new Vector2(180f, 0f)
		};
		_readyButton.Pressed += ToggleReady;
		bottomRow.AddChild(_readyButton);

		_launchButton = new Button
		{
			Text = "Launch LAN Race",
			CustomMinimumSize = new Vector2(240f, 0f)
		};
		_launchButton.Pressed += LaunchRace;
		bottomRow.AddChild(_launchButton);
	}

	private void RefreshUi()
	{
		var challenge = GameState.Instance.GetSelectedAsyncChallenge();
		var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage));
		var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
		var previewDeck = GameState.Instance.GetSelectedAsyncChallengeDeckUnits();
		var previewSpells = GameState.Instance.GetSelectedAsyncChallengeDeckSpells();
		RebuildResourcesRow();
		_challengeCodeLabel.Text = string.Empty;
		var titleRow = _challengeCodeLabel.GetParent<HBoxContainer>();
		while (titleRow.GetChildCount() > 3) titleRow.GetChild(titleRow.GetChildCount() - 1).QueueFree();
		titleRow.AddChild(UiBadgeFactory.CreateMetaMetric("challenge", challenge.Code, new Vector2(24f, 24f)));
		var deckMode = GameState.Instance.HasSelectedAsyncChallengeLockedDeck
			? $"Locked LAN squad: {string.Join(", ", previewDeck.Select(unit => unit.DisplayName))}"
			: $"Player async squad: {string.Join(", ", previewDeck.Select(unit => unit.DisplayName))}";
		_boardLabel.Text =
			$"{challenge.Code}\n" +
			$"{stage.MapName} - Stage {stage.StageNumber}: {stage.StageName}\n" +
			$"Mutator: {mutator.Title}\n" +
			$"{deckMode}\n" +
			$"{GameState.Instance.BuildSelectedAsyncChallengeDeckSynergyInlineSummary()}\n\n" +
			$"{StageEncounterIntel.BuildCompactSummary(stage)}\n\n" +
			"Change the armed board on the Multiplayer Challenge screen, then return here to host or rebroadcast it.";
		RebuildBoardBadgeRows(previewDeck, previewSpells);

		_roomLabel.Text = LanChallengeService.Instance?.BuildRoomSummary() ?? "LAN room service unavailable.";
		_readinessLabel.Text = LanChallengeService.Instance?.BuildLaunchReadinessSummary() ?? "LAN launch readiness unavailable.";
		_monitorLabel.Text = LanChallengeService.Instance?.BuildRaceMonitorSummary() ?? "LAN race monitor unavailable.";
		_scoreboardLabel.Text = LanChallengeService.Instance?.ScoreboardSummary ?? "LAN scoreboard unavailable.";
		_sessionLabel.Text = LanChallengeService.Instance?.SessionStandingsSummary ?? "LAN session standings unavailable.";
		_statusLabel.Text = $"Status:\n{LanChallengeService.Instance?.SessionStatus ?? "No LAN room service available."}";

		var service = LanChallengeService.Instance;
		_hostButton.Text = service != null && service.IsHosting ? "Rehost Current Board" : "Host Current Board";
		_hostButton.Disabled = service != null && service.IsHosting && service.RoundLocked;
		_refreshButton.Disabled = service == null || !service.IsHosting || service.RoundLocked;
		_joinButton.Disabled = service != null && service.HasRoom && !service.IsClient;
		_closeButton.Disabled = service == null || !service.HasRoom;
		_readyButton.Disabled =
			service == null ||
			!service.HasRoom ||
			(service.RoundLocked && !service.RoundComplete);
		_readyButton.Text =
			service == null || !service.HasRoom
				? "Ready Up"
				: service.LocalSpectating && !service.RoundComplete
					? "Spectating"
					: service.LocalSpectating && service.RoundComplete
						? "Join Rematch"
						: service.LocalReady
							? "Unready"
							: service.RoundComplete
								? "Ready Rematch"
								: "Ready Up";
		_launchButton.Disabled =
			service == null ||
			!service.IsHosting ||
			service.RoundLocked ||
			string.IsNullOrWhiteSpace(service.SharedChallengeCode) ||
			!service.AllPeersReady ||
			!service.AllPeersDecksReady;
	}

	private void RebuildResourcesRow()
	{
		foreach (var child in _resourcesRow.GetChildren())
		{
			child.QueueFree();
		}

		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", GameState.Instance.Gold.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", GameState.Instance.Food.ToString("N0"), new Vector2(24f, 24f)));
	}

	private void RebuildBoardBadgeRows(IReadOnlyList<UnitDefinition> units, IReadOnlyList<SpellDefinition> spells)
	{
		foreach (var child in _boardDeckRow.GetChildren())
		{
			child.QueueFree();
		}

		foreach (var child in _boardSpellRow.GetChildren())
		{
			child.QueueFree();
		}

		foreach (var unit in units)
		{
			_boardDeckRow.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(34f, 34f)));
		}

		if (spells.Count == 0)
		{
			var emptyText = GameState.Instance.HasSelectedAsyncChallengeLockedDeck ? "Spell lock" : "No spells";
			_boardSpellRow.AddChild(UiBadgeFactory.CreateRewardMetric("spell", "", emptyText, new Vector2(24f, 24f)));
			return;
		}

		foreach (var spell in spells)
		{
			_boardSpellRow.AddChild(UiBadgeFactory.CreateSpellBadge(spell, new Vector2(34f, 34f)));
		}
	}

	private void HostBoard()
	{
		if (LanChallengeService.Instance == null)
		{
			return;
		}

		if (LanChallengeService.Instance.HostSelectedBoard(out var message))
		{
			_statusLabel.Text = $"Status:\n{message}";
		}
		else
		{
			_statusLabel.Text = $"Status:\n{message}";
		}

		RefreshUi();
	}

	private void RefreshHostedBoard()
	{
		if (LanChallengeService.Instance == null)
		{
			return;
		}

		if (!LanChallengeService.Instance.RefreshHostedBoard(out var message))
		{
			_statusLabel.Text = $"Status:\n{message}";
			return;
		}

		_statusLabel.Text = $"Status:\n{message}";
		RefreshUi();
	}

	private void JoinHost()
	{
		if (LanChallengeService.Instance == null)
		{
			return;
		}

		if (!LanChallengeService.Instance.JoinRoom(_addressEdit.Text, out var message))
		{
			_statusLabel.Text = $"Status:\n{message}";
			return;
		}

		_statusLabel.Text = $"Status:\n{message}";
		RefreshUi();
	}

	private void CloseRoom()
	{
		LanChallengeService.Instance?.CloseRoom();
		RefreshUi();
	}

	private void ToggleReady()
	{
		if (LanChallengeService.Instance == null)
		{
			return;
		}

		if (!LanChallengeService.Instance.ToggleLocalReady(out var message))
		{
			_statusLabel.Text = $"Status:\n{message}";
			return;
		}

		_statusLabel.Text = $"Status:\n{message}";
		RefreshUi();
	}

	private void LaunchRace()
	{
		if (LanChallengeService.Instance == null)
		{
			return;
		}

		if (!LanChallengeService.Instance.LaunchRace(out var message))
		{
			_statusLabel.Text = $"Status:\n{message}";
			return;
		}

		_statusLabel.Text = $"Status:\n{message}";
	}

	private void OnLanStateChanged()
	{
		RefreshUi();
	}
}
