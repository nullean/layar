using System.Text.RegularExpressions;

namespace Laya.Tokenization;

/// <summary>Splits text into the word-ish chunks GPT-2-family byte-level BPE tokenizes
/// independently — the standard pattern the <c>tokenizers</c> library's <c>ByteLevel</c>
/// pre-tokenizer applies when <c>use_regex</c> is true (the case for both checkpoints this port
/// targets).</summary>
internal static partial class Gpt2PreTokenizer
{
	[GeneratedRegex(
		"'s|'t|'re|'ve|'m|'ll|'d| ?\\p{L}+| ?\\p{N}+| ?[^\\s\\p{L}\\p{N}]+|\\s+(?!\\S)|\\s+",
		RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex Pattern();

	public static List<string> Split(string text)
	{
		var words = new List<string>();
		foreach (var m in Pattern().EnumerateMatches(text))
			words.Add(text.Substring(m.Index, m.Length));
		return words;
	}
}
