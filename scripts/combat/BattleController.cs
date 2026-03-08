using System;
using System.Collections.Generic;
using Godot;

public partial class BattleController : Node2D
{
	private readonly struct TerrainPalette
	{
		public TerrainPalette(
			Color skyColor,
			Color groundColor,
			Color accentColor,
			Color playerBaseColor,
			Color enemyBaseColor,
			Color playerCoreColor,
			Color enemyCoreColor)
		{
			SkyColor = skyColor;
			GroundColor = groundColor;
			AccentColor = accentColor;
			PlayerBaseColor = playerBaseColor;
			EnemyBaseColor = enemyBaseColor;
			PlayerCoreColor = playerCoreColor;
			EnemyCoreColor = enemyCoreColor;
		}

		public Color SkyColor { get; }
		public Color GroundColor { get; }
		public Color AccentColor { get; }
		public Color PlayerBaseColor { get; }
		public Color EnemyBaseColor { get; }
		public Color PlayerCoreColor { get; }
		public Color EnemyCoreColor { get; }
	}

	private sealed class DeploySlot
	{
		public DeploySlot(UnitDefinition definition, Button button)
		{
			Definition = definition;
			Button = button;
		}

		public UnitDefinition Definition { get; }
		public Button Button { get; }
	}

	private readonly struct EndlessDraftOption
	{
		public EndlessDraftOption(string id, string title, string summary)
		{
			Id = id;
			Title = title;
			Summary = summary;
		}

		public string Id { get; }
		public string Title { get; }
		public string Summary { get; }
	}

	private CombatTuning _combat = new();

	private readonly List<Unit> _units = new();
	private readonly BattleDeckState _deck = new();
	private readonly RandomNumberGenerator _rng = new();
	private BattleSpawnDirector _spawnDirector = null!;
	private BattleRunMode _battleMode;

	private Label _baseHealthLabel = null!;
	private Label _resourceLabel = null!;
	private Label _timerLabel = null!;
	private Label _statusLabel = null!;
	private Label _waveIntelLabel = null!;
	private Label _objectiveStatusLabel = null!;
	private Label _fpsLabel = null!;
	private Label _endLabel = null!;
	private PanelContainer _endPanel = null!;
	private CenterContainer _endCenter = null!;
	private CenterContainer _draftCenter = null!;
	private PanelContainer _draftPanel = null!;
	private Label _draftLabel = null!;
	private CheckBox _showDevUiToggle = null!;
	private CheckBox _showFpsToggle = null!;
	private readonly List<DeploySlot> _deploySlots = new();
	private readonly List<Button> _draftButtons = new();
	private readonly HashSet<string> _endlessRunUpgrades = new(StringComparer.OrdinalIgnoreCase);

	private StageDefinition _stageData = null!;

	private int _stage;
	private string _activeRouteId = "city";
	private string _endlessBoonId = EndlessBoonCatalog.SurplusCourageId;
	private string _endlessRouteForkId = EndlessRouteForkCatalog.MainlinePushId;
	private string _endlessSupportEventLabel = "No convoy support event yet.";

	private float _playerBaseHealth;
	private float _playerBaseMaxHealth;
	private float _enemyBaseHealth;
	private float _enemyBaseMaxHealth;
	private float _courage;
	private float _maxCourage;
	private float _courageGainPerSecond;
	private float _elapsed;
	private int _playerDeployments;
	private int _enemyDefeats;
	private float _playerBaseFlashTimer;
	private float _enemyBaseFlashTimer;
	private bool _battleEnded;
	private bool _endlessCheckpointActive;
	private float _endlessUnitHealthScale = 1f;
	private float _endlessUnitDamageScale = 1f;
	private float _endlessScrapScale = 1f;
	private string[] _draftOptionIds = Array.Empty<string>();
	private bool _draftingRouteFork;

	private bool IsEndlessMode => _battleMode == BattleRunMode.Endless;

	private float PlayerBaseX => _combat.PlayerBaseX;
	private float EnemyBaseX => _combat.EnemyBaseX;
	private float PlayerSpawnX => _combat.PlayerSpawnX;
	private float EnemySpawnX => _combat.EnemySpawnX;

	private float BattlefieldLeft => _combat.BattlefieldLeft;
	private float BattlefieldRight => _combat.BattlefieldRight;
	private float BattlefieldTop => _combat.BattlefieldTop;
	private float BattlefieldBottom => _combat.BattlefieldBottom;
	private float SpawnVerticalPadding => _combat.SpawnVerticalPadding;
	private float BaseCoreRadius => _combat.BaseCoreRadius;
	private float BaseApproachDistance => _combat.BaseApproachDistance;

	private float BaseCenterY => (BattlefieldTop + BattlefieldBottom) * 0.5f;
	private Vector2 PlayerBaseCorePosition => new(PlayerBaseX, BaseCenterY);
	private Vector2 EnemyBaseCorePosition => new(EnemyBaseX, BaseCenterY);

	public override void _Ready()
	{
		_rng.Randomize();
		_spawnDirector = new BattleSpawnDirector(_rng);
		_combat = GameData.Combat;
		_battleMode = GameState.Instance.CurrentBattleMode;

		if (IsEndlessMode)
			{
				_activeRouteId = NormalizeRouteId(GameState.Instance.SelectedEndlessRouteId);
				_endlessBoonId = EndlessBoonCatalog.Normalize(GameState.Instance.SelectedEndlessBoonId);
				_endlessRouteForkId = EndlessRouteForkCatalog.MainlinePushId;
				_stageData = GameData.GetLatestStageForMap(_activeRouteId);
			_stage = _stageData.StageNumber;
			_playerBaseMaxHealth = _stageData.PlayerBaseHealth * StageModifiers.ResolvePlayerBaseHealthScale(_stageData) * 1.08f;
			_enemyBaseMaxHealth = _stageData.EnemyBaseHealth * StageModifiers.ResolveEnemyBaseHealthScale(_stageData);
		}
		else
		{
			_stage = Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage);
			_stageData = GameData.GetStage(_stage);
			_activeRouteId = NormalizeRouteId(_stageData.MapId);
			_playerBaseMaxHealth = _stageData.PlayerBaseHealth * StageModifiers.ResolvePlayerBaseHealthScale(_stageData);
			_enemyBaseMaxHealth = _stageData.EnemyBaseHealth * StageModifiers.ResolveEnemyBaseHealthScale(_stageData);
		}

		_playerBaseHealth = _playerBaseMaxHealth;
		_enemyBaseHealth = _enemyBaseMaxHealth;

		_courage = _combat.CourageStart;
		_maxCourage = _combat.CourageMax;
		_courageGainPerSecond = _combat.CourageGainPerSecond * StageModifiers.ResolveCourageGainScale(_stageData);

		if (IsEndlessMode)
		{
			ApplyEndlessBoon();
		}

		_playerDeployments = 0;
		_enemyDefeats = 0;
		_endlessCheckpointActive = false;
		_endlessUnitHealthScale = 1f;
		_endlessUnitDamageScale = 1f;
			_endlessScrapScale = 1f;
			_endlessRunUpgrades.Clear();
			_draftOptionIds = Array.Empty<string>();
			_draftingRouteFork = false;
			_endlessSupportEventLabel = IsEndlessMode
				? "Opening convoy package deployed."
				: "No convoy support event yet.";

		_deck.Initialize(GameState.Instance.GetActiveDeckUnits());
		if (IsEndlessMode)
		{
			_spawnDirector.InitializeEndless(_activeRouteId, _stageData, _combat, GameData.GetEnemyUnits());
		}
		else
		{
			_spawnDirector.Initialize(_stage, _stageData, _combat, GameData.GetEnemyUnits());
		}

