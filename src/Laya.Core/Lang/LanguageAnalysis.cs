using System.Collections.Frozen;
using System.Text.Json;

namespace Laya.Core.Lang;

/// <summary>Full detection result for a state: which script dominates, a best-effort language
/// guess, and whether the English checkpoint can be expected to read it.</summary>
public readonly record struct LanguageAnalysis(
	string Script,
	FrozenDictionary<string, double> ScriptProfile,
	string? Language,
	bool IsEnglish,
	bool LanguageUndecided,
	double DiacriticRate,
	double NonLatinFraction);

/// <summary>
/// Decides whether a state's text can be trusted to the English checkpoint or needs the
/// multilingual one. Script detection is exact; the Latin-script language guess is a best-effort
/// heuristic (see <see cref="LatinLanguageDetector"/>) — routing on it is why the English
/// checkpoint's own (over)confidence on non-English text never gets a chance to mislead a caller.
/// </summary>
public static class LanguageAnalyzer
{
	/// <summary>Flattens a JSON state into the text used for detection: object keys are ignored
	/// (they are usually English field names), only string leaves are collected, up to 6 levels
	/// deep and <paramref name="maxChars"/> characters.</summary>
	public static string StateText(JsonElement state, int maxChars = 4000)
	{
		var sink = new List<string>();
		CollectText(state, sink, 0);
		var joined = string.Join(' ', sink);
		return joined.Length <= maxChars ? joined : joined[..maxChars];
	}

	private static void CollectText(JsonElement element, List<string> sink, int depth)
	{
		if (depth > 6)
			return;
		switch (element.ValueKind)
		{
			case JsonValueKind.String:
				var s = element.GetString();
				if (s is not null)
					sink.Add(s);
				break;
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject())
					CollectText(property.Value, sink, depth + 1);
				break;
			case JsonValueKind.Array:
				foreach (var item in element.EnumerateArray())
					CollectText(item, sink, depth + 1);
				break;
		}
	}

	public static LanguageAnalysis Analyse(JsonElement state) => Analyse(StateText(state));

	public static LanguageAnalysis Analyse(ReadOnlySpan<char> text)
	{
		var script = ScriptDetector.DetectScript(text);
		var profile = ScriptDetector.ScriptProfile(text);
		var nonLatinFraction = profile.Count == 0
			? 0.0
			: Math.Round(1.0 - profile.GetValueOrDefault("latin", 0.0), 4);

		if (script == "unknown")
			return new LanguageAnalysis("unknown", profile, null, true, true, 0.0, 0.0);

		if (script != "latin")
			return new LanguageAnalysis(script, profile, null, false, true, 0.0, nonLatinFraction);

		var latin = LatinLanguageDetector.Profile(text);
		// Undecided is not English. Treating it as English would send every Latin-script language
		// this port holds no stopwords for to the checkpoint that cannot read it, silently.
		var undecided = latin.Language is null;
		var isEnglish = latin.Language == "en" || (undecided && !latin.LooksNonEnglish);
		return new LanguageAnalysis(
			"latin", profile, latin.Language, isEnglish, undecided,
			Math.Round(latin.DiacriticRate, 4), nonLatinFraction);
	}

	public static bool IsEnglish(ReadOnlySpan<char> text) => Analyse(text).IsEnglish;

	public static bool IsEnglish(JsonElement state) => Analyse(state).IsEnglish;
}
