using System.Collections.Generic;
using Godot;

public static class UiTextureLoader
{
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static readonly HashSet<string> Missing = new();

    private const string ScreenBackgroundPath = "res://assets/ui/backgrounds/";
    private const string MapBackgroundPath = "res://assets/map/backgrounds/";

    public static Texture2D TryLoadScreenBackground(string screenId, string variantId = "")
    {
        var normalizedScreenId = AssetCoverageCatalog.NormalizeId(screenId);
        if (string.IsNullOrWhiteSpace(normalizedScreenId))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(variantId))
        {
            var variantTexture = TryLoad(ScreenBackgroundPath, AssetCoverageCatalog.BuildScreenVariantId(normalizedScreenId, variantId));
            if (variantTexture != null)
            {
                return variantTexture;
            }
        }

        return TryLoad(ScreenBackgroundPath, normalizedScreenId);
    }

    public static Texture2D TryLoadMapBackground(string routeId)
    {
        return TryLoad(MapBackgroundPath, AssetCoverageCatalog.NormalizeId(routeId));
    }

    private static Texture2D TryLoad(string basePath, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var key = $"{basePath}{id}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (Missing.Contains(key))
        {
            return null;
        }

        var path = $"{basePath}{id}.png";
        if (!ResourceLoader.Exists(path))
        {
            Missing.Add(key);
            return null;
        }

        var texture = ResourceLoader.Load<Texture2D>(path);
        if (texture == null)
        {
            Missing.Add(key);
            return null;
        }

        Cache[key] = texture;
        return texture;
    }

    public static void ClearCache()
    {
        Cache.Clear();
        Missing.Clear();
    }
}
