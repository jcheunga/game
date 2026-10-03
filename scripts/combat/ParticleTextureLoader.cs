using System.Collections.Generic;
using Godot;

public static class ParticleTextureLoader
{
	private static readonly Dictionary<string, Texture2D> Cache = new();
	private static readonly HashSet<string> Missing = new();
	private const string ParticlePath = "res://assets/particles/";
	private static Texture2D _softFallback;
	public static Texture2D SoftTexture => TryLoad("particle_soft") ?? (_softFallback ??= new GradientTexture2D {
		Width = 32, Height = 32, Fill = GradientTexture2D.FillEnum.Radial,
		FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f),
		Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) }, Offsets = new[] { 0f, 1f } }
	});

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
