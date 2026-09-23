namespace Laya.Core;

/// <summary>
/// Embeds a batch of strings for cosine-similarity ranking — the interchangeable half of the
/// Python package's caller-supplied <c>embed_fn</c> (see <c>shortlist.py</c>). <see cref="Shortlist"/>
/// calls this once per ranked question: the query text first, then one string per option in
/// criteria order, matching <c>embed_fn(texts) -> (n, dim)</c> exactly.
/// </summary>
/// <remarks>
/// Python also ships <c>embed_fn_from_agent</c> — an embedder derived from the loaded checkpoint's
/// own encoder (mean-pooled hidden states). No equivalent ships here: <see cref="IDecisionBackend"/>
/// only exposes the decision head's final output, not raw encoder hidden states, so deriving
/// embeddings from a Layar backend would need a third exported graph (encoder-only, mean-pooled)
/// that <c>tools/layar-export</c> doesn't produce today. <see cref="Shortlist"/> itself doesn't
/// care how embeddings are produced — any implementation of this interface works, including one
/// backed by a real bi-encoder, which the Python docs note usually shortlists better anyway.
/// </remarks>
public interface IEmbeddingProvider
{
	/// <summary>Embeds each of <paramref name="texts"/>, returning one vector per input in the
	/// same order. All vectors must have the same length.</summary>
	ValueTask<ReadOnlyMemory<float>[]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
