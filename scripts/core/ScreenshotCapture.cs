using System;
using Godot;

public static class ScreenshotCapture
{
	private const string ScreenshotDir = "user://screenshots";

	public static string Capture(string prefix = "screenshot")
	{
		try
		{
			var viewport = (Engine.GetMainLoop() as SceneTree)?.Root?.GetViewport();
			if (viewport == null) return "";

			var image = viewport.GetTexture().GetImage();
			if (image == null) return "";

			DirAccess.MakeDirRecursiveAbsolute(ScreenshotDir);

			var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
			var filename = $"{prefix}_{timestamp}.png";
			var path = $"{ScreenshotDir}/{filename}";

			var error = image.SavePng(path);
			if (error != Error.Ok)
			{
				GD.PrintErr($"ScreenshotCapture: failed to save {path}: {error}");
				return "";
			}

			GD.Print($"ScreenshotCapture: saved {path}");
			return path;
		}
		catch (Exception e)
		{
			GD.PrintErr($"ScreenshotCapture: {e.Message}");
			return "";
		}
	}

	public static string CaptureAndShare(string prefix = "screenshot")
	{
		var path = Capture(prefix);
		if (string.IsNullOrWhiteSpace(path)) return "";

		// On desktop, just notify via clipboard
		DisplayServer.ClipboardSet($"Screenshot saved: {path}");
		return path;
	}

	public static string GetScreenshotDirectory()
	{
		return ProjectSettings.GlobalizePath(ScreenshotDir);
	}
}
