using System.Numerics.Tensors;

namespace Laya.Core;

/// <summary>
/// Opt-in embedding shortlist for high-cardinality choice questions: choice options share one
/// head-token budget (see <see cref="SequenceBuilder"/>), so a large label set leaves only a few
/// tokens per label. This ranks options by cosine similarity to the state and keeps the top
/// <see cref="DefaultK"/>, so a decision model with a fixed token budget can still choose sensibly
/// among far more than ~20 labels. Direct port of the Python package's <c>shortlist.py</c> — the
/// coarse-to-fine pattern its docs describe.
/// </summary>
/// <remarks>This only reduces which options are sent to <see cref="DecisionEngine"/> — it does not
/// run the decision model a second time or replace its own forward pass.</remarks>
public static class Shortlist
{
	public const int DefaultK = 20;

	/// <summary>The outcome of ranking one choice question's options.</summary>
	/// <param name="Labels">Kept labels, best match first. All of them when <see cref="Passthrough"/>.</param>
	/// <param name="Scores">Cosine similarity per kept label, in the same order — null when
	/// <see cref="Passthrough"/> (nothing was dropped, so no ranking happened).</param>
	/// <param name="Passthrough">True when the question already had &lt;= k options, so every
	/// label was kept in its original order and the embedding provider was never called.</param>
	/// <param name="OptionCount">The question's original option count, before shortlisting.</param>
	public readonly record struct Result(IReadOnlyList<string> Labels, IReadOnlyList<float>? Scores, bool Passthrough, int OptionCount);

	/// <summary>
	/// Ranks <paramref name="question"/>'s options against <paramref name="stateText"/> and keeps
	/// the top <paramref name="k"/>. Ties keep the earlier label; a zero vector (or a zero-norm
	/// query) scores 0 and does not outrank an option that came before it — matching the Python
	/// port's <c>_cosine</c>/<c>_rank</c> exactly, including NaN/Infinity in a provider's output
	/// being treated as 0 rather than propagating.
	/// </summary>
	public static async ValueTask<Result> RankChoiceAsync(
		string stateText,
		ChoiceQuestion question,
		IEmbeddingProvider embeddings,
		int k = DefaultK,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(stateText);
		ArgumentNullException.ThrowIfNull(question);
		ArgumentNullException.ThrowIfNull(embeddings);
		if (k < 1)
			throw new ArgumentOutOfRangeException(nameof(k), k, "k must be a positive integer");

		var options = question.Options;
		var n = options.Count;
		if (n == 0)
			throw new ArgumentException("choice question must have at least one option", nameof(question));

		var seen = new HashSet<string>(n);
		foreach (var option in options)
		{
			if (!seen.Add(option.Label))
				throw new ArgumentException($"choice option label '{option.Label}' is duplicated", nameof(question));
		}

		if (k >= n)
		{
			var allLabels = new string[n];
			for (var i = 0; i < n; i++)
				allLabels[i] = options[i].Label;
			return new Result(allLabels, null, true, n);
		}

		var queryText = string.IsNullOrEmpty(question.Instructions)
			? stateText
			: $"{question.Instructions}\n{stateText}";

		var optionTexts = question.RenderOptions();
		var texts = new string[n + 1];
		texts[0] = queryText;
		for (var i = 0; i < n; i++)
			texts[i + 1] = optionTexts[i];

		var vectors = await embeddings.EmbedBatchAsync(texts, cancellationToken).ConfigureAwait(false);
		if (vectors.Length != texts.Length)
		{
			throw new InvalidOperationException(
				$"embedding provider returned {vectors.Length} vectors for {texts.Length} texts");
		}

		var query = Sanitized(vectors[0]);
		var docs = new float[n][];
		for (var i = 0; i < n; i++)
			docs[i] = Sanitized(vectors[i + 1]);

		var similarities = Cosine(query, docs);

		var order = new int[n];
		for (var i = 0; i < n; i++)
			order[i] = i;
		// Stable descending sort: OrderByDescending preserves relative order among ties, matching
		// numpy's argsort(-sims, kind="mergesort").
		var ranked = order.OrderByDescending(i => similarities[i]).Take(k).ToArray();

		var labels = new string[ranked.Length];
		var scores = new float[ranked.Length];
		for (var i = 0; i < ranked.Length; i++)
		{
			labels[i] = options[ranked[i]].Label;
			scores[i] = similarities[ranked[i]];
		}
		return new Result(labels, scores, false, n);
	}

	/// <summary>Rebuilds <paramref name="question"/> with only the ranked options, in ranked
	/// order — a no-op when <paramref name="result"/> is a passthrough.</summary>
	public static ChoiceQuestion Apply(ChoiceQuestion question, Result result)
	{
		ArgumentNullException.ThrowIfNull(question);
		if (result.Passthrough)
			return question;

		var byLabel = new Dictionary<string, ChoiceOption>(question.Options.Count);
		foreach (var option in question.Options)
			byLabel[option.Label] = option;

		var subset = new ChoiceOption[result.Labels.Count];
		for (var i = 0; i < result.Labels.Count; i++)
			subset[i] = byLabel[result.Labels[i]];

		return question with { Options = subset };
	}

	private static float[] Sanitized(ReadOnlyMemory<float> vector)
	{
		var array = vector.ToArray();
		for (var i = 0; i < array.Length; i++)
		{
			if (float.IsNaN(array[i]) || float.IsInfinity(array[i]))
				array[i] = 0f;
		}
		return array;
	}

	private static float[] Cosine(float[] query, float[][] docs)
	{
		var similarities = new float[docs.Length];
		var queryNorm = TensorPrimitives.Norm((ReadOnlySpan<float>)query);
		if (queryNorm == 0f)
			return similarities;

		for (var i = 0; i < docs.Length; i++)
		{
			var docNorm = TensorPrimitives.Norm((ReadOnlySpan<float>)docs[i]);
			var denominator = docNorm * queryNorm;
			if (denominator > 0f)
				similarities[i] = TensorPrimitives.Dot((ReadOnlySpan<float>)docs[i], query) / denominator;
		}
		return similarities;
	}
}
