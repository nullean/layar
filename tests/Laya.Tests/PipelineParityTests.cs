using AwesomeAssertions;
using Laya.Core;
using Laya.Onnx;
using Laya.Tokenization;
using Laya.TorchSharp;

namespace Laya.Tests;

/// <summary>
/// End-to-end parity against the Python package's own <c>agent.predict()</c> on a real 3-question
/// batch — department/urgency/churn_risk over the same state, run through the full
/// Core+Tokenization+backend pipeline for both backends. Oracle values were captured once from
/// <c>~/Projects/laya</c>'s venv; this locks them in as a regression test instead of a one-off
/// scratchpad check.
/// </summary>
public class PipelineParityTests
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

	[Test]
	public async Task Onnx_backend_matches_python_oracle()
	{
		var dir = CheckpointFixtures.RequireOrSkip("multilingual");
		using var backend = new OnnxDecisionBackend(Path.Combine(dir, "model.onnx"));
		await AssertMatchesOracle(dir, backend);
	}

	[Test]
	public async Task TorchSharp_backend_matches_python_oracle()
	{
		var dir = CheckpointFixtures.RequireOrSkip("multilingual");
		using var backend = new TorchSharpDecisionBackend(Path.Combine(dir, "model.pt"));
		await AssertMatchesOracle(dir, backend);
	}

	private static async Task AssertMatchesOracle(string modelDir, IDecisionBackend backend)
	{
		var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(Path.Combine(modelDir, "tokenizer"));
		var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

		var answers = await engine.PredictAsync(State, Questions);

		var department = (ChoiceAnswer)answers["department"];
		department.Choice.Should().Be("billing");
		department.Confidence.Should().BeApproximately(1.0f, 1e-3f);
		department.Action.ActProbability.Should().BeApproximately(1.0f, 1e-3f);

		var urgency = (ScoreAnswer)answers["urgency"];
		urgency.Score.Should().BeApproximately(1.8658f, 1e-3f);
		urgency.Confidence.Should().BeApproximately(0.6313f, 1e-3f);

		var churnRisk = (NoulAnswer)answers["churn_risk"];
		churnRisk.Noul.Should().BeApproximately(0.1461f, 1e-3f);
		churnRisk.Confidence.Should().BeApproximately(0.8539f, 1e-3f);
	}
}
