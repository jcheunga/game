using Godot;

public partial class BattleController
{
    private BattleShadowCanvas _shadowCanvas;
    private BattleLighting FieldLighting => BattleLighting.ForZone(_activeRouteId);
    private BattleStructureArt WagonArt => BattleStructureArt.Load(GameState.Instance.SelectedWagonSkinId == WagonSkinCatalog.DefaultSkinId
        ? "war_wagon" : "war_wagon_" + GameState.Instance.SelectedWagonSkinId) ?? BattleStructureArt.Load("war_wagon");
    // The stage's painted stronghold. Every zone has them; only a missing file leaves the procedural wall in its place.
    private EnemyBaseArt _paintedBase;
    private bool _paintedBaseResolved;
    private EnemyBaseArt PaintedBase
    {
        get
        {
            if (_paintedBaseResolved) return _paintedBase;
            _paintedBaseResolved = true;
            return _paintedBase = EnemyBaseArt.Resolve(_stageData?.StageNumber ?? 0, _activeRouteId);
        }
    }
    // A painted stronghold stands behind the road, like the scenery it was painted into: it sorts above the band's
    // top line, so every troop on the road passes in front of it.
    private Vector2 CastleSortPosition => PaintedBase != null ? new Vector2(EnemyBaseX, BattlefieldTop - 1) : EnemyBaseCorePosition;

    private void InitializeBattlePresentation()
    {
        _shadowCanvas = new BattleShadowCanvas { Name = "BattleShadows", ZIndex = -5,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps, Paint = DrawBattleGroundShadows };
        AddChild(_shadowCanvas);
        var caravan = new BattleBaseCanvas { Name = "Caravan", Position = WagonSortPosition,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Paint = canvas => DrawPlayerBus(canvas, ResolveTerrainPalette(), RouteCatalog.Get(_activeRouteId)) };
        AddChild(caravan);
        BuildWagonLayers(caravan);
        AddChild(new BattleBaseCanvas { Name = "Castle", Position = CastleSortPosition,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Paint = canvas => DrawEnemyBarricade(canvas, ResolveTerrainPalette(), RouteCatalog.Get(_activeRouteId)) });
        if (PaintedBase != null) BuildPaintedBaseGlow();
        InitializeStrongholdLife();
    }

    private void DrawBattleGroundShadows(CanvasItem canvas)
    {
        var light = FieldLighting;
        // The painted stronghold carries its own contact shadow; only the caravan casts one here.
        DrawStructureGround(canvas, WagonArt, WagonGround);
        foreach (var unit in _units)
            if (IsInstanceValid(unit) && unit.IsInsideTree() && unit.Visible && !unit.IsDead)
                unit.DrawGroundShadow(canvas, light);
        foreach (var child in GetChildren())
            if (child is UnitDeathVisual death && !death.IsQueuedForDeletion()) death.DrawGroundShadow(canvas, light);
    }

    private void DrawStructureGround(CanvasItem canvas, BattleStructureArt art, Vector2 ground)
    {
        var light = FieldLighting;
        var scale = _combat.StructureScale;
        if (art == null || art.Contacts.Length < 4)
        {
            light.DrawContact(canvas, ground + new Vector2(0, -5) * scale, new Vector2(67, 8) * scale, .55f);
            if (art == null) return;
        }
        var rect = art.At(Vector2.Zero);
        light.DrawShadow(canvas, art.Texture, new Rect2(Vector2.Zero, art.Texture.GetSize()), rect, ground, 1, 2.4f);
        if (art.Contacts.Length >= 4)
        {
            // The dark the body keeps under itself, from the far wheels to the near ones and a little past each end:
            // without it the wagon reads as hovering wherever the road is pale.
            Vector2 farRear = art.Point(ground, art.Contacts[0]), nearRear = art.Point(ground, art.Contacts[2]), nearFront = art.Point(ground, art.Contacts[3]);
            var half = (nearFront.X - nearRear.X) * .5f;
            var depth = Mathf.Max(3f, nearRear.Y - farRear.Y);
            // It lies a little toward the viewer, where the wheels and the hold don't hide it.
            var middle = new Vector2((nearRear.X + nearFront.X) * .5f + depth * .2f, nearRear.Y + depth * .1f);
            light.DrawContact(canvas, middle, new Vector2(half * 1.6f, depth * .9f), 1.6f);
            light.DrawContact(canvas, middle, new Vector2(half * 1.25f, depth * .55f), 1.4f);
        }
        foreach (var contact in art.Contacts)
            light.DrawContact(canvas, art.Point(ground, contact), new Vector2(9, 3.2f) * scale, 1.5f);
        foreach (var lamp in art.Lights) light.DrawLampGlow(canvas, art.Point(ground, lamp));
    }

    private void UpdateActorLighting()
    {
        var strength = FieldLighting.LampStrength;
        foreach (var unit in _units)
        {
            if (!IsInstanceValid(unit)) continue;
            var warm = Mathf.Max(0, 1 - unit.Position.DistanceTo(PlayerBaseCorePosition) / 60f) * strength;
            var cool = Mathf.Max(0, 1 - unit.Position.DistanceTo(EnemyBaseCorePosition) / 60f) * strength;
            unit.LocalLightTint = new Color(1 + warm * .15f - cool * .025f, 1 + warm * .06f + cool * .09f, 1 - warm * .025f + cool * .12f);
            var band = Mathf.Max(1f, (BattlefieldBottom - SpawnVerticalPadding) - (BattlefieldTop + SpawnVerticalPadding));
            unit.DepthInField = Mathf.Clamp(((BattlefieldBottom - SpawnVerticalPadding) - unit.Position.Y) / band, 0, 1);
        }
    }

    private Vector2 WagonMountPosition(int index)
    {
        if (WagonArt != null && WagonArt.Mounts.Length > 0)
            return WagonArt.Point(WagonGround, WagonArt.Mounts[Mathf.Clamp(index, 0, WagonArt.Mounts.Length - 1)]);
        return WagonGround + new Vector2(-20 + index * 27, -38);
    }
}
