using System.Collections.Generic;
using Godot;

/// <summary>Engine-rendered review sheet, showing the same floor mapping used by battles.</summary>
public partial class WorldArtGallery : Control
{
    public List<(int Stage, Texture2D Texture, string Title, Rect2 Floor)> Entries { get; } = new();
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("102027"));
        var font = ThemeDB.FallbackFont;
        for (var i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            var box = new Vector2(16 + i % 4 * 316, 16 + i / 4 * 232);
            DrawString(font, box + new Vector2(0,19), $"{entry.Stage:00} · {entry.Title}", fontSize:15, modulate:new Color("e9d6ae"));
            DrawTextureRect(entry.Texture, new Rect2(box + new Vector2(0,30),new Vector2(300,300 * entry.Texture.GetHeight() / (float)entry.Texture.GetWidth())), false);
            DrawString(font, box + new Vector2(0,135), "Playable ground", fontSize:12, modulate:new Color("99b5b5"));
            DrawTextureRectRegion(entry.Texture, new Rect2(box + new Vector2(0,145),new Vector2(300,73)),
                new Rect2(entry.Texture.GetSize() * entry.Floor.Position, entry.Texture.GetSize() * entry.Floor.Size));
        }
    }
    public override void _ExitTree() => Entries.Clear();
}
