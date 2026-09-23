namespace Laya.Core;

/// <summary>
/// The minimal tokenizer surface <see cref="SequenceBuilder"/> needs. Implemented by
/// <c>Laya.Tokenization</c> for the two checkpoint families (byte-level BPE for the English
/// checkpoint, Metaspace BPE for the multilingual one); a test double can implement it directly
/// against fixed token ids without a real BPE merge table.
/// </summary>
public interface ITokenizer
{
	int ClsTokenId { get; }
	int SepTokenId { get; }
	int MaskTokenId { get; }
	int PadTokenId { get; }

	/// <summary>The literal mask token text (e.g. <c>"&lt;mask&gt;"</c>), stripped out of any text
	/// that flows into the sequence before encoding, matching the Python port's guard against a
	/// caller's instructions or state text accidentally containing it.</summary>
	string MaskToken { get; }

	/// <summary>
	/// Encodes <paramref name="text"/> (no special tokens added) into <paramref name="destination"/>,
	/// returning the number of token ids written. <paramref name="destination"/> must be at least
	/// <see cref="MaxTokenCount"/> long for this text.
	/// </summary>
	int Encode(ReadOnlySpan<char> text, Span<int> destination);

	/// <summary>Safe upper bound on the token count <see cref="Encode"/> can produce for text of
	/// this length, for sizing the destination buffer without a trial encode.</summary>
	int MaxTokenCount(ReadOnlySpan<char> text);
}
