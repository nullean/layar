using AwesomeAssertions;
using Laya.Tokenization;

namespace Laya.Tests;

/// <summary>
/// Locks in the from-scratch BPE tokenizer against fixed oracle token ids captured from the
/// Python package's own <c>AutoTokenizer</c> (via <c>~/Projects/laya</c>'s venv) — the same
/// check that was done by hand while building <c>Laya.Tokenization</c>, now regression-tested.
/// </summary>
public class TokenizerConformanceTests
{
	private static readonly string[] Texts =
	[
		"Hello world",
		"Hi, we were billed twice for March. Please refund the duplicate today!",
		"I can't believe it's already 2026 -- time flies.",
		"Mein Konto wurde zweimal belastet, bitte helfen Sie mir.",
		"Bonjour, merci beaucoup pour votre aide rapide!",
	];

	private static readonly int[][] EnglishOracleIds =
	[
		[12092, 1533],
		[12764, 13, 359, 497, 47045, 7019, 323, 3919, 15, 7764, 23005, 253, 21036, 3063, 2],
		[42, 476, 626, 2868, 352, 434, 2168, 1384, 1731, 1969, 673, 19826, 15],
		[5072, 249, 611, 10905, 40803, 1182, 664, 1983, 1112, 505, 292, 13, 2372, 442, 1203, 25947, 15983, 6385, 15],
		[27157, 19923, 13, 14480, 74, 320, 47802, 6531, 48449, 35200, 17845, 504, 2],
	];

	private static readonly int[][] MultilingualOracleIds =
	[
		[25957, 2134],
		[11192, 235269, 783, 1049, 100159, 11594, 604, 4482, 235265, 5651, 19745, 573, 38294, 3646, 235341],
		[590, 798, 235303, 235251, 4564, 665, 235303, 235256, 3303, 235248, 235284, 235276, 235284, 235318, 3297, 1069, 33772, 235265],
		[36066, 113485, 9907, 169541, 2100, 142216, 235269, 48273, 44682, 3670, 6613, 235265],
		[97882, 235269, 42309, 20698, 1982, 6225, 36252, 23185, 235341],
	];

	[Test]
	public async Task English_byte_level_bpe_matches_AutoTokenizer()
	{
		var dir = CheckpointFixtures.RequireOrSkip("english");
		var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(Path.Combine(dir, "tokenizer"));
		AssertMatches(tokenizer, EnglishOracleIds);
	}

	[Test]
	public async Task Multilingual_metaspace_bpe_matches_AutoTokenizer()
	{
		var dir = CheckpointFixtures.RequireOrSkip("multilingual");
		var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(Path.Combine(dir, "tokenizer"));
		AssertMatches(tokenizer, MultilingualOracleIds);
	}

	private static void AssertMatches(HuggingFaceBpeTokenizer tokenizer, int[][] oracleIds)
	{
		for (var i = 0; i < Texts.Length; i++)
		{
			var buffer = new int[tokenizer.MaxTokenCount(Texts[i])];
			var count = tokenizer.Encode(Texts[i], buffer);
			buffer[..count].Should().Equal(oracleIds[i], $"tokenizing '{Texts[i]}'");
		}
	}
}
