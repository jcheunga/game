using Godot;
using System.Collections.Generic;

public static class AdventureMapArt
{
    private static readonly Dictionary<string, Texture2D> Terrains = new();
    private static Texture2D _portraits;
    private static Texture2D _miniatures;
    private static readonly Texture2D[] Pieces = new Texture2D[6];
    private static readonly Texture2D[] Leaders = new Texture2D[6];
    public static Texture2D TerrainForMap(string mapId)
    {
        var biome = mapId switch { "harbor" => "coast", "foundry" or "citadel" => "ashlands",
            "thornwall" => "highlands", "quarantine" or "mire" or "gloamwood" => "marsh", _ => "kingdom_overworld" };
        if (!Terrains.TryGetValue(biome, out var terrain)) Terrains[biome] = terrain = ResourceLoader.Load<Texture2D>($"res://assets/map/adventure/{biome}.png");
        return terrain;
    }
    public static Texture2D Miniature(AdventureSiteKind kind) => Piece(kind switch {
        AdventureSiteKind.Gold => 0, AdventureSiteKind.Food => 1, AdventureSiteKind.Watchtower => 2,
        AdventureSiteKind.Shrine => 3, AdventureSiteKind.Camp => 4, _ => 5 });
    public static Texture2D Piece(int index)
    {
        _miniatures ??= ResourceLoader.Load<Texture2D>("res://assets/map/adventure/landmark_miniatures.png");
        var cell = _miniatures.GetSize() / new Vector2(3, 2);
        return Pieces[index] ??= new AtlasTexture { Atlas = _miniatures, Region = new Rect2(new Vector2(index % 3, index / 3) * cell, cell) };
    }
    public static Texture2D Leader(int index)
    {
        index = Mathf.PosMod(index, 6);
        _portraits ??= ResourceLoader.Load<Texture2D>("res://assets/map/adventure/rival_leaders.png");
        var cell = _portraits.GetSize() / new Vector2(3, 2);
        return Leaders[index] ??= new AtlasTexture { Atlas = _portraits, Region = new Rect2(new Vector2(index % 3, index / 3) * cell, cell) };
    }
}
