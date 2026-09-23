using Laya.Core;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Laya.Onnx;

/// <summary>
/// Runs the decision model's forward pass through ONNX Runtime. The exported graph (see
/// <c>tools/layar-export</c>) takes the same five inputs as the Python package's
/// <c>DecisionModel.forward</c> and returns the same two outputs.
/// </summary>
public sealed class OnnxDecisionBackend(string modelPath, SessionOptions? options = null) : IDecisionBackend
{
	private readonly InferenceSession _session = new(modelPath, options ?? new SessionOptions());

	public ValueTask<DecisionOutput> PredictAsync(DecisionRequest request, CancellationToken cancellationToken = default)
	{
		var batch = request.BatchSize;
		var seqLen = request.SequenceLength;
		var markerCapacity = request.MarkerCapacity;

		var inputIds = ToInt64(request.InputIds);
		var attentionMask = ToInt64(request.AttentionMask);
		var markerPos = ToInt64(request.MarkerPositions);
		var qtype = ToInt64(request.QuestionTypes);

		var inputIdsTensor = new DenseTensor<long>(inputIds, [batch, seqLen]);
		var attentionMaskTensor = new DenseTensor<long>(attentionMask, [batch, seqLen]);
		var markerPosTensor = new DenseTensor<long>(markerPos, [batch, markerCapacity]);
		var markerMaskTensor = new DenseTensor<bool>(request.MarkerMask, [batch, markerCapacity]);
		var qtypeTensor = new DenseTensor<long>(qtype, [batch]);

		var inputs = new List<NamedOnnxValue>
		{
			NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
			NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
			NamedOnnxValue.CreateFromTensor("marker_pos", markerPosTensor),
			NamedOnnxValue.CreateFromTensor("marker_mask", markerMaskTensor),
			NamedOnnxValue.CreateFromTensor("qtype", qtypeTensor),
		};

		using var results = _session.Run(inputs);
		var logits = results.First(r => r.Name == "logits").AsEnumerable<float>().ToArray();
		var act = results.First(r => r.Name == "act_logits").AsEnumerable<float>().ToArray();
		var actionClassCount = act.Length / batch;

		return ValueTask.FromResult(new DecisionOutput(logits, act, batch, markerCapacity, actionClassCount));
	}

	private static long[] ToInt64(int[] source)
	{
		var result = new long[source.Length];
		for (var i = 0; i < source.Length; i++)
			result[i] = source[i];
		return result;
	}

	public void Dispose() => _session.Dispose();
}
