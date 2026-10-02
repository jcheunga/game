using System;
using System.Linq;
using Godot;

public partial class MapPathCanvas
{
    private Color TileMistColor() => new(ActiveMapId switch {
        "foundry" => "282a2e", "quarantine" => "202f30", "thornwall" => "28333d", "basilica" => "282f39",
        "mire" => "1e3030", "steppe" => "323138", "gloamwood" => "1d2b35", "citadel" => "252b36", _ => "21343e" });
    private Color TileGroundColor() => new(ActiveMapId switch {
        "harbor" => "8c9e91", "foundry" => "9b7250", "quarantine" => "7f8d72", "thornwall" => "a4aaa0",
        "basilica" => "b4ab8e", "mire" => "647e62", "steppe" => "b29e66", "gloamwood" => "657d70", "citadel" => "8b8d96", _ => "8f9f69" });
    private Color TileRoofColor() => new(ActiveMapId switch {
        "harbor" => "4b7376", "foundry" => "763f2a", "quarantine" => "526357", "thornwall" => "546475",
        "basilica" => "a39a77", "mire" => "514f42", "steppe" => "97613d", "gloamwood" => "475578", "citadel" => "665d75", _ => "8c5741" });
    private void DrawTree(Vector2 p, Color color, float height, uint hash)
    {
        DrawColoredPolygon(new[] { p + new Vector2(-12, 3), p + new Vector2(12, 3), p + new Vector2(24, 9), p + new Vector2(0, 12) }, new Color(0, 0, 0, .15f));
        DrawLine(p, p - new Vector2(0, height * .7f), color.Darkened(.45f), 3, true);
        for (var tier = 0; tier < 3; tier++)
        {
            var y = -height + tier * height * .22f; var w = height * (.28f + tier * .1f);
            DrawColoredPolygon(new[] { p + new Vector2(0, y), p + new Vector2(w, y + height * .43f), p + new Vector2(-w, y + height * .43f) }, color.Lightened(tier * .055f));
            DrawLine(p + new Vector2(0, y), p + new Vector2(-w, y + height * .43f), color.Lightened(.19f), 1, true);
        }
    }
    private void DrawTileLandmark(AdventureTile tile, bool complete)
    {
        if (tile.Site != null && !string.IsNullOrEmpty(tile.Site.RequiredVisit) && !GameState.Instance.HasVisitedAdventureSite(tile.Site.RequiredVisit)) return;
        var p = tile.Point + new Vector2(0, 14); var roof = TileRoofColor();
        if (tile.Site?.Kind == AdventureSiteKind.Leader
            && DrawPaintedSprite(GameState.Instance.IsAdventureBoss(tile.Site.Stage) ? 16 : 15, p, GameState.Instance.IsAdventureBoss(tile.Site.Stage) ? 126 : 92)) return;
        if (tile.Site?.Kind == AdventureSiteKind.Camp && DrawPaintedSprite(19, p, 70)) return;
        if (tile.Site?.Kind == AdventureSiteKind.Watchtower && DrawPaintedSprite(17, p, 83)) return;
        if (tile.Site?.Kind == AdventureSiteKind.Shrine && DrawPaintedSprite(18, p, 76)) return;
        if (tile.Site?.Kind == AdventureSiteKind.Leader)
        {
            var boss = GameState.Instance.IsAdventureBoss(tile.Site.Stage);
            DrawFort(p, roof, boss, complete);
        }
        else if (tile.Site?.Kind == AdventureSiteKind.Camp)
        {
            DrawTent(p + new Vector2(-24, 4), new Color("d0bd8e"));
            DrawTent(p + new Vector2(29, -6), new Color("bca97e"));
            DrawCircle(p + new Vector2(0, 17), 7, new Color("7c4a2c"));
            DrawColoredPolygon(new[] { p + new Vector2(-4, 18), p + new Vector2(0, 5), p + new Vector2(6, 18) }, new Color("e4ae59"));
            DrawBanner(p + new Vector2(29, -25), new Color("476a85"));
        }
        else if (tile.Site?.Kind == AdventureSiteKind.Watchtower)
        {
            DrawTower(p, 60, roof);
            DrawBanner(p - new Vector2(0, 69), new Color("bd9b58"));
        }
        else if (tile.Site?.Kind == AdventureSiteKind.Shrine)
        {
            DrawIsoBlock(p, 29, 16, 7, new Color("797e6c"));
            DrawIsoBlock(p + new Vector2(-18, -4), 6, 5, 38, new Color("a4a28b"));
            DrawIsoBlock(p + new Vector2(18, -4), 6, 5, 38, new Color("a4a28b"));
            DrawIsoBlock(p - new Vector2(0, 39), 24, 6, 8, new Color("beb99b"));
            DrawCircle(p - new Vector2(0, 15), 6, new Color(complete ? "aacd8e" : "9aaea0"));
        }
        else if (!complete && (tile.Site != null || tile.Discovery != null))
        {
            var foodCache = tile.Site?.Kind == AdventureSiteKind.Food || tile.Discovery?.Kind == AdventureDiscoveryKind.Food;
            if (DrawPaintedSprite(foodCache ? 21 : 20, p, foodCache ? 41 : 31)) return;
            if (tile.Site?.Kind == AdventureSiteKind.Food || tile.Discovery?.Kind == AdventureDiscoveryKind.Food) DrawSupplyWagon(p);
            else
            {
                DrawIsoBlock(p + new Vector2(-11, 2), 20, 12, 16, new Color("816044"));
                DrawIsoBlock(p + new Vector2(22, -5), 11, 7, 12, new Color("9c7951"));
                DrawLine(p + new Vector2(-21, -15), p + new Vector2(-2, -6), new Color("c0ad75"), 3, true);
                if (tile.Discovery?.Kind is AdventureDiscoveryKind.Tomes or AdventureDiscoveryKind.Essence or AdventureDiscoveryKind.Survey)
                    DrawCircle(p + new Vector2(-7, -23), 4, new Color(tile.Discovery.Kind == AdventureDiscoveryKind.Essence ? "98b6be" : "c6b388"));
            }
        }
        else if (complete && (tile.Site == null || tile.Site.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food))
        {
            DrawLine(p + new Vector2(-18, 1), p + new Vector2(15, 15), TileGroundColor().Darkened(.25f), 2, true);
            DrawLine(p + new Vector2(-6, -3), p + new Vector2(19, 6), TileGroundColor().Darkened(.18f), 2, true);
        }
    }
    private void DrawIsoBlock(Vector2 p, float w, float d, float h, Color color)
    {
        var a = p + new Vector2(-w, 0); var b = p + new Vector2(0, d); var c = p + new Vector2(w, 0); var e = p - new Vector2(0, d);
        var lift = new Vector2(0, h);
        DrawColoredPolygon(new[] { a, b, b - lift, a - lift }, color.Darkened(.22f));
        DrawColoredPolygon(new[] { b, c, c - lift, b - lift }, color.Darkened(.38f));
        DrawColoredPolygon(new[] { a - lift, b - lift, c - lift, e - lift }, color.Lightened(.13f));
        DrawPolyline(new[] { a - lift, e - lift, c - lift }, color.Lightened(.28f), 1, true);
    }
    private void DrawTower(Vector2 p, float height, Color roof)
    {
        var stone = new Color(ActiveMapId is "foundry" or "citadel" ? "817b73" : "b7b299");
        DrawIsoBlock(p, 16, 10, height, stone);
        var top = p - new Vector2(0, height);
        DrawColoredPolygon(new[] { top + new Vector2(-22, 1), top + new Vector2(0, -23), top + new Vector2(22, 1), top + new Vector2(0, 11) }, roof);
        DrawLine(top + new Vector2(-22, 1), top - new Vector2(0, 23), roof.Lightened(.28f), 1.5f, true);
        DrawLine(p + new Vector2(4, -height * .65f), p + new Vector2(4, -height * .48f), new Color("313a35"), 5, true);
        for (var i = 1; i <= 3; i++) DrawLine(p + new Vector2(-14, -i * height / 5), p + new Vector2(-1, 8 - i * height / 5), stone.Darkened(.32f), 1, true);
    }
    private void DrawFort(Vector2 p, Color roof, bool boss, bool complete)
    {
        var stone = new Color(ActiveMapId is "foundry" or "citadel" ? "7d7871" : "aaa78d");
        DrawColoredPolygon(new[] { p + new Vector2(-69, 9), p + new Vector2(0, -27), p + new Vector2(75, 12), p + new Vector2(4, 48) }, new Color(0, 0, 0, .17f));
        DrawIsoBlock(p, 54, 29, 22, stone);
        DrawIsoBlock(p - new Vector2(0, 10), 30, 18, boss ? 62 : 45, stone);
        var top = p - new Vector2(0, boss ? 72 : 55);
        DrawColoredPolygon(new[] { top + new Vector2(-35, 0), top + new Vector2(0, -24), top + new Vector2(35, 0), top + new Vector2(0, 20) }, roof);
        DrawLine(top - new Vector2(35, 0), top - new Vector2(0, 24), roof.Lightened(.32f), 2, true);
        DrawTower(p + new Vector2(-46, 1), boss ? 57 : 40, roof);
        DrawTower(p + new Vector2(46, 1), boss ? 57 : 40, roof);
        DrawLine(p + new Vector2(6, 20), p + new Vector2(6, 0), new Color("343b35"), 10, true);
        DrawLine(p + new Vector2(3, -32), p + new Vector2(3, -19), new Color("39433c"), 4, true);
        DrawBanner(p + new Vector2(-33, -20), complete ? new Color("577a8b") : new Color("874e40"));
        if (ActiveMapId == "foundry")
        {
            DrawIsoBlock(p + new Vector2(25, -19), 6, 4, 59, new Color("504f48"));
            DrawCircle(p + new Vector2(25, -80), 8, new Color("a1846355"));
            DrawCircle(p + new Vector2(32, -93), 10, new Color("a1846333"));
        }
    }
    private void DrawBanner(Vector2 p, Color color)
    {
        DrawLine(p + new Vector2(0, 16), p - new Vector2(0, 25), new Color("65543b"), 2, true);
        DrawColoredPolygon(new[] { p - new Vector2(0, 25), p + new Vector2(23, -18), p + new Vector2(20, -4), p + new Vector2(0, -10) }, color);
        DrawLine(p + new Vector2(3, -22), p + new Vector2(20, -17), color.Lightened(.25f), 1, true);
    }
    private void DrawTent(Vector2 p, Color color)
    {
        DrawColoredPolygon(new[] { p + new Vector2(-27, 8), p + new Vector2(0, -34), p + new Vector2(30, 8), p + new Vector2(0, 22) }, color);
        DrawColoredPolygon(new[] { p + new Vector2(0, -34), p + new Vector2(30, 8), p + new Vector2(0, 22) }, color.Darkened(.25f));
        DrawColoredPolygon(new[] { p + new Vector2(-10, 16), p + new Vector2(0, -6), p + new Vector2(9, 15) }, color.Darkened(.55f));
    }
    private void DrawSupplyWagon(Vector2 p)
    {
        DrawIsoBlock(p, 27, 13, 12, new Color("816445"));
        DrawColoredPolygon(new[] { p + new Vector2(-28, -10), p + new Vector2(-9, -30), p + new Vector2(25, -13), p + new Vector2(27, 0), p + new Vector2(1, 7) }, new Color("c0b18c"));
        foreach (var x in new[] { -19, 20 }) { DrawCircle(p + new Vector2(x, 12), 8, new Color("463f32")); DrawCircle(p + new Vector2(x, 12), 5, new Color("93805a")); DrawLine(p + new Vector2(x - 4, 12), p + new Vector2(x + 4, 12), new Color("554637"), 1, true); }
    }
    private void DrawCaravan(Vector2 p, bool moving)
    {
        var bob = moving && !GameState.Instance.ReducedMotion ? Mathf.Sin(_time * 14) * 2 : 0;
        p.Y += bob;
        DrawColoredPolygon(new[] { p + new Vector2(-22, 10), p + new Vector2(6, -3), p + new Vector2(32, 12), p + new Vector2(5, 26) }, new Color(0, 0, 0, .3f));
        DrawIsoBlock(p, 20, 12, 14, new Color("6b563e"));
        DrawColoredPolygon(new[] { p + new Vector2(-21, -13), p + new Vector2(-4, -29), p + new Vector2(22, -14), p + new Vector2(22, 1), p + new Vector2(0, 10) }, new Color("c9b992"));
        DrawBanner(p + new Vector2(-13, -14), new Color("456f8b"));
        DrawCircle(p + new Vector2(-14, 11), 7, new Color("332f29")); DrawCircle(p + new Vector2(17, 11), 7, new Color("332f29"));
        DrawCircle(p + new Vector2(-14, 11), 3, new Color("b39a65")); DrawCircle(p + new Vector2(17, 11), 3, new Color("b39a65"));
        DrawCircle(p + new Vector2(21, -4), 3, new Color("f0cb73"));
    }
}
