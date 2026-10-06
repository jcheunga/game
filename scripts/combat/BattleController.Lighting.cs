using Godot;

public partial class BattleController
{
    private BattleShadowCanvas _shadowCanvas;
    private BattleLighting FieldLighting => BattleLighting.ForZone(_activeRouteId);
    private BattleStructureArt WagonArt => BattleStructureArt.Load(GameState.Instance.SelectedWagonSkinId == WagonSkinCatalog.DefaultSkinId
        ? "war_wagon" : "war_wagon_" + GameState.Instance.SelectedWagonSkinId) ?? BattleStructureArt.Load("war_wagon");
    private BattleStructureArt CastleArt => BattleStructureArt.Load("gatehouse");
    // The stronghold straddles the band: its visual mass sits on the centre line, where troops
    // strike it, instead of standing on that line with its whole body above the field.
    private Vector2 CastleGround => CastleArt is { } art
        ? EnemyBaseCorePosition + new Vector2(0, (art.Anchor.Y - art.Centre.Y) * art.Size.Y) : EnemyBaseCorePosition;

    private void InitializeBattlePresentation()
    {
        _shadowCanvas = new BattleShadowCanvas { Name = "BattleShadows", ZIndex = -5,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps, Paint = DrawBattleGroundShadows };
        AddChild(_shadowCanvas);
        AddChild(new BattleBaseCanvas { Name = "Caravan", Position = WagonSortPosition,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Paint = canvas => { DrawPlayerBus(canvas, ResolveTerrainPalette(), RouteCatalog.Get(_activeRouteId)); DrawWagonArmaments(canvas); } });
        AddChild(new BattleBaseCanvas { Name = "Castle", Position = CastleGround,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Paint = canvas => DrawEnemyBarricade(canvas, ResolveTerrainPalette(), RouteCatalog.Get(_activeRouteId)) });
        BuildOutworks();
    }

    private void DrawBattleGroundShadows(CanvasItem canvas)
    {
        var light = FieldLighting;
        DrawStructureGround(canvas, WagonArt, WagonGround, false);
        DrawStructureGround(canvas, CastleArt, CastleGround, true);
        DrawOutworkGround(canvas);
        foreach (var unit in _units)
            if (IsInstanceValid(unit) && unit.IsInsideTree() && unit.Visible && !unit.IsDead)
                unit.DrawGroundShadow(canvas, light);
        foreach (var child in GetChildren())
            if (child is UnitDeathVisual death && !death.IsQueuedForDeletion()) death.DrawGroundShadow(canvas, light);
    }

    private void DrawStructureGround(CanvasItem canvas, BattleStructureArt art, Vector2 ground, bool castle)
    {
        var light = FieldLighting;
        var scale = _combat.StructureScale;
        light.DrawContact(canvas, ground + new Vector2(0, -5) * scale, new Vector2(castle ? 66 : 67, castle ? 12 : 8) * scale, .55f);
        if (art == null) return;
        var rect = art.At(Vector2.Zero);
        light.DrawShadow(canvas, art.Texture, new Rect2(Vector2.Zero, art.Texture.GetSize()), rect, ground, 1, 2.4f);
        foreach (var contact in art.Contacts)
            light.DrawContact(canvas, art.Point(ground, contact), new Vector2(castle ? 19 : 7, castle ? 5 : 2.8f) * scale, .8f);
        foreach (var lamp in art.Lights) light.DrawLampGlow(canvas, art.Point(ground, lamp), castle);
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
        }
    }

    private Vector2 WagonMountPosition(int index)
    {
        if (WagonArt != null && WagonArt.Mounts.Length > 0)
            return WagonArt.Point(WagonGround, WagonArt.Mounts[Mathf.Clamp(index, 0, WagonArt.Mounts.Length - 1)]);
        return WagonGround + new Vector2(-20 + index * 27, -38);
    }
}
