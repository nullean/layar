namespace Laya.Core;

/// <summary>
/// Pads and stacks the per-question sequences of one request into a single
/// <see cref="DecisionRequest"/> batch — the direct port of the Python package's
/// <c>collate_items</c>, minus the training-only <c>target</c>/<c>label</c>/<c>meta</c> fields an
/// inference-only port has no use for.
/// </summary>
public static class SequenceBatch
{
	public static DecisionRequest Collate(
		IReadOnlyList<SequenceBuilder.BuiltSequence> sequences,
		IReadOnlyList<QuestionType> questionTypes,
		int padTokenId)
	{
		ArgumentNullException.ThrowIfNull(sequences);
		ArgumentNullException.ThrowIfNull(questionTypes);
		if (sequences.Count == 0)
			throw new ArgumentException("at least one sequence is required", nameof(sequences));
		if (sequences.Count != questionTypes.Count)
			throw new ArgumentException("sequences and questionTypes must have the same length", nameof(questionTypes));

		var batch = sequences.Count;
		var seqLen = 0;
		// At least 2: both exported backends (Laya.Onnx, Laya.TorchSharp) trace a fixed graph from
		// an example batch, and the model's own top-2-vs-rest "act" feature branches on whether
		// there are >= 2 markers — tracing bakes that branch in as a constant. A single-option
		// question padded to 2 (with the pad slot masked out) always takes the traced branch.
		var markerCapacity = 2;
		foreach (var s in sequences)
		{
			seqLen = Math.Max(seqLen, s.TokenIds.Length);
			markerCapacity = Math.Max(markerCapacity, s.MarkerPositions.Length);
		}

		var inputIds = new int[batch * seqLen];
		if (padTokenId != 0)
			Array.Fill(inputIds, padTokenId);
		var attentionMask = new int[batch * seqLen];
		var markerPositions = new int[batch * markerCapacity];
		var markerMask = new bool[batch * markerCapacity];
		var qtypes = new int[batch];

		for (var i = 0; i < batch; i++)
		{
			var s = sequences[i];
			var rowOffset = i * seqLen;
			s.TokenIds.CopyTo(inputIds.AsSpan(rowOffset));
			attentionMask.AsSpan(rowOffset, s.TokenIds.Length).Fill(1);

			var markerOffset = i * markerCapacity;
			for (var k = 0; k < s.MarkerPositions.Length; k++)
			{
				markerPositions[markerOffset + k] = s.MarkerPositions[k];
				markerMask[markerOffset + k] = true;
			}

			qtypes[i] = (int)questionTypes[i];
		}

		return new DecisionRequest(inputIds, attentionMask, markerPositions, markerMask, qtypes, batch, seqLen, markerCapacity);
	}
}
