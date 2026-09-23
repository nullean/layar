using System.Collections.Frozen;

namespace Laya.Tokenization;

internal enum PreTokenizerKind
{
	ByteLevel,
	Metaspace,
}

/// <summary>Everything <see cref="HuggingFaceBpeTokenizer"/> needs, parsed once from a Hugging Face
/// <c>tokenizer.json</c> + <c>tokenizer_config.json</c> pair.</summary>
internal sealed class TokenizerModel
{
	public required FrozenDictionary<string, int> Vocab { get; init; }
	public required FrozenDictionary<(string Left, string Right), int> MergeRanks { get; init; }
	public required PreTokenizerKind PreTokenizer { get; init; }
	public required char MetaspaceReplacement { get; init; }
	public required bool ByteFallback { get; init; }

	public required int ClsTokenId { get; init; }
	public required int SepTokenId { get; init; }
	public required int MaskTokenId { get; init; }
	public required int PadTokenId { get; init; }
	public required int? UnkTokenId { get; init; }
	public required string MaskToken { get; init; }
}