		BuildUi();
		SetStatus(
			IsEndlessMode
				? "Select a squad card, click the battlefield to deploy, and hold against escalating waves."
				: "Select a squad card, then click the battlefield to deploy from the bus.");
		UpdateHud();
	}

	public override void _Draw()
	{
		var palette = ResolveTerrainPalette();

		DrawRect(new Rect2(0f, 0f, 1280f, 720f), palette.SkyColor, true);

		DrawRect(
			new Rect2(
				BattlefieldLeft,
				BattlefieldTop,
				BattlefieldRight - BattlefieldLeft,
				BattlefieldBottom - BattlefieldTop),
			palette.GroundColor,
			true);

		const int stripeCount = 6;
		for (var i = 0; i <= stripeCount; i++)
		{
			var t = i / (float)stripeCount;
			var y = Mathf.Lerp(BattlefieldTop, BattlefieldBottom, t);
			DrawLine(
				new Vector2(BattlefieldLeft, y),
				new Vector2(BattlefieldRight, y),
				palette.AccentColor,
				1.2f,
				true);
		}

		DrawTerrainDecoration();

		DrawPlayerBus(palette);
		DrawEnemyBarricade(palette);
	}

	private TerrainPalette ResolveTerrainPalette()
	{
		var terrainId = (_stageData?.TerrainId ?? "urban").ToLowerInvariant();
		return terrainId switch
		{
			"highway" => new TerrainPalette(
				new Color("1c2a39"),
				new Color("2a3b4f"),
				new Color(1f, 1f, 1f, 0.12f),
				new Color("2a9d8f"),
				new Color("ef476f"),
				new Color("4cc9a6"),
				new Color("ff4d6d")),
			"night" => new TerrainPalette(
				new Color("0b132b"),
				new Color("1a1e3d"),
				new Color(0.75f, 0.89f, 1f, 0.14f),
				new Color("2d6a4f"),
				new Color("9d0208"),
				new Color("52b788"),
				new Color("ef476f")),
			"industrial" => new TerrainPalette(
				new Color("273043"),
				new Color("3b4252"),
				new Color(0.95f, 0.95f, 0.95f, 0.09f),
				new Color("588157"),
				new Color("bc4749"),
				new Color("84a98c"),
				new Color("e63946")),
			"swamp" => new TerrainPalette(
				new Color("274029"),
				new Color("3a5a40"),
				new Color(0.82f, 0.93f, 0.76f, 0.08f),
				new Color("40916c"),
				new Color("bc4749"),
				new Color("74c69d"),
				new Color("ef476f")),
			"shipyard" => new TerrainPalette(
				new Color("102a43"),
				new Color("243b53"),
				new Color(0.88f, 0.95f, 1f, 0.1f),
				new Color("2f9e8f"),
				new Color("c1121f"),
				new Color("52b788"),
				new Color("ef476f")),
			_ => new TerrainPalette(
				new Color("14213d"),
				new Color("22324f"),
				new Color(1f, 1f, 1f, 0.08f),
				new Color("2a9d8f"),
				new Color("e63946"),
				new Color("52b788"),
				new Color("ef476f"))
		};
	}

	private void DrawTerrainDecoration()
	{
		var terrainId = (_stageData?.TerrainId ?? "urban").ToLowerInvariant();
		switch (terrainId)
		{
			case "highway":
				for (var i = 0; i < 14; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 42f, BattlefieldRight - 42f, i / 13f);
					DrawRect(new Rect2(x - 8f, BaseCenterY - 3f, 16f, 6f), new Color(1f, 0.92f, 0.52f, 0.4f), true);
				}
				break;
			case "night":
				DrawCircle(new Vector2(BattlefieldLeft + 180f, BattlefieldTop + 54f), 38f, new Color(0.4f, 0.7f, 1f, 0.09f));
				DrawCircle(new Vector2(BattlefieldRight - 220f, BattlefieldBottom - 62f), 46f, new Color(1f, 0.4f, 0.7f, 0.08f));
				break;
			case "industrial":
				DrawRect(new Rect2(BattlefieldLeft + 170f, BattlefieldTop + 30f, 84f, 24f), new Color(0f, 0f, 0f, 0.25f), true);
				DrawRect(new Rect2(BattlefieldRight - 294f, BattlefieldBottom - 60f, 84f, 24f), new Color(0f, 0f, 0f, 0.25f), true);
				DrawRect(new Rect2(BattlefieldRight - 198f, BattlefieldBottom - 60f, 84f, 24f), new Color(0f, 0f, 0f, 0.2f), true);
				break;
			case "swamp":
				DrawCircle(new Vector2(BattlefieldLeft + 270f, BattlefieldBottom - 58f), 30f, new Color(0.13f, 0.25f, 0.17f, 0.45f));
				DrawCircle(new Vector2(BattlefieldRight - 290f, BattlefieldTop + 62f), 24f, new Color(0.13f, 0.25f, 0.17f, 0.45f));
				break;
			case "shipyard":
				DrawRect(new Rect2(BattlefieldLeft, BattlefieldBottom - 46f, BattlefieldRight - BattlefieldLeft, 22f), new Color(0.2f, 0.35f, 0.5f, 0.45f), true);
				for (var i = 0; i < 7; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 110f, BattlefieldRight - 110f, i / 6f);
					DrawLine(
						new Vector2(x - 20f, BattlefieldTop + 36f),
						new Vector2(x + 20f, BattlefieldTop + 72f),
						new Color(1f, 1f, 1f, 0.12f),
						2f,
						true);
				}
				break;
		}
	}

	private void DrawPlayerBus(TerrainPalette palette)
	{
		var healthRatio = Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f);
		var offset = GetBaseShakeOffset(true);
		var bodyColor = ResolveBaseBodyColor(palette.PlayerBaseColor, _playerBaseFlashTimer, healthRatio);
		var cabinColor = ResolveBaseCoreColor(palette.PlayerCoreColor, _playerBaseFlashTimer, healthRatio);
		var bodyRect = new Rect2(PlayerBaseX - 46f, BaseCenterY - 34f, 122f, 58f);
		var cabinRect = new Rect2(PlayerBaseX + 44f, BaseCenterY - 24f, 34f, 34f);
		var bumperRect = new Rect2(PlayerBaseX - 58f, BaseCenterY + 10f, 16f, 12f);

		DrawRect(OffsetRect(bodyRect, offset), bodyColor, true);
		DrawRect(OffsetRect(cabinRect, offset), cabinColor, true);
		DrawRect(OffsetRect(bumperRect, offset), cabinColor.Darkened(0.2f), true);
		DrawRect(
			OffsetRect(new Rect2(PlayerBaseX - 26f, BaseCenterY - 20f, 44f, 18f), offset),
			new Color(1f, 1f, 1f, 0.18f + ((_playerBaseFlashTimer > 0f) ? 0.12f : 0f)),
			true);
		DrawCircle(new Vector2(PlayerBaseX - 12f, BaseCenterY + 28f) + offset, 12f, new Color("1f2933"));
		DrawCircle(new Vector2(PlayerBaseX + 42f, BaseCenterY + 28f) + offset, 12f, new Color("1f2933"));

		var warningLight = healthRatio < 0.45f && Mathf.Sin(_elapsed * 10f) > 0f
			? new Color(1f, 0.34f, 0.28f, 0.95f)
			: new Color(1f, 0.96f, 0.62f, 0.95f);
		DrawCircle(new Vector2(PlayerBaseX + 72f, BaseCenterY - 8f) + offset, 4f, warningLight);

		DrawDamageSmoke(new Vector2(PlayerBaseX - 18f, BaseCenterY - 48f) + offset, healthRatio, bodyColor);
		DrawBaseHealthMeter(
			new Vector2(PlayerBaseX + 8f, BaseCenterY - 58f) + offset,
			132f,
			healthRatio,
			bodyColor.Lightened(0.35f),
			new Color("80ed99"));
	}

	private void DrawEnemyBarricade(TerrainPalette palette)
	{
		var healthRatio = Mathf.Clamp(_enemyBaseHealth / Mathf.Max(1f, _enemyBaseMaxHealth), 0f, 1f);
		var offset = GetBaseShakeOffset(false);
		var wallColor = ResolveBaseBodyColor(palette.EnemyBaseColor, _enemyBaseFlashTimer, healthRatio);
		var coreColor = ResolveBaseCoreColor(palette.EnemyCoreColor, _enemyBaseFlashTimer, healthRatio);
		var wallBaseX = EnemyBaseX - 52f;
		DrawRect(OffsetRect(new Rect2(wallBaseX, BaseCenterY - 54f, 76f, 112f), offset), wallColor, true);
		DrawRect(OffsetRect(new Rect2(wallBaseX - 18f, BaseCenterY - 12f, 18f, 72f), offset), coreColor.Darkened(0.1f), true);
		DrawRect(OffsetRect(new Rect2(wallBaseX + 76f, BaseCenterY - 36f, 18f, 94f), offset), coreColor.Darkened(0.15f), true);
		DrawRect(OffsetRect(new Rect2(wallBaseX + 12f, BaseCenterY - 72f, 22f, 18f), offset), coreColor, true);
		DrawRect(OffsetRect(new Rect2(wallBaseX + 40f, BaseCenterY - 84f, 22f, 30f), offset), coreColor, true);
		DrawCircle(new Vector2(EnemyBaseX - 10f, BaseCenterY - 26f) + offset, 7f, new Color(1f, 0.45f, 0.2f, 0.9f));

		if (healthRatio < 0.75f)
		{
			DrawLine(
				new Vector2(wallBaseX + 16f, BaseCenterY - 22f) + offset,
				new Vector2(wallBaseX + 42f, BaseCenterY + 16f) + offset,
				coreColor.Lightened(0.25f),
				3f,
				true);
		}

		if (healthRatio < 0.45f)
		{
			DrawLine(
				new Vector2(wallBaseX + 48f, BaseCenterY - 44f) + offset,
				new Vector2(wallBaseX + 18f, BaseCenterY + 6f) + offset,
				new Color(1f, 0.68f, 0.38f, 0.8f),
				4f,
				true);
		}

		DrawDamageSmoke(new Vector2(EnemyBaseX - 14f, BaseCenterY - 88f) + offset, healthRatio, wallColor);
		DrawBaseHealthMeter(
			new Vector2(EnemyBaseX - 10f, BaseCenterY - 112f) + offset,
			124f,
			healthRatio,
			wallColor.Lightened(0.25f),
			new Color("ff8fab"));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_battleEnded || _endlessCheckpointActive)
		{
			return;
		}

		var deltaF = (float)delta;
		_elapsed += deltaF;
		_playerBaseFlashTimer = Mathf.Max(0f, _playerBaseFlashTimer - deltaF);
		_enemyBaseFlashTimer = Mathf.Max(0f, _enemyBaseFlashTimer - deltaF);

		_courage += _courageGainPerSecond * deltaF;
		if (_courage > _maxCourage)
		{
			_courage = _maxCourage;
		}

		_deck.TickCooldowns(deltaF);
		_spawnDirector.Tick(deltaF, _elapsed, () => CountTeamUnits(Team.Enemy), SpawnEnemyUnit, SetStatus);

		SimulateUnits(deltaF);
		CleanupDeadUnits();
		MaybeOpenEndlessDraft();
		UpdateHud();
		QueueRedraw();
		CheckBattleEnd();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_battleEnded || _endlessCheckpointActive)
		{
			return;
		}

		if (@event is not InputEventMouseButton mouseButton)
		{
			return;
		}

		if (!mouseButton.Pressed || mouseButton.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		if (IsInBattlefield(mouseButton.Position))
		{
			TryDeployAtY(mouseButton.Position.Y);
		}
	}

	private void BuildUi()
	{
		var canvasLayer = new CanvasLayer();
		AddChild(canvasLayer);

		var root = new Control();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		canvasLayer.AddChild(root);

		var topPanel = new PanelContainer
		{
			Position = new Vector2(16f, 16f),
			Size = new Vector2(540f, 170f)
		};
		root.AddChild(topPanel);

		var topVBox = new VBoxContainer();
		topVBox.AddThemeConstantOverride("separation", 6);
		topPanel.AddChild(topVBox);

		_baseHealthLabel = new Label();
		topVBox.AddChild(_baseHealthLabel);

		_resourceLabel = new Label();
		topVBox.AddChild(_resourceLabel);

		_timerLabel = new Label();
		topVBox.AddChild(_timerLabel);

		_statusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		topVBox.AddChild(_statusLabel);

		_fpsLabel = new Label();
		topVBox.AddChild(_fpsLabel);

		var infoPanel = new PanelContainer
		{
			Position = new Vector2(572f, 16f),
			Size = new Vector2(380f, 336f)
		};
		root.AddChild(infoPanel);

		var infoVBox = new VBoxContainer();
		infoVBox.AddThemeConstantOverride("separation", 8);
		infoPanel.AddChild(infoVBox);

		infoVBox.AddChild(new Label { Text = IsEndlessMode ? "Endless Deployment" : "Deployment" });
		infoVBox.AddChild(new Label
		{
			Text = IsEndlessMode
				? "1) Pick a squad card below.\n2) Click the battlefield lane.\n3) Survive escalating waves or retreat with salvage."
				: "1) Pick a squad card below.\n2) Click the battlefield lane.\nCards spend courage and go on cooldown after deployment."
		});

		_waveIntelLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		infoVBox.AddChild(_waveIntelLabel);

		_objectiveStatusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		infoVBox.AddChild(_objectiveStatusLabel);

		var retreatButton = new Button
		{
			Text = IsEndlessMode ? "Retreat From Run" : "Retreat To Map",
			Position = new Vector2(970f, 22f),
			Size = new Vector2(292f, 44f)
		};
		retreatButton.Pressed += RetreatToMap;
		root.AddChild(retreatButton);

		var settingsPanel = new PanelContainer
		{
			Position = new Vector2(970f, 76f),
			Size = new Vector2(292f, 92f)
		};
		root.AddChild(settingsPanel);

		var settingsVBox = new VBoxContainer();
		settingsVBox.AddThemeConstantOverride("separation", 3);
		settingsPanel.AddChild(settingsVBox);

		settingsVBox.AddChild(new Label { Text = "UI Settings" });

		_showDevUiToggle = new CheckBox
		{
			Text = "Show dev UI",
			ButtonPressed = GameState.Instance.ShowDevUi
		};
		_showDevUiToggle.Toggled += OnShowDevUiToggled;
		settingsVBox.AddChild(_showDevUiToggle);

		_showFpsToggle = new CheckBox
		{
			Text = "Show FPS counter",
			ButtonPressed = GameState.Instance.ShowFpsCounter
		};
		_showFpsToggle.Toggled += OnShowFpsToggled;
		settingsVBox.AddChild(_showFpsToggle);

		var spawnPanel = new PanelContainer
		{
			Position = new Vector2(16f, 586f),
			Size = new Vector2(1246f, 114f)
		};
		root.AddChild(spawnPanel);

		var spawnRow = new HBoxContainer();
		spawnRow.AddThemeConstantOverride("separation", 10);
		spawnPanel.AddChild(spawnRow);

		foreach (var definition in _deck.Roster)
		{
			var unit = definition;
			var button = new Button
			{
				Text = $"Deploy {unit.DisplayName} ({unit.Cost})",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0f, 82f)
			};
			button.AddThemeColorOverride("font_color", Colors.White);
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			button.AddThemeColorOverride("font_pressed_color", Colors.White);
			button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
			button.Pressed += () => ArmPlayerUnit(unit);
			spawnRow.AddChild(button);
			_deploySlots.Add(new DeploySlot(unit, button));
		}

		_endCenter = new CenterContainer();
		_endCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_endCenter.Visible = false;
		root.AddChild(_endCenter);

		_endPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(480f, 280f),
			Visible = false
		};
		_endCenter.AddChild(_endPanel);

		var endPadding = new MarginContainer();
		endPadding.AddThemeConstantOverride("margin_left", 20);
		endPadding.AddThemeConstantOverride("margin_right", 20);
		endPadding.AddThemeConstantOverride("margin_top", 20);
		endPadding.AddThemeConstantOverride("margin_bottom", 20);
		_endPanel.AddChild(endPadding);

		var endVBox = new VBoxContainer();
		endVBox.AddThemeConstantOverride("separation", 12);
		endPadding.AddChild(endVBox);

		_endLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		endVBox.AddChild(_endLabel);

		var retryButton = new Button
		{
			Text = IsEndlessMode ? "Restart Run" : "Retry Stage",
			CustomMinimumSize = new Vector2(0f, 48f)
		};
		retryButton.Pressed += () => SceneRouter.Instance.RetryBattle();
		endVBox.AddChild(retryButton);

		var mapButton = new Button
		{
			Text = IsEndlessMode ? "Back To Endless Prep" : "Back To Map",
			CustomMinimumSize = new Vector2(0f, 48f)
		};
		mapButton.Pressed += () =>
		{
			if (IsEndlessMode)
			{
				SceneRouter.Instance.GoToEndless();
				return;
			}

			SceneRouter.Instance.GoToMap();
		};
		endVBox.AddChild(mapButton);

		_draftCenter = new CenterContainer();
		_draftCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_draftCenter.Visible = false;
		root.AddChild(_draftCenter);

		_draftPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(560f, 320f),
			Visible = false
		};
		_draftCenter.AddChild(_draftPanel);

		var draftPadding = new MarginContainer();
		draftPadding.AddThemeConstantOverride("margin_left", 20);
		draftPadding.AddThemeConstantOverride("margin_right", 20);
		draftPadding.AddThemeConstantOverride("margin_top", 20);
		draftPadding.AddThemeConstantOverride("margin_bottom", 20);
		_draftPanel.AddChild(draftPadding);

		var draftVBox = new VBoxContainer();
		draftVBox.AddThemeConstantOverride("separation", 12);
		draftPadding.AddChild(draftVBox);

		_draftLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		draftVBox.AddChild(_draftLabel);

		for (var i = 0; i < 3; i++)
		{
			var draftIndex = i;
			var draftButton = new Button
			{
				CustomMinimumSize = new Vector2(0f, 56f)
			};
			draftButton.Pressed += () => ApplyEndlessDraftChoice(draftIndex);
			draftVBox.AddChild(draftButton);
			_draftButtons.Add(draftButton);
		}

		ApplyDevUiSettings();
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}

	private void UpdateHud()
	{
		_baseHealthLabel.Text = IsEndlessMode
			? $"Bus hull: {Mathf.CeilToInt(_playerBaseHealth)}/{Mathf.CeilToInt(_playerBaseMaxHealth)}   |   Route: {ResolveRouteLabel(_activeRouteId)} endless hold"
			: $"Bus hull: {Mathf.CeilToInt(_playerBaseHealth)}/{Mathf.CeilToInt(_playerBaseMaxHealth)}   |   Barricade: {Mathf.CeilToInt(_enemyBaseHealth)}/{Mathf.CeilToInt(_enemyBaseMaxHealth)}";
		_resourceLabel.Text = IsEndlessMode
			? $"Courage: {Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}   |   Endless wave {_spawnDirector.EndlessWaveNumber}   |   Best {GameState.Instance.BestEndlessWave}"
			: $"Courage: {Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}   |   Stage {_stage}";
		var waveStatus = _spawnDirector.IsEndlessMode
			? $"   |   Pending surge: {Mathf.Max(0f, _spawnDirector.NextEndlessWaveTime - _elapsed):0.0}s   |   Queued spawns: {_spawnDirector.PendingSpawnCount}"
			: _spawnDirector.UsesScriptedWaves
				? $"   |   Waves: {_spawnDirector.NextScriptedWaveIndex}/{_spawnDirector.TotalScriptedWaves}   |   Queued spawns: {_spawnDirector.PendingSpawnCount}"
				: "";
		_timerLabel.Text =
			$"Time: {_elapsed:0.0}s   |   Active enemies: {CountTeamUnits(Team.Enemy)}   |   Active allies: {CountTeamUnits(Team.Player)}{waveStatus}";
		_fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
		_waveIntelLabel.Text = BuildWaveIntelText();
		_objectiveStatusLabel.Text = IsEndlessMode
			? BuildEndlessStatusText()
			: StageObjectives.BuildLiveSummary(_stageData, BuildStageBattleResult());

		foreach (var slot in _deploySlots)
		{
			var cooldown = _deck.GetCooldownRemaining(slot.Definition.Id);
			var isReady = cooldown <= 0.05f;
			var hasCourage = _courage >= slot.Definition.Cost;
			slot.Button.Disabled = _battleEnded || _endlessCheckpointActive || !isReady || !hasCourage;
			var level = GameState.Instance.GetUnitLevel(slot.Definition.Id);

			var stateLabel = !isReady
				? $"RECOVER {cooldown:0.0}s"
				: hasCourage
					? "READY"
					: $"NEED {slot.Definition.Cost}";
			var marker = slot.Definition == _deck.ArmedUnit ? "> " : "";
			slot.Button.Text =
				$"{marker}Lv{level} {slot.Definition.DisplayName}\n{stateLabel}  |  {slot.Definition.Cost} courage";
			slot.Button.SelfModulate = ResolveDeployButtonTint(slot.Definition, isReady, hasCourage, slot.Definition == _deck.ArmedUnit);
			slot.Button.TooltipText = BuildDeployButtonTooltip(slot.Definition, level, isReady, cooldown);
		}
	}

	private void OnShowDevUiToggled(bool enabled)
	{
		GameState.Instance.SetShowDevUi(enabled);
		ApplyDevUiSettings();
	}

	private void OnShowFpsToggled(bool enabled)
	{
		GameState.Instance.SetShowFpsCounter(enabled);
		ApplyDevUiSettings();
	}

	private void ApplyDevUiSettings()
	{
		var showDevUi = GameState.Instance.ShowDevUi;
		_timerLabel.Visible = showDevUi;
		_statusLabel.Visible = showDevUi;
		_showFpsToggle.Disabled = !showDevUi;
		_fpsLabel.Visible = showDevUi && GameState.Instance.ShowFpsCounter;
	}

	private int CountTeamUnits(Team team)
	{
		var count = 0;
		foreach (var unit in _units)
		{
			if (!unit.IsDead && unit.Team == team)
			{
				count++;
			}
		}

		return count;
	}

	private void ArmPlayerUnit(UnitDefinition definition)
	{
		if (_battleEnded)
		{
			return;
		}

		_deck.Arm(definition);
		SetStatus(
			$"Selected Lv{GameState.Instance.GetUnitLevel(definition.Id)} {definition.DisplayName}. Click the battlefield to deploy from the bus.");
		UpdateHud();
	}

	private bool IsInBattlefield(Vector2 position)
	{
		return position.X >= BattlefieldLeft &&
			position.X <= BattlefieldRight &&
			position.Y >= BattlefieldTop &&
			position.Y <= BattlefieldBottom;
	}

	private void TryDeployAtY(float clickY)
	{
		if (!_deck.HasArmedUnit)
		{
			SetStatus("Pick a squad card first, then click the battlefield.");
			return;
		}

		if (!_deck.CanDeploy(_deck.ArmedUnit, _courage, _battleEnded, out var reason))
		{
			SetStatus(reason);
			return;
		}

		var spawnY = Mathf.Clamp(clickY, BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding);
		TrySpawnPlayer(_deck.ArmedUnit, new Vector2(PlayerSpawnX, spawnY));
	}

	private void TrySpawnPlayer(UnitDefinition definition, Vector2 spawnPosition)
	{
		if (_battleEnded)
		{
			return;
		}

		if (!_deck.CanDeploy(definition, _courage, _battleEnded, out var reason))
		{
			SetStatus(reason);
			return;
		}

		var stats = BuildPlayerUnitStatsForBattle(definition);
		_courage -= stats.Cost;
		_deck.MarkDeployed(definition);
		_playerDeployments++;
		SpawnUnit(Team.Player, stats, spawnPosition);
		SpawnEffect(spawnPosition, stats.Color, 12f, 42f, 0.28f);
		SetStatus(
			$"Deployed Lv{GameState.Instance.GetUnitLevel(definition.Id)} {stats.Name} from the bus at lane height {Mathf.RoundToInt(spawnPosition.Y)}.");
		UpdateHud();
	}

	private void SpawnEnemyUnit(UnitStats stats, Vector2 position)
	{
		SpawnUnit(Team.Enemy, stats, position);
		SpawnEffect(position, stats.Color.Darkened(0.15f), 10f, 26f, 0.22f, false);
	}

	private void SpawnUnit(Team team, UnitStats stats, Vector2 position)
	{
		var unit = new Unit();
		unit.Setup(team, stats, position);
		AddChild(unit);
		_units.Add(unit);
	}

	private void SpawnProjectile(Unit attacker, Unit target)
	{
		var projectile = new Projectile();
		projectile.GlobalPosition = attacker.GlobalPosition;

		var speed = attacker.ProjectileSpeed > 0f ? attacker.ProjectileSpeed : 420f;
		var color = attacker.Tint.Lightened(0.25f);
		projectile.Setup(target, attacker.AttackDamage, speed, color, SpawnDamageFeedback);
		AddChild(projectile);
	}

	private void TryAttackBase(Unit attacker)
	{
		if (IsEndlessMode && attacker.Team == Team.Player)
		{
			return;
		}

		var targetBase = attacker.Team == Team.Player ? EnemyBaseCorePosition : PlayerBaseCorePosition;
		if (!attacker.TryBeginAttackPosition(targetBase, BaseCoreRadius))
		{
			return;
		}

		if (attacker.Team == Team.Player)
		{
			_enemyBaseHealth -= attacker.BaseDamage;
			_enemyBaseFlashTimer = 0.22f;
			SpawnEffect(EnemyBaseCorePosition, attacker.Tint, 8f, 26f, 0.18f);
			SpawnFloatText(EnemyBaseCorePosition + new Vector2(0f, -24f), $"-{attacker.BaseDamage}", attacker.Tint.Lightened(0.18f), 0.44f);
		}
		else
		{
			_playerBaseHealth -= attacker.BaseDamage;
			_playerBaseFlashTimer = 0.22f;
			SpawnEffect(PlayerBaseCorePosition, attacker.Tint, 8f, 26f, 0.18f);
			SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -24f), $"-{attacker.BaseDamage}", attacker.Tint.Lightened(0.18f), 0.44f);
		}
	}

	private bool ShouldApproachBase(Unit unit)
	{
		if (unit.Team == Team.Player)
		{
			return unit.Position.X >= EnemyBaseX - BaseApproachDistance;
		}

		return unit.Position.X <= PlayerBaseX + BaseApproachDistance;
	}

	private void SimulateUnits(float delta)
	{
		foreach (var unit in _units)
		{
			if (unit.IsDead)
			{
				continue;
			}

			unit.TickAttackTimer(delta);
			var target = FindClosestEnemy(unit);

			if (target != null && unit.CanAttack(target))
			{
				if (unit.UsesProjectile)
				{
					if (unit.TryBeginAttack(target))
					{
						SpawnProjectile(unit, target);
					}
				}
				else
				{
					if (unit.TryBeginAttack(target))
					{
						var appliedDamage = target.TakeDamage(unit.AttackDamage);
						SpawnDamageFeedback(target.Position, appliedDamage, unit.Tint);
					}
				}
			}
			else if (target != null)
			{
				if (unit.UsesProjectile)
				{
					// Ranged units anchor their line when an enemy is nearby.
					// They do not chase targets that move out of firing range.
				}
				else
				{
					unit.MoveToward(
						target.Position,
						delta,
						BattlefieldLeft,
						BattlefieldRight,
						BattlefieldTop + SpawnVerticalPadding,
						BattlefieldBottom - SpawnVerticalPadding);
				}
			}
			else
			{
				var targetBase = unit.Team == Team.Player ? EnemyBaseCorePosition : PlayerBaseCorePosition;
				if (unit.CanAttackPosition(targetBase, BaseCoreRadius))
				{
					TryAttackBase(unit);
				}
				else if (ShouldApproachBase(unit))
				{
					unit.MoveToward(
						targetBase,
						delta,
						BattlefieldLeft,
						BattlefieldRight,
						BattlefieldTop + SpawnVerticalPadding,
						BattlefieldBottom - SpawnVerticalPadding);
				}
				else
				{
					unit.Advance(
						delta,
						BattlefieldLeft,
						BattlefieldRight,
						BattlefieldTop + SpawnVerticalPadding,
						BattlefieldBottom - SpawnVerticalPadding);
				}
			}
		}
	}

	private Unit FindClosestEnemy(Unit source)
	{
		Unit bestTarget = null;
		var bestDistance = float.MaxValue;

		foreach (var candidate in _units)
		{
			if (candidate.IsDead || candidate.Team == source.Team)
			{
				continue;
			}

			if (!source.IsInAggroRange(candidate))
			{
				continue;
			}

			var distance = source.Position.DistanceSquaredTo(candidate.Position);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestTarget = candidate;
			}
		}

		return bestTarget;
	}

	private void CleanupDeadUnits()
	{
		for (var i = _units.Count - 1; i >= 0; i--)
		{
			if (!_units[i].IsDead)
			{
				continue;
			}

			var deadUnit = _units[i];
			if (deadUnit.Team == Team.Enemy)
			{
				_enemyDefeats++;
			}

			TriggerDeathBurst(deadUnit);
			TriggerSpawnOnDeath(deadUnit);
			SpawnEffect(deadUnit.Position, deadUnit.Tint, 8f, 24f, 0.22f);
			_units.RemoveAt(i);
			deadUnit.QueueFree();
		}
	}

	private void TriggerDeathBurst(Unit deadUnit)
	{
		if (deadUnit.DeathBurstDamage <= 0f || deadUnit.DeathBurstRadius <= 0f)
		{
			return;
		}

		SpawnEffect(
			deadUnit.Position,
			deadUnit.Tint.Lightened(0.15f),
			12f,
			deadUnit.DeathBurstRadius,
			0.26f,
			false);

		foreach (var candidate in _units)
		{
			if (candidate == deadUnit || candidate.IsDead || candidate.Team == deadUnit.Team)
			{
				continue;
			}

			if (candidate.Position.DistanceTo(deadUnit.Position) > deadUnit.DeathBurstRadius)
			{
				continue;
			}

			var appliedDamage = candidate.TakeDamage(deadUnit.DeathBurstDamage);
			SpawnDamageFeedback(candidate.Position, appliedDamage, deadUnit.Tint.Lightened(0.15f));
		}
	}

	private void TriggerSpawnOnDeath(Unit deadUnit)
	{
		if (deadUnit.Team != Team.Enemy ||
			string.IsNullOrWhiteSpace(deadUnit.SpawnOnDeathUnitId) ||
			deadUnit.SpawnOnDeathCount <= 0)
		{
			return;
		}

		if (!_spawnDirector.TryBuildEnemyStats(deadUnit.SpawnOnDeathUnitId, out var spawnedStats))
		{
			GD.PushWarning($"Could not resolve spawned enemy '{deadUnit.SpawnOnDeathUnitId}'.");
			return;
		}

		var count = deadUnit.SpawnOnDeathCount;
		const float spacing = 18f;
		var startOffset = -((count - 1) * spacing) * 0.5f;

		for (var i = 0; i < count; i++)
		{
			var spawnPosition = new Vector2(
				Mathf.Clamp(deadUnit.Position.X + _rng.RandfRange(-10f, 10f), BattlefieldLeft, BattlefieldRight),
				Mathf.Clamp(
					deadUnit.Position.Y + startOffset + (spacing * i),
					BattlefieldTop + SpawnVerticalPadding,
					BattlefieldBottom - SpawnVerticalPadding));
			SpawnEnemyUnit(spawnedStats, spawnPosition);
		}
	}

	private void SpawnEffect(Vector2 position, Color color, float startRadius, float endRadius, float lifetime, bool filled = true)
	{
		var effect = new BattleEffect();
		effect.Position = position;
		effect.Setup(color, startRadius, endRadius, lifetime, filled);
		AddChild(effect);
	}

	private void SpawnDamageFeedback(Vector2 position, float damage, Color color)
	{
		if (damage <= 0.05f)
		{
			return;
		}

		SpawnEffect(position, color.Lightened(0.12f), 4f, 18f + (damage * 0.18f), 0.14f, false);
		SpawnFloatText(
			position + new Vector2(_rng.RandfRange(-6f, 6f), -8f),
			$"-{Mathf.RoundToInt(damage)}",
			color.Lightened(0.3f),
			0.46f);
	}

	private void SpawnFloatText(Vector2 position, string text, Color color, float lifetime = 0.5f)
	{
		var floatText = new BattleFloatText();
		floatText.Position = position;
		floatText.Setup(
			text,
			color,
			lifetime,
			new Vector2(_rng.RandfRange(-10f, 10f), _rng.RandfRange(-54f, -42f)));
		AddChild(floatText);
	}

	private Rect2 OffsetRect(Rect2 rect, Vector2 offset)
	{
		return new Rect2(rect.Position + offset, rect.Size);
	}

	private Vector2 GetBaseShakeOffset(bool playerBase)
	{
		var flashTimer = playerBase ? _playerBaseFlashTimer : _enemyBaseFlashTimer;
		if (flashTimer <= 0f)
		{
			return Vector2.Zero;
		}

		var strength = Mathf.Clamp(flashTimer / 0.22f, 0f, 1f) * 4f;
		var phase = (_elapsed * 56f) + (playerBase ? 0f : 1.8f);
		return new Vector2(
			Mathf.Sin(phase) * strength,
			Mathf.Cos(phase * 1.37f) * strength * 0.38f);
	}

	private Color ResolveBaseBodyColor(Color source, float flashTimer, float healthRatio)
	{
		var wornColor = source.Lerp(new Color("4a4e69"), (1f - healthRatio) * 0.28f);
		var flashStrength = Mathf.Clamp(flashTimer / 0.22f, 0f, 1f);
		return wornColor.Lerp(Colors.White, flashStrength * 0.42f);
	}

	private Color ResolveBaseCoreColor(Color source, float flashTimer, float healthRatio)
	{
		var dangerColor = source.Lerp(new Color("ff6b6b"), (1f - healthRatio) * 0.24f);
		var flashStrength = Mathf.Clamp(flashTimer / 0.22f, 0f, 1f);
		return dangerColor.Lerp(Colors.White, flashStrength * 0.5f);
	}

	private void DrawBaseHealthMeter(Vector2 center, float width, float healthRatio, Color frameColor, Color fillColor)
	{
		var origin = center - new Vector2(width * 0.5f, 0f);
		DrawRect(new Rect2(origin, new Vector2(width, 8f)), new Color(0f, 0f, 0f, 0.55f), true);
		DrawRect(new Rect2(origin + new Vector2(1f, 1f), new Vector2((width - 2f) * healthRatio, 6f)), fillColor, true);
		DrawRect(new Rect2(origin, new Vector2(width, 8f)), frameColor, false, 2f);
	}

	private void DrawDamageSmoke(Vector2 origin, float healthRatio, Color sourceColor)
	{
		if (healthRatio >= 0.7f)
		{
			return;
		}

		var smokeStrength = Mathf.Clamp((0.7f - healthRatio) / 0.7f, 0f, 1f);
		var plumeOffset = Mathf.Sin(_elapsed * 1.8f) * 6f;
		DrawCircle(origin + new Vector2(plumeOffset, -12f), 10f + (smokeStrength * 5f), new Color(sourceColor.Darkened(0.7f), 0.18f + (smokeStrength * 0.12f)));
		DrawCircle(origin + new Vector2(-4f + (plumeOffset * 0.4f), -24f), 14f + (smokeStrength * 6f), new Color(0f, 0f, 0f, 0.12f + (smokeStrength * 0.12f)));
	}

	private Color ResolveDeployButtonTint(UnitDefinition definition, bool isReady, bool hasCourage, bool armed)
	{
		var tint = definition.GetTint();
		if (!isReady)
		{
			return tint.Darkened(0.45f);
		}

		if (!hasCourage)
		{
			return tint.Darkened(0.28f).Lerp(new Color("6c757d"), 0.35f);
		}

		return armed
			? tint.Lightened(0.25f)
			: tint.Lerp(Colors.White, 0.25f);
	}

	private string BuildDeployButtonTooltip(UnitDefinition definition, int level, bool isReady, float cooldown)
	{
		var stats = BuildPlayerUnitStatsForBattle(definition);
		var status = isReady ? "Ready to deploy" : $"Cooldown: {cooldown:0.0}s";
		return
			$"Lv{level} {definition.DisplayName}\n" +
			$"{status}\n" +
			$"HP {Mathf.RoundToInt(stats.MaxHealth)}  |  ATK {stats.AttackDamage:0.#}  |  Range {stats.AttackRange:0.#}\n" +
			$"Deploy CD {definition.DeployCooldown:0.#}s";
	}

	private string BuildWaveIntelText()
	{
		if (IsEndlessMode)
		{
			if (_spawnDirector.EndlessCheckpointPending)
			{
				var remainingEnemies = CountTeamUnits(Team.Enemy) + _spawnDirector.PendingSpawnCount;
				var checkpointLabel = IsRouteForkCheckpoint()
					? "route fork"
					: "upgrade";
				return remainingEnemies > 0
					? $"Checkpoint wave active: clear {remainingEnemies} remaining enemies to open the {checkpointLabel} draft."
					: $"Checkpoint ready: choose a {checkpointLabel} to resume the convoy.";
			}

			var endlessCountdown = Mathf.Max(0f, _spawnDirector.NextEndlessWaveTime - _elapsed);
			return
				$"Endless intel: {ResolveRouteLabel(_activeRouteId)} surge route  |  Path: {EndlessRouteForkCatalog.Get(_endlessRouteForkId).Title}\n" +
				$"Current wave: {_spawnDirector.EndlessWaveNumber}  |  Next surge in {endlessCountdown:0.0}s  |  Queued: {_spawnDirector.PendingSpawnCount}\n" +
				$"Pressure profile: {BuildEndlessPressureText()}\n" +
				$"Segment event: {_spawnDirector.EndlessSegmentEventLabel}\n" +
				$"Convoy support: {_endlessSupportEventLabel}";
		}

		var modifierSummary = $"Modifiers: {StageModifiers.BuildInlineSummary(_stageData)}";

		if (!_spawnDirector.UsesScriptedWaves)
		{
			return $"{modifierSummary}\nEncounter intel: dynamic pressure spawns are active on this route.";
		}

		if (!_spawnDirector.TryGetNextScriptedWave(out var nextWave))
		{
			var suffix = _spawnDirector.PendingSpawnCount > 0
				? $"Encounter intel: {_spawnDirector.PendingSpawnCount} enemies still queued from the active scripted wave."
				: "Encounter intel: all scripted waves have deployed. Finish the route.";
			return $"{modifierSummary}\n{suffix}";
		}

		var countdown = Mathf.Max(0f, nextWave.TriggerTime - _elapsed);
		var label = string.IsNullOrWhiteSpace(nextWave.Label)
			? $"Wave {_spawnDirector.NextScriptedWaveIndex + 1}"
			: nextWave.Label;
		return
			$"{modifierSummary}\n" +
			$"Next wave in {countdown:0.0}s: {label}\n" +
			$"{BuildWaveEntrySummary(nextWave)}";
	}

	private string BuildWaveEntrySummary(StageWaveDefinition wave)
	{
		var parts = new List<string>();
		for (var i = 0; i < wave.Entries.Length; i++)
		{
			var entry = wave.Entries[i];
			if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
			{
				continue;
			}

			var displayName = GameData.GetUnit(entry.UnitId).DisplayName;
			parts.Add($"{displayName} x{Mathf.Max(1, entry.Count)}");
		}

		return parts.Count > 0
			? string.Join(", ", parts)
			: "No enemy composition data.";
	}

	private string BuildEndlessStatusText()
	{
		var projectedScrap = CalculateEndlessScrapReward();
		var projectedFuel = CalculateEndlessFuelReward();
		return
			"Endless run:\n" +
			$"Wave reached: {_spawnDirector.EndlessWaveNumber}  |  Enemy defeats: {_enemyDefeats}  |  Survival: {_elapsed:0.0}s\n" +
			$"Projected salvage: +{projectedScrap} scrap, +{projectedFuel} fuel  |  Boon: {EndlessBoonCatalog.Get(_endlessBoonId).Title}\n" +
			$"Path: {EndlessRouteForkCatalog.Get(_endlessRouteForkId).Title}  |  Run upgrades: {_endlessRunUpgrades.Count}\n" +
			$"Segment event: {_spawnDirector.EndlessSegmentEventLabel}\n" +
			$"Convoy support: {_endlessSupportEventLabel}\n" +
			$"Record: wave {GameState.Instance.BestEndlessWave}  |  {GameState.Instance.BestEndlessTimeSeconds:0.0}s";
	}

	private string BuildEndlessPressureText()
	{
		var routeText = _activeRouteId switch
		{
			"harbor" => "Heavier bodies, bloaters, and crusher spikes as the clock climbs.",
			_ => "Faster runners, frequent spitters, and tighter surge cadence over time."
		};

		var forkText = _endlessRouteForkId switch
		{
			EndlessRouteForkCatalog.MainlinePushId => "Current fork speeds up the line and raises ranged pressure.",
			EndlessRouteForkCatalog.ScavengeDetourId => "Current fork slows the surge slightly but adds heavier salvage lanes.",
			EndlessRouteForkCatalog.FortifiedBlockId => "Current fork softens pressure at the cost of lower scrap efficiency.",
			_ => ""
		};

		return $"{routeText} {forkText}".Trim();
	}

	private void MaybeOpenEndlessDraft()
	{
		if (!IsEndlessMode || _endlessCheckpointActive || !_spawnDirector.EndlessCheckpointPending)
		{
			return;
		}

		if (_spawnDirector.PendingSpawnCount > 0 || CountTeamUnits(Team.Enemy) > 0)
		{
			return;
		}

		_endlessCheckpointActive = true;
		_draftingRouteFork = IsRouteForkCheckpoint();
		_draftOptionIds = _draftingRouteFork ? BuildRouteForkOptions() : BuildDraftOptions();
		_draftLabel.Text = _draftingRouteFork
			? $"Checkpoint secure on wave {_spawnDirector.EndlessWaveNumber}.\nChoose the next route segment before the convoy rolls out."
			: $"Checkpoint secure on wave {_spawnDirector.EndlessWaveNumber}.\nChoose one run upgrade before the next surge.";

		for (var i = 0; i < _draftButtons.Count; i++)
		{
			var option = _draftingRouteFork
				? GetRouteForkOption(_draftOptionIds[i])
				: GetDraftOption(_draftOptionIds[i]);
			_draftButtons[i].Text = $"{option.Title}\n{option.Summary}";
			_draftButtons[i].Disabled = false;
		}

		_draftCenter.Visible = true;
		_draftPanel.Visible = true;
		SetStatus(_draftingRouteFork ? "Checkpoint held. Pick the next route segment." : "Checkpoint held. Pick a convoy upgrade.");
		UpdateHud();
	}

	private string[] BuildDraftOptions()
	{
		var available = new List<string>
		{
			"bus_plates",
			"supply_drop",
			"courage_pump",
			"shock_drill",
			"field_tonic",
			"salvage_contract"
		};

		for (var i = available.Count - 1; i >= 0; i--)
		{
			if (_endlessRunUpgrades.Contains(available[i]))
			{
				available.RemoveAt(i);
			}
		}

		while (available.Count < 3)
		{
			available.Add("supply_drop");
		}

		var picked = new List<string>();
		while (picked.Count < 3 && available.Count > 0)
		{
			var index = _rng.RandiRange(0, available.Count - 1);
			picked.Add(available[index]);
			available.RemoveAt(index);
		}

		return picked.ToArray();
	}

	private string[] BuildRouteForkOptions()
	{
		var options = EndlessRouteForkCatalog.GetAll();
		return new[]
		{
			options[0].Id,
			options[1].Id,
			options[2].Id
		};
	}

	private void ApplyEndlessDraftChoice(int draftIndex)
	{
		if (!_endlessCheckpointActive || draftIndex < 0 || draftIndex >= _draftOptionIds.Length)
		{
			return;
		}

		if (_draftingRouteFork)
		{
			var fork = GetRouteForkOption(_draftOptionIds[draftIndex]);
			ApplyEndlessRouteFork(fork.Id);
			SetStatus($"Route fork selected: {fork.Title}.");
		}
		else
		{
			var option = GetDraftOption(_draftOptionIds[draftIndex]);
			ApplyEndlessRunUpgrade(option.Id);
			SetStatus($"Checkpoint upgrade applied: {option.Title}.");
		}

		_endlessCheckpointActive = false;
		_draftCenter.Visible = false;
		_draftPanel.Visible = false;
		_draftOptionIds = Array.Empty<string>();
		_draftingRouteFork = false;
		_spawnDirector.ResumeEndlessAfterCheckpoint(_elapsed);
		UpdateHud();
	}

	private void ApplyEndlessRunUpgrade(string optionId)
	{
		switch (optionId)
		{
			case "bus_plates":
			{
				_endlessRunUpgrades.Add(optionId);
				var hullGain = _playerBaseMaxHealth * 0.15f;
				_playerBaseMaxHealth += hullGain;
				_playerBaseHealth = Mathf.Min(_playerBaseMaxHealth, _playerBaseHealth + hullGain);
				break;
			}
			case "supply_drop":
				_courage = Mathf.Min(_maxCourage, _courage + 25f);
				break;
			case "courage_pump":
				if (_endlessRunUpgrades.Add(optionId))
				{
					_courageGainPerSecond *= 1.2f;
				}
				break;
			case "shock_drill":
				if (_endlessRunUpgrades.Add(optionId))
				{
					_endlessUnitDamageScale *= 1.12f;
				}
				break;
			case "field_tonic":
				if (_endlessRunUpgrades.Add(optionId))
				{
					_endlessUnitHealthScale *= 1.18f;
				}
				break;
			case "salvage_contract":
				if (_endlessRunUpgrades.Add(optionId))
				{
					_endlessScrapScale *= 1.15f;
				}
				break;
		}
	}

	private static EndlessDraftOption GetDraftOption(string optionId)
	{
		return optionId switch
		{
			"bus_plates" => new EndlessDraftOption("bus_plates", "Bus Plates", "Add 15% max bus hull and repair the convoy by the same amount."),
			"supply_drop" => new EndlessDraftOption("supply_drop", "Supply Drop", "Immediately gain +25 courage for the next deployment burst."),
			"courage_pump" => new EndlessDraftOption("courage_pump", "Courage Pump", "Increase courage generation by 20% for the rest of the run."),
			"shock_drill" => new EndlessDraftOption("shock_drill", "Shock Drill", "Future deployed units deal 12% more damage for the rest of the run."),
			"field_tonic" => new EndlessDraftOption("field_tonic", "Field Tonic", "Future deployed units gain 18% more health for the rest of the run."),
			"salvage_contract" => new EndlessDraftOption("salvage_contract", "Salvage Contract", "Increase final scrap payout by 15% for the rest of the run."),
			_ => new EndlessDraftOption("supply_drop", "Supply Drop", "Immediately gain +25 courage for the next deployment burst.")
		};
	}

	private static EndlessDraftOption GetRouteForkOption(string optionId)
	{
		var fork = EndlessRouteForkCatalog.Get(optionId);
		return new EndlessDraftOption(fork.Id, fork.Title, fork.Summary);
	}

	private void ApplyEndlessRouteFork(string optionId)
	{
		_endlessRouteForkId = EndlessRouteForkCatalog.Normalize(optionId);
		_spawnDirector.SetEndlessRouteFork(_endlessRouteForkId);

		TriggerRouteForkSupportEvent(_endlessRouteForkId);
	}

	private void TriggerRouteForkSupportEvent(string routeForkId)
	{
		switch (routeForkId)
		{
			case EndlessRouteForkCatalog.MainlinePushId:
				_endlessSupportEventLabel = "Dispatch riders arrived: +20 courage and cooldown recovery across the squad.";
				_courage = Mathf.Min(_maxCourage, _courage + 20f);
				_deck.ReduceCooldowns(3f);
				break;
			case EndlessRouteForkCatalog.ScavengeDetourId:
				_endlessSupportEventLabel = "Scavenger escort arrived: raider reinforcement deployed and convoy patched.";
				RepairBusByRatio(0.1f);
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerRaiderId)
					? GameData.PlayerRaiderId
					: GameData.PlayerBrawlerId);
				break;
			case EndlessRouteForkCatalog.FortifiedBlockId:
				_endlessSupportEventLabel = "Safehouse militia joined: defender reinforcement and bus repairs secured the block.";
				RepairBusByRatio(0.12f);
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerDefenderId)
					? GameData.PlayerDefenderId
					: GameData.PlayerBrawlerId);
				break;
		}
	}

	private UnitStats BuildPlayerUnitStatsForBattle(UnitDefinition definition)
	{
		if (!IsEndlessMode)
		{
			return GameState.Instance.BuildPlayerUnitStats(definition);
		}

		return GameState.Instance.BuildPlayerUnitStats(
			definition,
			_endlessUnitHealthScale,
			_endlessUnitDamageScale,
			0f,
			0);
	}

	private void SpawnSupportUnit(string unitId)
	{
		if (string.IsNullOrWhiteSpace(unitId))
		{
			return;
		}

		var definition = GameData.GetUnit(unitId);
		if (!definition.IsPlayerSide)
		{
			return;
		}

		var spawnPosition = new Vector2(
			PlayerSpawnX + _rng.RandfRange(-10f, 10f),
			Mathf.Clamp(
				BaseCenterY + _rng.RandfRange(-72f, 72f),
				BattlefieldTop + SpawnVerticalPadding,
				BattlefieldBottom - SpawnVerticalPadding));

		var stats = BuildPlayerUnitStatsForBattle(definition);
		SpawnUnit(Team.Player, stats, spawnPosition);
		SpawnEffect(spawnPosition, stats.Color.Lightened(0.15f), 12f, 32f, 0.24f);
		SpawnFloatText(spawnPosition + new Vector2(0f, -22f), $"+ {stats.Name}", stats.Color.Lightened(0.35f), 0.54f);
	}

	private void RepairBusByRatio(float ratio)
	{
		if (ratio <= 0f)
		{
			return;
		}

		var healAmount = _playerBaseMaxHealth * ratio;
		_playerBaseHealth = Mathf.Min(_playerBaseMaxHealth, _playerBaseHealth + healAmount);
		_playerBaseFlashTimer = 0.18f;
		SpawnEffect(PlayerBaseCorePosition, new Color("80ed99"), 10f, 28f, 0.22f);
		SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -38f), $"+{Mathf.RoundToInt(healAmount)}", new Color("b7efc5"), 0.56f);
	}

	private bool IsRouteForkCheckpoint()
	{
		return _spawnDirector.EndlessWaveNumber > 0 && _spawnDirector.EndlessWaveNumber % 10 == 0;
	}

	private void CheckBattleEnd()
	{
		if (IsEndlessMode)
		{
			if (_playerBaseHealth <= 0f)
			{
				EndBattle(false);
			}

			return;
		}

		if (_enemyBaseHealth <= 0f)
		{
			EndBattle(true);
			return;
		}

		if (_playerBaseHealth <= 0f)
		{
			EndBattle(false);
		}
	}

	private void EndBattle(bool playerWon)
	{
		if (_battleEnded)
		{
			return;
		}

		_battleEnded = true;
		_playerBaseHealth = Mathf.Max(0f, _playerBaseHealth);
		_enemyBaseHealth = Mathf.Max(0f, _enemyBaseHealth);

		if (IsEndlessMode)
		{
			var rewardScrap = CalculateEndlessScrapReward();
			var rewardFuel = CalculateEndlessFuelReward();
			GameState.Instance.ApplyEndlessResult(_activeRouteId, _spawnDirector.EndlessWaveNumber, _elapsed, _enemyDefeats, rewardScrap, rewardFuel, false);
			_endLabel.Text =
				$"Endless run ended on {ResolveRouteLabel(_activeRouteId)}.\n" +
				$"Wave reached: {_spawnDirector.EndlessWaveNumber}\n" +
				$"Survival time: {_elapsed:0.0}s   |   Enemy defeats: {_enemyDefeats}\n" +
				$"Salvage secured: +{rewardScrap} scrap, +{rewardFuel} fuel";
			SetStatus("The convoy was eventually overrun. Salvage crews recovered what they could.");
			_endCenter.Visible = true;
			_endPanel.Visible = true;
			UpdateHud();
			return;
		}

		if (playerWon)
		{
			var evaluation = StageObjectives.EvaluateVictory(_stageData, BuildStageBattleResult());
			var bestStars = Mathf.Max(GameState.Instance.GetStageStars(_stage), evaluation.StarsEarned);
			GameState.Instance.ApplyVictory(_stage, _stageData.RewardScrap, _combat.VictoryFuelReward, evaluation.StarsEarned);
			_endLabel.Text =
				$"Victory on stage {_stage}.\n" +
				$"{StageObjectives.BuildResultSummary(_stageData, evaluation, _stageData.RewardScrap, _combat.VictoryFuelReward, bestStars)}";
			SetStatus("Barricade smashed. Route secured.");
		}
		else
		{
			GameState.Instance.ApplyDefeat(_stage);
			_endLabel.Text = "Defeat on this stage. Refit your squad and try a different timing.";
			SetStatus("The bus was overrun. Regroup.");
		}

		_endCenter.Visible = true;
		_endPanel.Visible = true;
		UpdateHud();
	}

	private void RetreatToMap()
	{
		if (IsEndlessMode)
		{
			if (!_battleEnded)
			{
				var rewardScrap = CalculateEndlessScrapReward();
				var rewardFuel = CalculateEndlessFuelReward();
				GameState.Instance.ApplyEndlessResult(_activeRouteId, _spawnDirector.EndlessWaveNumber, _elapsed, _enemyDefeats, rewardScrap, rewardFuel, true);
			}

			SceneRouter.Instance.GoToEndless();
			return;
		}

		if (!_battleEnded)
		{
			GameState.Instance.ApplyRetreat(_stage);
		}

		SceneRouter.Instance.GoToMap();
	}

	private StageBattleResult BuildStageBattleResult()
	{
		return new StageBattleResult
		{
			PlayerBaseHealth = _playerBaseHealth,
			PlayerBaseMaxHealth = _playerBaseMaxHealth,
			Elapsed = _elapsed,
			PlayerDeployments = _playerDeployments,
			EnemyDefeats = _enemyDefeats
		};
	}

	private int CalculateEndlessScrapReward()
	{
		var timeBonus = Mathf.FloorToInt(_elapsed / 18f) * 3;
		var reward = Math.Max(0, (_spawnDirector.EndlessWaveNumber * 16) + (_enemyDefeats * 2) + timeBonus);
		reward = Mathf.RoundToInt(reward * _endlessScrapScale);
		reward = Mathf.RoundToInt(reward * ResolveRouteForkScrapScale());
		if (_endlessBoonId == EndlessBoonCatalog.SalvageCacheId)
		{
			reward = Mathf.RoundToInt(reward * 1.25f);
		}

		return reward;
	}

	private int CalculateEndlessFuelReward()
	{
		if (_spawnDirector.EndlessWaveNumber <= 0)
		{
			return 0;
		}

		return Math.Max(0, _spawnDirector.EndlessWaveNumber / 4);
	}

	private static string ResolveRouteLabel(string routeId)
	{
		return NormalizeRouteId(routeId) switch
		{
			"harbor" => "Harbor Front",
			_ => "City Route"
		};
	}

	private static string NormalizeRouteId(string routeId)
	{
		return string.IsNullOrWhiteSpace(routeId)
			? "city"
			: routeId.Trim().ToLowerInvariant();
	}

	private void ApplyEndlessBoon()
	{
		switch (_endlessBoonId)
		{
			case EndlessBoonCatalog.ReinforcedBusId:
				_playerBaseMaxHealth *= 1.2f;
				_playerBaseHealth = _playerBaseMaxHealth;
				break;
			case EndlessBoonCatalog.SurplusCourageId:
				_courage = Mathf.Min(_maxCourage, _courage + 25f);
				break;
		}
	}

	private float ResolveRouteForkScrapScale()
	{
		return _endlessRouteForkId switch
		{
			EndlessRouteForkCatalog.MainlinePushId => 1.1f,
			EndlessRouteForkCatalog.ScavengeDetourId => 1.2f,
			EndlessRouteForkCatalog.FortifiedBlockId => 0.9f,
			_ => 1f
		};
	}
}
