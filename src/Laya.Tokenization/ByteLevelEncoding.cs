using System.Collections.Frozen;
using System.Text;

namespace Laya.Tokenization;

/// <summary>
/// The GPT-2 byte&lt;-&gt;printable-Unicode-character mapping every byte-level BPE tokenizer (GPT-2,
/// RoBERTa, ModernBERT's OLMo-derived tokenizer, ...) uses so that BPE — which operates on
/// printable "symbols" — can merge over arbitrary bytes, including ones with no printable glyph.
/// Built at static-init time by the same algorithm the reference implementation uses, rather than
/// hand-transcribing a 256-entry table.
/// </summary>
internal static class ByteLevelEncoding
{
	private static readonly char[] ByteToChar = Build();
	private static readonly FrozenDictionary<char, byte> CharToByteMap = BuildReverse();

	private static char[] Build()
	{
		var printable = new List<int>();
		for (var b = '!'; b <= '~'; b++)
			printable.Add(b);
		for (var b = '¡'; b <= '¬'; b++)
			printable.Add(b);
		for (var b = '®'; b <= 'ÿ'; b++)
			printable.Add(b);

		var table = new char[256];
		var assigned = new bool[256];
		foreach (var b in printable)
		{
			table[b] = (char)b;
			assigned[b] = true;
		}

		var n = 0;
		for (var b = 0; b < 256; b++)
		{
			if (assigned[b])
				continue;
			table[b] = (char)(256 + n);
			n++;
		}
		return table;
	}

	private static FrozenDictionary<char, byte> BuildReverse()
	{
		var map = new Dictionary<char, byte>(256);
		for (var b = 0; b < 256; b++)
			map[ByteToChar[b]] = (byte)b;
		return map.ToFrozenDictionary();
	}

	/// <summary>Encodes the UTF-8 bytes of <paramref name="text"/> as one printable character per
	/// byte, the alphabet byte-level BPE merges over.</summary>
	public static string Encode(ReadOnlySpan<char> text)
	{
		var byteCount = Encoding.UTF8.GetByteCount(text);
		scoped Span<byte> bytes;
		if (byteCount <= 1024)
		{
			Span<byte> stack = stackalloc byte[byteCount];
			bytes = stack;
		}
		else
		{
			bytes = new byte[byteCount];
		}
		_ = Encoding.UTF8.GetBytes(text, bytes);

		var chars = new char[byteCount];
		for (var i = 0; i < byteCount; i++)
			chars[i] = ByteToChar[bytes[i]];
		return new string(chars);
	}

	/// <summary>Decodes a byte-level-encoded symbol back to its raw bytes.</summary>
	public static void DecodeInto(ReadOnlySpan<char> symbol, Span<byte> destination)
	{
		for (var i = 0; i < symbol.Length; i++)
			destination[i] = CharToByteMap[symbol[i]];
	}
}
