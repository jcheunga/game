using System.Collections.Generic;
using System.Linq;
using Godot;

// The painted stronghold, alive (data from art/royal/bases_life.py): its troops march out through the gate, drums and
// a glowing gate warn of each wave, sentries keep watch behind the parapets, its candles and braziers flicker and the
// zone's own weather drifts about it. A barricade of the zone's materials closes the road in front. Damage cracks,
// chips and scorches the walls and sets the roofs burning; destroyed, it sinks into its own dust and the caravan's
// standard is raised where its gate stood. Its health hangs above the gate as a war banner.
public partial class BattleController
{
    private const float WaveWarningSeconds = 2.4f;
    private const float CollapseSeconds = 1.6f;
    // The result board waits for the standard to be raised over the rubble.
    private const float CollapseRevealDelay = 2.3f;
    private const float BannerWidth = 84f;
    private const float FlagHeight = 72f;

    private readonly Dictionary<Unit, (Vector2 From, Vector2 To, float Elapsed, float Duration)> _gateExits = new();
    private readonly List<(Vector2 At, int Piece, float Born, float Size, float Angle, Color Paint)> _baseScars = new();
    private Image _plateImage;
    private readonly List<(SceneryFigure Figure, Vector2 Feet, float Facing, float NextTurn)> _sentries = new();
    private readonly List<CpuParticles2D> _roofFires = new();
    private float _showTime, _gateOpenUntil = -1f, _waveWarnAt = -10f, _baseFellAt = -1f, _scarDebt;
    private int _warnedWave = -1, _crackStage, _nextScarSpot;
    private bool _flagRaised;
    private Texture2D _decals, _banner, _flag;
    private Rect2[] _decalRects = System.Array.Empty<Rect2>();
    private string[] _decalKinds = System.Array.Empty<string>();
    private Rect2 _bannerRod, _bannerCloth;
    private Rect2 _flagCloth;
    private float _flagPole;

    private Vector2 GatePoint => PaintedBase is { } art
        ? new Vector2(art.GateX, Mathf.Min(art.Foot - 2f, BattlefieldTop + SpawnVerticalPadding)) : new Vector2(EnemySpawnX, BaseCenterY);
    private bool StrongholdFallen => _baseFellAt >= 0f;
    // 0 standing .. 1 sunk: the collapse's shake comes first, then the sink.
    private float CollapseAmount => !StrongholdFallen ? 0f
        : ReducedMotion ? 1f : Mathf.Pow(Mathf.Clamp((_showTime - _baseFellAt - .3f) / (CollapseSeconds - .3f), 0f, 1f), 2.2f);

    private void InitializeStrongholdLife()
    {
        AddChild(new BattleTicker { Name = "Presentation", Tick = TickPresentation });
        if (PaintedBase is not { } art) return;
        _decals = LoadStructureTexture("base_decals.png");
        if (ReadStructureJson("base_decals.json") is { } decals)
        {
            _decalRects = decals.RootElement.GetProperty("rects").EnumerateArray()
                .Select(r => new Rect2(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle())).ToArray();
            _decalKinds = decals.RootElement.GetProperty("kinds").EnumerateArray().Select(k => k.GetString()).ToArray();
            decals.Dispose();
        }
        _banner = LoadStructureTexture("base_banner.png");
        if (ReadStructureJson("base_banner.json") is { } banner)
        {
            var root = banner.RootElement;
            Rect2 Box(string key) { var b = root.GetProperty(key); return new Rect2(b[0].GetSingle(), b[1].GetSingle(), b[2].GetSingle() - b[0].GetSingle(), b[3].GetSingle() - b[1].GetSingle()); }
            _bannerRod = Box("rod");
            _bannerCloth = Box("cloth");
            banner.Dispose();
        }
        _flag = LoadStructureTexture("victory_flag.png");
        if (ReadStructureJson("victory_flag.json") is { } flag)
        {
            var c = flag.RootElement.GetProperty("cloth");
            var size = flag.RootElement.GetProperty("size");
            _flagCloth = new Rect2(c[0].GetSingle() / size[0].GetSingle(), c[1].GetSingle() / size[1].GetSingle(),
                (c[2].GetSingle() - c[0].GetSingle()) / size[0].GetSingle(), (c[3].GetSingle() - c[1].GetSingle()) / size[1].GetSingle());
            _flagPole = flag.RootElement.GetProperty("pole").GetSingle() / size[0].GetSingle();
            flag.Dispose();
        }
        BuildZoneBarricade();
        BuildSentries(art);
        BuildZoneAmbience(art);
    }

