using System.Text;
using Laya.Core;

namespace Laya.Tokenization;

/// <summary>
/// A from-scratch BPE tokenizer reading a Hugging Face fast-tokenizer's <c>tokenizer.json</c>
/// directly, covering the two pre-tokenizer families the checkpoints this port targets use:
/// <c>ByteLevel</c> (English/ModernBERT, GPT-2-style) and <c>Metaspace</c> (multilingual, Gemma's
/// SentencePiece-style BPE). No external tokenizer package and no Python at runtime.
/// </summary>
public sealed class HuggingFaceBpeTokenizer : ITokenizer
{
	private readonly TokenizerModel _model;

	private HuggingFaceBpeTokenizer(TokenizerModel model) => _model = model;

	/// <summary>Loads a tokenizer from a directory containing <c>tokenizer.json</c> and
	/// <c>tokenizer_config.json</c>, exactly as a Layar checkpoint's <c>tokenizer/</c> subfolder
	/// ships them.</summary>
	public static HuggingFaceBpeTokenizer FromDirectory(string tokenizerDirectory)
	{
		var model = TokenizerLoader.Load(
			Path.Combine(tokenizerDirectory, "tokenizer.json"),
			Path.Combine(tokenizerDirectory, "tokenizer_config.json"));
		return new HuggingFaceBpeTokenizer(model);
	}

	public int ClsTokenId => _model.ClsTokenId;
	public int SepTokenId => _model.SepTokenId;
	public int MaskTokenId => _model.MaskTokenId;
	public int PadTokenId => _model.PadTokenId;
	public string MaskToken => _model.MaskToken;

	public int MaxTokenCount(ReadOnlySpan<char> text) => _model.PreTokenizer switch
	{
		PreTokenizerKind.ByteLevel => Encoding.UTF8.GetByteCount(text) + 4,
		PreTokenizerKind.Metaspace => text.Length + 4,
		_ => throw new NotSupportedException(),
	};

	public int Encode(ReadOnlySpan<char> text, Span<int> destination)
	{
		var s = text.ToString();
		var ids = new List<int>(MaxTokenCount(text));

		if (_model.PreTokenizer == PreTokenizerKind.ByteLevel)
		{
			foreach (var word in Gpt2PreTokenizer.Split(s))
			{
				var encoded = ByteLevelEncoding.Encode(word);
				var symbols = SplitChars(encoded);
				var merged = BpeMerger.Merge(symbols, _model.MergeRanks);
				AppendTokens(merged, ids);
			}
		}
		else
		{
			var normalized = s.Replace(' ', _model.MetaspaceReplacement);
			foreach (var segment in MetaspaceSplit(normalized, _model.MetaspaceReplacement))
			{
				var symbols = SplitChars(segment);
				var merged = BpeMerger.Merge(symbols, _model.MergeRanks);
				AppendTokens(merged, ids);
			}
		}

		var count = Math.Min(ids.Count, destination.Length);
		for (var i = 0; i < count; i++)
			destination[i] = ids[i];
		return ids.Count;
	}

	private static List<string> SplitChars(string text)
	{
		var symbols = new List<string>(text.Length);
		foreach (var ch in text)
			symbols.Add(ch.ToString());
		return symbols;
	}

	private static IEnumerable<string> MetaspaceSplit(string text, char replacement)
	{
		if (text.Length == 0)
			yield break;

		var withPrefix = text[0] == replacement ? text : replacement + text;
		var start = 0;
		for (var i = 1; i < withPrefix.Length; i++)
		{
			if (withPrefix[i] != replacement)
				continue;
			yield return withPrefix[start..i];
			start = i;
		}
		yield return withPrefix[start..];
	}

	private void AppendTokens(List<string> mergedSymbols, List<int> output)
	{
		foreach (var symbol in mergedSymbols)
		{
			if (_model.Vocab.TryGetValue(symbol, out var id))
			{
				output.Add(id);
				continue;
			}

			if (_model.ByteFallback && TryByteFallback(symbol, output))
				continue;

			if (_model.UnkTokenId is { } unkId)
			{
				output.Add(unkId);
				continue;
			}

			throw new InvalidOperationException(
				$"no vocab entry for symbol '{symbol}' and this tokenizer has neither byte-fallback nor an unk token");
		}
	}

	private bool TryByteFallback(string symbol, List<int> output)
	{
		var byteCount = Encoding.UTF8.GetByteCount(symbol);
		scoped Span<byte> bytes;
		if (byteCount <= 64)
		{
			Span<byte> stack = stackalloc byte[byteCount];
			bytes = stack;
		}
		else
		{
			bytes = new byte[byteCount];
		}
		_ = Encoding.UTF8.GetBytes(symbol, bytes);

		Span<int> fallbackIds = stackalloc int[byteCount];
		for (var i = 0; i < byteCount; i++)
		{
			var token = $"<0x{bytes[i]:X2}>";
			if (!_model.Vocab.TryGetValue(token, out var byteId))
				return false;
			fallbackIds[i] = byteId;
		}

		foreach (var id in fallbackIds)
			output.Add(id);
		return true;
	}
}
