using System.Collections.Generic;
using Godot;

public static class BattlefieldTextureLoader
{
	private static readonly Dictionary<string, Texture2D> Cache = new();
	private static readonly HashSet<string> Missing = new();

	private const string BackgroundPath = "res://assets/backgrounds/";
	private const string StructurePath = "res://assets/structures/";

	public static Texture2D TryLoadBackground(string terrainId)
	{
		return TryLoad(BackgroundPath, terrainId);
	}

	public static Texture2D TryLoadStructure(string structureId)
	{
		return TryLoad(StructurePath, structureId);
	}

	private static Texture2D TryLoad(string basePath, string id)
	{
		if (string.IsNullOrWhiteSpace(id))
			return null;

		var key = $"{basePath}{id}";
		if (Cache.TryGetValue(key, out var cached))
			return cached;
		if (Missing.Contains(key))
			return null;

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
