using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Stages and captures the app store screenshots on a mid-campaign save, through the live router and battle.
///   --save-suffix=store-shots-&lt;id&gt; --window=WxH [--out=/abs/dir] [--only=battle,boss,map,warband,zone,victory,caravan,store]
///   [--device-safe-area=x,y,w,h]: the safe area a phone reports, in screen pixels (iPhone 6.9" landscape:
///   186,0,2496,1257). Desktop reports none, so it is turned into canvas insets with SafeAreaService's own math
///   and applied before each battle, as the device would (only the battle HUD reads it).
/// Each capture is the whole window as the player sees it, bars included: the root viewport only renders the
/// kept-aspect canvas, so it is placed on black at the window's own offset (Godot clears the rest to black).
/// </summary>
public partial class StoreScreenshots : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private Vector2I _size;
    private string _output;
    private string[] _only;
    private readonly RandomNumberGenerator _dice = new() { Seed = 2026 };

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private static string Arg(string prefix) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith(prefix))?[prefix.Length..];
    private bool Want(string name) => _only == null || _only.Contains(name);

    private async void Run()
    {
        var code = 0;
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--save-suffix=store-shots-")))
                throw new InvalidOperationException("Store screenshots require --save-suffix=store-shots-<id>.");
            var window = Arg("--window=")?.Split('x');
            _size = window == null ? new Vector2I(1280, 720) : new Vector2I(int.Parse(window[0]), int.Parse(window[1]));
            _output = Arg("--out=") ?? ProjectSettings.GlobalizePath("res://artifacts/store-shots");
            _only = Arg("--only=")?.Split(',');
            System.IO.Directory.CreateDirectory(_output);
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = _size;
            for (var i = 0; i < 20; i++) await Frame();
            GD.Print($"STORE_WINDOW size={GetWindow().Size} visible={GetViewport().GetVisibleRect()} screen={GetViewport().GetScreenTransform()} display={DisplayServer.ScreenGetSize()} scale={DisplayServer.ScreenGetScale()}");
            if (GetWindow().Size != _size) throw new InvalidOperationException($"Window is {GetWindow().Size}, wanted {_size}.");
            LiveUiReview.PreserveDriver(this);
            Seed();

            if (Want("battle")) await BusyBattle();
            if (Want("boss")) await BossFight();
            if (Want("map")) await AdventureMap();
            if (Want("warband")) await Warband();
            if (Want("zone")) await SecondZone();
            if (Want("victory")) await Victory();
            if (Want("caravan")) await Caravan();
            if (Want("store")) await Storehouse();
            if (_only != null && _only.Contains("spells")) await Spells(); // review only, not a store shot
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); code = 1; }
        Engine.TimeScale = 1;
        await LiveUiReview.StopAudio(this);
        GD.Print($"STORE_SCREENSHOTS_RESULT: {code}");
        GetTree().Quit(code);
    }

    // ---------- save ----------

    private static void ApplySave(GameSaveData data) =>
        typeof(GameState).GetMethod("ApplySavedData", Hidden)!.Invoke(GameState.Instance, new object[] { data });

    private static int Reached => int.Parse(Arg("--reached=") ?? "88");

    // A late-campaign player: eight zones behind them, the Gloamwood Verge under way, a levelled warband.
    private void Seed()
    {
        var state = GameState.Instance;
        state.ResetProgress();
        var f = state.BuildSaveData();
        f.Gold = 12480; f.Food = 22; f.RelicShards = 34; f.Sigils = 12; f.Essence = 40;
        f.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        f.HighestUnlockedStage = Reached; f.SelectedStage = Reached;
        f.OwnedPlayerUnitIds = GameData.GetPlayerUnits().Where(u => u.UnlockStage <= Reached).Select(u => u.Id).ToArray();
        f.OwnedPlayerSpellIds = GameData.GetPlayerSpells().Where(s => s.UnlockStage <= Reached).Select(s => s.Id).ToArray();
        f.UnitLevels = f.OwnedPlayerUnitIds.Select((id, i) => (id, level: 3 + (i * 7 % 9))).ToDictionary(x => x.id, x => x.level);
        f.SpellLevels = f.OwnedPlayerSpellIds.Select((id, i) => (id, level: 1 + (i * 5 % 3))).ToDictionary(x => x.id, x => x.level);
        foreach (var id in new[] { "spell_meteor", "spell_lightning_strike", "spell_fireball", "spell_barrier_ward", "spell_frost_burst" }) f.SpellLevels[id] = 3;
        f.ActiveDeckUnitIds = new[] { "player_paladin", "player_defender", "player_ranger", "player_breacher" };
        f.ActiveDeckSpellIds = new[] { "spell_meteor", "spell_lightning_strike" };
        f.StageStars = Enumerable.Range(1, state.MaxStage).Select(s => s < Reached ? (s % 7 == 0 ? 2 : 3) : 0).ToArray();
        var level = 46;
        f.PlayerXP = PlayerLevelCatalog.TotalXPForLevel(level) + 120;
        f.ClaimedLevelRewards = Enumerable.Range(1, level).ToArray();
        f.HasChosenCallsign = true; f.PlayerCallsign = "Lantern";
        f.OwnedEquipmentIds = new[] { "relic_iron_pendant", "relic_swift_boots", "relic_wolftooth_charm", "relic_spectral_lantern", "relic_sages_ring" };
        f.DiscoveredCodexIds = CodexCatalog.GetAll().Select(e => e.Id).ToArray();
        // Every earlier zone charted; the current zone explored up to the reached stage.
        var open = new List<string>();
        foreach (var map in GameData.Stages.Where(s => s.StageNumber < Reached).Select(s => s.MapId).Distinct())
        {
            var last = GameData.Stages.Where(s => s.MapId == map).Max(s => s.StageNumber);
            var first = GameData.Stages.Where(s => s.MapId == map).Min(s => s.StageNumber);
            var limit = last < Reached ? last : Reached - 1;
            var index = limit - first; // stage order within the zone, 0-based
            var charted = AdventureTileCatalog.Roads.Where(r => r.From <= index && r.To <= index)
                .SelectMany(r => SafePath(map, r.From, r.To))
                .SelectMany(t => AdventureTileCatalog.Neighbors(t).Append(t))
                .Where(t => !t.IsResource && (t.Site == null || t.Site.Stage <= limit + 1));
            open.AddRange(charted.Select(t => t.Id));
        }
        f.AdventureOpenTiles = open.Distinct().ToArray();
        f.ShowFpsCounter = false; f.ShowDevUi = false; f.DeveloperModeEnabled = false; f.ShowHints = false;
        f.HasShownConsentPrompt = true; f.AnalyticsConsent = false;
        ApplySave(f);
        state.SetShowHints(false); state.SetAnalyticsConsent(false); state.SetShowFpsCounter(false);
        GD.Print($"STORE_SEED stage={state.HighestUnlockedStage} units={f.OwnedPlayerUnitIds.Length} spells={f.OwnedPlayerSpellIds.Length} tiles={f.AdventureOpenTiles.Length} level={state.PlayerLevel}");
    }

    private static IEnumerable<AdventureTile> SafePath(string map, int from, int to)
    {
        try { return GameState.AdventureTilePath(AdventureTileCatalog.Stage(map, from), AdventureTileCatalog.Stage(map, to)); }
        catch { return Array.Empty<AdventureTile>(); }
    }

    private void SetDeck(string[] units, string[] spells)
    {
        var f = GameState.Instance.BuildSaveData();
        f.ActiveDeckUnitIds = units; f.ActiveDeckSpellIds = spells;
        ApplySave(f);
    }

    // ---------- capture ----------

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    // Battle time: the timer slows with Engine.TimeScale, so slow-motion waits land on battle moments.
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);

    private async Task Capture(string name)
    {
        if (GetTree().CurrentScene is BattleController battle && Read<bool>(battle, "_battlePaused") && !Read<bool>(battle, "_battleEnded"))
        { Call(battle, "TogglePause"); GetViewport().GuiReleaseFocus(); await Frame(); }
        GetViewport().GuiReleaseFocus();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var canvas = GetViewport().GetTexture().GetImage();
        canvas.Convert(Image.Format.Rgb8);
        var image = canvas;
        Image framed = null;
        if (canvas.GetSize() != _size)
        {
            // The kept-aspect canvas sits inside the window at the screen transform's offset; the rest is bars.
            var origin = GetViewport().GetScreenTransform().Origin;
            framed = Image.CreateEmpty(_size.X, _size.Y, false, Image.Format.Rgb8);
            framed.Fill(Colors.Black);
            framed.BlitRect(canvas, new Rect2I(Vector2I.Zero, canvas.GetSize()), new Vector2I(Mathf.RoundToInt(origin.X), Mathf.RoundToInt(origin.Y)));
            image = framed;
        }
        var path = $"{_output}/{name}.png";
        if (image.SavePng(path) != Error.Ok) throw new System.IO.IOException("Unable to save " + path);
        GD.Print($"STORE_CAPTURE {name} canvas={canvas.GetSize()} saved={image.GetSize()} origin={GetViewport().GetScreenTransform().Origin}");
        framed?.Dispose();
    }

    // ---------- battle helpers ----------

    private static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static void Write(object owner, string name, object value) => owner.GetType().GetField(name, Hidden)!.SetValue(owner, value);
    private static T Prop<T>(object owner, string name) => (T)owner.GetType().GetProperty(name, Hidden)!.GetValue(owner)!;
    private static object Call(object owner, string name, params object[] args) =>
        owner.GetType().GetMethod(name, Hidden, null, args.Select(a => a.GetType()).ToArray(), null)!.Invoke(owner, args);

    private async Task<BattleController> OpenBattle(int stage)
    {
        GameState.Instance.SetSelectedStage(stage);
        GameState.Instance.PrepareCampaignBattle();
        ApplyDeviceSafeArea();
        await LiveUiReview.Open(this, "Battle");
        var battle = (BattleController)GetTree().CurrentScene;
        Resume(battle);
        // Hold the waves: the scene is staged by hand.
        var director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        typeof(BattleSpawnDirector).GetProperty("NextScriptedWaveIndex")!.SetValue(director, director.TotalScriptedWaves);
        typeof(BattleSpawnDirector).GetField("_enemySpawnTimer", Hidden)!.SetValue(director, 1e9f);
        var pending = typeof(BattleSpawnDirector).GetField("_pendingEnemySpawns", Hidden)!.GetValue(director)!;
        pending.GetType().GetMethod("Clear")!.Invoke(pending, null);
        await Wait(1.5); // the stage banner and HUD entrance
        Resume(battle);
        return battle;
    }

    private void ApplyDeviceSafeArea()
    {
        var area = Arg("--device-safe-area=")?.Split(',').Select(float.Parse).ToArray();
        if (area == null || SafeAreaService.Instance == null) return;
        var insets = SafeAreaService.LogicalInsets(new Rect2(area[0], area[1], area[2], area[3]), GetViewport().GetScreenTransform(), GetViewport().GetVisibleRect().Size);
        var service = SafeAreaService.Instance;
        typeof(SafeAreaService).GetProperty("SafeArea")!.SetValue(service, new Rect2I((int)area[0], (int)area[1], (int)area[2], (int)area[3]));
        typeof(SafeAreaService).GetProperty("MarginLeft")!.SetValue(service, Mathf.CeilToInt(insets.X));
        typeof(SafeAreaService).GetProperty("MarginTop")!.SetValue(service, Mathf.CeilToInt(insets.Y));
        typeof(SafeAreaService).GetProperty("MarginRight")!.SetValue(service, Mathf.CeilToInt(insets.Z));
        typeof(SafeAreaService).GetProperty("MarginBottom")!.SetValue(service, Mathf.CeilToInt(insets.W));
        GD.Print($"STORE_SAFE_AREA canvas insets L{service.MarginLeft} T{service.MarginTop} R{service.MarginRight} B{service.MarginBottom}");
    }

    private void Resume(BattleController battle)
    {
        if (Read<bool>(battle, "_battlePaused") && !Read<bool>(battle, "_battleEnded")) { Call(battle, "TogglePause"); GetViewport().GuiReleaseFocus(); }
    }

    private Unit Spawn(BattleController battle, Team team, string id, Vector2 at, float health = 1f, float damage = 1f)
    {
        UnitStats stats;
        if (team == Team.Enemy)
        {
            var director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
            if (!director.TryBuildEnemyStats(id, out stats)) stats = new UnitStats(GameData.GetUnit(id));
            stats = new UnitStats(GameData.GetUnit(id), healthScale: stats.MaxHealth / Mathf.Max(1, GameData.GetUnit(id).MaxHealth) * health, damageScale: damage);
        }
        else stats = new UnitStats(GameData.GetUnit(id), healthScale: health, damageScale: damage, level: GameState.Instance.GetUnitLevel(id));
        var unit = (Unit)Call(battle, "SpawnUnit", team, stats, at);
        unit.CanDropCourage = false;
        return unit;
    }

    private (float Top, float Bottom, float Mid, float Right) Band(BattleController battle)
    {
        var top = Prop<float>(battle, "BattlefieldTop"); var bottom = Prop<float>(battle, "BattlefieldBottom");
        return (top, bottom, (top + bottom) / 2, Prop<float>(battle, "BattlefieldRight"));
    }

    private float Lane(BattleController battle, int i, int of)
    {
        var (top, bottom, _, _) = Band(battle);
        var pad = (bottom - top) * .12f;
        return Mathf.Lerp(top + pad, bottom - pad, of <= 1 ? .5f : (float)i / (of - 1)) + _dice.RandfRange(-4, 4);
    }

    private static string[] StageEnemies(int stage) => GameData.GetStage(stage).Waves.SelectMany(w => w.Entries).Select(e => e.UnitId)
        .Where(id => !id.Contains("boss")).Distinct().ToArray();

    private void Hud(BattleController battle, float courage, float mana)
    {
        Write(battle, "_courage", courage); Write(battle, "_mana", mana);
        Call(battle, "UpdateHud");
    }

    private void Camera(BattleController battle, float x)
    {
        Resume(battle);
        Call(battle, "SetBattleCameraX", x);
    }

    // Columns behind each front line: the leaders meet at the front, the rest still marching up behind them.
    private static readonly float[] Columns = { 10, 46, 86, 130, 178, 230 };

    // Two hosts meet at `front`: the player's deck from the left (ranged units at the back), the stage's enemies from the right.
    private async Task Clash(BattleController battle, float front, float camera, int allies, int foes, float health, double settle, string[] enemyIds = null)
    {
        var deck = GameState.Instance.ActiveDeckUnitIds.ToArray();
        var melee = deck.Where(id => !GameData.GetUnit(id).UsesProjectile).ToArray();
        var ranged = deck.Where(id => GameData.GetUnit(id).UsesProjectile).ToArray();
        if (melee.Length == 0) melee = deck;
        enemyIds ??= StageEnemies(GameState.Instance.SelectedStage);
        for (var i = 0; i < allies; i++)
        {
            var column = i / 3;
            var back = column >= (allies - 1) / 3 && ranged.Length > 0;
            var id = back ? ranged[i % ranged.Length] : melee[i % melee.Length];
            var u = Spawn(battle, Team.Player, id, new Vector2(front - Columns[Math.Min(column, Columns.Length - 1)] - _dice.RandfRange(0, 10), Lane(battle, i % 3, 3)), health);
            if (_dice.Randf() < .6f) u.TakeDamage(u.MaxHealth * _dice.RandfRange(.1f, .45f), "staging", CombatDamageType.True);
        }
        for (var i = 0; i < foes; i++)
        {
            var id = enemyIds[(i + i / 3) % enemyIds.Length];
            var u = Spawn(battle, Team.Enemy, id, new Vector2(front + Columns[Math.Min(i / 3, Columns.Length - 1)] + _dice.RandfRange(0, 10), Lane(battle, (i + 1) % 3, 3)), health);
            if (_dice.Randf() < .7f) u.TakeDamage(u.MaxHealth * _dice.RandfRange(.15f, .55f), "staging", CombatDamageType.True);
        }
        Camera(battle, camera);
        var clock = 0.0;
        while (clock < settle)
        {
            await Wait(.25); clock += .25;
            Resume(battle);
            Camera(battle, camera);
        }
    }

    private async Task CloseBattle()
    {
        Engine.TimeScale = 1;
        await LiveUiReview.Open(this, "MainMenu");
        await Wait(.3);
    }

    private IEnumerable<Unit> Live(BattleController battle, Team team) => Read<List<Unit>>(battle, "_units").Where(u => !u.IsDead && u.Team == team);

    // ---------- shots ----------

    // 1. Thornwall Pass in snow: two hosts locked together as a meteor lands on the enemy's.
    private async Task BusyBattle()
    {
        SetDeck(new[] { "player_paladin", "player_defender", "player_breacher", "player_ranger" }, new[] { "spell_meteor", "spell_lightning_strike" });
        var battle = await OpenBattle(int.Parse(Arg("--battle-stage=") ?? "47"));
        var (_, _, mid, _) = Band(battle);
        var front = float.Parse(Arg("--battle-front=") ?? "350");
        var camera = float.Parse(Arg("--battle-camera=") ?? "360");
        Hud(battle, 71, 58);
        await Clash(battle, front, camera, 13, 13, 4f, double.Parse(Arg("--battle-settle=") ?? "0.4"));
        Hud(battle, 71, 58);
        await Wait(.6); // the meters ease to their values
        var foes = Live(battle, Team.Enemy).Where(u => u.Position.X > front + 30).ToArray();
        var target = foes.Length == 0 ? new Vector2(front + 80, mid)
            : new Vector2(foes.Average(u => u.Position.X), mid);
        var spell = GameState.Instance.BuildSpellStats(GameData.GetSpell(Arg("--battle-spell=") ?? "spell_meteor"));
        Engine.TimeScale = .25f;
        Call(battle, "ApplySpellEffect", spell, target);
        var times = (Arg("--battle-times=") ?? "1.55").Split(',').Select(float.Parse).ToArray();
        var clock = 0f;
        foreach (var at in times)
        {
            await Wait(at - clock); clock = at;
            Camera(battle, camera);
            await Capture(times.Length == 1 ? "01-battle" : $"01-battle-{at:0.00}");
        }
        await CloseBattle();
    }

    // 2. The Bone Pontiff at the Sepulcher Crown, ringed by the warband, as lightning strikes him.
    private async Task BossFight()
    {
        SetDeck(new[] { "player_gravebound_knight", "player_greatswordsman", "player_paladin", "player_ranger" }, new[] { "spell_lightning_strike", "spell_barrier_ward" });
        var stage = int.Parse(Arg("--boss-stage=") ?? "55");
        var battle = await OpenBattle(stage);
        var (top, bottom, mid, right) = Band(battle);
        var bossId = Arg("--boss-id=") ?? GameData.GetStage(stage).Waves.SelectMany(w => w.Entries).Select(e => e.UnitId).First(id => id.Contains("boss"));
        var front = float.Parse(Arg("--boss-front=") ?? "380");
        var camera = float.Parse(Arg("--boss-camera=") ?? "380");
        Hud(battle, 64, 49);
        var boss = Spawn(battle, Team.Enemy, bossId, new Vector2(front + float.Parse(Arg("--boss-gap=") ?? "56"), mid + 2), 8f);
        boss.TakeDamage(boss.MaxHealth * .38f, "staging", CombatDamageType.True);
        // The boss's guard stands behind him; the warband's front line closes on him.
        var guard = StageEnemies(stage);
        for (var i = 0; i < 3; i++)
            Spawn(battle, Team.Enemy, guard[i % guard.Length], new Vector2(front + 120 + i * 30, Lane(battle, i, 3)), 4f)
                .TakeDamage(10, "staging", CombatDamageType.True);
        await Clash(battle, front, camera, int.Parse(Arg("--boss-allies=") ?? "8"), 0, 4f, double.Parse(Arg("--boss-settle=") ?? "1.0"));
        Hud(battle, 64, 49);
        await Wait(.6);
        var spellId = Arg("--boss-spell=") ?? "none";
        var times = (Arg("--boss-times=") ?? "0.04").Split(',').Select(float.Parse).ToArray();
        Engine.TimeScale = .25f;
        if (spellId != "none" && !boss.IsDead)
            Call(battle, "ApplySpellEffect", GameState.Instance.BuildSpellStats(GameData.GetSpell(spellId)), boss.Position);
        var clock = 0f;
        foreach (var at in times)
        {
            await Wait(at - clock); clock = at;
            Camera(battle, camera);
            await Capture(times.Length == 1 ? "02-boss" : $"02-boss-{at:0.00}");
        }
        await CloseBattle();
    }

    // 3. The campaign map, mid-zone, with the explored land and the cloud beyond.
    private async Task AdventureMap()
    {
        GameState.Instance.SetSelectedStage(int.Parse(Arg("--map-stage=") ?? Reached.ToString()));
        await LiveUiReview.Open(this, "MainMenu");
        await Wait(1.2);
        var canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().First();
        var zoom = float.Parse(Arg("--map-zoom=") ?? "2.5");
        if (zoom > 0)
        {
            canvas.ChangeZoom(.05f); canvas.ChangeZoom(zoom);
            var site = Arg("--map-site=") ?? "6";
            if (site != null) canvas.FocusSite(AdventureTileCatalog.Stage(canvas.ActiveMapId, int.Parse(site)).Id); else canvas.FocusOverview();
            await Wait(.6);
        }
        await Capture(Arg("--map-name=") ?? "03-map");
    }

    // 4. The warband: a full loadout and the collection, one veteran unit selected.
    private async Task Warband()
    {
        SetDeck(new[] { "player_paladin", "player_defender", "player_ranger", "player_breacher", "player_coordinator", "player_raider" }, Array.Empty<string>());
        await LiveUiReview.Open(this, "ShopMenu");
        await Wait(.6);
        var pick = Arg("--warband-unit=") ?? "Paladin";
        var button = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().FirstOrDefault(b => b.IsVisibleInTree() && (b.TooltipText == pick || b.AccessibilityName == pick));
        if (button != null) { button.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.6); }
        else GD.Print("STORE_NOTE warband unit button not found: " + pick);
        await Capture("04-warband");
    }

    // 5. A second zone: the Sunfall Steppe at sundown, frost bursting over the enemy line.
    private async Task SecondZone()
    {
        SetDeck(new[] { "player_man_at_arms", "player_axeman", "player_raider", "player_shooter" }, new[] { "spell_fireball", "spell_frost_burst" });
        var battle = await OpenBattle(int.Parse(Arg("--zone-stage=") ?? "75"));
        var front = float.Parse(Arg("--zone-front=") ?? "330");
        var camera = float.Parse(Arg("--zone-camera=") ?? "340");
        Hud(battle, 46, 35);
        await Clash(battle, front, camera, 12, 12, 4f, double.Parse(Arg("--zone-settle=") ?? "1.2"));
        Hud(battle, 46, 35);
        await Wait(.6);
        var spellId = Arg("--zone-spell=") ?? "spell_frost_burst";
        var times = (Arg("--zone-times=") ?? "0.35").Split(',').Select(float.Parse).ToArray();
        Engine.TimeScale = .25f;
        if (spellId != "none")
        {
            var foes = Live(battle, Team.Enemy).Where(u => u.Position.X > front + 20).ToArray();
            var (_, _, mid, _) = Band(battle);
            var target = foes.Length == 0 ? new Vector2(front + 60, mid) : new Vector2(foes.Average(u => u.Position.X), mid);
            Call(battle, "ApplySpellEffect", GameState.Instance.BuildSpellStats(GameData.GetSpell(spellId)), target);
        }
        var clock = 0f;
        foreach (var at in times)
        {
            await Wait(at - clock); clock = at;
            Camera(battle, camera);
            await Capture(times.Length == 1 ? "05-zone" : $"05-zone-{GameState.Instance.SelectedStage}-{at:0.00}");
        }
        await CloseBattle();
    }

    // 6. The result board after a win at Saltwake Docks.
    private async Task Victory()
    {
        SetDeck(new[] { "player_paladin", "player_defender", "player_ranger", "player_breacher" }, new[] { "spell_meteor", "spell_lightning_strike" });
        var stage = int.Parse(Arg("--victory-stage=") ?? "14");
        var battle = await OpenBattle(stage);
        var (_, _, mid, right) = Band(battle);
        await Clash(battle, right - 120, 420, 7, 2, 3f, 1.2);
        Write(battle, "_enemyDefeats", 38);
        Write(battle, "_elapsed", 142f);
        Call(battle, "EndBattle", true);
        await Wait(double.Parse(Arg("--victory-wait=") ?? "2.2"));
        await Capture("06-victory");
        await CloseBattle();
    }

    // 7. The caravan's Arcane Barrage raining on a packed host on King's Road.
    private async Task Caravan()
    {
        SetDeck(new[] { "player_paladin", "player_defender", "player_ranger", "player_breacher" }, new[] { "spell_meteor", "spell_lightning_strike" });
        var battle = await OpenBattle(int.Parse(Arg("--caravan-stage=") ?? "8"));
        var (top, bottom, mid, right) = Band(battle);
        var enemies = StageEnemies(GameState.Instance.SelectedStage);
        for (var i = 0; i < 12; i++)
        {
            var u = Spawn(battle, Team.Enemy, enemies[i % enemies.Length], new Vector2(260 + i * 48 + _dice.RandfRange(-8, 8), Lane(battle, i % 3, 3)), 6f);
            u.TakeDamage(u.MaxHealth * _dice.RandfRange(.1f, .5f), "staging", CombatDamageType.True);
        }
        for (var i = 0; i < 4; i++)
            Spawn(battle, Team.Player, GameState.Instance.ActiveDeckUnitIds.ElementAt(i % 4), new Vector2(170 + i * 22, Lane(battle, i % 3, 3)), 4f);
        Camera(battle, 330);
        await Wait(1.0);
        Hud(battle, 52, Read<float>(battle, "_maxMana"));
        await Wait(1.0);
        Resume(battle);
        Engine.TimeScale = .25f;
        Call(battle, "TryCastCaravanSpell");
        var times = (Arg("--caravan-times=") ?? "2.0").Split(',').Select(float.Parse).ToArray();
        var clock = 0f;
        foreach (var at in times)
        {
            await Wait(at - clock); clock = at;
            Camera(battle, 330);
            await Capture(times.Length == 1 ? "07-caravan" : $"07-caravan-{at:0.00}");
        }
        await CloseBattle();
    }

    // 8. The Royal Storehouse.
    private async Task Storehouse()
    {
        await LiveUiReview.Open(this, "MainMenu");
        await Wait(.6);
        ((MapMenu)GetTree().CurrentScene).OpenHomeDestination(SceneRouter.CashShopScene);
        await Wait(1.0);
        await Capture("08-storehouse");
    }

    private async Task Spells()
    {
        await LiveUiReview.Open(this, "ShopMenu");
        await Wait(.6);
        var tab = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().FirstOrDefault(b => b.IsVisibleInTree() && b.Text == "Spells");
        tab?.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.8);
        var pick = Arg("--spell-pick=") ?? "Meteor";
        var button = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().FirstOrDefault(b => b.IsVisibleInTree() && (b.TooltipText == pick || b.AccessibilityName == pick));
        button?.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.6);
        await Capture("08-spells");
    }

    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var d in Walk(child)) yield return d;
    }
}
