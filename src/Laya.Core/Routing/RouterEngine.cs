using System.Text.Json;
using System.Threading;

namespace Laya.Core.Routing;

/// <summary>Builds the <see cref="DecisionEngine"/> for a checkpoint — typically loading a
/// tokenizer and constructing an <c>OnnxDecisionBackend</c>/<c>TorchSharpDecisionBackend</c> from a
/// local model directory. Laya.Core stays backend-agnostic; the caller supplies this.</summary>
public delegate DecisionEngine ModelLoader(ModelKey key);

/// <summary>
/// Routes a request to the right checkpoint and lazily loads/evicts the <see cref="DecisionEngine"/>
/// for it — the model-lifecycle half of the Python package's <c>Router</c> class (the routing
/// *decision* itself is <see cref="ModelRouter"/>, used internally here). A cold load is expensive
/// (building a backend from a checkpoint); at <c>maxLoaded=1</c> (the default), traffic that
/// alternates checkpoints reloads on every switch — call <see cref="Preload"/> for a server or
/// demo where that cost is unacceptable.
/// </summary>
public sealed class RouterEngine : IDisposable
{
	private readonly ModelLoader _loader;
	private readonly ModelRouter _router;
	private readonly Lock _gate = new();
	private readonly Dictionary<ModelKey, DecisionEngine> _loaded = [];
	private readonly List<ModelKey> _order = []; // least-recently-used first
	private int _maxLoaded;

	public RouterEngine(
		ModelLoader loader,
		int maxLoaded = 1,
		ModelKey defaultModel = ModelKey.English,
		bool autoTaskDetection = false)
	{
		ArgumentNullException.ThrowIfNull(loader);
		_loader = loader;
		_maxLoaded = Math.Max(1, maxLoaded);
		_router = new ModelRouter(defaultModel, autoTaskDetection);
	}

	/// <summary>Checkpoints currently resident, least-recently-used first.</summary>
	public IReadOnlyList<ModelKey> Loaded
	{
		get
		{
			lock (_gate)
				return [.. _order];
		}
	}

	/// <summary>Returns the engine for <paramref name="key"/>, building it via the loader on first
	/// use. Concurrent callers for the same key share one engine rather than building duplicates.</summary>
	public DecisionEngine Load(ModelKey key)
	{
		lock (_gate)
		{
			if (_loaded.TryGetValue(key, out var existing))
			{
				Touch(key);
				return existing;
			}

			var engine = _loader(key);
			_loaded[key] = engine;
			_order.Add(key);
			Evict();
			return engine;
		}
	}

	/// <summary>Registers an already-built engine under <paramref name="key"/> instead of loading a
	/// second copy — useful when the process already built one for other reasons.</summary>
	public void Attach(ModelKey key, DecisionEngine engine)
	{
		ArgumentNullException.ThrowIfNull(engine);
		lock (_gate)
		{
			_loaded[key] = engine;
			Touch(key);
			_maxLoaded = Math.Max(_maxLoaded, _loaded.Count);
		}
	}

	/// <summary>Builds every checkpoint in <paramref name="keys"/> (default: all three) up front,
	/// raising <c>maxLoaded</c> to fit so eviction doesn't immediately undo it.</summary>
	public void Preload(IReadOnlyList<ModelKey>? keys = null)
	{
		keys ??= [ModelKey.English, ModelKey.Multilingual, ModelKey.TypedDecisions];
		lock (_gate)
			_maxLoaded = Math.Max(_maxLoaded, keys.Count);
		foreach (var key in keys)
			_ = Load(key);
	}

	/// <summary>Frees one checkpoint, or all of them when <paramref name="key"/> is null.</summary>
	public void Unload(ModelKey? key = null)
	{
		lock (_gate)
		{
			if (key is null)
			{
				foreach (var engine in _loaded.Values)
					engine.Dispose();
				_loaded.Clear();
				_order.Clear();
			}
			else if (_loaded.Remove(key.Value, out var removed))
			{
				_ = _order.Remove(key.Value);
				removed.Dispose();
			}
		}
	}

	/// <summary>Routes <paramref name="state"/>, loading the chosen checkpoint if needed, then
	/// answers every question in <paramref name="questions"/> in one forward pass.</summary>
	public async ValueTask<IReadOnlyDictionary<string, Answer>> PredictAsync(
		JsonElement state,
		IReadOnlyDictionary<string, Question> questions,
		ModelKey? model = null,
		string? task = null,
		string? lang = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(questions);
		var decision = _router.Route(state, [.. questions.Keys], model, task, lang);
		var engine = Load(decision.Model);
		// Same rule as Python's serialize_state: a plain string state passes through unchanged;
		// anything structured becomes JSON — CriterionText.Render already implements exactly this.
		var stateText = CriterionText.Render(state);
		return await engine.PredictAsync(stateText, questions, cancellationToken).ConfigureAwait(false);
	}

	private void Touch(ModelKey key)
	{
		_ = _order.Remove(key);
		_order.Add(key);
	}

	private void Evict()
	{
		while (_order.Count > _maxLoaded)
		{
			var victim = _order[0];
			_order.RemoveAt(0);
			if (_loaded.Remove(victim, out var removed))
				removed.Dispose();
		}
	}

	public void Dispose() => Unload();
}
