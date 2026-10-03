using Godot;

public partial class BattleController
{
    private NoiseTexture2D _groundTexture;
    private Texture2D _stageArtwork;
    private bool _stageArtworkChecked;
    private BattleTerrainCanvas _terrainCanvas;
    private void DrawPlayableTerrain(TerrainPalette palette)
    {
        if (!_stageArtworkChecked)
        {
            _stageArtwork = WorldEnvironmentArt.LoadBattle(_stageData?.StageNumber ?? 1);
            _stageArtworkChecked = true;
            // The legacy fallback is painted on the parent. Its shadow layer must
            // follow that paint, while authored terrain lives behind both passes.
            if (_shadowCanvas != null) _shadowCanvas.ZIndex = _stageArtwork != null ? -5 : 0;
        }
        if (_stageArtwork != null)
        {
            if (_terrainCanvas == null)
            {
                _terrainCanvas = new BattleTerrainCanvas { Name = "BattleTerrain", ShowBehindParent = true, ZIndex = -10,
                    TextureFilter = TextureFilterEnum.LinearWithMipmaps, Artwork = _stageArtwork,
                    Backdrop = palette.SkyColor.Darkened(.55f),
                    Ground = new Rect2(BattlefieldLeft, BattlefieldTop, BattlefieldRight - BattlefieldLeft, BattlefieldBottom - BattlefieldTop) };
                AddChild(_terrainCanvas); _terrainCanvas.AddGroundDetail(_activeRouteId, _stage);
            }
            _terrainCanvas.QueueRedraw();
            return;
        }
        var background = BattlefieldTextureLoader.TryLoadBackground((_stageData?.TerrainId ?? "urban").ToLowerInvariant());
        if (background != null) DrawBattleBackground(background);
        else DrawRect(new Rect2(0,0,BattleWorldWidth,BattleWorldHeight), palette.SkyColor.Darkened(.35f));
        var earth = new Color(_activeRouteId switch {
            "harbor" => "63706c", "foundry" => "715c4a", "quarantine" => "646453",
            "thornwall" => "647363", "basilica" => "716d61", "mire" => "46553c",
            "steppe" => "768253", "gloamwood" => "465e42", "citadel" => "626b6d", _ => "6b7050"
        });
        if (_groundTexture == null)
        {
            var noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = .022f,
                FractalOctaves = 3, Seed = 905 };
            var gradient = new Gradient(); gradient.SetColor(0, earth.Darkened(.06f)); gradient.SetColor(1, earth.Lightened(.07f));
            _groundTexture = new NoiseTexture2D { Width = 256, Height = 256, Noise = noise, ColorRamp = gradient, Seamless = true };
            TextureRepeat = TextureRepeatEnum.Enabled;
        }
        var ground = new Rect2(BattlefieldLeft, BattlefieldTop, BattlefieldRight - BattlefieldLeft, BattlefieldBottom - BattlefieldTop);
        DrawRect(ground, earth);
        DrawTextureRect(_groundTexture, ground, true);
        for (var i = 0; i < 190; i++)
        {
            var x = BattlefieldLeft + 18 + (i * 173.7f % (ground.Size.X - 36));
            var y = BattlefieldTop + 16 + (i * 53.3f % (ground.Size.Y - 32));
            var point = new Vector2(x,y);
            if (i % 4 == 0)
                DrawCircle(point, 1.8f, earth.Lightened(.1f));
            else
            {
                var ink = earth.Darkened(.1f);
                DrawLine(point, point + new Vector2(-2,-4), ink, 1, true);
                DrawLine(point, point + new Vector2(2,-5), ink, 1, true);
            }
        }
        // All playable ground stays unobstructed. Landscape art is confined to the margins.
        DrawRect(new Rect2(BattlefieldLeft, BattlefieldTop - 6, ground.Size.X, 6), earth.Darkened(.2f));
        DrawRect(new Rect2(BattlefieldLeft, BattlefieldBottom, ground.Size.X, 6), earth.Darkened(.2f));
    }
}
