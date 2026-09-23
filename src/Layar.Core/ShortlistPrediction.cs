namespace Laya.Core;

/// <summary>Shortlists every high-cardinality choice question in a request, then predicts once —
/// the direct port of the Python package's <c>predict_shortlist</c>.</summary>
public static class ShortlistPrediction
{
	/// <summary>
	/// Shortlists each <see cref="ChoiceQuestion"/> in <paramref name="questions"/> against
	/// <paramref name="stateText"/> (non-choice questions, and any choice question with
	/// &lt;= <paramref name="k"/> options, are forwarded unchanged and never call
	/// <paramref name="embeddings"/>), then answers the reduced question set in one
	/// <see cref="DecisionEngine.PredictAsync"/> call. The engine's own probabilities/confidence
	/// on a shortlisted choice are over the kept labels only.
	/// </summary>
	public static async ValueTask<(IReadOnlyDictionary<string, Answer> Answers, IReadOnlyDictionary<string, Shortlist.Result> Shortlists)>
		PredictShortlistAsync(
			this DecisionEngine engine,
			string stateText,
			IReadOnlyDictionary<string, Question> questions,
			IEmbeddingProvider embeddings,
			int k = Shortlist.DefaultK,
			CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(engine);
		ArgumentNullException.ThrowIfNull(questions);

		var reduced = new Dictionary<string, Question>(questions.Count);
		var shortlists = new Dictionary<string, Shortlist.Result>();

		foreach (var (id, question) in questions)
		{
			if (question is not ChoiceQuestion choice)
			{
				reduced[id] = question;
				continue;
			}

			var result = await Shortlist.RankChoiceAsync(stateText, choice, embeddings, k, cancellationToken).ConfigureAwait(false);
			shortlists[id] = result;
			reduced[id] = Shortlist.Apply(choice, result);
		}

		var answers = await engine.PredictAsync(stateText, reduced, cancellationToken).ConfigureAwait(false);
		return (answers, shortlists);
	}
}
