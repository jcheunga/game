using System;
using System.Linq;
using Godot;

public partial class DeepLinkHandler : Node
{
	public static DeepLinkHandler Instance { get; private set; }

	public string PendingChallengeCode { get; private set; } = "";

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public override void _Ready()
	{
		ProcessCommandLineArgs();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationApplicationFocusIn)
		{
			// On mobile, check for new deep link on resume
			ProcessCommandLineArgs();
		}
	}

	private void ProcessCommandLineArgs()
	{
		var args = OS.GetCmdlineUserArgs();
		if (args.Length == 0)
			args = OS.GetCmdlineArgs();

		foreach (var arg in args)
		{
			if (TryExtractChallengeCode(arg, out var code))
			{
				PendingChallengeCode = code;
				GD.Print($"DeepLinkHandler: challenge code detected: {code}");
				return;
			}
		}
	}

	public bool HasPendingChallenge()
	{
		return !string.IsNullOrWhiteSpace(PendingChallengeCode);
	}

	public string ConsumePendingChallenge()
	{
		var code = PendingChallengeCode;
		PendingChallengeCode = "";
		return code;
	}

	public static string BuildShareUrl(string challengeCode)
	{
		if (string.IsNullOrWhiteSpace(challengeCode))
			return "";
		return $"https://crownroad.game/challenge/{Uri.EscapeDataString(challengeCode)}";
	}

	public static void ShareChallenge(string challengeCode)
	{
		var url = BuildShareUrl(challengeCode);
		if (string.IsNullOrWhiteSpace(url)) return;

		if (OS.HasFeature("ios") || OS.HasFeature("android"))
		{
			// Mobile: use native share sheet if available, fall back to clipboard
			DisplayServer.ClipboardSet(url);
		}
		else
		{
			DisplayServer.ClipboardSet(url);
		}
	}

	private static bool TryExtractChallengeCode(string input, out string code)
	{
		code = "";
		if (string.IsNullOrWhiteSpace(input))
			return false;

		// Direct code argument: --challenge=CH-01-PRS-1001
		if (input.StartsWith("--challenge=", StringComparison.OrdinalIgnoreCase))
		{
			code = input["--challenge=".Length..].Trim();
			return IsValidChallengeCode(code);
		}

		// URL format: https://crownroad.game/challenge/CH-01-PRS-1001
		if (input.Contains("/challenge/", StringComparison.OrdinalIgnoreCase))
		{
			var idx = input.IndexOf("/challenge/", StringComparison.OrdinalIgnoreCase);
			var raw = input[(idx + "/challenge/".Length)..].Trim();
			// Strip query strings and fragments
			var end = raw.IndexOfAny(new[] { '?', '#', '&' });
			code = end >= 0 ? raw[..end] : raw;
			code = Uri.UnescapeDataString(code);
			return IsValidChallengeCode(code);
		}

		// Bare code: CH-01-PRS-1001
		if (input.StartsWith("CH-", StringComparison.OrdinalIgnoreCase) && input.Count(c => c == '-') >= 3)
		{
			code = input.Trim();
			return IsValidChallengeCode(code);
		}

		return false;
	}

	private static bool IsValidChallengeCode(string code)
	{
		return !string.IsNullOrWhiteSpace(code) &&
			code.StartsWith("CH-", StringComparison.OrdinalIgnoreCase) &&
			code.Length >= 8 &&
			code.Length <= 32;
	}
}
