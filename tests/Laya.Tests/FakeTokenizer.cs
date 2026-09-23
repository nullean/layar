using Laya.Core;

namespace Laya.Tests;

/// <summary>A deterministic, non-BPE tokenizer for testing <see cref="SequenceBuilder"/>'s budget
/// and layout logic in isolation from real tokenization (see Laya.Tokenization's own conformance
/// tests, verified against the Python oracle, for BPE correctness).</summary>
internal sealed class FakeTokenizer : ITokenizer
{
	public int ClsTokenId => 1;
	public int SepTokenId => 2;
	public int MaskTokenId => 3;
	public int PadTokenId => 0;
	public string MaskToken => "[MASK]";

	public int MaxTokenCount(ReadOnlySpan<char> text) => text.Length + 1;

	public int Encode(ReadOnlySpan<char> text, Span<int> destination)
	{
		var count = 0;
		foreach (var ch in text)
		{
			if (char.IsWhiteSpace(ch))
				continue;
			if (count >= destination.Length)
				break;
			destination[count++] = 100 + ch;
		}
		return count;
	}
}
