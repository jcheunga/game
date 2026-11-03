using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// The rules every nickname follows: 3 to 18 letters, numbers, spaces, hyphens or underscores, and no blocked word
/// however it is written (repeated letters, digits for letters, look-alike letters from other alphabets, gaps between
/// letters). The game checks a name as the player chooses it; the server compiles this file too (server/Server.csproj)
/// and replaces a blocked name that reaches it, so an old or altered game can't show one to other players. No filter
/// catches everything: support renames a player with POST /admin/support/rename (docs/store/SUPPORT_PLAYBOOK.md).
/// </summary>
public static class Nickname
{
	public const string Default = "Lantern";
	public const int MinLength = 3;
	public const int MaxLength = 18;

	// Blocked anywhere in a name, even inside a longer word. A letter may repeat ("fuuuck"); a doubled letter must stay
	// doubled, so "nigger" doesn't block Niger.
	private static readonly string[] Anywhere =
	{
		"fuck", "cunt", "nigger", "nigga", "fagot", "shit", "bitch", "whore", "slut", "wank", "twat", "dildo", "penis",
		"vagina", "porn", "pusy", "jiz", "retard", "rapist", "molest", "pedophil", "paedo", "hitler", "kkk", "1488",
		"ashole", "arsehole", "bastard", "blowjob", "handjob", "orgasm", "hentai", "wetback", "trany", "killyourself",
		"crownroad",
	};

	// Blocked only as a whole word of the name (or the whole name with its gaps closed), since they hide inside
	// ordinary words: ass in Assassin, cock in Peacock, anus in Anonymous, milf in Milford.
	private static readonly string[] WholeWord =
	{
		"ass", "arse", "anal", "anus", "cock", "dick", "cum", "tits", "titty", "sex", "sexy", "horny", "nude", "nudes",
		"rape", "raped", "piss", "fag", "fags", "homo", "dyke", "lesbo", "coon", "spic", "kike", "chink", "gook",
		"beaner", "nazi", "nazis", "heil", "kys", "milf", "fuk", "phuk",
		"admin", "administrator", "moderator", "mod", "staff", "support", "official", "developer",
	};

	// Ordinary words and places that contain a blocked word.
	private static readonly string[] Allowed = { "scunthorpe", "penistone", "shiitake", "shitake", "matsushita", "therapist", "snigger" };

	// Digits and letters that stand in for a letter.
	private static readonly Dictionary<char, string> StandIns = new()
	{
		['a'] = "a4", ['b'] = "b8", ['c'] = "ck", ['e'] = "e3", ['g'] = "g9", ['i'] = "i1", ['l'] = "l1", ['o'] = "o0",
		['s'] = "s5z", ['t'] = "t7", ['u'] = "uv",
	};

	// Cyrillic and Greek letters drawn like Latin ones.
	private static readonly Dictionary<char, char> LookAlikes = new()
	{
		['а'] = 'a', ['в'] = 'b', ['е'] = 'e', ['к'] = 'k', ['м'] = 'm', ['н'] = 'h', ['о'] = 'o', ['р'] = 'p',
		['с'] = 'c', ['т'] = 't', ['у'] = 'y', ['х'] = 'x', ['і'] = 'i', ['ј'] = 'j', ['ѕ'] = 's', ['ԁ'] = 'd', ['ӏ'] = 'l',
		['α'] = 'a', ['β'] = 'b', ['ε'] = 'e', ['ι'] = 'i', ['κ'] = 'k', ['ν'] = 'v', ['ο'] = 'o', ['ρ'] = 'p', ['τ'] = 't',
		['υ'] = 'u', ['χ'] = 'x',
	};

	private static readonly Regex AnywherePattern = new(Pattern(Anywhere), RegexOptions.CultureInvariant);
	private static readonly Regex WholeWordPattern = new("^" + Pattern(WholeWord) + "$", RegexOptions.CultureInvariant);

	/// <summary>Keeps the letters, numbers, spaces, hyphens and underscores, cut to <see cref="MaxLength"/>.</summary>
	public static string Normalize(string name, string fallback = Default)
	{
		var kept = new string((name ?? "").Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();
		if (kept.Length > MaxLength) kept = kept[..MaxLength].TrimEnd();
		return kept.Length == 0 ? fallback : kept;
	}

	/// <summary>The name as <see cref="Normalize"/> keeps it, or <paramref name="fallback"/> when it is blocked.</summary>
	public static string Clean(string name, string fallback = Default)
	{
		var normalized = Normalize(name, fallback);
		return IsBlocked(normalized) ? fallback : normalized;
	}

	public static bool IsBlocked(string name)
	{
		var words = Words(name ?? "").Select(Fold).Where(w => w.Length > 0).ToList();
		var joined = string.Concat(words);
		var withoutAllowed = Allowed.Aggregate(joined, (text, word) => text.Replace(word, "", StringComparison.Ordinal));
		return AnywherePattern.IsMatch(withoutAllowed) || WholeWordPattern.IsMatch(joined) || words.Any(WholeWordPattern.IsMatch);
	}

	// Splits at spaces, hyphens, underscores and the start of each capitalised word ("BigAss" is "Big" and "Ass").
	private static IEnumerable<string> Words(string name)
	{
		foreach (var part in name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries))
		{
			var start = 0;
			for (var i = 1; i < part.Length; i++)
			{
				var startsWord = char.IsUpper(part[i]) &&
					(char.IsLower(part[i - 1]) || (char.IsUpper(part[i - 1]) && i + 1 < part.Length && char.IsLower(part[i + 1])));
				if (!startsWord) continue;
				yield return part[start..i];
				start = i;
			}
			yield return part[start..];
		}
	}

	// Lower case, without accents, with look-alike letters swapped for the Latin ones.
	private static string Fold(string word)
	{
		var builder = new StringBuilder(word.Length);
		foreach (var c in word.Normalize(NormalizationForm.FormKD).ToLowerInvariant())
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
			builder.Append(LookAlikes.TryGetValue(c, out var latin) ? latin : c);
		}
		return builder.ToString();
	}

	private static string Pattern(IEnumerable<string> words) =>
		"(?:" + string.Join("|", words.Select(word => string.Concat(word.Select(c =>
			(StandIns.TryGetValue(c, out var any) ? "[" + any + "]" : Regex.Escape(c.ToString())) + "+")))) + ")";
}
