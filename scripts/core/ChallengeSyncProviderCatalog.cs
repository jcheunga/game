using System;

public static class ChallengeSyncProviderCatalog
{
	public const string LocalJournalId = "local_journal";
	public const string HttpApiId = "http_api";

	public static string NormalizeId(string providerId)
	{
		return !string.IsNullOrWhiteSpace(providerId) &&
			providerId.Equals(HttpApiId, StringComparison.OrdinalIgnoreCase)
			? HttpApiId
			: LocalJournalId;
	}

	public static string GetDisplayName(string providerId)
	{
		return NormalizeId(providerId) == HttpApiId
			? "HTTP API"
			: "Local Journal Stub";
	}
}
