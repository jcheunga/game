using Godot;

public partial class BattleController
{
    private Texture2D _stageArtwork;
    private bool _stageArtworkChecked;
    private ZoneBackdrop _stageBackdrop;
    private BattleTerrainCanvas _terrainCanvas;
    private void DrawPlayableTerrain(TerrainPalette palette)
    {
        if (!_stageArtworkChecked)
        {
            _stageBackdrop = WorldEnvironmentArt.LoadZoneBackdrop(_activeRouteId);
            _stageArtwork = _stageBackdrop?.Near;
            _stageArtworkChecked = true;
            // The painted layers live behind both passes, so the shadow layer sits just above them.
            if (_shadowCanvas != null) _shadowCanvas.ZIndex = _stageBackdrop != null ? -5 : 0;
        }
        if (_stageBackdrop == null)
        {
            // Every zone ships a painted backdrop; a missing one leaves a plain field rather than failing.
            DrawSetTransformMatrix(GetGlobalTransformWithCanvas().AffineInverse());
            DrawRect(GetViewportRect(), palette.SkyColor.Darkened(.35f));
            DrawSetTransform(Vector2.Zero);
            return;
        }
        if (_terrainCanvas == null)
        {
            var centre = (BattlefieldLeft + BattlefieldRight) * .5f;
            _terrainCanvas = new BattleTerrainCanvas { Name = "BattleTerrain", ShowBehindParent = true, ZIndex = -10,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps, Layers = _stageBackdrop, WorldCentreX = centre };
            AddChild(_terrainCanvas);
            if (System.Linq.Enumerable.Any(_stageBackdrop.Layers, layer => layer.Front))
                AddChild(new BattleTerrainCanvas { Name = "BattleForeground", ZIndex = 60, FrontLayers = true, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
                    Layers = _stageBackdrop, WorldCentreX = centre });
        }
        _terrainCanvas.QueueRedraw();
    }
}
