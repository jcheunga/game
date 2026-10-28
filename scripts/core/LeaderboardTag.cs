using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// The anonymous name a player has on one public leaderboard: "Player" and six hex digits from a hash of their player
/// ID and the board. Public leaderboards never show player IDs or nicknames, and the same player has a different tag
/// on each board, so a tag can't be used to act on an account or to follow a player. The game computes its own tag
/// to mark "You". The server compiles this file too (server/Server.csproj), so both always agree.
/// </summary>
public static class LeaderboardTag
{
	public const string You = "You";

	public static string For(string profileId, string board) =>
		"Player " + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profileId + "\n" + board)))[..6];
}