    private static Texture2D LoadStructureTexture(string file) =>
        ResourceLoader.Exists("res://assets/structures/" + file) ? ResourceLoader.Load<Texture2D>("res://assets/structures/" + file) : null;

    private static System.Text.Json.JsonDocument ReadStructureJson(string file)
    {
        var path = "res://assets/structures/" + file;
        return FileAccess.FileExists(path) ? System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString(path)) : null;
    }

    private void TickPresentation(float delta)
    {
        if (!IsInstanceValid(this)) return;
        // The show clock follows the battle while it runs, keeps going after it ends, and stops for a pause.
        if (_battleEnded) _showTime += delta;
        else _showTime = _elapsed;
        if (_battlePaused && !_battleEnded) return;
        UpdateWagonLife(delta);
        UpdateStrongholdLife(delta);
    }

    // ── troops come out of the gate ──

    /// <summary>A reinforcement appears in the gateway and walks down onto its line; until then it neither fights nor
    /// is pushed about. The gate's light stays up while troops come through.</summary>
    private void BeginGateExit(Unit unit)
    {
        if (PaintedBase == null || StrongholdFallen) return;
        var from = GatePoint;
        var to = unit.Position;
        if (from.DistanceTo(to) < 2f) return;
        var duration = Mathf.Clamp(from.DistanceTo(to) / Mathf.Max(18f, unit.Speed), .35f, .8f);
        _gateExits[unit] = (from, to, 0f, duration);
        unit.Position = from;
        unit.Modulate = new Color(1, 1, 1, 0);
        _gateOpenUntil = Mathf.Max(_gateOpenUntil, _elapsed + duration + .5f);
    }

    /// <summary>Moves a unit along its walk out of the gate. Returns false once it is free to fight.</summary>
    private bool TickGateExit(Unit unit, float delta)
    {
        if (!_gateExits.TryGetValue(unit, out var exit)) return false;
        exit.Elapsed += delta;
        var t = Mathf.Clamp(exit.Elapsed / exit.Duration, 0f, 1f);
        unit.Position = exit.From.Lerp(exit.To, t);
        unit.Modulate = new Color(1, 1, 1, Mathf.Clamp(t * 3.5f, 0f, 1f));
        if (t < 1f)
        {
            _gateExits[unit] = exit;
            return true;
        }
        _gateExits.Remove(unit);
        unit.Modulate = Colors.White;
        return false;
    }

    // ── the barricade across the road ──

    /// <summary>The zone's barricade pieces stand across the road just behind the stronghold's hitbox, one per lane
    /// depth, so troops on every line meet something; each sorts with the troops at its own depth.</summary>
    private void BuildZoneBarricade()
    {
        var json = ReadStructureJson($"barricades/{_activeRouteId}.json");
        if (json == null) return;
        var pieces = json.RootElement.GetProperty("pieces").EnumerateArray()
            .Select(p => (File: p.GetProperty("file").GetString(), Size: new Vector2(p.GetProperty("size")[0].GetSingle(), p.GetProperty("size")[1].GetSingle()),
                Foot: p.GetProperty("foot").GetSingle())).ToArray();
        json.Dispose();
        if (pieces.Length == 0) return;
        // Rows from the far edge of the road to the near one; the far row stands aside of the gate it would hide.
        var rows = new[] { BattlefieldTop + 5f, BattlefieldTop + 21f, BattlefieldTop + 37f, BattlefieldBottom - 1f };
        var rng = new RandomNumberGenerator { Seed = (ulong)(_stageData?.StageNumber ?? 1) * 7919UL };
        var order = Enumerable.Range(0, pieces.Length).OrderBy(_ => rng.Randf()).ToArray();
        var tallest = pieces.Max(p => p.Foot);
        for (var i = 0; i < rows.Length; i++)
        {
            var piece = pieces[order[i % order.Length]];
            var texture = LoadStructureTexture("barricades/" + piece.File);
            if (texture == null) continue;
            // About chest high to a soldier at the tallest; every piece keeps the sheet's relative scale.
            var scale = 26f / tallest;
            var size = piece.Size * scale;
            var x = EnemyBaseX + (i == 0 ? 26f : 6f) + rng.RandfRange(-4f, 4f);
            var foot = new Vector2(x, rows[i]);
            var flip = rng.Randf() < .5f;
            AddChild(new BattleBaseCanvas
            {
                Name = $"Barricade{i}", Position = foot, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
                Paint = canvas =>
                {
                    if (StrongholdFallen && _showTime - _baseFellAt > .3f) return;
                    var rect = new Rect2(foot - new Vector2(size.X * .5f, piece.Foot * scale), size);
                    if (flip) rect = new Rect2(rect.End.X, rect.Position.Y, -rect.Size.X, rect.Size.Y);
                    // (DrawContact works in the canvas's own space and resets its transform.)
                    FieldLighting.DrawContact(canvas, foot - ((Node2D)canvas).Position, new Vector2(size.X * .42f, 2.6f), .55f);
                    canvas.DrawSetTransform(-((Node2D)canvas).Position);
                    canvas.DrawTextureRect(texture, rect, false, FieldLighting.Tint);
                }
            });
        }
    }

    // ── sentries ──

    private void BuildSentries(EnemyBaseArt art)
    {
        var id = SentryUnitId();
        foreach (var post in art.Posts.Take(2))
        {
            var figure = SceneryFigure.Load(id, .9f);
            if (figure == null) break;
            // They stand behind the parapet: the wall hides them below the chest.
            var feet = new Vector2(post.X, post.Y + figure.HeadHeight * .58f);
            _sentries.Add((figure, feet, _rng.Randf() < .5f ? -1f : 1f, _rng.RandfRange(2f, 6f)));
        }
    }

    /// <summary>A ranged troop from the stage's waves (the commonest), else the commonest foot soldier.</summary>
    private string SentryUnitId()
    {
        var entries = (_stageData?.Waves ?? System.Array.Empty<StageWaveDefinition>())
            .SelectMany(w => w.Entries).GroupBy(e => e.UnitId).OrderByDescending(g => g.Sum(e => e.Count)).Select(g => g.Key)
            .Where(id => GameData.GetUnit(id) is { } def && def.VisualScale <= 1.15f && !id.Contains("boss")).ToArray();
        return entries.FirstOrDefault(id => new UnitStats(GameData.GetUnit(id)).UsesProjectile) ?? entries.FirstOrDefault() ?? "enemy_walker";
    }

    // ── the zone's weather around the stronghold ──

    private void BuildZoneAmbience(EnemyBaseArt art)
    {
        var roof = art.Smoke.Length > 0 ? art.Smoke[0] : art.Rect.GetCenter();
        var foot = new Vector2(art.GateX + 20f, art.Foot - 4f);
        var span = new Vector2(art.Rect.Size.X * .42f, 3f);
        switch (_activeRouteId)
        {
            case "city": // hearth smoke from a chimney
                Ambient("p_smoke", roof, new Vector2(3, 1), 5, 4.5f, new Color(.78f, .76f, .74f, .32f), 9f, 16f, new Vector2(4, -9), 0f, false);
                break;
            case "harbor": // sea mist creeping along the footings
                Ambient("p_smoke", foot, span, 7, 7f, new Color(.8f, .88f, .92f, .2f), 20f, 30f, new Vector2(-4, -1), 0f, false);
                break;
            case "foundry": // embers rising from the forges
                Ambient("p_ember", roof, new Vector2(30, 4), 12, 2.8f, new Color(1f, .62f, .25f, .9f), 1.6f, 3f, new Vector2(-3, -18), -6f, true);
                break;
            case "quarantine": // a low green miasma
                Ambient("p_smoke", foot, span, 7, 6.5f, new Color(.62f, .78f, .5f, .2f), 18f, 28f, new Vector2(-3, -1.5f), 0f, false);
                break;
            case "thornwall": // snow blowing off the roofs
                Ambient("p_dust", roof + new Vector2(0, 8), new Vector2(50, 10), 16, 3f, new Color(.95f, .97f, 1f, .75f), 1.2f, 2.4f, new Vector2(-16, 4), 4f, false);
                break;
            case "basilica": // candle motes drifting up
                Ambient("p_orb", foot + new Vector2(0, -26), new Vector2(50, 20), 9, 4f, new Color(1f, .82f, .55f, .75f), 1f, 2f, new Vector2(0, -5), -1f, true);
                break;
            case "mire": // swamp fog and fireflies
                Ambient("p_smoke", foot, span, 6, 7f, new Color(.66f, .74f, .58f, .2f), 18f, 28f, new Vector2(-2.5f, -1), 0f, false);
                Ambient("p_orb", foot + new Vector2(0, -18), new Vector2(60, 16), 7, 3.5f, new Color(.85f, 1f, .45f, .8f), .9f, 1.6f, new Vector2(1.5f, -1.5f), 0f, true);
                break;
            case "steppe": // dust gusting along the ground
                Ambient("p_dust", foot + new Vector2(30, 0), span, 9, 2.2f, new Color(.82f, .68f, .48f, .35f), 5f, 10f, new Vector2(-30, -2), 0f, false);
                break;
            case "gloamwood": // glowing spores
                Ambient("p_orb", foot + new Vector2(0, -24), new Vector2(60, 22), 11, 5f, new Color(.45f, .85f, 1f, .75f), .9f, 1.8f, new Vector2(0, -3), -.5f, true);
                break;
            case "citadel": // fire embers and falling ash
                Ambient("p_ember", roof, new Vector2(30, 4), 10, 2.6f, new Color(1f, .55f, .2f, .9f), 1.5f, 2.8f, new Vector2(-4, -16), -5f, true);
                Ambient("p_ash", roof + new Vector2(0, -20), new Vector2(70, 10), 10, 4f, new Color(.32f, .3f, .3f, .6f), 1.4f, 2.6f, new Vector2(-5, 7), 1f, false);
                break;
        }
    }

    /// <summary>A soft continuous emitter behind the troops (sorted at the band's far edge).</summary>
    private CpuParticles2D Ambient(string texture, Vector2 at, Vector2 extents, int amount, float life, Color colour,
        float sizeMin, float sizeMax, Vector2 velocity, float gravity, bool additive)
    {
        var tex = SpellVfx.Tex(texture);
        var width = Mathf.Max(1f, tex.GetWidth());
        var particles = new CpuParticles2D
        {
            Name = "Ambient", Texture = tex, Amount = ReducedMotion ? Mathf.Max(1, amount / 2) : amount, Lifetime = life,
            Preprocess = life, LocalCoords = false, EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = extents, Direction = velocity.Length() > .01f ? velocity.Normalized() : Vector2.Up, Spread = 25f,
            InitialVelocityMin = velocity.Length() * .6f, InitialVelocityMax = velocity.Length() * 1.3f, Gravity = new Vector2(0, gravity),
            ScaleAmountMin = sizeMin / width, ScaleAmountMax = sizeMax / width, AngleMin = 0f, AngleMax = 360f,
            ColorRamp = new Gradient { Offsets = new[] { 0f, .25f, .7f, 1f },
                Colors = new[] { new Color(colour, 0), colour, colour, new Color(colour, 0) } },
            Material = additive ? SpellVfx.Additive : null,
        };
        // Sorted with the stronghold, behind every troop; the emitter itself stays where the effect belongs.
        var holder = new Node2D { Name = "AmbientHolder", Position = new Vector2(at.X, BattlefieldTop - 1f) };
        particles.Position = at - holder.Position;
        holder.AddChild(particles);
        AddChild(holder);
        return particles;
    }

    // ── per-frame: warnings, damage, sentries, collapse ──

    private void UpdateStrongholdLife(float delta)
    {
        if (_gateExits.Count > 0)
            foreach (var unit in _gateExits.Keys.Where(u => !IsInstanceValid(u) || u.IsDead).ToArray())
            {
                if (IsInstanceValid(unit)) unit.Modulate = Colors.White;
                _gateExits.Remove(unit);
            }
        if (PaintedBase == null) return;
        if (!_battleEnded) WarnOfWaves();
        for (var i = 0; i < _sentries.Count; i++)
        {
            var (figure, feet, facing, nextTurn) = _sentries[i];
            figure.Tick(delta);
            nextTurn -= delta;
            if (nextTurn <= 0f) { facing = -facing; nextTurn = _rng.RandfRange(4f, 9f); }
            _sentries[i] = (figure, feet, facing, nextTurn);
        }
        // Each quarter of the stronghold's health lost cracks its walls; from half health its roofs burn.
        var ratio = Mathf.Clamp(_enemyBaseHealth / Mathf.Max(1f, _enemyBaseMaxHealth), 0f, 1f);
        while (!StrongholdFallen && _crackStage < 3 && ratio <= .75f - _crackStage * .25f)
        {
            _crackStage++;
            AddScar("crack");
            AddScar("crack");
            if (_crackStage >= 2) LightRoofFire(_crackStage - 2);
        }
        if (StrongholdFallen) UpdateCollapse();
    }

    private void WarnOfWaves()
    {
        int index;
        float next;
        if (_spawnDirector.UsesScriptedWaves && _spawnDirector.TryGetNextScriptedWave(out _))
        {
            index = _spawnDirector.NextScriptedWaveIndex;
            next = _spawnDirector.NextScriptedWaveTime;
            if (index == 0) return; // the caravan's horn opens the battle
        }
        else if (IsEndlessMode)
        {
            index = 1000 + _spawnDirector.EndlessWaveNumber;
            next = _spawnDirector.NextEndlessWaveTime;
        }
        else return;
        var lead = next - _elapsed;
        if (index == _warnedWave || lead <= 0f || lead > WaveWarningSeconds) return;
        _warnedWave = index;
        _waveWarnAt = _elapsed;
        // War drums roll in the stronghold, the gate kindles and its banner lifts; the horn sounds as the wave comes.
        AudioDirector.Instance?.Play("det_war_drums", 2f);
    }

    /// <summary>Damage the stronghold took: every few percent of its health leaves a chip or scorch on its walls.</summary>
    private void ScarStronghold(float damage)
    {
        if (PaintedBase == null) return;
        _scarDebt += damage / Mathf.Max(1f, _enemyBaseMaxHealth);
        while (_scarDebt >= .09f)
        {
            _scarDebt -= .09f;
            AddScar(_baseScars.Count % 3 == 2 ? "scorch" : "chip");
        }
    }

    private void AddScar(string kind)
    {
        if (PaintedBase is not { } art || _decals == null || _nextScarSpot >= art.Wall.Length) return;
        var pieces = Enumerable.Range(0, _decalKinds.Length).Where(i => _decalKinds[i] == kind).ToArray();
        if (pieces.Length == 0) return;
        var at = art.Wall[_nextScarSpot++];
        var size = kind == "crack" ? 11f : kind == "scorch" ? 11f : 8f;
        _baseScars.Add((at, pieces[_rng.RandiRange(0, pieces.Length - 1)], _showTime, size, kind == "chip" ? 0f : _rng.RandfRange(-.6f, .6f),
            PlatePaintAt(art, at, size * .5f)));
        if (!ReducedMotion) BattleParticles.SpawnBaseHitDebris(this, at, new Color("8a7a66"));
    }

    /// <summary>The stronghold's average paint around a point (opaque pixels only), so a mark painted in neutral
    /// stone takes on the wood, brick or bone it lands on.</summary>
    private Color PlatePaintAt(EnemyBaseArt art, Vector2 at, float radius)
    {
        if (_plateImage == null)
        {
            _plateImage = art.Texture.GetImage();
            if (_plateImage == null) return Colors.White;
            if (_plateImage.IsCompressed()) _plateImage.Decompress();
        }
        var size = new Vector2(_plateImage.GetWidth(), _plateImage.GetHeight());
        var centre = (at - art.Rect.Position) / art.Rect.Size * size;
        var reach = radius / art.Rect.Size.X * size.X;
        var sum = new Vector3();
        var count = 0;
        for (var y = -2; y <= 2; y++)
        for (var x = -2; x <= 2; x++)
        {
            var px = (int)Mathf.Clamp(centre.X + x * reach * .5f, 0, size.X - 1);
            var py = (int)Mathf.Clamp(centre.Y + y * reach * .5f, 0, size.Y - 1);
            var c = _plateImage.GetPixel(px, py);
            if (c.A < .5f) continue;
            sum += new Vector3(c.R, c.G, c.B);
            count++;
        }
        if (count == 0) return Colors.White;
        sum /= count;
        // Lifted toward the mark's own lights, so its chipped edges read as fresh breaks in that material.
        var brightest = Mathf.Max(sum.X, Mathf.Max(sum.Y, sum.Z));
        var lift = Mathf.Clamp(.8f / Mathf.Max(.05f, brightest), 1f, 2.2f);
        return new Color(Mathf.Min(1f, sum.X * lift), Mathf.Min(1f, sum.Y * lift), Mathf.Min(1f, sum.Z * lift));
    }

    private void LightRoofFire(int index)
    {
        if (PaintedBase is not { } art || index >= art.Smoke.Length) return;
        var fire = Ambient("p_flame", art.Smoke[index] + new Vector2(0, 3), new Vector2(5, 1.5f), 9, .9f,
            new Color(1f, .62f, .26f, .9f), 4f, 7.5f, new Vector2(0, -16), -8f, true);
        _roofFires.Add(fire);
    }

    // ── collapse and the standard ──

    /// <summary>The stronghold falls: it shakes, sinks into a cloud of its own dust, and the caravan's standard rises
    /// where its gate stood.</summary>
    private void CollapseStronghold()
    {
        if (PaintedBase is not { } art || StrongholdFallen) return;
        _baseFellAt = _showTime;
        AudioDirector.Instance?.PlayAt("spell_earthquake", GatePoint, -2f);
        foreach (var fire in _roofFires) if (IsInstanceValid(fire)) fire.Emitting = false;
        foreach (var unit in _gateExits.Keys) if (IsInstanceValid(unit)) unit.Modulate = Colors.White;
        _gateExits.Clear();
        if (ReducedMotion) RaiseStandard();
    }

    private float _collapseDustAt = -1f;

    private void UpdateCollapse()
    {
        if (PaintedBase is not { } art) return;
        var t = _showTime - _baseFellAt;
        // Dust rolls out along the foot in three gusts as it goes down, with stones thrown clear.
        if (!ReducedMotion && t >= .3f && (_collapseDustAt < 0f || (t - _collapseDustAt >= .45f && t < 1.4f)))
        {
            _collapseDustAt = t;
            var width = art.Rect.Size.X * .7f;
            for (var i = 0; i < 4; i++)
            {
                var at = new Vector2(art.GateX + _rng.RandfRange(-width * .45f, width * .55f), art.Foot - 3f);
                SpellVfx.Burst(this, at, "p_smoke", 6, 1.8f, SpellVfx.Fade(new Color(.62f, .56f, .5f, .7f), new Color(.45f, .42f, .4f, .4f)),
                    16f, 30f, 10f, 34f, spread: 70f, direction: Vector2.Up, gravity: -6f, additive: false, groundRadius: 14f, explosiveness: .6f);
            }
            SpellVfx.Burst(this, GatePoint + new Vector2(0, -30), "p_rubble", 10, 1.1f, SpellVfx.Fade(new Color("8c7a64")), 2.5f, 5f, 40f, 110f,
                spread: 80f, direction: Vector2.Up, gravity: 260f, additive: false);
            AudioDirector.Instance?.PlayAt("death_debris", GatePoint, -3f);
        }
        if (!_flagRaised && t >= 1.25f) RaiseStandard();
        // The ruin smoulders once the dust settles.
        if (!_ruinSmoking && t >= 1.1f)
        {
            _ruinSmoking = true;
            Ambient("p_smoke", new Vector2(art.GateX + 18f, art.Foot - 10f), new Vector2(art.Rect.Size.X * .25f, 4f), 6, 5f,
                new Color(.42f, .4f, .38f, .32f), 14f, 26f, new Vector2(3f, -8f), -1f, false);
        }
    }

    private bool _ruinSmoking;

    private void RaiseStandard()
    {
        if (_flagRaised || _flag == null) return;
        _flagRaised = true;
        var foot = new Vector2(GatePoint.X + 4f, BattlefieldTop + 4f);
        var cloth = new ShaderMaterial { Shader = _clothShader ??= new Shader { Code = ClothShader } };
        cloth.SetShaderParameter("flag", new Vector4(_flagCloth.Position.X, _flagCloth.Position.Y, _flagCloth.End.X, _flagCloth.End.Y));
        cloth.SetShaderParameter("awning", Vector4.Zero);
        cloth.SetShaderParameter("pole_left", 1f);
        cloth.SetShaderParameter("wind", ReducedMotion ? 0f : 1.3f);
        var raisedAt = _showTime;
        AddChild(new BattleBaseCanvas
        {
            Name = "VictoryFlag", Position = foot, Material = cloth, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Paint = canvas =>
            {
                cloth.SetShaderParameter("time", _showTime);
                var size = new Vector2(FlagHeight * _flag.GetWidth() / _flag.GetHeight(), FlagHeight);
                // It pushes up out of the dust, overshoots a little and settles upright.
                var t = ReducedMotion ? 1f : Mathf.Clamp((_showTime - raisedAt) / .6f, 0f, 1f);
                var rise = 1f - Mathf.Pow(1f - t, 3f);
                var lean = ReducedMotion ? 0f : Mathf.Sin(Mathf.Clamp((_showTime - raisedAt - .4f) / .8f, 0f, 1f) * Mathf.Pi * 2f) * .05f
                    * (1f - Mathf.Clamp((_showTime - raisedAt - .4f) / .8f, 0f, 1f));
                var shown = size.Y * rise;
                var pole = foot.X - _flagPole * size.X;
                FieldLighting.DrawContact(canvas, foot - ((Node2D)canvas).Position, new Vector2(7f, 2f), .6f * rise);
                canvas.DrawSetTransformMatrix(Transform2D.Identity.Translated(-foot).Rotated(lean).Translated(foot - ((Node2D)canvas).Position));
                canvas.DrawTextureRectRegion(_flag, new Rect2(pole, foot.Y - shown, size.X, shown),
                    new Rect2(0, 0, _flag.GetWidth(), _flag.GetHeight() * rise), FieldLighting.Tint);
            }
        });
        if (!ReducedMotion)
            SpellVfx.Burst(this, foot, "p_dust", 8, .9f, SpellVfx.Fade(new Color(.7f, .64f, .56f, .6f)), 6f, 12f, 10f, 30f,
                spread: 70f, direction: Vector2.Up, additive: false, groundRadius: 6f);
        AudioDirector.Instance?.Play("det_banner", 2f);
    }

    // ── drawing, called from DrawPaintedBase / DrawPaintedBaseGlow ──

    /// <summary>Sentries, drawn before the plate so its parapets hide them below the chest.</summary>
    private void DrawSentries(CanvasItem canvas)
    {
        if (_sentries.Count == 0) return;
        var fade = StrongholdFallen ? Mathf.Clamp(1f - (_showTime - _baseFellAt) / .3f, 0f, 1f) : 1f;
        if (fade <= 0f) return;
        var tint = new Color(FieldLighting.Tint.Darkened(.12f), fade);
        foreach (var (figure, feet, facing, _) in _sentries) figure.Draw(canvas, feet, facing, tint);
    }

    /// <summary>The plate, sunk by the collapse: the part that has gone below its foot line is not drawn.</summary>
    private void DrawStrongholdPlate(CanvasItem canvas, EnemyBaseArt art, Color modulate)
    {
        var collapse = CollapseAmount;
        if (!StrongholdFallen)
        {
            canvas.DrawTextureRect(art.Texture, art.Rect, false, modulate);
            DrawScars(canvas, 0f);
            return;
        }
        var t = _showTime - _baseFellAt;
        var shake = ReducedMotion || t > 1.5f ? 0f : Mathf.Sin(t * 57f) * 1.3f * (1f - Mathf.Clamp(t / 1.5f, 0f, 1f));
        var rect = art.Rect;
        var standing = art.Foot - rect.Position.Y;
        var sink = standing * .62f * collapse;
        var size = art.Texture.GetSize();
        var shown = (standing - sink) / rect.Size.Y;
        var tint = modulate.Darkened(.45f * collapse);
        canvas.DrawSetTransform(new Vector2(shake, 0) - ((Node2D)canvas).Position);
        canvas.DrawTextureRectRegion(art.Texture, new Rect2(rect.Position.X, rect.Position.Y + sink, rect.Size.X, standing - sink),
            new Rect2(0, 0, size.X, size.Y * shown), tint);
        // Its baked ground shadow fades as it goes.
        var below = (rect.End.Y - art.Foot) / rect.Size.Y;
        canvas.DrawTextureRectRegion(art.Texture, new Rect2(rect.Position.X, art.Foot, rect.Size.X, rect.End.Y - art.Foot),
            new Rect2(0, size.Y * (1f - below), size.X, size.Y * below), new Color(tint, 1f - collapse));
        DrawScars(canvas, sink);
        canvas.DrawSetTransform(-((Node2D)canvas).Position);
    }

    private void DrawScars(CanvasItem canvas, float sink)
    {
        if (_decals == null || PaintedBase is not { } art) return;
        foreach (var (at, piece, born, size, angle, paint) in _baseScars)
        {
            var point = at + new Vector2(0, sink);
            if (point.Y > art.Foot - size * .5f || piece >= _decalRects.Length) continue;
            var source = _decalRects[piece];
            // Each lands with a little punch.
            var pop = ReducedMotion ? 1f : 1f + .35f * Mathf.Clamp(1f - (_showTime - born) / .18f, 0f, 1f);
            var extent = source.Size / Mathf.Max(source.Size.X, source.Size.Y) * size * pop;
            var tint = new Color(paint, _decalKinds[piece] == "scorch" ? .7f : .85f);
            canvas.DrawSetTransformMatrix(Transform2D.Identity.Rotated(angle).Translated(point - ((Node2D)canvas).Position));
            canvas.DrawTextureRectRegion(_decals, new Rect2(-extent * .5f, extent), source, tint);
        }
        canvas.DrawSetTransform(-((Node2D)canvas).Position);
    }

    /// <summary>Additive light for the stronghold: candles and braziers breathing, and the gate kindling before a wave
    /// and while troops come through.</summary>
    private void DrawStrongholdGlow(CanvasItem canvas, EnemyBaseArt art)
    {
        var fade = 1f - CollapseAmount;
        if (fade <= 0f) return;
        var glow = LanternGlowTexture();
        for (var i = 0; i < art.Warm.Length; i++)
        {
            var (point, radius) = art.Warm[i];
            var flicker = .62f + .24f * Mathf.Sin(_showTime * (2.1f + i * .37f) + i * 1.9f) + .14f * Mathf.Sin(_showTime * 7.3f + i * 1.3f);
            var r = radius * 1.7f;
            canvas.DrawTextureRect(glow, new Rect2(point - Vector2.One * r, Vector2.One * r * 2), false,
                new Color(1f, .72f, .38f, .24f * flicker * fade));
        }
        var warn = Mathf.Clamp((_elapsed - _waveWarnAt) / WaveWarningSeconds, 0f, 1f) * (_elapsed - _waveWarnAt < WaveWarningSeconds + 1.2f ? 1f : 0f);
        var open = _gateOpenUntil > _elapsed ? 1f : Mathf.Clamp(1f - (_elapsed - _gateOpenUntil) / .6f, 0f, 1f);
        var gate = Mathf.Max(warn * (.7f + .3f * Mathf.Sin(_showTime * 9f)), open) * fade;
        if (gate <= .01f) return;
        var colour = art.Lights.Length > 0 ? art.Lights[0].Colour : new Color(.6f, 1f, .4f);
        var centre = new Vector2(art.GateX, art.Foot - 14f);
        canvas.DrawTextureRect(glow, new Rect2(centre - new Vector2(24, 20), new Vector2(48, 40)), false, new Color(colour, .38f * gate));
        canvas.DrawTextureRect(glow, new Rect2(centre - new Vector2(10, 12), new Vector2(20, 24)), false, new Color(colour.Lightened(.4f), .45f * gate));
    }

    /// <summary>The stronghold's health as its war banner: the cloth that still hangs is its health, what has been torn
    /// away shows faded; fresh damage flashes pale first. The banner lifts when a wave is called.</summary>
    private void DrawStrongholdBanner(CanvasItem canvas, EnemyBaseArt art, float ratio)
    {
        if (StrongholdFallen) return;
        var highContrast = GameState.Instance?.HighContrast ?? false;
        if (_banner == null || highContrast)
        {
            DrawBaseHealthMeter(canvas, new Vector2(art.GateX, PaintedBaseBarY), 132f, ratio, false);
            return;
        }
        var size = _banner.GetSize();
        var scale = BannerWidth / size.X;
        var lift = Mathf.Clamp(_elapsed - _waveWarnAt, 0f, 9f);
        var raise = lift < .6f ? Mathf.Sin(lift / .6f * Mathf.Pi) * 3f : 0f;
        var origin = new Vector2(art.GateX - BannerWidth * .5f, PaintedBaseBarY - size.Y * scale * .5f - raise);
        Rect2 World(Rect2 r) => new(origin + r.Position * scale, r.Size * scale);
        var tint = FieldLighting.Tint;
        // The whole banner faint: the length it had.
        canvas.DrawTextureRect(_banner, new Rect2(origin, size * scale), false, new Color(tint.Darkened(.72f), .3f));
        var cloth = _bannerCloth;
        Rect2 Part(float from, float to) => new(cloth.Position.X + cloth.Size.X * from, cloth.Position.Y, cloth.Size.X * (to - from), cloth.Size.Y);
        var trail = Mathf.Max(ratio, _enemyHealthBarMotion.TrailRatio);
        if (trail > ratio + .002f)
        {
            var torn = Part(ratio, trail);
            canvas.DrawTextureRectRegion(_banner, World(torn), torn, new Color(2.2f, 2f, 1.8f, .55f));
        }
        if (ratio > 0f)
        {
            var whole = Part(0f, ratio);
            canvas.DrawTextureRectRegion(_banner, World(whole), whole, tint);
        }
        canvas.DrawTextureRectRegion(_banner, World(_bannerRod), _bannerRod, tint);
    }
}
