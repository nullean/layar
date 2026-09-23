namespace Laya.Core;

/// <summary>
/// Orchestrates one <c>predict</c> call: builds a sequence per question, batches them into a
/// single forward pass, and turns the backend's raw logits into calibrated <see cref="Answer"/>
/// objects. The direct port of the Python package's <c>Agent.system_one</c>, parametrized over
/// whichever <see cref="IDecisionBackend"/> is loaded (ONNX or TorchSharp).
/// </summary>
public sealed class DecisionEngine(
	ITokenizer tokenizer,
	IDecisionBackend backend,
	int maxLen = 512,
	int headMaxLen = 192,
	float[]? temperatureByType = null,
	IReadOnlyDictionary<string, float>? temperatureByOptions = null) : IDisposable
{
	private readonly float[] _temperatureByType = temperatureByType ?? [1f, 1f, 1f];
	private readonly IReadOnlyDictionary<string, float> _temperatureByOptions =
		temperatureByOptions ?? new Dictionary<string, float>();

	/// <summary>Disposes the backend this engine owns — <see cref="Routing.RouterEngine"/> relies
	/// on this to evict/unload cleanly. Don't also separately dispose the same backend instance;
	/// whether that double-dispose is safe depends on the backend implementation.</summary>
	public void Dispose() => backend.Dispose();

	public async ValueTask<IReadOnlyDictionary<string, Answer>> PredictAsync(
		string state,
		IReadOnlyDictionary<string, Question> questions,
		CancellationToken cancellationToken = default)
	{
		var ids = questions.Keys.ToArray();
		var sequences = new SequenceBuilder.BuiltSequence[ids.Length];
		var types = new QuestionType[ids.Length];
		for (var i = 0; i < ids.Length; i++)
		{
			sequences[i] = SequenceBuilder.Build(tokenizer, state, questions[ids[i]], maxLen, headMaxLen);
			types[i] = questions[ids[i]].Type;
		}

		var request = SequenceBatch.Collate(sequences, types, tokenizer.PadTokenId);
		var output = await backend.PredictAsync(request, cancellationToken).ConfigureAwait(false);

		var answers = new Dictionary<string, Answer>();
		for (var r = 0; r < ids.Length; r++)
		{
			var question = questions[ids[r]];
			var k = sequences[r].MarkerPositions.Length;
			var bucket = ProbabilityMath.TemperatureBucket(question.Type, k);
			var temperature = _temperatureByOptions.TryGetValue(bucket, out var t)
				? t
				: _temperatureByType[(int)question.Type];

			var logitsRow = output.Logits.AsSpan(r * output.MarkerCapacity, output.MarkerCapacity);
			var probs = new float[k];
			ProbabilityMath.SoftmaxWithTemperature(logitsRow, k, ProbabilityMath.ClampTemperature(temperature), probs);
			var confidence = ProbabilityMath.ConfidenceFromProbabilities(probs);

			var actRow = output.ActionLogits.AsSpan(r * output.ActionClassCount, output.ActionClassCount);
			var actProbs = new float[actRow.Length];
			ProbabilityMath.SoftmaxWithTemperature(actRow, actRow.Length, 1f, actProbs);
			var action = new ActionInfo(actProbs[0]);

			answers[ids[r]] = question switch
			{
				ChoiceQuestion choice => BuildChoiceAnswer(choice, probs, confidence, action),
				ScoreQuestion score => BuildScoreAnswer(score, probs, confidence, action),
				NoulQuestion => new NoulAnswer(probs[1], MathF.Max(probs[1], 1f - probs[1]), action),
				_ => throw new NotSupportedException($"unsupported question type {question.GetType()}"),
			};
		}
		return answers;
	}

	private static ChoiceAnswer BuildChoiceAnswer(ChoiceQuestion question, float[] probabilities, float confidence, ActionInfo action)
	{
		var best = 0;
		for (var i = 1; i < probabilities.Length; i++)
		{
			if (probabilities[i] > probabilities[best])
				best = i;
		}

		var byLabel = new Dictionary<string, float>(probabilities.Length);
		for (var i = 0; i < probabilities.Length; i++)
			byLabel[question.Options[i].Label] = probabilities[i];

		return new ChoiceAnswer(question.Options[best].Label, byLabel, confidence, action);
	}

	private static ScoreAnswer BuildScoreAnswer(ScoreQuestion question, float[] probabilities, float confidence, ActionInfo action)
	{
		var expected = 0f;
		for (var i = 0; i < probabilities.Length; i++)
			expected += i * probabilities[i];
		return new ScoreAnswer(expected, question.Levels, probabilities, confidence, action);
	}
}
