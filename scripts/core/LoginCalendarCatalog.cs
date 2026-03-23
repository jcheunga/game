using System;
using System.Collections.Generic;

public sealed class LoginCalendarReward
{
	public int Day { get; }
	public string RewardType { get; }
	public string RewardItemId { get; }
	public int RewardAmount { get; }
	public string Label { get; }

	public LoginCalendarReward(int day, string rewardType, int rewardAmount, string label, string rewardItemId = "")
	{
		Day = day;
		RewardType = rewardType;
		RewardAmount = rewardAmount;
		Label = label;
		RewardItemId = rewardItemId;
	}
}

public static class LoginCalendarCatalog
{
	public const int TotalDays = 30;

	private static readonly LoginCalendarReward[] Days =
	{
		new(1, "gold", 50, "50 Gold"),
		new(2, "gold", 75, "75 Gold"),
		new(3, "gold", 100, "100 Gold"),
		new(4, "food", 3, "3 Food"),
		new(5, "gold", 150, "150 Gold"),
		new(6, "food", 5, "5 Food"),
		new(7, "tomes", 1, "1 Tome"),
		new(8, "gold", 200, "200 Gold"),
		new(9, "food", 6, "6 Food"),
		new(10, "tomes", 2, "2 Tomes"),
		new(11, "gold", 250, "250 Gold"),
		new(12, "essence", 1, "1 Essence"),
		new(13, "food", 8, "8 Food"),
		new(14, "tomes", 2, "2 Tomes"),
		new(15, "sigils", 1, "1 Sigil"),
		new(16, "gold", 300, "300 Gold"),
		new(17, "essence", 2, "2 Essence"),
		new(18, "food", 10, "10 Food"),
		new(19, "tomes", 3, "3 Tomes"),
		new(20, "sigils", 2, "2 Sigils"),
		new(21, "gold", 400, "400 Gold"),
		new(22, "essence", 3, "3 Essence"),
		new(23, "food", 12, "12 Food"),
		new(24, "tomes", 3, "3 Tomes"),
		new(25, "sigils", 3, "3 Sigils"),
		new(26, "gold", 500, "500 Gold"),
		new(27, "essence", 4, "4 Essence"),
		new(28, "tomes", 4, "4 Tomes"),
		new(29, "sigils", 4, "4 Sigils"),
		new(30, "essence", 8, "8 Essence + Bonus!"),
	};

	public static LoginCalendarReward GetDay(int day)
	{
		var index = day - 1;
		return index >= 0 && index < Days.Length ? Days[index] : null;
	}

	public static IReadOnlyList<LoginCalendarReward> GetAll() => Days;

	public static string GetCurrentMonth() => DateTime.UtcNow.ToString("yyyy-MM");
}
