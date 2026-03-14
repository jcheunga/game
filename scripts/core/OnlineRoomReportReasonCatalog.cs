using System.Collections.Generic;

public sealed class OnlineRoomReportReason
{
	public OnlineRoomReportReason(string id, string title, string summary)
	{
		Id = id;
		Title = title;
		Summary = summary;
	}

	public string Id { get; }
	public string Title { get; }
	public string Summary { get; }
}

public static class OnlineRoomReportReasonCatalog
{
	public const string SuspiciousScoreId = "suspicious_score";
	public const string OffensiveNameId = "offensive_name";
	public const string HarassmentId = "harassment";
	public const string RoomAbuseId = "room_abuse";

	private static readonly OnlineRoomReportReason[] Reasons =
	[
		new(SuspiciousScoreId, "Suspicious score", "Use for impossible pacing, suspicious splits, or clearly invalid result spikes."),
		new(OffensiveNameId, "Offensive name", "Use for abusive or offensive callsigns in the room."),
		new(HarassmentId, "Harassment", "Use for direct harassment or abusive room behavior."),
		new(RoomAbuseId, "Room abuse", "Use for griefing, exploit setup, or abusive room-host behavior.")
	];

	public static IReadOnlyList<OnlineRoomReportReason> GetAll()
	{
		return Reasons;
	}

	public static OnlineRoomReportReason Get(string reasonId)
	{
		var normalized = NormalizeId(reasonId);
		foreach (var reason in Reasons)
		{
			if (reason.Id == normalized)
			{
				return reason;
			}
		}

		return Reasons[0];
	}

	public static string NormalizeId(string reasonId)
	{
		if (string.IsNullOrWhiteSpace(reasonId))
		{
			return SuspiciousScoreId;
		}

		var normalized = reasonId.Trim().ToLowerInvariant();
		foreach (var reason in Reasons)
		{
			if (reason.Id == normalized)
			{
				return normalized;
			}
		}

		return SuspiciousScoreId;
	}
}
