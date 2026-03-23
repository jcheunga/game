using System;

public sealed class LiveConfig
{
	public string Announcement { get; set; } = "";
	public string Motd { get; set; } = "";
	public float GoldMultiplier { get; set; } = 1f;
	public float XPMultiplier { get; set; } = 1f;
	public string[] DisabledFeatureIds { get; set; } = Array.Empty<string>();
}

public static class LiveConfigService
{
	private static LiveConfig _cached = new();
	private static bool _loaded;

	public static LiveConfig Current => _cached;

	public static void SetConfig(LiveConfig config)
	{
		_cached = config ?? new LiveConfig();
		_loaded = true;
	}

	public static bool IsLoaded => _loaded;

	public static float GetGoldMultiplier() => _cached.GoldMultiplier;
	public static float GetXPMultiplier() => _cached.XPMultiplier;
	public static string GetAnnouncement() => _cached.Announcement ?? "";
	public static string GetMotd() => _cached.Motd ?? "";

	public static bool IsFeatureDisabled(string featureId)
	{
		if (_cached.DisabledFeatureIds == null) return false;
		foreach (var id in _cached.DisabledFeatureIds)
		{
			if (string.Equals(id, featureId, StringComparison.OrdinalIgnoreCase))
				return true;
		}
		return false;
	}

	public static void LoadDefaults()
	{
		_cached = new LiveConfig();
		_loaded = true;
	}
}
