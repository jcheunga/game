using System.Collections.Generic;
using Godot;

public enum UnitAnimState
{
	Idle,
	Walk,
	Attack,
	Hit,
	Death,
	Deploy
}

public sealed class UnitSpriteSheet
{
	public Texture2D Texture { get; set; }
	public int FrameWidth { get; set; }
	public int FrameHeight { get; set; }
	public Dictionary<UnitAnimState, SpriteAnimRange> Animations { get; set; } = new();
}

public sealed class SpriteAnimRange
{
	public int StartFrame { get; set; }
	public int FrameCount { get; set; }
	public float FrameDuration { get; set; } = 0.12f;
	public bool Loop { get; set; } = true;
}

public static class UnitSpriteLoader
{
	private static readonly Dictionary<string, UnitSpriteSheet> Cache = new();
	private static readonly HashSet<string> MissingIds = new();

	private const string SpritePath = "res://assets/units/";

	public static UnitSpriteSheet TryLoad(string visualClass)
	{
		if (string.IsNullOrWhiteSpace(visualClass))
			return null;

		if (Cache.TryGetValue(visualClass, out var cached))
			return cached;

		if (MissingIds.Contains(visualClass))
			return null;

		var sheetPath = $"{SpritePath}{visualClass}.png";
		var metaPath = $"{SpritePath}{visualClass}.json";

		if (!ResourceLoader.Exists(sheetPath))
		{
			MissingIds.Add(visualClass);
			return null;
		}

		var texture = ResourceLoader.Load<Texture2D>(sheetPath);
		if (texture == null)
		{
			MissingIds.Add(visualClass);
			return null;
		}

		var sheet = new UnitSpriteSheet
		{
			Texture = texture,
			FrameWidth = 64,
			FrameHeight = 64
		};

		// Try loading animation metadata from JSON
		if (ResourceLoader.Exists(metaPath))
		{
			TryLoadMeta(metaPath, sheet);
		}
		else
		{
			// Default layout: 6 columns per row, rows = idle/walk/attack/hit/death/deploy
			ApplyDefaultLayout(sheet, texture);
		}

		Cache[visualClass] = sheet;
		return sheet;
	}

	private static void TryLoadMeta(string path, UnitSpriteSheet sheet)
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			ApplyDefaultLayout(sheet, sheet.Texture);
			return;
		}

		var json = file.GetAsText();
		if (string.IsNullOrWhiteSpace(json))
		{
			ApplyDefaultLayout(sheet, sheet.Texture);
			return;
		}

		try
		{
			using var doc = System.Text.Json.JsonDocument.Parse(json);
			var root = doc.RootElement;

			if (root.TryGetProperty("frameWidth", out var fw) && fw.TryGetInt32(out var fwVal))
				sheet.FrameWidth = fwVal;
			if (root.TryGetProperty("frameHeight", out var fh) && fh.TryGetInt32(out var fhVal))
				sheet.FrameHeight = fhVal;

			if (root.TryGetProperty("animations", out var anims) && anims.ValueKind == System.Text.Json.JsonValueKind.Object)
			{
				foreach (var prop in anims.EnumerateObject())
				{
					if (!System.Enum.TryParse<UnitAnimState>(prop.Name, true, out var state))
						continue;

					var startFrame = 0;
					var frameCount = 4;
					var frameDuration = 0.12f;
					var loop = state is UnitAnimState.Idle or UnitAnimState.Walk;

					if (prop.Value.TryGetProperty("start", out var s) && s.TryGetInt32(out var sv)) startFrame = sv;
					if (prop.Value.TryGetProperty("count", out var c) && c.TryGetInt32(out var cv)) frameCount = cv;
					if (prop.Value.TryGetProperty("duration", out var d) && d.TryGetDouble(out var dv)) frameDuration = (float)dv;
					if (prop.Value.TryGetProperty("loop", out var l) && l.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False) loop = l.GetBoolean();

					sheet.Animations[state] = new SpriteAnimRange
					{
						StartFrame = startFrame,
						FrameCount = frameCount,
						FrameDuration = frameDuration,
						Loop = loop
					};
				}
			}
		}
		catch
		{
			ApplyDefaultLayout(sheet, sheet.Texture);
		}
	}

	private static void ApplyDefaultLayout(UnitSpriteSheet sheet, Texture2D texture)
	{
		var cols = Mathf.Max(1, texture.GetWidth() / sheet.FrameWidth);
		var rows = Mathf.Max(1, texture.GetHeight() / sheet.FrameHeight);

		var framesPerRow = Mathf.Min(cols, 8);

		var states = new[] { UnitAnimState.Idle, UnitAnimState.Walk, UnitAnimState.Attack, UnitAnimState.Hit, UnitAnimState.Death, UnitAnimState.Deploy };
		for (var row = 0; row < Mathf.Min(rows, states.Length); row++)
		{
			var state = states[row];
			var count = state switch
			{
				UnitAnimState.Idle => Mathf.Min(framesPerRow, 6),
				UnitAnimState.Walk => Mathf.Min(framesPerRow, 8),
				UnitAnimState.Attack => Mathf.Min(framesPerRow, 6),
				UnitAnimState.Hit => Mathf.Min(framesPerRow, 3),
				UnitAnimState.Death => Mathf.Min(framesPerRow, 6),
				UnitAnimState.Deploy => Mathf.Min(framesPerRow, 4),
				_ => Mathf.Min(framesPerRow, 4)
			};

			sheet.Animations[state] = new SpriteAnimRange
			{
				StartFrame = row * cols,
				FrameCount = count,
				FrameDuration = 0.12f,
				Loop = state is UnitAnimState.Idle or UnitAnimState.Walk
			};
		}
	}

	public static Rect2 GetFrameRect(UnitSpriteSheet sheet, int globalFrame)
	{
		var cols = Mathf.Max(1, sheet.Texture.GetWidth() / sheet.FrameWidth);
		var col = globalFrame % cols;
		var row = globalFrame / cols;
		return new Rect2(col * sheet.FrameWidth, row * sheet.FrameHeight, sheet.FrameWidth, sheet.FrameHeight);
	}

	public static void ClearCache()
	{
		Cache.Clear();
		MissingIds.Clear();
	}
}
