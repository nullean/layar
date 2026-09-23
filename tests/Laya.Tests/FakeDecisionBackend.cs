using Laya.Core;

namespace Laya.Tests;

/// <summary>A trivial backend for testing orchestration (<see cref="Laya.Core.Routing.RouterEngine"/>,
/// <see cref="DecisionEngine"/>) without a real model: returns fixed logits and tracks disposal.</summary>
internal sealed class FakeDecisionBackend : IDecisionBackend
{
	public bool Disposed { get; private set; }
	public int PredictCallCount { get; private set; }

	public ValueTask<DecisionOutput> PredictAsync(DecisionRequest request, CancellationToken cancellationToken = default)
	{
		PredictCallCount++;
		var logits = new float[request.BatchSize * request.MarkerCapacity];
		for (var i = 0; i < request.BatchSize; i++)
			logits[i * request.MarkerCapacity] = 10f; // first option always wins, deterministically
		var action = new float[request.BatchSize * 2];
		return ValueTask.FromResult(new DecisionOutput(logits, action, request.BatchSize, request.MarkerCapacity, 2));
	}

	public void Dispose() => Disposed = true;
}
