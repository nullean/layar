using Laya.Core;
using TorchSharp;
using static TorchSharp.torch;

namespace Laya.TorchSharp;

/// <summary>
/// Runs the decision model's forward pass through libtorch via a loaded TorchScript module (see
/// <c>tools/layar-export</c> — the same checkpoint Laya.Onnx's backend gets exported from as ONNX,
/// so the two backends answer identical requests from the same underlying computation).
/// </summary>
public sealed class TorchSharpDecisionBackend : IDecisionBackend
{
	private readonly jit.ScriptModule _module;

	public TorchSharpDecisionBackend(string modelPath, DeviceType deviceType = DeviceType.CPU)
	{
		_module = jit.load(modelPath, deviceType);
		_module.eval();
	}

	public ValueTask<DecisionOutput> PredictAsync(DecisionRequest request, CancellationToken cancellationToken = default)
	{
		var batch = request.BatchSize;
		var seqLen = request.SequenceLength;
		var markerCapacity = request.MarkerCapacity;

		using var _ = torch.no_grad();

		using var inputIds = ToTensor(request.InputIds, batch, seqLen, ScalarType.Int64);
		using var attentionMask = ToTensor(request.AttentionMask, batch, seqLen, ScalarType.Int64);
		using var markerPos = ToTensor(request.MarkerPositions, batch, markerCapacity, ScalarType.Int64);
		using var markerMask = ToBoolTensor(request.MarkerMask, batch, markerCapacity);
		using var qtype = ToTensor(request.QuestionTypes, batch, 1, ScalarType.Int64).reshape(batch);

		var raw = _module.forward([inputIds, attentionMask, markerPos, markerMask, qtype]);
		var (logitsTensor, actTensor) = ((Tensor, Tensor))raw;
		using (logitsTensor)
		using (actTensor)
		{
			var logits = TensorToFloatArray(logitsTensor);
			var act = TensorToFloatArray(actTensor);
			var actionClassCount = act.Length / batch;
			return ValueTask.FromResult(new DecisionOutput(logits, act, batch, markerCapacity, actionClassCount));
		}
	}

	private static Tensor ToTensor(int[] source, int rows, int cols, ScalarType dtype)
	{
		var longs = new long[source.Length];
		for (var i = 0; i < source.Length; i++)
			longs[i] = source[i];
		return torch.tensor(longs, dtype: dtype).reshape(rows, cols);
	}

	private static Tensor ToBoolTensor(bool[] source, int rows, int cols) =>
		torch.tensor(source).reshape(rows, cols);

	private static float[] TensorToFloatArray(Tensor tensor)
	{
		using var flat = tensor.reshape(-1).to(ScalarType.Float32).cpu();
		var result = new float[flat.NumberOfElements];
		flat.data<float>().CopyTo(result);
		return result;
	}

	public void Dispose() => _module.Dispose();
}
