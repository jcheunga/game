using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private async Task CaptureCouragePacks(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--screenshots") || DisplayServer.GetName() == "headless") return;
        var folder = ProjectSettings.GlobalizePath("res://artifacts/courage-drops");
        System.IO.Directory.CreateDirectory(folder);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng($"{folder}/{name}.png") == Error.Ok, "Capture courage packs: " + name);
    }

    private async Task CheckCourageDrops()
    {
        var tuning = GameData.Combat;
        var originalChance = tuning.CourageDropChance;
        var originalGuarantee = tuning.CourageDropGuaranteeAfterMisses;
        var originalWindow = GetWindow().Size;
        BattleController battle = null;
        try
        {
            var fixture = GameState.Instance.BuildSaveData();
            fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.Take(3).ToArray();
            fixture.ActiveDeckUnitIds = fixture.OwnedPlayerUnitIds;
            fixture.OwnedPlayerSpellIds = new[] { "spell_fireball" };
            fixture.ActiveDeckSpellIds = fixture.OwnedPlayerSpellIds;
            GameState.Instance.RestoreCloudSave(fixture);
            GetWindow().Size = new Vector2I(1280, 720);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            battle = await OpenBattle(1);
            var packs = Read<List<CouragePickup>>(battle, "_couragePickups");
            Unit Spawn(bool natural, string id = "enemy_walker")
            {
                Invoke(battle, natural ? "SpawnEnemyUnit" : "SpawnEnemyReinforcement", new UnitStats(GameData.GetUnit(id)), new Vector2(350, 340));
                var enemy = Read<List<Unit>>(battle, "_units").Last();
                enemy.Position = new Vector2(350, 340);
                enemy.SetProcess(false);
                return enemy;
            }
            void Defeat(Unit enemy)
            {
                enemy.TakeDamage(enemy.MaxHealth * 1000, "Test strike");
                Invoke(battle, "CleanupDeadUnits");
            }
            tuning.CourageDropChance = 1;
            Write(battle, "_courage", 0f);
            Write(battle, "_mana", 0f);
            var natural = Spawn(true);
            Check(natural.CanDropCourage, "Authored enemies are eligible for courage packs");
            Defeat(natural);
            Check(packs.Count == 1 && Read<float>(battle, "_courage") == 0, "Defeat drops a physical pack without automatically granting courage");
            Check(Read<float>(battle, "_mana") == tuning.ManaPerEnemyKill, "Courage drops preserve ordinary kill-earned mana");
            var expired = packs[0];
            Write(battle, "_battlePaused", true);
            Invoke(battle, "TickCouragePickups", 10f);
            Check(expired.RemainingSeconds == tuning.CourageDropLifetime, "Pause freezes pickup lifetime");
            Check(!(bool)Invoke(battle, "CollectCouragePickup", expired), "Pause prevents collection");
            Write(battle, "_battlePaused", false);
            Write(battle, "_endlessCheckpointActive", true);
            Invoke(battle, "TickCouragePickups", 10f);
            Check(expired.RemainingSeconds == tuning.CourageDropLifetime, "An endless checkpoint freezes pickup lifetime");
            Write(battle, "_endlessCheckpointActive", false);
            foreach (var barrier in new[] { "_lanStartBarrierActive", "_onlineRoomStartBarrierActive" })
            {
                Write(battle, barrier, true);
                Invoke(battle, "TickCouragePickups", 10f);
                Check(expired.RemainingSeconds == tuning.CourageDropLifetime && !(bool)Invoke(battle, "CollectCouragePickup", expired),
                    barrier + ": multiplayer launch barrier freezes expiry and collection");
                Write(battle, barrier, false);
            }
            Invoke(battle, "TickCouragePickups", tuning.CourageDropLifetime - tuning.CourageDropFadeDuration + .5f);
            Check(expired.Available && expired.FadeAlpha > 0 && expired.FadeAlpha < 1, "Pack fades only in the final expiry window");
            Invoke(battle, "TickCouragePickups", tuning.CourageDropFadeDuration);
            Check(packs.Count == 0 && Read<float>(battle, "_courage") == 0 && !expired.Available, "Uncollected pack expires without any reward");

            var summoned = Spawn(false);
            Check(!summoned.CanDropCourage, "Summoned and reanimated enemies are ineligible");
            Defeat(summoned);
            Check(packs.Count == 0, "Reinforcements cannot farm courage packs");
            var nest = Spawn(true, "enemy_splitter");
            Defeat(nest);
            Check(packs.Count == 1 && Read<List<Unit>>(battle, "_units").All(x => !x.CanDropCourage), "Death split grants at most the original body's roll");
            foreach (var enemy in Read<List<Unit>>(battle, "_units").ToArray()) Defeat(enemy);
            Check(packs.Count == 1, "Split bodies grant no further pack rolls");
            Invoke(battle, "TickCouragePickups", 10f);
            var tower = Spawn(true, "enemy_siegetower");
            Invoke(battle, "TrySiegeTowerDeploy", tower);
            Check(!tower.CanDropCourage, "A self-destroying siege carrier forfeits its drop eligibility");
            Invoke(battle, "CleanupDeadUnits");
            foreach (var enemy in Read<List<Unit>>(battle, "_units").ToArray()) Defeat(enemy);
            Check(packs.Count == 0, "Carrier self-destruction and its escorts create no packs");
            // Pool reuse must reset provenance instead of leaking an authored enemy's eligibility.
            var pooled = UnitPool.Acquire();
            pooled.Setup(Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker")), Vector2.Zero);
            pooled.CanDropCourage = true;
            pooled.Setup(Team.Player, new UnitStats(GameData.GetUnit("player_brawler")), Vector2.Zero);
            Check(!pooled.CanDropCourage, "Pool setup resets courage-drop eligibility");
            UnitPool.Release(pooled);

            tuning.CourageDropChance = float.Epsilon;
            tuning.CourageDropGuaranteeAfterMisses = 15;
            Invoke(battle, "InitializeCourageDrops", 77UL);
            for (var kill = 0; kill < 15; kill++) Defeat(Spawn(true));
            Check(packs.Count == 0 && Read<int>(battle, "_courageDropMisses") == 15, "Fifteen eligible misses build the guarantee counter");
            Defeat(Spawn(true));
            Check(packs.Count == 1 && Read<int>(battle, "_courageDropMisses") == 0, "The next eligible kill guarantees a pack and resets on spawn");
            Invoke(battle, "TickCouragePickups", 10f);
            Check(Read<int>(battle, "_courageDropMisses") == 0, "Missing a pack does not reset or duplicate the guaranteed reward");

            tuning.CourageDropChance = .1f;
            List<bool> Sequence(bool cosmetic)
            {
                Invoke(battle, "InitializeCourageDrops", 991UL);
                var result = new List<bool>();
                for (var i = 0; i < 36; i++)
                {
                    if (cosmetic) for (var j = 0; j < 11; j++) Read<RandomNumberGenerator>(battle, "_rng").Randf();
                    var count = packs.Count;
                    Defeat(Spawn(true));
                    result.Add(packs.Count > count);
                }
                Invoke(battle, "TickCouragePickups", 10f);
                return result;
            }
            var sequence = Sequence(false);
            Check(sequence.SequenceEqual(Sequence(true)) && sequence.Any(x => x) && sequence.Any(x => !x),
                "Seeded pack rolls are reproducible and independent of combat/cosmetic RNG consumption");

            // Only enemies leave packs: courage from crates, quartermasters, relics and objectives goes straight to the meter.
            var flights = Read<List<CouragePickup>>(battle, "_courageFlights");
            Write(battle, "_courage", 0f);
            var granted = (float)Invoke(battle, "GrantCourage", new Vector2(400, 340), 8f);
            Check(granted == 8f && Read<float>(battle, "_courage") == 8f && packs.Count == 0 && flights.Count == 1 && !flights[0].Available,
                "A direct courage grant adds to the meter at once and leaves nothing to tap");
            Write(battle, "_courage", Read<float>(battle, "_maxCourage") - 3f);
            granted = (float)Invoke(battle, "GrantCourage", new Vector2(400, 340), 8f);
            Check(granted == 3f && Read<float>(battle, "_courage") == Read<float>(battle, "_maxCourage"), "A direct grant clamps at capacity");
            Check((float)Invoke(battle, "GrantCourage", new Vector2(400, 340), 8f) == 0f && flights.Count == 2, "A full meter takes no grant and shows no flight");
            Invoke(battle, "TickCouragePickups", 1f);
            Check(flights.Count == 0, "Grant flights finish and free themselves");
            Write(battle, "_courage", 0f);
            await CloseBattle(battle); battle = null;

            // Exercise actual viewport input with mouse and touch, at a monitor and a phone size.
            tuning.CourageDropChance = 1;
            foreach (var touch in new[] { false, true })
            {
                var mode = touch ? "phone" : "desktop";
                GetWindow().Size = touch ? new Vector2I(844, 390) : new Vector2I(1280, 720);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                battle = await OpenBattle(1);
                // The game pauses a battle when its window loses OS focus (AppLifecycleService -> TogglePause). These checks
                // drive input synthetically, so run them on an unpaused battle whatever the review window's focus is.
                AppLifecycleService.Instance?.Notification((int)Node.NotificationWMWindowFocusIn);
                if (Read<bool>(battle, "_battlePaused")) Invoke(battle, "TogglePause");
                packs = Read<List<CouragePickup>>(battle, "_couragePickups");
                var camera = Read<Camera2D>(battle, "_battleCamera");
                Invoke(battle, "SetBattleCameraFollow", false);
                camera.Position = new Vector2(700, camera.Position.Y);
                Invoke(battle, "ClampBattleCamera", camera); camera.ForceUpdateScroll();
                var world = new Vector2(720, 340);
                Vector2 Point(CouragePickup pickup) => pickup.GetGlobalTransformWithCanvas() * CouragePickup.TapCenter;
                CouragePickup Pack() => (CouragePickup)Invoke(battle, "SpawnCouragePickup", world, null);
                void Send(InputEvent e) => GetViewport().PushInput(e, true);
                void Down(Vector2 at, int finger = 7)
                {
                    if (touch) Send(new InputEventScreenTouch { Index = finger, Position = at, Pressed = true });
                    else Send(new InputEventMouseButton { Position = at, GlobalPosition = at, Pressed = true, ButtonIndex = MouseButton.Left });
                }
                void Up(Vector2 at, bool cancel = false, int finger = 7)
                {
                    if (touch) Send(new InputEventScreenTouch { Index = finger, Position = at, Canceled = cancel });
                    else Send(new InputEventMouseButton { Position = at, GlobalPosition = at, Canceled = cancel, ButtonIndex = MouseButton.Left });
                }
                void Move(Vector2 at)
                {
                    if (touch) Send(new InputEventScreenDrag { Index = 7, Position = at });
                    else Send(new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = MouseButtonMask.Left });
                }
                var fireball = GameData.GetSpell("spell_fireball");
                var spells = Read<BattleSpellState>(battle, "_spellDeck");
                spells.Initialize(new[] { fireball });
                Write(battle, "_mana", 99f); Write(battle, "_courage", 12f);
                Invoke(battle, "ArmSpell", fireball);
                var casts = Read<int>(battle, "_spellsCast");
                var pickup = Pack(); var point = Point(pickup);
                Check(GetViewport().GetVisibleRect().HasPoint(point), mode + ": pack remains visible after camera panning");
                Down(point);
                Check(Read<float>(battle, "_courage") == 12f && Read<bool>(battle, "_couragePickupPointerDown"), mode + ": press captures the pack without collecting");
                Up(point + new Vector2(2, 1));
                Check(pickup.Collected && Read<float>(battle, "_courage") == 32f, mode + ": click/tap collects +20 at the transformed camera position");
                Check(Read<float>(battle, "_mana") == 99f && Read<int>(battle, "_spellsCast") == casts && spells.ArmedSpell == fireball,
                    mode + ": collection spends no mana, casts no spell and preserves the armed spell");
                Up(point);
                Check(Read<float>(battle, "_courage") == 32f, mode + ": duplicate release cannot reward the same pack twice");
                Check(!(bool)Invoke(battle, "CollectCouragePickup", pickup), mode + ": claimed packs reject repeated collection");
                Invoke(battle, "TickCouragePickups", 1f);
                Check(packs.Count == 0, mode + ": collected flight finishes and frees the pack");

                pickup = Pack(); point = Point(pickup);
                Write(battle, "_courage", Read<float>(battle, "_maxCourage"));
                Down(point); Up(point);
                Check(pickup.Available && Read<float>(battle, "_courage") == Read<float>(battle, "_maxCourage"), mode + ": clicking a full meter preserves the expiring pack");
                Write(battle, "_courage", Read<float>(battle, "_maxCourage") - 3f);
                Down(point); Up(point);
                Check(pickup.Collected && Read<float>(battle, "_courage") == Read<float>(battle, "_maxCourage"), mode + ": collection clamps at capacity");
                Invoke(battle, "TickCouragePickups", 1f);

                Write(battle, "_courage", 0f);
                pickup = Pack(); point = Point(pickup);
                Down(point); Move(point + new Vector2(80, 0)); Up(point + new Vector2(80, 0));
                Check(pickup.Available && Read<float>(battle, "_courage") == 0 && Read<int>(battle, "_spellsCast") == casts,
                    mode + ": dragging from a pack does not collect or cast");
                if (touch)
                {
                    var synthetic = Point(pickup);
                    Send(new InputEventMouseButton { Device = -1, Position = synthetic, Pressed = true, ButtonIndex = MouseButton.Left });
                    Send(new InputEventMouseButton { Device = -1, Position = synthetic, ButtonIndex = MouseButton.Left });
                    Check(pickup.Available && Read<float>(battle, "_courage") == 0, "Touch-pan synthetic mouse events cannot collect a pack");
                    point = Point(pickup); Down(point);
                    Down(point, 8); Up(point, finger: 8);
                    Check(pickup.Available && Read<float>(battle, "_courage") == 0, "A second finger cannot collect another finger's held pack");
                    Up(point);
                    Check(pickup.Collected && Read<float>(battle, "_courage") == 20, "The owning touch collects exactly once");
                    Invoke(battle, "TickCouragePickups", 1f);
                    pickup = Pack();
                }
                point = Point(pickup); Down(point); Up(point, cancel: true);
                Check(pickup.Available, mode + ": canceled pointer does not collect");
                Down(point); battle._Notification((int)Node.NotificationApplicationFocusOut); Up(point);
                Check(pickup.Available && !Read<bool>(battle, "_couragePickupPointerDown"), mode + ": focus loss cancels a held pack and discards its late release");
                Down(point); Invoke(battle, "TickCouragePickups", 10f); Up(point);
                Check(packs.Count == 0, mode + ": a pack expiring between press and release cannot be collected");

                // A dragged spell may land over a pack without collecting it.
                Write(battle, "_courage", 0f); Write(battle, "_mana", 99f); spells.ReduceCooldowns(1000);
                pickup = Pack(); point = Point(pickup);
                Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit(GameData.EnemyBruteId)), pickup.Position);
                var spellCard = Read<Control>(battle, "_battleUiRoot").FindChildren("*", "Control", true, false)
                    .OfType<BattleActionCard>().Last();
                var cardPoint = spellCard.GetGlobalTransformWithCanvas() * (spellCard.Size * .5f);
                Down(cardPoint); Move(point); Up(point);
                Check(pickup.Available && Read<float>(battle, "_courage") == 0 && Read<int>(battle, "_spellsCast") == casts + 1,
                    mode + ": explicit spell drag casts over a pack without collecting it");
                Invoke(battle, "TickCouragePickups", 10f);

                // Capture actual battle presentation: a settled pack and a fading one, then one tumbling out of a body
                // and one streaking to the meter.
                for (var frame = 0; frame < 40; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var faded = Pack(); Invoke(battle, "TickCouragePickups", 6.8f);
                var settled = (CouragePickup)Invoke(battle, "SpawnCouragePickup", world + new Vector2(38, 0), null);
                Invoke(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit("player_brawler")), world - new Vector2(65, 0));
                // Review frames run uncapped, so wait in real time for the embers to rise, and step the motion by hand.
                await ToSignal(GetTree().CreateTimer(.8, true, false, true), SceneTreeTimer.SignalName.Timeout);
                AppLifecycleService.Instance?.Notification((int)Node.NotificationWMWindowFocusIn);
                if (Read<bool>(battle, "_battlePaused")) Invoke(battle, "TogglePause");
                await CaptureCouragePacks(mode + "-packs");
                var popping = (CouragePickup)Invoke(battle, "SpawnCouragePickup", world + new Vector2(-24, 14), null);
                popping.PopFrom(new Vector2(0, -30));
                Write(battle, "_courage", 0f);
                Invoke(battle, "CollectCouragePickup", settled);
                Invoke(battle, "GrantCourage", world - new Vector2(150, 0), 6f);
                for (var step = 0; step < 17; step++) Invoke(battle, "TickCouragePickups", .025f);
                await CaptureCouragePacks(mode + "-motion");
                for (var step = 0; step < 7; step++) Invoke(battle, "TickCouragePickups", .025f);
                await CaptureCouragePacks(mode + "-arrival");
                Write(battle, "_battleEnded", true); Invoke(battle, "UpdateCouragePickupPresentation");
                Check(packs.Count == 0 && !Read<bool>(battle, "_couragePickupPointerDown"), mode + ": battle end clears packs and input state");
                await CloseBattle(battle); battle = null;
            }
        }
        finally
        {
            if (battle != null && IsInstanceValid(battle)) await CloseBattle(battle);
            tuning.CourageDropChance = originalChance;
            tuning.CourageDropGuaranteeAfterMisses = originalGuarantee;
            GetWindow().Size = originalWindow;
            GetTree().Paused = false;
        }
    }
}
