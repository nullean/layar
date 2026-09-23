using System.Buffers;

namespace Laya.Core;

/// <summary>
/// Builds the fixed-shape token sequence a <see cref="Question"/> and a state string are packed
/// into for one forward pass: <c>[CLS] &lt;type&gt; question: instructions [SEP] [MASK] opt0 [MASK]
/// opt1 ... [SEP] state [SEP]</c>. Options and the head instructions share a fixed head-max-length
/// token budget; the state gets whatever is left of the overall max length.
/// </summary>
public static class SequenceBuilder
{
	private const int MaxOptionTokens = 48;

	public readonly struct BuiltSequence(int[] tokenIds, int[] markerPositions)
	{
		/// <summary>The full input sequence, ready to feed to a decision backend.</summary>
		public int[] TokenIds { get; } = tokenIds;

		/// <summary>Position of each option's <c>[MASK]</c> token within <see cref="TokenIds"/>,
		/// in option order — the decision head gathers hidden state at these positions.</summary>
		public int[] MarkerPositions { get; } = markerPositions;
	}

	public static BuiltSequence Build(
		ITokenizer tokenizer,
		string state,
		Question question,
		int maxLen = 512,
		int headMaxLen = 192,
		bool truncateLeft = false)
	{
		ArgumentNullException.ThrowIfNull(tokenizer);
		ArgumentNullException.ThrowIfNull(question);
		ArgumentNullException.ThrowIfNull(state);

		var maskToken = tokenizer.MaskToken;
		var options = question.RenderOptions();
		if (options.Count == 0)
			throw new ArgumentException("question has no options to render", nameof(question));

		var instructions = question.Instructions.Replace(maskToken, " ");
		var headText = $"{TypeName(question.Type)} question: {instructions}";

		var headPool = ArrayPool<int>.Shared.Rent(Math.Max(1, tokenizer.MaxTokenCount(headText)));
		int headCount;
		int[][] optionIds;
		try
		{
			headCount = tokenizer.Encode(headText, headPool);

			// Each option is [MASK] followed by up to MaxOptionTokens encoded tokens.
			optionIds = new int[options.Count][];
			Span<int> optScratch = stackalloc int[MaxOptionTokens + 8];
			for (var i = 0; i < options.Count; i++)
			{
				var optionText = " " + options[i].Replace(maskToken, " ");
				var written = EncodeCapped(tokenizer, optionText, optScratch);
				var take = Math.Min(written, MaxOptionTokens);
				var tokens = new int[take + 1];
				tokens[0] = tokenizer.MaskTokenId;
				optScratch[..take].CopyTo(tokens.AsSpan(1));
				optionIds[i] = tokens;
			}

			var optionTotal = Sum(optionIds);
			if (headMaxLen - optionTotal < 16)
			{
				var per = Math.Max(4, (headMaxLen - 16) / Math.Max(1, optionIds.Length));
				for (var i = 0; i < optionIds.Length; i++)
				{
					if (optionIds[i].Length > per)
						optionIds[i] = optionIds[i][..per];
				}
				optionTotal = Sum(optionIds);
			}

			var optBudget = headMaxLen - optionTotal;
			var headTake = Math.Min(headCount, Math.Max(8, optBudget));

			// [CLS] head [SEP] opt0 opt1 ... [SEP]
			var prefixLength = 1 + headTake + 1;
			var beforeState = prefixLength + optionTotal + 1;
			var room = Math.Max(0, maxLen - beforeState - 1);

			var stateText = state.Replace(maskToken, " ");
			var statePool = ArrayPool<int>.Shared.Rent(Math.Max(1, tokenizer.MaxTokenCount(stateText)));
			try
			{
				var stateCount = tokenizer.Encode(stateText, statePool);
				var stateTake = Math.Min(stateCount, room);
				var stateStart = truncateLeft ? stateCount - stateTake : 0;

				var totalLength = Math.Min(maxLen, beforeState + stateTake + 1);
				var ids = new int[totalLength];
				var markerPositions = new int[optionIds.Length];

				var pos = 0;
				ids[pos++] = tokenizer.ClsTokenId;
				headPool.AsSpan(0, headTake).CopyTo(ids.AsSpan(pos));
				pos += headTake;
				ids[pos++] = tokenizer.SepTokenId;

				var markerCount = 0;
				for (var i = 0; i < optionIds.Length; i++)
				{
					if (pos < totalLength)
						markerPositions[markerCount++] = pos;
					var o = optionIds[i];
					var copyLen = Math.Min(o.Length, Math.Max(0, totalLength - pos));
					o.AsSpan(0, copyLen).CopyTo(ids.AsSpan(pos));
					pos += copyLen;
				}
				if (pos < totalLength)
					ids[pos++] = tokenizer.SepTokenId;

				if (pos < totalLength)
				{
					var copyLen = Math.Min(stateTake, totalLength - pos);
					statePool.AsSpan(stateStart, copyLen).CopyTo(ids.AsSpan(pos));
					pos += copyLen;
				}

				if (pos < totalLength)
					ids[pos++] = tokenizer.SepTokenId;

				return new BuiltSequence(ids, markerPositions[..markerCount]);
			}
			finally
			{
				ArrayPool<int>.Shared.Return(statePool);
			}
		}
		finally
		{
			ArrayPool<int>.Shared.Return(headPool);
		}
	}

	private static int EncodeCapped(ITokenizer tokenizer, string text, Span<int> scratch)
	{
		var upperBound = tokenizer.MaxTokenCount(text);
		if (upperBound <= scratch.Length)
			return tokenizer.Encode(text, scratch);

		// Rare: an option text long enough that its raw token count could exceed the small
		// stack buffer before capping to MaxOptionTokens. Fall back to a pooled buffer.
		var pooled = ArrayPool<int>.Shared.Rent(upperBound);
		try
		{
			var written = tokenizer.Encode(text, pooled);
			var take = Math.Min(written, scratch.Length);
			pooled.AsSpan(0, take).CopyTo(scratch);
			return written;
		}
		finally
		{
			ArrayPool<int>.Shared.Return(pooled);
		}
	}

	private static int Sum(int[][] jagged)
	{
		var total = 0;
		foreach (var a in jagged)
			total += a.Length;
		return total;
	}

	private static string TypeName(QuestionType type) => type switch
	{
		QuestionType.Choice => "choice",
		QuestionType.Score => "score",
		QuestionType.Noul => "noul",
		_ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
	};
}
