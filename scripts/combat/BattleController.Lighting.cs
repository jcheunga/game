using Godot;

public partial class BattleController
{
    private BattleShadowCanvas _shadowCanvas;
    private BattleLighting FieldLighting => BattleLighting.ForZone(_activeRouteId);
    private BattleStructureArt WagonArt => BattleStructureArt.Load(GameState.Instance.SelectedWagonSkinId == WagonSkinCatalog.DefaultSkinId
        ? "war_wagon" : "war_wagon_" + GameState.Instance.SelectedWagonSkinId) ?? BattleStructureArt.Load("war_wagon");
    private BattleStructureArt CastleArt => BattleStructureArt.Load("gatehouse");

    private void InitializeBattlePresentation()
    {
        _shadowCanvas = new BattleShadowCanvas { Name = "BattleShadows", ZIndex = -5,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps, Paint = DrawBattleGroundShadows };
        AddChild(_shadowCanvas);
        AddChild(new BattleBaseCanvas { Name = "Caravan", Position = PlayerBaseCorePosition,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Paint = canvas => { DrawPlayerBus(canvas, ResolveTerrainPalette(), RouteCatalog.Get(_activeRouteId)); DrawBaseArmaments(canvas, true); } });
        AddChild(new BattleBaseCanvas { Name = "Castle", Position = EnemyBaseCorePosition,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Paint = canvas => { DrawEnemyBarricade(canvas, ResolveTerrainPalette(), RouteCatalog.Get(_activeRouteId)); DrawBaseArmaments(canvas, false); } });
    }

    private void DrawBattleGroundShadows(CanvasItem canvas)
    {
        var light = FieldLighting;
        DrawStructureGround(canvas, WagonArt, PlayerBaseCorePosition, false);
        DrawStructureGround(canvas, CastleArt, EnemyBaseCorePosition, true);
        foreach (var unit in _units)
            if (IsInstanceValid(unit) && unit.IsInsideTree() && unit.Visible && !unit.IsDead)
                unit.DrawGroundShadow(canvas, light);
        foreach (var child in GetChildren())
            if (child is UnitDeathVisual death && !death.IsQueuedForDeletion()) death.DrawGroundShadow(canvas, light);
    }

    private void DrawStructureGround(CanvasItem canvas, BattleStructureArt art, Vector2 ground, bool castle)
    {
        var light = FieldLighting;
        light.DrawContact(canvas, ground + new Vector2(0, -5), new Vector2(castle ? 66 : 67, castle ? 12 : 8), .55f);
        if (art == null) return;
        var rect = art.At(Vector2.Zero);
        light.DrawShadow(canvas, art.Texture, new Rect2(Vector2.Zero, art.Texture.GetSize()), rect, ground, 1, 2.4f);
        foreach (var contact in art.Contacts)
            light.DrawContact(canvas, art.Point(ground, contact), new Vector2(castle ? 19 : 7, castle ? 5 : 2.8f), .8f);
        foreach (var lamp in art.Lights) light.DrawLampGlow(canvas, art.Point(ground, lamp), castle);
    }

    private void UpdateActorLighting()
    {
        var strength = FieldLighting.LampStrength;
        foreach (var unit in _units)
        {
            if (!IsInstanceValid(unit)) continue;
            var warm = Mathf.Max(0, 1 - unit.Position.DistanceTo(PlayerBaseCorePosition) / 120f) * strength;
            var cool = Mathf.Max(0, 1 - unit.Position.DistanceTo(EnemyBaseCorePosition) / 120f) * strength;
            unit.LocalLightTint = new Color(1 + warm * .15f - cool * .025f, 1 + warm * .06f + cool * .09f, 1 - warm * .025f + cool * .12f);
        }
    }

    private Vector2 BaseMountPosition(bool player, int index = 0)
    {
        var art = player ? WagonArt : CastleArt;
        var ground = player ? PlayerBaseCorePosition : EnemyBaseCorePosition;
        if (art != null && art.Mounts.Length > 0)
            return art.Point(ground, art.Mounts[Mathf.Clamp(index, 0, art.Mounts.Length - 1)]);
        return ground + (player ? new Vector2(-20 + index * 27, -38) : new Vector2(32, -62));
    }
}
