using System.Collections.Frozen;

namespace Laya.Core.Lang;

/// <summary>
/// Detects the dominant Unicode script of a run of text — the primary signal for routing a
/// request between an English-only checkpoint and a multilingual one, since an English checkpoint
/// collapses to near-random accuracy off Latin script entirely (see <see cref="LanguageAnalysis"/>).
/// All ranges are within the Basic Multilingual Plane, so iterating UTF-16 code units (rather than
/// full Unicode scalar values) matches the source data with no surrogate-pair handling needed.
/// </summary>
public static class ScriptDetector
{
	private readonly record struct CodepointRange(int Low, int High)
	{
		public bool Contains(int codepoint) => codepoint >= Low && codepoint <= High;
	}

	private static readonly (string Name, CodepointRange[] Ranges)[] ScriptRanges =
	[
		("greek", [new(0x0370, 0x03FF), new(0x1F00, 0x1FFF)]),
		("cyrillic", [new(0x0400, 0x052F), new(0x2DE0, 0x2DFF), new(0xA640, 0xA69F)]),
		("armenian", [new(0x0530, 0x058F)]),
		("hebrew", [new(0x0590, 0x05FF)]),
		("arabic", [new(0x0600, 0x06FF), new(0x0750, 0x077F), new(0x08A0, 0x08FF), new(0xFB50, 0xFDFF), new(0xFE70, 0xFEFF)]),
		("devanagari", [new(0x0900, 0x097F), new(0xA8E0, 0xA8FF)]),
		("bengali", [new(0x0980, 0x09FF)]),
		("gurmukhi", [new(0x0A00, 0x0A7F)]),
		("gujarati", [new(0x0A80, 0x0AFF)]),
		("oriya", [new(0x0B00, 0x0B7F)]),
		("tamil", [new(0x0B80, 0x0BFF)]),
		("telugu", [new(0x0C00, 0x0C7F)]),
		("kannada", [new(0x0C80, 0x0CFF)]),
		("malayalam", [new(0x0D00, 0x0D7F)]),
		("sinhala", [new(0x0D80, 0x0DFF)]),
		("thai", [new(0x0E00, 0x0E7F)]),
		("lao", [new(0x0E80, 0x0EFF)]),
		("tibetan", [new(0x0F00, 0x0FFF)]),
		("myanmar", [new(0x1000, 0x109F)]),
		("georgian", [new(0x10A0, 0x10FF)]),
		("ethiopic", [new(0x1200, 0x137F)]),
		("khmer", [new(0x1780, 0x17FF)]),
		("hangul", [new(0x1100, 0x11FF), new(0x3130, 0x318F), new(0xAC00, 0xD7AF)]),
		("kana", [new(0x3040, 0x309F), new(0x30A0, 0x30FF), new(0x31F0, 0x31FF)]),
		("han", [new(0x3400, 0x4DBF), new(0x4E00, 0x9FFF), new(0xF900, 0xFAFF)]),
	];

	private static string? Classify(char ch)
	{
		if (!char.IsLetter(ch))
			return null;
		if (ch is < (char)0x0250 or (>= (char)0x1E00 and <= (char)0x1EFF))
			return "latin";
		var cp = ch;
		foreach (var (name, ranges) in ScriptRanges)
		{
			foreach (var range in ranges)
			{
				if (range.Contains(cp))
					return name;
			}
		}
		return null;
	}

	/// <summary>The dominant script in <paramref name="text"/>: a name from the table above, or
	/// <c>"unknown"</c> when the text has no letters this detector recognizes.</summary>
	public static string DetectScript(ReadOnlySpan<char> text)
	{
		Span<int> counts = stackalloc int[ScriptRanges.Length + 1]; // + latin
		var latin = 0;
		foreach (var ch in text)
		{
			var script = Classify(ch);
			if (script is null)
				continue;
			if (script == "latin")
			{
				latin++;
				continue;
			}
			for (var i = 0; i < ScriptRanges.Length; i++)
			{
				if (ScriptRanges[i].Name == script)
				{
					counts[i]++;
					break;
				}
			}
		}

		var bestIndex = -1;
		var bestCount = latin;
		for (var i = 0; i < ScriptRanges.Length; i++)
		{
			if (counts[i] > bestCount)
			{
				bestCount = counts[i];
				bestIndex = i;
			}
		}

		if (bestCount == 0 && latin == 0)
			return "unknown";
		return bestIndex < 0 ? "latin" : ScriptRanges[bestIndex].Name;
	}

	/// <summary>Fraction of alphabetic characters belonging to each detected script.</summary>
	public static FrozenDictionary<string, double> ScriptProfile(ReadOnlySpan<char> text)
	{
		Span<int> counts = stackalloc int[ScriptRanges.Length];
		var latin = 0;
		var total = 0;
		foreach (var ch in text)
		{
			var script = Classify(ch);
			if (script is null)
				continue;
			total++;
			if (script == "latin")
			{
				latin++;
				continue;
			}
			for (var i = 0; i < ScriptRanges.Length; i++)
			{
				if (ScriptRanges[i].Name == script)
				{
					counts[i]++;
					break;
				}
			}
		}

		if (total == 0)
			return FrozenDictionary<string, double>.Empty;

		var profile = new Dictionary<string, double>();
		if (latin > 0)
			profile["latin"] = (double)latin / total;
		for (var i = 0; i < ScriptRanges.Length; i++)
		{
			if (counts[i] > 0)
				profile[ScriptRanges[i].Name] = (double)counts[i] / total;
		}
		return profile.ToFrozenDictionary();
	}
}
