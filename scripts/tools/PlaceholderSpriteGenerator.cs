using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Generates simple colored placeholder sprite sheets for every visual class.
/// Run from the debug console: "generate sprites"
/// Creates 6-row sheets (idle/walk/attack/hit/death/deploy) with 8 columns.
/// Each frame is a simple colored silhouette shape matching the unit's visual class.
/// </summary>
public static class PlaceholderSpriteGenerator
{
	private const int FrameSize = 64;
	private const int Columns = 8;
	private const int Rows = 6; // idle, walk, attack, hit, death, deploy
	private const string OutputDir = "res://assets/units/";

	private static readonly Dictionary<string, Color> VisualClassColors = new()
	{
		["fighter"] = new Color("f4a261"),
		["gunner"] = new Color("8ecae6"),
		["shield"] = new Color("f6bd60"),
		["skirmisher"] = new Color("ffb703"),
		["support"] = new Color("90be6d"),
		["sniper"] = new Color("e9c46a"),
		["hound"] = new Color("d4a373"),
		["banner"] = new Color("ffd166"),
		["necromancer"] = new Color("9b5de5"),
		["berserker"] = new Color("e63946"),
		["walker"] = new Color("84a98c"),
		["runner"] = new Color("6d597a"),
		["bloater"] = new Color("c77dff"),
		["brute"] = new Color("9d0208"),
		["spitter"] = new Color("bdb2ff"),
		["splitter"] = new Color("43aa8b"),
		["saboteur"] = new Color("f94144"),
		["howler"] = new Color("f8961e"),
		["jammer"] = new Color("577590"),
		["crusher"] = new Color("7f5539"),
		["boss"] = new Color("5a189a"),
		["shieldwall"] = new Color("8d99ae"),
		["lich"] = new Color("7209b7"),
		["siegetower"] = new Color("6c584c"),
		["mirror"] = new Color("adb5bd"),
		["tunneler"] = new Color("5c4033"),
		["skeleton"] = new Color("c8b6a6"),
	};

	public static string GenerateAll()
	{
		var generated = 0;

		foreach (var (visualClass, color) in VisualClassColors)
		{
			if (GenerateSheet(visualClass, color))
				generated++;
		}

		// Also generate JSON metadata for each
		foreach (var visualClass in VisualClassColors.Keys)
		{
			GenerateMetadata(visualClass);
		}

		return $"Generated {generated} placeholder sprite sheets in {OutputDir}";
	}

	private static bool GenerateSheet(string visualClass, Color baseColor)
	{
		var width = FrameSize * Columns;
		var height = FrameSize * Rows;
		var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

		var accentColor = baseColor.Lightened(0.25f);
		var darkColor = baseColor.Darkened(0.3f);

		for (var row = 0; row < Rows; row++)
		{
			for (var col = 0; col < GetFrameCount(row); col++)
			{
				var originX = col * FrameSize;
				var originY = row * FrameSize;
				var animOffset = GetAnimOffset(row, col);
				DrawUnitShape(image, originX, originY, baseColor, accentColor, darkColor, visualClass, animOffset);
			}
		}

		var path = $"{OutputDir}{visualClass}.png";
		var error = image.SavePng(path);
		if (error != Error.Ok)
		{
			GD.PrintErr($"PlaceholderSpriteGenerator: failed to save {path}: {error}");
			return false;
		}

		return true;
	}

	private static int GetFrameCount(int row)
	{
		return row switch
		{
			0 => 6, // idle
			1 => 8, // walk
			2 => 5, // attack
			3 => 3, // hit
			4 => 6, // death
			5 => 4, // deploy
			_ => 4
		};
	}

	private static float GetAnimOffset(int row, int col)
	{
		var count = GetFrameCount(row);
		return count > 1 ? (float)col / (count - 1) : 0f;
	}

