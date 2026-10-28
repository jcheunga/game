/// <summary>
/// Pages on the public website (site/) the game links to. The domain must match "domain" in site/site.json;
/// scripts/release/preflight.py checks that they agree.
/// </summary>
public static class SiteLinks
{
	public const string Base = "https://crownroad.game";
	public const string Privacy = Base + "/privacy";
	public const string Terms = Base + "/terms";
	public const string Support = Base + "/support";
}
