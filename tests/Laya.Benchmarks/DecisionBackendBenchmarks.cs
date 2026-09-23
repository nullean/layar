using BenchmarkDotNet.Attributes;
using Laya.Core;
using Laya.Onnx;
using Laya.Tokenization;
using Laya.TorchSharp;

namespace Laya.Benchmarks;

/// <summary>
/// The actual "so we can compare" deliverable: ONNX Runtime vs. TorchSharp answering the same
/// real 3-question batch through the same <see cref="DecisionEngine"/> orchestration, on the
/// multilingual checkpoint <c>tools/layar-export</c> produces. Requires that checkpoint to be
/// exported locally first (<c>.artifacts/models/multilingual</c>) — there's no fixture-skip here
/// the way there is in <c>Laya.Tests</c>, since a benchmark with nothing to measure isn't useful;
/// run the export tool before running this project.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DecisionBackendBenchmarks
{
	private const string State =
		"{\"body\": \"Hi, we were billed twice for March. Please refund the duplicate today or we will cancel our plan.\"}";

	private static readonly IReadOnlyDictionary<string, Question> Questions = new Dictionary<string, Question>
	{
		["department"] = new ChoiceQuestion(
			"Which department should handle this?",
			[
				new("billing", "invoices, payments, refunds"),
				new("technical", "bugs, outages"),
				new("sales", "pricing"),
				new("other", "everything else"),
			]),
		["urgency"] = new ScoreQuestion("How urgent is this?", ["not urgent", "soon", "critical"]),
		["churn_risk"] = new NoulQuestion("Does the user threaten to cancel?"),
	};

	private DecisionEngine _onnxEngine = null!;
	private DecisionEngine _torchSharpEngine = null!;

	[GlobalSetup]
	public void Setup()
	{
		var modelDir = FindCheckpointDirectory("multilingual") ?? throw new InvalidOperationException(
			"multilingual checkpoint not found under .artifacts/models/multilingual — run " +
			"tools/layar-export/export.py first (see README).");

		var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(Path.Combine(modelDir, "tokenizer"));
		var onnxBackend = new OnnxDecisionBackend(Path.Combine(modelDir, "model.onnx"));
		var torchSharpBackend = new TorchSharpDecisionBackend(Path.Combine(modelDir, "model.pt"));

		_onnxEngine = new DecisionEngine(tokenizer, onnxBackend, maxLen: 1024, headMaxLen: 256);
		_torchSharpEngine = new DecisionEngine(tokenizer, torchSharpBackend, maxLen: 1024, headMaxLen: 256);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_onnxEngine.Dispose();
		_torchSharpEngine.Dispose();
	}

	[Benchmark(Baseline = true)]
	public async Task<IReadOnlyDictionary<string, Answer>> Onnx() =>
		await _onnxEngine.PredictAsync(State, Questions);

	[Benchmark]
	public async Task<IReadOnlyDictionary<string, Answer>> TorchSharp() =>
		await _torchSharpEngine.PredictAsync(State, Questions);

	private static string? FindCheckpointDirectory(string checkpointName)
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			if (dir.GetFiles("layar.slnx").Length > 0)
			{
				var candidate = Path.Combine(dir.FullName, ".artifacts", "models", checkpointName);
				return Directory.Exists(Path.Combine(candidate, "tokenizer")) ? candidate : null;
			}
			dir = dir.Parent;
		}
		return null;
	}
}
