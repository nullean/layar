using Laya.Core;

namespace Laya.Tests;

/// <summary>Maps each text to a fixed vector via a lookup, so tests can hand-compute expected
/// cosine similarities instead of depending on a real embedding model.</summary>
internal sealed class FakeEmbeddingProvider(IReadOnlyDictionary<string, float[]> vectorsByText) : IEmbeddingProvider
{
	public int CallCount { get; private set; }

	public ValueTask<ReadOnlyMemory<float>[]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
	{
		CallCount++;
		var result = new ReadOnlyMemory<float>[texts.Count];
		for (var i = 0; i < texts.Count; i++)
		{
			if (!vectorsByText.TryGetValue(texts[i], out var vector))
				throw new KeyNotFoundException($"no fake vector registered for text '{texts[i]}'");
			result[i] = vector;
		}
		return ValueTask.FromResult(result);
	}
}
