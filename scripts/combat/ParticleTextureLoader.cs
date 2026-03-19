using System.Collections.Generic;
using Godot;

public static class ParticleTextureLoader
{
	private static readonly Dictionary<string, Texture2D> Cache = new();
	private static readonly HashSet<string> Missing = new();
	private const string ParticlePath = "res://assets/particles/";

	public static Texture2D TryLoad(string textureId)
	{
		if (string.IsNullOrWhiteSpace(textureId))
			return null;

		if (Cache.TryGetValue(textureId, out var cached))
			return cached;
		if (Missing.Contains(textureId))
			return null;

		var path = $"{ParticlePath}{textureId}.png";
		if (!ResourceLoader.Exists(path))
		{
			Missing.Add(textureId);
			return null;
		}

		var texture = ResourceLoader.Load<Texture2D>(path);
		if (texture == null)
		{
			Missing.Add(textureId);
			return null;
		}

		Cache[textureId] = texture;
		return texture;
	}

	public static void ClearCache()
	{
		Cache.Clear();
		Missing.Clear();
	}
}