	private static void DrawUnitShape(Image image, int ox, int oy, Color body, Color accent, Color dark, string visualClass, float animT)
	{
		var cx = ox + FrameSize / 2;
		var cy = oy + FrameSize / 2;

		switch (visualClass)
		{
			case "shield":
				// Wide body with shield
				FillRect(image, cx - 14, cy - 10, 28, 24, body);
				FillRect(image, cx - 18, cy - 8, 10, 20, accent); // shield
				FillCircle(image, cx, cy - 18, 8, accent); // head
				FillRect(image, cx - 6, cy + 14, 12, 8, dark); // legs
				break;

			case "gunner":
			case "sniper":
				// Lean with extended weapon
				FillRect(image, cx - 8, cy - 6, 16, 20, body);
				FillCircle(image, cx, cy - 16, 7, accent); // head
				FillRect(image, cx + 8, cy - 4, 16, 4, dark); // weapon
				FillRect(image, cx - 5, cy + 14, 10, 8, dark); // legs
				break;

			case "hound":
				// Low quadruped
				FillRect(image, cx - 14, cy + 2, 28, 10, body);
				FillCircle(image, cx + 10, cy - 2, 6, accent); // head
				FillRect(image, cx - 12, cy + 12, 6, 8, dark); // back legs
				FillRect(image, cx + 6, cy + 12, 6, 8, dark); // front legs
				break;

			case "banner":
				// Knight with tall banner
				FillRect(image, cx - 10, cy - 6, 20, 22, body);
				FillCircle(image, cx, cy - 16, 8, accent); // head
				FillRect(image, cx + 10, cy - 28, 4, 40, dark); // banner pole
				FillRect(image, cx + 10, cy - 28, 12, 8, accent); // banner
				FillRect(image, cx - 5, cy + 16, 10, 8, dark); // legs
				break;

			case "necromancer":
				// Robed caster with staff
				FillRect(image, cx - 10, cy - 8, 20, 26, body);
				FillCircle(image, cx, cy - 18, 7, accent); // head/hood
				FillRect(image, cx - 16, cy - 24, 4, 36, dark); // staff
				FillCircle(image, cx - 16, cy - 26, 4, new Color("9b5de5")); // staff orb
				break;

			case "berserker":
				// Broad chest, twin axes
				FillRect(image, cx - 14, cy - 8, 28, 22, body);
				FillCircle(image, cx, cy - 18, 9, accent); // head
				FillRect(image, cx - 20, cy - 6, 6, 14, dark); // left axe
				FillRect(image, cx + 14, cy - 6, 6, 14, dark); // right axe
				FillRect(image, cx - 6, cy + 14, 12, 8, dark); // legs
				break;

			case "bloater":
				// Round swollen body
				FillCircle(image, cx, cy, 18, body);
				FillCircle(image, cx - 6, cy - 2, 4, accent); // pustule
				FillCircle(image, cx + 8, cy + 4, 3, accent); // pustule
				FillCircle(image, cx, cy - 16, 6, dark); // head
				break;

			case "boss":
				// Large with crown
				FillRect(image, cx - 16, cy - 8, 32, 28, body);
				FillCircle(image, cx, cy - 18, 10, accent); // head
				FillRect(image, cx - 10, cy - 28, 4, 10, dark); // crown spike
				FillRect(image, cx - 2, cy - 30, 4, 12, dark); // crown spike
				FillRect(image, cx + 6, cy - 28, 4, 10, dark); // crown spike
				FillRect(image, cx - 14, cy + 20, 28, 4, dark); // cape bottom
				break;

			case "siegetower":
				// Tall structure
				FillRect(image, cx - 12, cy - 24, 24, 48, body);
				FillRect(image, cx - 8, cy - 20, 16, 6, dark); // window
				FillRect(image, cx - 8, cy - 8, 16, 6, dark); // window
				FillRect(image, cx - 14, cy + 20, 28, 6, accent); // base
				break;

			default:
				// Generic humanoid (walker, fighter, runner, splitter, etc)
				var bob = (int)(Mathf.Sin(animT * Mathf.Pi * 2f) * 2f);
				FillRect(image, cx - 10, cy - 6 + bob, 20, 20, body);
				FillCircle(image, cx, cy - 16 + bob, 8, accent); // head
				FillRect(image, cx - 5, cy + 14, 10, 8, dark); // legs
				if (visualClass == "skirmisher")
				{
					// Lean forward
					FillRect(image, cx + 8, cy - 2, 10, 4, dark); // weapon
				}
				break;
		}
	}

	private static void FillRect(Image image, int x, int y, int w, int h, Color color)
	{
		for (var py = Math.Max(0, y); py < Math.Min(image.GetHeight(), y + h); py++)
		{
			for (var px = Math.Max(0, x); px < Math.Min(image.GetWidth(), x + w); px++)
			{
				image.SetPixel(px, py, color);
			}
		}
	}

	private static void FillCircle(Image image, int cx, int cy, int radius, Color color)
	{
		for (var py = cy - radius; py <= cy + radius; py++)
		{
			for (var px = cx - radius; px <= cx + radius; px++)
			{
				if (px < 0 || py < 0 || px >= image.GetWidth() || py >= image.GetHeight()) continue;
				var dx = px - cx;
				var dy = py - cy;
				if (dx * dx + dy * dy <= radius * radius)
				{
					image.SetPixel(px, py, color);
				}
			}
		}
	}

	private static void GenerateMetadata(string visualClass)
	{
		var json = "{\n" +
			"  \"frameWidth\": 64,\n" +
			"  \"frameHeight\": 64,\n" +
			"  \"animations\": {\n" +
			"    \"idle\": { \"start\": 0, \"count\": 6, \"duration\": 0.15, \"loop\": true },\n" +
			"    \"walk\": { \"start\": 8, \"count\": 8, \"duration\": 0.10, \"loop\": true },\n" +
			"    \"attack\": { \"start\": 16, \"count\": 5, \"duration\": 0.10, \"loop\": false },\n" +
			"    \"hit\": { \"start\": 24, \"count\": 3, \"duration\": 0.08, \"loop\": false },\n" +
			"    \"death\": { \"start\": 32, \"count\": 6, \"duration\": 0.12, \"loop\": false },\n" +
			"    \"deploy\": { \"start\": 40, \"count\": 4, \"duration\": 0.10, \"loop\": false }\n" +
			"  }\n" +
			"}";

		var path = $"{OutputDir}{visualClass}.json";
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		file?.StoreString(json);
	}
}
