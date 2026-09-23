using System.Text.RegularExpressions;

namespace Laya.Core.Lang;

/// <summary>Evidence behind a Latin-script language guess: not just the verdict, because
/// "undecided" and "English" need different routing (see <see cref="Laya.Core.Routing.ModelRouter"/>).</summary>
public readonly record struct LatinLanguageProfile(string? Language, int EnglishHits, double DiacriticRate, bool LooksNonEnglish);

/// <summary>
/// Best-effort language guess for Latin-script text: a stopword/diacritic heuristic, not a
/// classifier. A non-English language is only named when it matched at least one word no other
/// language's list claims, and only by a clear margin over English — see the field comments below
/// for why (issues #172, #54 in the Python port this mirrors).
/// </summary>
public static partial class LatinLanguageDetector
{
	/// <summary>A diacritic rate at or above this is evidence the text is not English, even when no
	/// stopword list matches it.</summary>
	public const double NonEnglishDiacriticRate = 0.02;

	// Fixed language order, matching the Python port's dict insertion order: en, fr, de, es, pt, it,
	// nl, ro. When two languages tie on score, the earlier one in this order wins, exactly as
	// Python's `max(..., default=...)` picks the first max it sees in insertion order.
	private static readonly string[] LanguageOrder = ["en", "fr", "de", "es", "pt", "it", "nl", "ro"];

	[GeneratedRegex(@"[^\W\d_]+", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex WordPattern();

	public static LatinLanguageProfile Profile(ReadOnlySpan<char> text)
	{
		var diacriticCount = 0;
		var length = text.Length;
		scoped Span<char> lowerScratch;
		if (length <= 512)
		{
			Span<char> stack = stackalloc char[length];
			lowerScratch = stack;
		}
		else
		{
			lowerScratch = new char[length];
		}
		_ = text.ToLowerInvariant(lowerScratch);
		foreach (var ch in lowerScratch)
		{
			if (StopWords.NonEnglishDiacritics.Contains(ch))
				diacriticCount++;
		}
		var diacriticRate = (double)diacriticCount / Math.Max(1, length);
		var looksNonEnglish = diacriticRate >= NonEnglishDiacriticRate;

		var lowered = new string(lowerScratch);
		var words = new List<string>();
		foreach (var m in WordPattern().EnumerateMatches(lowered))
			words.Add(lowered.Substring(m.Index, m.Length));

		if (words.Count < 4)
			return new LatinLanguageProfile(null, 0, diacriticRate, looksNonEnglish);

		var uniqueWords = words.ToHashSet();
		var scores = new Dictionary<string, int>(LanguageOrder.Length);
		foreach (var lang in LanguageOrder)
		{
			var set = StopWords.ByLanguage[lang];
			var count = 0;
			foreach (var w in words)
			{
				if (set.Contains(w))
					count++;
			}
			scores[lang] = count;
		}
		var englishHits = scores["en"];

		string? bestLanguage = null;
		var bestScore = 0;
		foreach (var lang in LanguageOrder)
		{
			if (lang == "en")
				continue;
			var set = StopWords.ByLanguage[lang];
			var hasOwnEvidence = false;
			foreach (var w in uniqueWords)
			{
				if (set.Contains(w) && !StopWords.Shared.Contains(w))
				{
					hasOwnEvidence = true;
					break;
				}
			}
			if (!hasOwnEvidence)
				continue;
			if (scores[lang] > bestScore)
			{
				bestScore = scores[lang];
				bestLanguage = lang;
			}
		}

		string? language = null;
		if (bestLanguage is not null && bestScore >= Math.Max(2, englishHits + 2))
			language = bestLanguage;
		else if (bestLanguage is not null && looksNonEnglish && bestScore >= Math.Max(2, englishHits))
			language = bestLanguage;
		else if (englishHits > 0 && !looksNonEnglish)
			language = "en";

		return new LatinLanguageProfile(language, englishHits, diacriticRate, looksNonEnglish);
	}

	/// <summary>The language, or null when undecided. Short inputs (fewer than 4 words) are
	/// undecided by design.</summary>
	public static string? GuessLanguage(ReadOnlySpan<char> text) => Profile(text).Language;
}
