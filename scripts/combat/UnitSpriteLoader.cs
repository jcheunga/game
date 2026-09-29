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
	public float DrawScale { get; set; } = 1f;
	public float AnchorY { get; set; } = 1f;
	public float AnchorX { get; set; } = 0.5f;
	public Vector2 BodyOffset { get; set; } = new(0,-.25f);
	public Vector2 ContactOffset { get; set; } = new(.28f,-.25f);
	public string MotionProfile { get; set; } = "sword-cut";
	public float HealthBarY { get; set; } = 0.8f;
	public Dictionary<UnitAnimState, SpriteAnimRange> Animations { get; set; } = new();
}

public sealed class SpriteAnimRange
{
	public int StartFrame { get; set; }
	public int FrameCount { get; set; }
	public float FrameDuration { get; set; } = 0.12f;
	public bool Loop { get; set; } = true;
	public int ContactFrame { get; set; } = 2;
}

public static class UnitSpriteLoader
{
	// Preview textures are owned by one viewer and explicitly released when it
	// closes or changes character, rather than caching the whole roster in VRAM.
	internal static UnitSpriteSheet LoadOwnedPreview(string unitId)
	{
		var path=$"res://assets/ui/models/{unitId}";
		if(!ResourceLoader.Exists(path+".png") || !Godot.FileAccess.FileExists(path+".json")) return null;
		var texture=ResourceLoader.Load<Texture2D>(path+".png",cacheMode:ResourceLoader.CacheMode.Ignore);
		if(texture==null) return null;
		var sheet=new UnitSpriteSheet {Texture=texture,FrameWidth=256,FrameHeight=320};
		TryLoadMeta(path+".json",sheet);
		return sheet;
	}

	private static readonly Dictionary<string, UnitSpriteSheet> Cache = new();
	private static readonly HashSet<string> MissingIds = new();

	private const string SpritePath = "res://assets/units/";

	public static UnitSpriteSheet TryLoad(string visualClass, string unitId = "")
	{
		if (string.IsNullOrWhiteSpace(visualClass))
			return null;

		var assetId = !string.IsNullOrWhiteSpace(unitId) && ResourceLoader.Exists($"{SpritePath}{unitId}.png")
			? unitId : visualClass;
		if (Cache.TryGetValue(assetId, out var cached))
			return cached;

		if (MissingIds.Contains(assetId))
			return null;

		var sheetPath = $"{SpritePath}{assetId}.png";
		var metaPath = $"{SpritePath}{assetId}.json";

		if (!ResourceLoader.Exists(sheetPath))
		{
			var illustrated = TryLoadIllustrated(visualClass);
			if (illustrated != null) return Cache[visualClass] = illustrated;
			MissingIds.Add(assetId);
			return null;
		}

		var texture = ResourceLoader.Load<Texture2D>(sheetPath);
		if (texture == null)
		{
			MissingIds.Add(assetId);
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

		Cache[assetId] = sheet;
		return sheet;
	}

    private static UnitSpriteSheet TryLoadIllustrated(string visualClass)
    {
        var index = visualClass switch { "fighter" => 0, "gunner" => 1, "shield" => 2, "walker" => 3, "runner" => 4, "brute" => 5, _ => -1 };
        const string path = SpritePath + "warband_sprites.png";
        if (index < 0 || !ResourceLoader.Exists(path)) return null;
        var atlas = ResourceLoader.Load<Texture2D>(path);
        var cell = atlas.GetSize() / new Vector2(3, 2);
        var sheet = new UnitSpriteSheet
        {
            Texture = new AtlasTexture { Atlas = atlas, Region = new Rect2(new Vector2(index % 3 * cell.X, index / 3 * cell.Y), cell) },
            FrameWidth = (int)cell.X, FrameHeight = (int)cell.Y, DrawScale = 1.8f
        };
        // Single painted poses use the renderer's movement bob and attack lunge.
        foreach (UnitAnimState state in System.Enum.GetValues<UnitAnimState>())
            sheet.Animations[state] = new SpriteAnimRange { StartFrame = 0, FrameCount = 1, Loop = true };
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
			if (root.TryGetProperty("drawScale", out var ds) && ds.TryGetSingle(out var dsVal))
				sheet.DrawScale = Mathf.Clamp(dsVal, 0.1f, 6f);
			if (root.TryGetProperty("anchorY", out var ay) && ay.TryGetSingle(out var ayVal))
				sheet.AnchorY = Mathf.Clamp(ayVal, 0f, 1f);
			if (root.TryGetProperty("anchorX", out var ax) && ax.TryGetSingle(out var axVal))
				sheet.AnchorX = Mathf.Clamp(axVal, 0f, 1f);
			if (root.TryGetProperty("motion", out var motion))
			{
				sheet.MotionProfile = motion.GetProperty("profile").GetString();
				var body = motion.GetProperty("body");
				var contact = motion.GetProperty("contact");
				sheet.BodyOffset = new Vector2(body[0].GetSingle(), body[1].GetSingle());
				sheet.ContactOffset = new Vector2(contact[0].GetSingle(), contact[1].GetSingle());
			}
			if (root.TryGetProperty("healthBarY", out var hy) && hy.TryGetSingle(out var hyVal))
				sheet.HealthBarY = Mathf.Clamp(hyVal, 0.1f, 1f);

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
						ContactFrame = prop.Value.TryGetProperty("contactFrame", out var contactFrame) ? contactFrame.GetInt32() : 2,
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
