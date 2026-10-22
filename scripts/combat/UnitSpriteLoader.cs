using System.Collections.Generic;
using Godot;

public enum UnitAnimState
{
	Idle,
	Walk,
	Attack,
	Hit,
	Death,
	Deploy,
	// Ranged units' close-quarters strike; units without one reuse Attack.
	Melee,
	// The ability pose (a channel, a rally, a howl): rises into it, then holds by looping its last frames.
	Cast
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
	// Death presentation family authored with the clip (art/remaster/roster/deaths.py).
	public string DeathFx { get; set; } = "";
	public Dictionary<UnitAnimState, SpriteAnimRange> Animations { get; set; } = new();
	// Trimmed atlases (pack.py trimmed_pack): each frame's page, rectangle on it, and offset inside the logical
	// FrameWidth x FrameHeight frame. Null for regular grid atlases.
	public Texture2D[] Pages { get; set; }
	public FramePiece[] Frames { get; set; }
}

public readonly record struct FramePiece(int Page, Rect2 Source, Vector2 Offset);

/// <summary>One frame ready to draw: the texture holding it, its rectangle there, and where that rectangle sits
/// inside the logical frame (in frame pixels).</summary>
public readonly record struct FrameDraw(Texture2D Texture, Rect2 Source, Rect2 InFrame);

public sealed class SpriteAnimRange
{
	public int StartFrame { get; set; }
	public int FrameCount { get; set; }
	public float FrameDuration { get; set; } = 0.12f;
	public bool Loop { get; set; } = true;
	public int ContactFrame { get; set; } = 2;
	// Death clips: the frame the body hits the ground and where the torso lands, in frame
	// units relative to the ground anchor (same convention as motion.body).
	public int ImpactFrame { get; set; } = -1;
	public Vector2 ImpactPoint { get; set; }
	// Walk clips: ground covered in one cycle, in frame widths (the game plays the cycle to match its speed).
	public float Stride { get; set; }
	// Walk clips from the walking puppets: the gait the cycle was sized for (march, heavy, shamble, glide, jog,
	// prowl); empty for older clips.
	public string Gait { get; set; } = "";
	// Cast clips: the first frame of the held pose, looped while the cast lasts.
	public int HoldFrom { get; set; } = -1;
}

public static class UnitSpriteLoader
{
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
			if (root.TryGetProperty("frames", out var framesEl) && framesEl.ValueKind == System.Text.Json.JsonValueKind.Array)
			{
				var pageCount = root.TryGetProperty("pages", out var pc) && pc.TryGetInt32(out var pcv) ? pcv : 1;
				var baseId = root.TryGetProperty("sheet", out var sheetEl) ? sheetEl.GetString() : null;
				var stem = path[..path.LastIndexOf('.')];
				if (!string.IsNullOrEmpty(baseId)) stem = stem[..(stem.LastIndexOf('/') + 1)] + baseId;
				sheet.Pages = new Texture2D[pageCount];
				for (var i = 0; i < pageCount; i++)
				{
					var pagePath = i == 0 ? stem + ".png" : $"{stem}_{i}.png";
					sheet.Pages[i] = i == 0 && string.IsNullOrEmpty(baseId) ? sheet.Texture
						: ResourceLoader.Exists(pagePath) ? ResourceLoader.Load<Texture2D>(pagePath) : null;
				}
				// a class fallback borrows a unit's pages: its own png is only a stand-in
				if (!string.IsNullOrEmpty(baseId) && sheet.Pages.Length > 0 && sheet.Pages[0] != null) sheet.Texture = sheet.Pages[0];
				var list = new List<FramePiece>();
				foreach (var f in framesEl.EnumerateArray())
					list.Add(new FramePiece(f[0].GetInt32(), new Rect2(f[1].GetSingle(), f[2].GetSingle(), f[3].GetSingle(), f[4].GetSingle()),
						new Vector2(f[5].GetSingle(), f[6].GetSingle())));
				sheet.Frames = list.ToArray();
			}

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

					var range = new SpriteAnimRange
					{
						StartFrame = startFrame,
						FrameCount = frameCount,
						FrameDuration = frameDuration,
						ContactFrame = prop.Value.TryGetProperty("contactFrame", out var contactFrame) ? contactFrame.GetInt32() : 2,
						Loop = loop
					};
					if (prop.Value.TryGetProperty("impactFrame", out var impact) && impact.TryGetInt32(out var impactVal))
						range.ImpactFrame = Mathf.Clamp(impactVal, 0, frameCount - 1);
					if (prop.Value.TryGetProperty("holdFrom", out var hold) && hold.TryGetInt32(out var holdVal))
						range.HoldFrom = Mathf.Clamp(holdVal, 0, frameCount - 1);
					if (prop.Value.TryGetProperty("stride", out var stride) && stride.TryGetSingle(out var strideVal))
						range.Stride = Mathf.Max(0f, strideVal);
					if (prop.Value.TryGetProperty("gait", out var gait) && gait.ValueKind == System.Text.Json.JsonValueKind.String)
						range.Gait = gait.GetString() ?? "";
					if (prop.Value.TryGetProperty("impactPoint", out var point) && point.GetArrayLength() == 2)
						range.ImpactPoint = new Vector2(point[0].GetSingle(), point[1].GetSingle());
					if (state == UnitAnimState.Death && prop.Value.TryGetProperty("fx", out var fx))
						sheet.DeathFx = fx.GetString() ?? "";
					sheet.Animations[state] = range;
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

	public static FrameDraw Frame(UnitSpriteSheet sheet, int globalFrame)
	{
		if (sheet.Frames != null && sheet.Frames.Length > 0)
		{
			var f = sheet.Frames[Mathf.Clamp(globalFrame, 0, sheet.Frames.Length - 1)];
			var tex = sheet.Pages != null && f.Page < sheet.Pages.Length && sheet.Pages[f.Page] != null ? sheet.Pages[f.Page] : sheet.Texture;
			return new FrameDraw(tex, f.Source, new Rect2(f.Offset, f.Source.Size));
		}
		var rect = GetFrameRect(sheet, globalFrame);
		return new FrameDraw(sheet.Texture, rect, new Rect2(Vector2.Zero, rect.Size));
	}

	/// <summary>Where a frame's pixels land, given the rectangle the whole logical frame is drawn into.</summary>
	public static Rect2 Place(UnitSpriteSheet sheet, FrameDraw f, Rect2 frameRect)
	{
		var sx = frameRect.Size.X / Mathf.Max(1, sheet.FrameWidth);
		var sy = frameRect.Size.Y / Mathf.Max(1, sheet.FrameHeight);
		return new Rect2(frameRect.Position + new Vector2(f.InFrame.Position.X * sx, f.InFrame.Position.Y * sy),
			new Vector2(f.InFrame.Size.X * sx, f.InFrame.Size.Y * sy));
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
