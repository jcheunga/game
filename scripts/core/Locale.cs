using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

public static class Locale
{
	private const string LocalePath = "res://data/locale/";
	private const string DefaultLanguage = "en";

	private static string _currentLanguage = DefaultLanguage;
	private static readonly Dictionary<string, string> Strings = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, string> FallbackStrings = new(StringComparer.OrdinalIgnoreCase);
	private static bool _loaded;

	public static string CurrentLanguage => _currentLanguage;

	public static void SetLanguage(string language)
	{
		if (string.IsNullOrWhiteSpace(language))
			language = DefaultLanguage;

		language = language.Trim().ToLowerInvariant();
		if (language == _currentLanguage && _loaded)
			return;

		_currentLanguage = language;
		_loaded = false;
		EnsureLoaded();
	}

	public static string Get(string key, string fallback = "")
	{
		EnsureLoaded();

		if (!string.IsNullOrWhiteSpace(key))
		{
			if (Strings.TryGetValue(key, out var value))
				return value;
			if (FallbackStrings.TryGetValue(key, out var fbValue))
				return fbValue;
		}

		return string.IsNullOrWhiteSpace(fallback) ? key : fallback;
	}

	public static string Get(string key, params object[] args)
	{
		var template = Get(key);
		try
		{
			return string.Format(template, args);
		}
		catch
		{
			return template;
		}
	}

	public static string[] GetSupportedLanguages()
	{
		var languages = new List<string> { DefaultLanguage };

		using var dir = DirAccess.Open(LocalePath);
		if (dir == null) return languages.ToArray();

		dir.ListDirBegin();
		while (true)
		{
			var fileName = dir.GetNext();
			if (string.IsNullOrWhiteSpace(fileName)) break;
			if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
			var lang = fileName[..^5];
			if (!lang.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase))
				languages.Add(lang);
		}
		dir.ListDirEnd();

		return languages.ToArray();
	}

	private static void EnsureLoaded()
	{
		if (_loaded) return;
		_loaded = true;

		FallbackStrings.Clear();
		Strings.Clear();

		// Always load English as fallback
		if (!_currentLanguage.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase))
		{
			LoadFile(DefaultLanguage, FallbackStrings);
		}

		LoadFile(_currentLanguage, Strings);
	}

	private static void LoadFile(string language, Dictionary<string, string> target)
	{
		var path = $"{LocalePath}{language}.json";
		if (!FileAccess.FileExists(path)) return;

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null) return;

		var json = file.GetAsText();
		if (string.IsNullOrWhiteSpace(json)) return;

		try
		{
			using var doc = JsonDocument.Parse(json);
			foreach (var prop in doc.RootElement.EnumerateObject())
			{
				if (prop.Value.ValueKind == JsonValueKind.String)
				{
					target[prop.Name] = prop.Value.GetString() ?? prop.Name;
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"Locale: failed to parse {path}: {e.Message}");
		}
	}
}
