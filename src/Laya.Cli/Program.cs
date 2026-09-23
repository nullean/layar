using System.Diagnostics;
using Laya.Core;
using Laya.Onnx;
using Laya.Tokenization;
using Laya.TorchSharp;

if (args.Length == 0)
{
	PrintUsage();
	return 1;
}

var command = args[0];
var rest = args[1..];

switch (command)
{
	case "predict":
		return await RunPredict(rest);
	case "benchmark":
		return await RunBenchmark(rest);
	default:
		PrintUsage();
		return 1;
}

static void PrintUsage() => Console.WriteLine("""
	layar predict --model <dir> --backend onnx|torchsharp --state <text>
	layar benchmark --model <dir> --state <text> [--iterations N]

	<dir> is a directory produced by tools/layar-export (containing model.onnx,
	model.pt, tokenizer/, config.json).
	""");

static string RequireOption(string[] args, string name)
{
	for (var i = 0; i < args.Length - 1; i++)
	{
		if (args[i] == name)
			return args[i + 1];
	}
	throw new ArgumentException($"missing required option {name}");
}

static string? GetOption(string[] args, string name)
{
	for (var i = 0; i < args.Length - 1; i++)
	{
		if (args[i] == name)
			return args[i + 1];
	}
	return null;
}

static (DecisionEngine Engine, ITokenizer Tokenizer, IDecisionBackend Backend) Load(string modelDir, string backendName)
{
	var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(Path.Combine(modelDir, "tokenizer"));
	IDecisionBackend backend = backendName switch
	{
		"onnx" => new OnnxDecisionBackend(Path.Combine(modelDir, "model.onnx")),
		"torchsharp" => new TorchSharpDecisionBackend(Path.Combine(modelDir, "model.pt")),
		_ => throw new ArgumentException($"unknown backend '{backendName}'; use 'onnx' or 'torchsharp'"),
	};
	var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);
	return (engine, tokenizer, backend);
}

static async Task<int> RunPredict(string[] args)
{
	var modelDir = RequireOption(args, "--model");
	var backendName = GetOption(args, "--backend") ?? "onnx";
	var state = RequireOption(args, "--state");

	var (engine, _, backend) = Load(modelDir, backendName);
	using var _ = backend;

	var answers = await engine.PredictAsync(state, Presets.TriageQuestions());
	var triage = answers.AsTriage(); // typed accessor — no string keys, no casts, for a known preset schema

	Console.WriteLine($"intent: {triage.Intent.Choice} (confidence {triage.Intent.Confidence:F2})");
	Console.WriteLine($"is_urgent: {triage.IsUrgent.Noul:F2} (confidence {triage.IsUrgent.Confidence:F2})");
	Console.WriteLine($"frustration: {triage.Frustration.Score:F2} (confidence {triage.Frustration.Confidence:F2})");
	Console.WriteLine($"refund_requested: {triage.RefundRequested.Noul:F2} (confidence {triage.RefundRequested.Confidence:F2})");
	Console.WriteLine($"churn_risk: {triage.ChurnRisk.Noul:F2} (confidence {triage.ChurnRisk.Confidence:F2})");
	return 0;
}

static async Task<int> RunBenchmark(string[] args)
{
	var modelDir = RequireOption(args, "--model");
	var state = RequireOption(args, "--state");
	var iterations = int.Parse(GetOption(args, "--iterations") ?? "20", System.Globalization.CultureInfo.InvariantCulture);

	var questions = Presets.TriageQuestions();

	var (onnxEngine, _, onnxBackend) = Load(modelDir, "onnx");
	using var _1 = onnxBackend;
	var (torchEngine, _, torchBackend) = Load(modelDir, "torchsharp");
	using var _2 = torchBackend;

	var onnxAnswers = await Time("ONNX", onnxEngine, state, questions, iterations);
	var torchAnswers = await Time("TorchSharp", torchEngine, state, questions, iterations);

	Console.WriteLine();
	Console.WriteLine("Numeric agreement (max abs diff per question, confidence):");
	foreach (var id in questions.Keys)
	{
		var a = onnxAnswers[id];
		var b = torchAnswers[id];
		var diff = Math.Abs(a.Confidence - b.Confidence);
		Console.WriteLine($"  {id}: confidence diff = {diff:E2}");
	}
	return 0;
}

static async Task<IReadOnlyDictionary<string, Answer>> Time(
	string name, DecisionEngine engine, string state, IReadOnlyDictionary<string, Question> questions, int iterations)
{
	var answers = await engine.PredictAsync(state, questions); // warmup + result to compare
	var sw = Stopwatch.StartNew();
	for (var i = 0; i < iterations; i++)
		_ = await engine.PredictAsync(state, questions);
	sw.Stop();
	Console.WriteLine($"{name}: {sw.Elapsed.TotalMilliseconds / iterations:F1} ms/call avg over {iterations} calls");
	return answers;
}
