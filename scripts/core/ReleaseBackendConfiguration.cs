using System;
using Godot;

/// <summary>
/// Build-owned API configuration. A non-empty value in project.godot locks the
/// public release to one HTTPS backend instead of trusting player-editable
/// settings or stale save data.
/// </summary>
public static class ReleaseBackendConfiguration
{
	public const string ApiBaseUrlSetting = "crownroad/network/api_base_url";

	public static string ApiBaseUrl
	{
		get
		{
			var configured = ProjectSettings.GetSetting(ApiBaseUrlSetting, "").AsString().Trim();
			if (string.IsNullOrWhiteSpace(configured))
			{
				return "";
			}

			if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) ||
				!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
				string.IsNullOrWhiteSpace(uri.Host) ||
				!string.IsNullOrWhiteSpace(uri.UserInfo) ||
				(uri.AbsolutePath != "/" && !string.IsNullOrWhiteSpace(uri.AbsolutePath)) ||
				!string.IsNullOrWhiteSpace(uri.Query) ||
				!string.IsNullOrWhiteSpace(uri.Fragment))
			{
				GD.PushError($"{ApiBaseUrlSetting} must be an HTTPS origin without a path, query, or fragment.");
				return "";
			}

			return uri.GetLeftPart(UriPartial.Authority);
		}
	}

	public static bool IsConfigured => !string.IsNullOrWhiteSpace(ApiBaseUrl);

	public static string ChallengeSyncEndpoint => string.IsNullOrWhiteSpace(ApiBaseUrl)
		? ""
		: $"{ApiBaseUrl}/challenge-sync";
}
