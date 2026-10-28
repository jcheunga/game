/// <summary>
/// Features held back from the 1.0 release. Each has its code and server routes in place; turn one on once
/// it is ready to ship, then run its review drivers. Review drivers may turn one on for a run.
/// </summary>
public static class LaunchFeatures
{
	/// <summary>
	/// Internet rooms: hosting, matching and joining room races from the Multiplayer screen. Off for 1.0:
	/// battles have no live link between players yet, and the room list would usually be empty at launch.
	/// </summary>
	public static bool InternetRooms { get; set; }

	/// <summary>The season pass's premium track. Off for 1.0: no store product sells it yet.</summary>
	public static bool SeasonPremiumTrack { get; set; }
}
