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

	private sealed class PendingEnemySpawn
	{
		public PendingEnemySpawn(UnitDefinition definition, float executeAt)
		{
			Definition = definition;
			ExecuteAt = executeAt;
		}

		public UnitDefinition Definition { get; }
		public float ExecuteAt { get; }
	}

	private CombatTuning _combat = new();

	private readonly List<Unit> _units = new();
	private readonly Dictionary<string, float> _deployCooldowns = new(StringComparer.OrdinalIgnoreCase);
	private readonly Queue<PendingEnemySpawn> _pendingEnemySpawns = new();
	private readonly RandomNumberGenerator _rng = new();

	private Label _baseHealthLabel = null!;
	private Label _resourceLabel = null!;
	private Label _timerLabel = null!;
	private Label _statusLabel = null!;
	private Label _fpsLabel = null!;
	private Label _endLabel = null!;
	private PanelContainer _endPanel = null!;
	private CenterContainer _endCenter = null!;
	private CheckBox _showDevUiToggle = null!;
	private CheckBox _showFpsToggle = null!;
	private readonly List<DeploySlot> _deploySlots = new();

	private readonly List<UnitDefinition> _playerRoster = new();
	private readonly List<UnitDefinition> _enemyRoster = new();
	private StageDefinition _stageData = null!;
	private UnitDefinition _armedPlayerUnit = null!;

	private int _stage;

	private float _playerBaseHealth;
	private float _enemyBaseHealth;
	private float _courage;
	private float _maxCourage;
	private float _courageGainPerSecond;
	private float _enemySpawnTimer;
	private float _elapsed;
	private bool _battleEnded;
	private int _nextScriptedWaveIndex;

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
		_combat = GameData.Combat;
		_stage = Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage);
		_stageData = GameData.GetStage(_stage);
		_playerBaseHealth = _stageData.PlayerBaseHealth;
		_enemyBaseHealth = _stageData.EnemyBaseHealth;

		_courage = _combat.CourageStart;
		_maxCourage = _combat.CourageMax;
		_courageGainPerSecond = _combat.CourageGainPerSecond;
		_enemySpawnTimer = _combat.InitialEnemySpawnDelay;

		_playerRoster.Clear();
		_playerRoster.AddRange(GameState.Instance.GetActiveDeckUnits());

		_enemyRoster.Clear();
		_enemyRoster.Add(GameData.GetUnit(GameData.EnemyWalkerId));
		_enemyRoster.Add(GameData.GetUnit(GameData.EnemyRunnerId));
		_enemyRoster.Add(GameData.GetUnit(GameData.EnemyBruteId));
		_enemyRoster.Add(GameData.GetUnit(GameData.EnemySpitterId));
		_enemyRoster.Add(GameData.GetUnit(GameData.EnemyCrusherId));
		_enemyRoster.Add(GameData.GetUnit(GameData.EnemyBossId));

		_deployCooldowns.Clear();
		foreach (var unit in _playerRoster)
		{
			_deployCooldowns[unit.Id] = 0f;
		}

		_pendingEnemySpawns.Clear();
		_nextScriptedWaveIndex = 0;

		BuildUi();
		_armedPlayerUnit = _playerRoster.Count > 0 ? _playerRoster[0] : null!;
		SetStatus("Select a squad card, then click the battlefield to deploy from the bus.");
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
		var bodyRect = new Rect2(PlayerBaseX - 46f, BaseCenterY - 34f, 122f, 58f);
		var cabinRect = new Rect2(PlayerBaseX + 44f, BaseCenterY - 24f, 34f, 34f);
		var bumperRect = new Rect2(PlayerBaseX - 58f, BaseCenterY + 10f, 16f, 12f);

		DrawRect(bodyRect, palette.PlayerBaseColor, true);
		DrawRect(cabinRect, palette.PlayerCoreColor, true);
		DrawRect(bumperRect, palette.PlayerCoreColor.Darkened(0.2f), true);
		DrawRect(new Rect2(PlayerBaseX - 26f, BaseCenterY - 20f, 44f, 18f), new Color(1f, 1f, 1f, 0.18f), true);
		DrawCircle(new Vector2(PlayerBaseX - 12f, BaseCenterY + 28f), 12f, new Color("1f2933"));
		DrawCircle(new Vector2(PlayerBaseX + 42f, BaseCenterY + 28f), 12f, new Color("1f2933"));
		DrawCircle(new Vector2(PlayerBaseX + 72f, BaseCenterY - 8f), 4f, new Color(1f, 0.96f, 0.62f, 0.95f));
	}

	private void DrawEnemyBarricade(TerrainPalette palette)
	{
		var wallBaseX = EnemyBaseX - 52f;
		DrawRect(new Rect2(wallBaseX, BaseCenterY - 54f, 76f, 112f), palette.EnemyBaseColor, true);
		DrawRect(new Rect2(wallBaseX - 18f, BaseCenterY - 12f, 18f, 72f), palette.EnemyCoreColor.Darkened(0.1f), true);
		DrawRect(new Rect2(wallBaseX + 76f, BaseCenterY - 36f, 18f, 94f), palette.EnemyCoreColor.Darkened(0.15f), true);
		DrawRect(new Rect2(wallBaseX + 12f, BaseCenterY - 72f, 22f, 18f), palette.EnemyCoreColor, true);
		DrawRect(new Rect2(wallBaseX + 40f, BaseCenterY - 84f, 22f, 30f), palette.EnemyCoreColor, true);
		DrawCircle(new Vector2(EnemyBaseX - 10f, BaseCenterY - 26f), 7f, new Color(1f, 0.45f, 0.2f, 0.9f));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_battleEnded)
		{
			return;
		}

		var deltaF = (float)delta;
		_elapsed += deltaF;

		_courage += _courageGainPerSecond * deltaF;
		if (_courage > _maxCourage)
		{
			_courage = _maxCourage;
		}

		TickDeployCooldowns(deltaF);

		if (_stageData.HasScriptedWaves)
		{
			TriggerScriptedWaves();
			FlushPendingEnemySpawns();
		}
		else
		{
			_enemySpawnTimer -= deltaF;
			if (_enemySpawnTimer <= 0f)
			{
				SpawnEnemyWave();

				var pressureTimeScale = Mathf.Max(1f, _combat.EnemySpawnPressureTimeScale);
				var pressure = Mathf.Clamp(
					1f + (_elapsed / pressureTimeScale),
					_combat.EnemySpawnPressureMin,
					_combat.EnemySpawnPressureMax);
				_enemySpawnTimer = Mathf.Max(
					_combat.EnemySpawnIntervalFloor,
					_rng.RandfRange(_stageData.EnemySpawnMin, _stageData.EnemySpawnMax) / pressure);
			}
		}

		SimulateUnits(deltaF);
		CleanupDeadUnits();
		UpdateHud();
		CheckBattleEnd();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_battleEnded)
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
			Size = new Vector2(380f, 170f)
		};
		root.AddChild(infoPanel);

		var infoVBox = new VBoxContainer();
		infoVBox.AddThemeConstantOverride("separation", 8);
		infoPanel.AddChild(infoVBox);

		infoVBox.AddChild(new Label { Text = "Deployment" });
		infoVBox.AddChild(new Label { Text = "1) Pick a squad card below.\n2) Click the battlefield lane.\nCards spend courage and go on cooldown after deployment." });

		var retreatButton = new Button
		{
			Text = "Retreat To Map",
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
			Position = new Vector2(16f, 606f),
			Size = new Vector2(1246f, 94f)
		};
		root.AddChild(spawnPanel);

		var spawnRow = new HBoxContainer();
		spawnRow.AddThemeConstantOverride("separation", 10);
		spawnPanel.AddChild(spawnRow);

		foreach (var definition in _playerRoster)
		{
			var unit = definition;
			var button = new Button
			{
				Text = $"Deploy {unit.DisplayName} ({unit.Cost})",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0f, 62f)
			};
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
			Text = "Retry Stage",
			CustomMinimumSize = new Vector2(0f, 48f)
		};
		retryButton.Pressed += () => SceneRouter.Instance.RetryBattle();
		endVBox.AddChild(retryButton);

		var mapButton = new Button
		{
			Text = "Back To Map",
			CustomMinimumSize = new Vector2(0f, 48f)
		};
		mapButton.Pressed += () => SceneRouter.Instance.GoToMap();
		endVBox.AddChild(mapButton);

		ApplyDevUiSettings();
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}

	private void UpdateHud()
	{
		_baseHealthLabel.Text =
			$"Bus hull: {Mathf.CeilToInt(_playerBaseHealth)}   |   Barricade: {Mathf.CeilToInt(_enemyBaseHealth)}";
		_resourceLabel.Text =
			$"Courage: {Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}   |   Stage {_stage}";
		var waveStatus = _stageData.HasScriptedWaves
			? $"   |   Waves: {_nextScriptedWaveIndex}/{_stageData.Waves.Length}   |   Queued spawns: {_pendingEnemySpawns.Count}"
			: "";
		_timerLabel.Text =
			$"Time: {_elapsed:0.0}s   |   Active enemies: {CountTeamUnits(Team.Enemy)}   |   Active allies: {CountTeamUnits(Team.Player)}{waveStatus}";
		_fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";

		foreach (var slot in _deploySlots)
		{
			var cooldown = GetDeployCooldownRemaining(slot.Definition.Id);
			var isReady = cooldown <= 0.05f;
			slot.Button.Disabled = _battleEnded || !isReady || _courage < slot.Definition.Cost;
			var level = GameState.Instance.GetUnitLevel(slot.Definition.Id);

			var label = $"Lv{level} {slot.Definition.DisplayName}  |  {slot.Definition.Cost} courage";
			if (!isReady)
			{
				label += $"  |  CD {cooldown:0.0}s";
			}

			slot.Button.Text = slot.Definition == _armedPlayerUnit
				? $"> {label} <"
				: label;
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

		_armedPlayerUnit = definition;
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
		if (_armedPlayerUnit == null)
		{
			SetStatus("Pick a squad card first, then click the battlefield.");
			return;
		}

		if (!CanDeployUnit(_armedPlayerUnit, out var reason))
		{
			SetStatus(reason);
			return;
		}

		var spawnY = Mathf.Clamp(clickY, BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding);
		TrySpawnPlayer(_armedPlayerUnit, new Vector2(PlayerSpawnX, spawnY));
	}

	private void TrySpawnPlayer(UnitDefinition definition, Vector2 spawnPosition)
	{
		if (_battleEnded)
		{
			return;
		}

		if (!CanDeployUnit(definition, out var reason))
		{
			SetStatus(reason);
			return;
		}

		var stats = GameState.Instance.BuildPlayerUnitStats(definition);
		_courage -= stats.Cost;
		_deployCooldowns[definition.Id] = Mathf.Max(0f, definition.DeployCooldown);
		SpawnUnit(Team.Player, stats, spawnPosition);
		SetStatus(
			$"Deployed Lv{GameState.Instance.GetUnitLevel(definition.Id)} {stats.Name} from the bus at lane height {Mathf.RoundToInt(spawnPosition.Y)}.");
		AutoArmNextReadyUnit(definition);
		UpdateHud();
	}

	private void TickDeployCooldowns(float delta)
	{
		foreach (var slot in _deploySlots)
		{
			var cooldown = GetDeployCooldownRemaining(slot.Definition.Id);
			if (cooldown <= 0f)
			{
				continue;
			}

			_deployCooldowns[slot.Definition.Id] = Mathf.Max(0f, cooldown - delta);
		}
	}

	private float GetDeployCooldownRemaining(string unitId)
	{
		return _deployCooldowns.TryGetValue(unitId, out var cooldown)
			? Mathf.Max(0f, cooldown)
			: 0f;
	}

	private bool CanDeployUnit(UnitDefinition definition, out string reason)
	{
		reason = "";
		if (_battleEnded)
		{
			reason = "Battle is already over.";
			return false;
		}

		var cooldown = GetDeployCooldownRemaining(definition.Id);
		if (cooldown > 0.05f)
		{
			reason = $"{definition.DisplayName} is still recovering ({cooldown:0.0}s).";
			return false;
		}

		if (_courage < definition.Cost)
		{
			reason = $"Not enough courage for {definition.DisplayName}.";
			return false;
		}

		return true;
	}

	private void AutoArmNextReadyUnit(UnitDefinition deployedUnit)
	{
		if (_armedPlayerUnit != deployedUnit)
		{
			return;
		}

		foreach (var unit in _playerRoster)
		{
			if (GetDeployCooldownRemaining(unit.Id) > 0.05f)
			{
				continue;
			}

			_armedPlayerUnit = unit;
			return;
		}
	}

	private void SpawnEnemyWave()
	{
		if (CountTeamUnits(Team.Enemy) >= GetMaxActiveEnemies())
		{
			return;
		}

		var spawnY = _rng.RandfRange(BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding);
		SpawnUnit(Team.Enemy, BuildEnemyStats(), new Vector2(EnemySpawnX, spawnY));

		if (_stageData.BonusWaveChance > 0f && _rng.Randf() < _stageData.BonusWaveChance)
		{
			if (CountTeamUnits(Team.Enemy) >= GetMaxActiveEnemies())
			{
				return;
			}

			var bonusY = _rng.RandfRange(BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding);
			SpawnUnit(Team.Enemy, BuildEnemyStats(), new Vector2(EnemySpawnX, bonusY));
		}
	}

	private void TriggerScriptedWaves()
	{
		while (_nextScriptedWaveIndex < _stageData.Waves.Length)
		{
			var wave = _stageData.Waves[_nextScriptedWaveIndex];
			if (_elapsed + 0.001f < wave.TriggerTime)
			{
				return;
			}

			QueueScriptedWave(wave);
			var label = string.IsNullOrWhiteSpace(wave.Label)
				? $"Wave {_nextScriptedWaveIndex + 1}"
				: wave.Label;
			SetStatus($"Enemy wave {_nextScriptedWaveIndex + 1} incoming: {label}.");
			_nextScriptedWaveIndex++;
		}
	}

	private void QueueScriptedWave(StageWaveDefinition wave)
	{
		var executeAt = Mathf.Max(_elapsed, wave.TriggerTime);
		var spawnInterval = Mathf.Max(0.1f, wave.SpawnInterval);

		foreach (var entry in wave.Entries)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
			{
				continue;
			}

			var enemyDefinition = GetEnemyById(entry.UnitId);
			var count = Math.Max(1, entry.Count);
			for (var i = 0; i < count; i++)
			{
				_pendingEnemySpawns.Enqueue(new PendingEnemySpawn(enemyDefinition, executeAt));
				executeAt += spawnInterval;
			}
		}
	}

	private void FlushPendingEnemySpawns()
	{
		while (_pendingEnemySpawns.Count > 0)
		{
			if (_pendingEnemySpawns.Peek().ExecuteAt > _elapsed)
			{
				return;
			}

			if (CountTeamUnits(Team.Enemy) >= GetMaxActiveEnemies())
			{
				return;
			}

			var pendingSpawn = _pendingEnemySpawns.Dequeue();
			var spawnY = _rng.RandfRange(BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding);
			SpawnUnit(Team.Enemy, BuildEnemyStats(pendingSpawn.Definition), new Vector2(EnemySpawnX, spawnY));
		}
	}

	private int GetMaxActiveEnemies()
	{
		return _combat.GetMaxActiveEnemies(_stage);
	}

	private UnitStats BuildEnemyStats()
	{
		return BuildEnemyStats(PickEnemyDefinition());
	}

	private UnitStats BuildEnemyStats(UnitDefinition source)
	{
		var cooldownReduction = (_stage - 1) * 0.05f;
		var baseDamageBonus = (_stage - 1) * 2;

		return new UnitStats(
			source,
			_stageData.EnemyHealthScale,
			_stageData.EnemyDamageScale,
			cooldownReduction,
			baseDamageBonus);
	}

	private UnitDefinition PickEnemyDefinition()
	{
		var walkerWeight = Mathf.Max(0f, _stageData.WalkerWeight);
		var runnerWeight = Mathf.Max(0f, _stageData.RunnerWeight);
		var bruteWeight = Mathf.Max(0f, _stageData.BruteWeight);
		var spitterWeight = Mathf.Max(0f, _stageData.SpitterWeight);
		var crusherWeight = Mathf.Max(0f, _stageData.CrusherWeight);
		var bossWeight = Mathf.Max(0f, _stageData.BossWeight);
		if (_elapsed < Mathf.Max(0f, _stageData.BossSpawnStartTime))
		{
			bossWeight = 0f;
		}
		var total = walkerWeight + runnerWeight + bruteWeight + spitterWeight + crusherWeight + bossWeight;

		if (total <= 0f)
		{
			return _enemyRoster[0];
		}

		var roll = _rng.RandfRange(0f, total);
		if (roll < walkerWeight)
		{
			return GetEnemyById(GameData.EnemyWalkerId);
		}

		roll -= walkerWeight;
		if (roll < runnerWeight)
		{
			return GetEnemyById(GameData.EnemyRunnerId);
		}

		roll -= runnerWeight;
		if (roll < bruteWeight)
		{
			return GetEnemyById(GameData.EnemyBruteId);
		}

		roll -= bruteWeight;
		if (roll < spitterWeight)
		{
			return GetEnemyById(GameData.EnemySpitterId);
		}

		roll -= spitterWeight;
		if (roll < crusherWeight)
		{
			return GetEnemyById(GameData.EnemyCrusherId);
		}

		if (bossWeight <= 0f)
		{
			return GetEnemyById(GameData.EnemyCrusherId);
		}

		return GetEnemyById(GameData.EnemyBossId);
	}

	private UnitDefinition GetEnemyById(string id)
	{
		foreach (var unit in _enemyRoster)
		{
			if (unit.Id == id)
			{
				return unit;
			}
		}

		return _enemyRoster[0];
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
		projectile.Setup(target, attacker.AttackDamage, speed, color);
		AddChild(projectile);
	}

	private void TryAttackBase(Unit attacker)
	{
		var targetBase = attacker.Team == Team.Player ? EnemyBaseCorePosition : PlayerBaseCorePosition;
		if (!attacker.TryBeginAttackPosition(targetBase, BaseCoreRadius))
		{
			return;
		}

		if (attacker.Team == Team.Player)
		{
			_enemyBaseHealth -= attacker.BaseDamage;
		}
		else
		{
			_playerBaseHealth -= attacker.BaseDamage;
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
					unit.TryAttack(target);
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
			_units.RemoveAt(i);
			deadUnit.QueueFree();
		}
	}

	private void CheckBattleEnd()
	{
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

		if (playerWon)
		{
			var starsEarned = CalculateVictoryStars();
			var bestStars = Mathf.Max(GameState.Instance.GetStageStars(_stage), starsEarned);
			GameState.Instance.ApplyVictory(_stage, _stageData.RewardScrap, _combat.VictoryFuelReward, starsEarned);
			_endLabel.Text =
				$"Victory on stage {_stage}.\n" +
				$"Reward: +{_stageData.RewardScrap} scrap, +{_combat.VictoryFuelReward} fuel.\n" +
				$"Stars earned: {starsEarned}/3   |   Best: {bestStars}/3.";
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

	private int CalculateVictoryStars()
	{
		var stars = 1;
		var hullThreshold = Mathf.Clamp(_stageData.TwoStarBusHullRatio, 0f, 1f);
		var timeLimit = Mathf.Max(0f, _stageData.ThreeStarTimeLimitSeconds);

		if (_playerBaseHealth / Mathf.Max(1f, _stageData.PlayerBaseHealth) >= hullThreshold)
		{
			stars++;
		}

		if (timeLimit <= 0f || _elapsed <= timeLimit)
		{
			stars++;
		}

		return Mathf.Clamp(stars, 1, 3);
	}

	private void RetreatToMap()
	{
		if (!_battleEnded)
		{
			GameState.Instance.ApplyRetreat(_stage);
		}

		SceneRouter.Instance.GoToMap();
	}
}
