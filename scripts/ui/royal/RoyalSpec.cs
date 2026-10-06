using System.Collections.Generic;
using Godot;

/// <summary>
/// Layout measured on a concept screen (art/royal/specs, installed to assets/ui/royal/specs). Screens
/// place live content by element id, so the code and the measured concept cannot drift apart.
/// </summary>
public sealed class RoyalSpec
{
    private static readonly Dictionary<string, RoyalSpec> Cache = new();
    private readonly Dictionary<string, Godot.Collections.Dictionary> _elements = new();
    public string Screen { get; }

    private RoyalSpec(string screen)
    {
        Screen = screen;
        var path = $"res://assets/ui/royal/specs/{screen}.json";
        if (!FileAccess.FileExists(path)) { GD.PushWarning($"Royal spec missing: {path}"); return; }
        var json = Json.ParseString(FileAccess.GetFileAsString(path)).AsGodotDictionary();
        foreach (var item in json["elements"].AsGodotArray())
        {
            var element = item.AsGodotDictionary();
            _elements[Key(element["id"].AsString())] = element;
        }
    }

    private static string Key(string id) => id.ToLowerInvariant().Replace(' ', '_');

    public static RoyalSpec For(string screen) => Cache.TryGetValue(screen, out var spec) ? spec : Cache[screen] = new RoyalSpec(screen);

    public bool Has(string id) => _elements.ContainsKey(Key(id));

    public Rect2 Rect(string id)
    {
        if (!_elements.TryGetValue(Key(id), out var element) || !element.ContainsKey("rect")) { GD.PushWarning($"{Screen}: no rect '{id}'"); return new Rect2(); }
        var r = element["rect"].AsGodotArray();
        return new Rect2((float)r[0].AsDouble(), (float)r[1].AsDouble(), (float)r[2].AsDouble(), (float)r[3].AsDouble());
    }

    public Rect2 Rect(string id, Rect2 fallback) => Has(id) ? Rect(id) : fallback;

    /// <summary>The first id the spec has, for screens whose concepts name the same element differently.</summary>
    public string First(params string[] ids)
    {
        foreach (var id in ids) if (Has(id)) return id;
        return ids[0];
    }

    /// <summary>A polyline element (callout leader lines).</summary>
    public Vector2[] Points(string id)
    {
        if (!_elements.TryGetValue(Key(id), out var element) || !element.ContainsKey("points")) return System.Array.Empty<Vector2>();
        var list = new System.Collections.Generic.List<Vector2>();
        foreach (var point in element["points"].AsGodotArray())
        {
            var pair = point.AsGodotArray();
            list.Add(new Vector2((float)pair[0].AsDouble(), (float)pair[1].AsDouble()));
        }
        return list.ToArray();
    }

    public float Number(string id, string key, float fallback = 0)
    {
        if (!_elements.TryGetValue(Key(id), out var element) || !element.ContainsKey(key)) return fallback;
        return (float)element[key].AsDouble();
    }

    /// <summary>
    /// A label with the measured font, size, colour and baseline. Left-aligned text starts at the
    /// measured pen position; centred and right-aligned text keep their measured centre or edge.
    /// <paramref name="room"/> is the width the text may use before it shrinks (defaults generously).
    /// </summary>
    public RoyalLabel Label(string id, string text = null, float room = -1, Color? ink = null)
    {
        if (!_elements.TryGetValue(Key(id), out var e)) { GD.PushWarning($"{Screen}: no text '{id}'"); return new RoyalLabel { Text = text ?? "" }; }
        var size = (int)Mathf.Round((float)e["size"].AsDouble());
        var cinzel = e.ContainsKey("font") && e["font"].AsString() == "cinzel";
        var weight = e.ContainsKey("weight") ? e["weight"].AsInt32() : 600;
        var gold = e.ContainsKey("style") && e["style"].AsString() == "gold";
        var label = gold ? RoyalText.Title(text ?? e["text"].AsString(), size)
            : cinzel ? RoyalText.Caps(text ?? e["text"].AsString(), size, null, weight)
            : RoyalText.Serif(text ?? e["text"].AsString(), size, null, weight);
        if (gold) label.Font = RoyalFonts.Display(weight);
        if (!gold) label.Ink = ink ?? (e.ContainsKey("color") ? new Color(e["color"].AsString()) : RoyalText.Cream);
        if (e.ContainsKey("tracking")) label.Tracking = (float)e["tracking"].AsDouble();
        var align = e.ContainsKey("align") ? e["align"].AsString() : "left";
        var baseline = (float)e["baseline"].AsDouble();
        var x = (float)(e.ContainsKey("pen_x") && align == "left" ? e["pen_x"].AsDouble() : e["x"].AsDouble());
        if (room < 0) room = Mathf.Max(size * 14f, 60);
        var height = size * 1.6f;
        label.Baseline = size * 1.1f;
        label.Align = align switch { "center" => HorizontalAlignment.Center, "right" => HorizontalAlignment.Right, _ => HorizontalAlignment.Left };
        var left = align switch { "center" => x - room / 2, "right" => x - room, _ => x };
        label.Position = new Vector2(left, baseline - label.Baseline);
        label.Size = new Vector2(room, height);
        return label;
    }
}
