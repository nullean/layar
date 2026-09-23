using System.Collections.Frozen;
using System.Text.Json;

namespace Laya.Tokenization;

/// <summary>
/// Parses a Hugging Face fast-tokenizer's <c>tokenizer.json</c> + <c>tokenizer_config.json</c>
/// using <see cref="JsonDocument"/> (no reflection-based deserialization, so this stays AOT/trim
/// safe without a source-generated <c>JsonSerializerContext</c>).
/// </summary>
internal static class TokenizerLoader
{
	public static TokenizerModel Load(string tokenizerJsonPath, string tokenizerConfigJsonPath)
	{
		using var tokenizerDoc = JsonDocument.Parse(File.ReadAllBytes(tokenizerJsonPath));
		using var configDoc = JsonDocument.Parse(File.ReadAllBytes(tokenizerConfigJsonPath));

		var root = tokenizerDoc.RootElement;
		var model = root.GetProperty("model");

		var vocab = new Dictionary<string, int>();
		foreach (var entry in model.GetProperty("vocab").EnumerateObject())
			vocab[entry.Name] = entry.Value.GetInt32();

		// Special tokens (CLS/SEP/MASK/PAD/...) live in `added_tokens`, not the base BPE vocab.
		if (root.TryGetProperty("added_tokens", out var addedTokens))
		{
			foreach (var entry in addedTokens.EnumerateArray())
				vocab[entry.GetProperty("content").GetString()!] = entry.GetProperty("id").GetInt32();
		}

		var mergeRanks = new Dictionary<(string, string), int>();
		var rank = 0;
		foreach (var entry in model.GetProperty("merges").EnumerateArray())
		{
			string left, right;
			if (entry.ValueKind == JsonValueKind.Array)
			{
				var pair = entry.EnumerateArray().ToArray();
				left = pair[0].GetString()!;
				right = pair[1].GetString()!;
			}
			else
			{
				var text = entry.GetString()!;
				var spaceIndex = text.IndexOf(' ');
				left = text[..spaceIndex];
				right = text[(spaceIndex + 1)..];
			}
			mergeRanks[(left, right)] = rank++;
		}

		var byteFallback = model.TryGetProperty("byte_fallback", out var bf) && bf.ValueKind == JsonValueKind.True;

		var preTokenizerElement = root.GetProperty("pre_tokenizer");
		var preTokenizerType = preTokenizerElement.GetProperty("type").GetString();
		var kind = preTokenizerType switch
		{
			"Metaspace" => PreTokenizerKind.Metaspace,
			"ByteLevel" => PreTokenizerKind.ByteLevel,
			_ => throw new NotSupportedException($"tokenizer.json pre_tokenizer type '{preTokenizerType}' is not supported"),
		};
		var replacement = kind == PreTokenizerKind.Metaspace
			? preTokenizerElement.GetProperty("replacement").GetString()![0]
			: '▁';

		string SpecialToken(string configKey) => configDoc.RootElement.GetProperty(configKey).GetString()!;
		int SpecialTokenId(string configKey) => vocab[SpecialToken(configKey)];

		return new TokenizerModel
		{
			Vocab = vocab.ToFrozenDictionary(),
			MergeRanks = mergeRanks.ToFrozenDictionary(),
			PreTokenizer = kind,
			MetaspaceReplacement = replacement,
			ByteFallback = byteFallback,
			ClsTokenId = SpecialTokenId("cls_token"),
			SepTokenId = SpecialTokenId("sep_token"),
			MaskTokenId = SpecialTokenId("mask_token"),
			PadTokenId = SpecialTokenId("pad_token"),
			UnkTokenId = configDoc.RootElement.TryGetProperty("unk_token", out var unk) && unk.ValueKind == JsonValueKind.String
				? vocab.GetValueOrDefault(unk.GetString()!, -1) is var id and >= 0 ? id : null
				: null,
			MaskToken = SpecialToken("mask_token"),
		};
	}
}
