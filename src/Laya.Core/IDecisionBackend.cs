namespace Laya.Core;

/// <summary>
/// A padded batch of sequences ready for one forward pass — every question in a single
/// <c>predict</c> call is answered together, in one batch, matching the Python port's
/// single-forward-pass design. Row-major: row <c>i</c> of a <c>batch*width</c> array occupies
/// <c>[i*width, (i+1)*width)</c>.
/// </summary>
public sealed record DecisionRequest(
	int[] InputIds,
	int[] AttentionMask,
	int[] MarkerPositions,
	bool[] MarkerMask,
	int[] QuestionTypes,
	int BatchSize,
	int SequenceLength,
	int MarkerCapacity);

/// <summary>
/// A backend's raw output for a <see cref="DecisionRequest"/>: per-marker logits (masked
/// positions carry an arbitrary large-negative value, matching the model's own masking) and the
/// action head's raw logits. <see cref="ProbabilityMath"/> turns these into calibrated answers.
/// </summary>
public sealed record DecisionOutput(
	float[] Logits,
	float[] ActionLogits,
	int BatchSize,
	int MarkerCapacity,
	int ActionClassCount);

/// <summary>
/// Runs the decision model's forward pass. Implemented by <c>Laya.Onnx</c> (ONNX Runtime) and
/// <c>Laya.TorchSharp</c> (TorchSharp) — the seam that makes the two backends interchangeable and
/// comparable from <c>Laya.Cli</c>'s <c>benchmark</c> command.
/// </summary>
public interface IDecisionBackend : IDisposable
{
	ValueTask<DecisionOutput> PredictAsync(DecisionRequest request, CancellationToken cancellationToken = default);
}
