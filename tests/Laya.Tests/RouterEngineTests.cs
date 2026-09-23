using System.Text.Json;
using AwesomeAssertions;
using Laya.Core;
using Laya.Core.Routing;

namespace Laya.Tests;

public class RouterEngineTests
{
	private static JsonElement State(string text) => JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(text)).RootElement;

	private static readonly IReadOnlyDictionary<string, Question> Questions = new Dictionary<string, Question>
	{
		["intent"] = new ChoiceQuestion("intent?", [new("a", null), new("b", null)]),
	};

	private static (RouterEngine Router, Dictionary<ModelKey, FakeDecisionBackend> Backends) MakeRouter(int maxLoaded = 1)
	{
		var backends = new Dictionary<ModelKey, FakeDecisionBackend>();
		var router = new RouterEngine(
			key =>
			{
				var backend = new FakeDecisionBackend();
				backends[key] = backend;
				return new DecisionEngine(new FakeTokenizer(), backend);
			},
			maxLoaded: maxLoaded);
		return (router, backends);
	}

	[Test]
	public async Task Load_builds_once_and_caches_on_second_call()
	{
		var (router, backends) = MakeRouter();
		var first = router.Load(ModelKey.English);
		var second = router.Load(ModelKey.English);
		first.Should().BeSameAs(second);
		backends.Should().HaveCount(1);
	}

	[Test]
	public async Task Eviction_disposes_the_least_recently_used_backend_at_max_loaded_one()
	{
		var (router, backends) = MakeRouter(maxLoaded: 1);
		_ = router.Load(ModelKey.English);
		_ = router.Load(ModelKey.Multilingual);

		backends[ModelKey.English].Disposed.Should().BeTrue();
		backends[ModelKey.Multilingual].Disposed.Should().BeFalse();
		router.Loaded.Should().Equal(ModelKey.Multilingual);
	}

	[Test]
	public async Task Preload_raises_max_loaded_to_fit_and_keeps_everything_resident()
	{
		var (router, backends) = MakeRouter(maxLoaded: 1);
		router.Preload([ModelKey.English, ModelKey.Multilingual]);

		backends[ModelKey.English].Disposed.Should().BeFalse();
		backends[ModelKey.Multilingual].Disposed.Should().BeFalse();
		router.Loaded.Should().HaveCount(2);
	}

	[Test]
	public async Task Unload_all_disposes_every_backend()
	{
		var (router, backends) = MakeRouter(maxLoaded: 2);
		router.Preload([ModelKey.English, ModelKey.Multilingual]);
		router.Unload();

		backends.Values.Should().OnlyContain(b => b.Disposed);
		router.Loaded.Should().BeEmpty();
	}

	[Test]
	public async Task Attach_registers_an_existing_engine_without_building_a_new_one()
	{
		var (router, backends) = MakeRouter();
		var backend = new FakeDecisionBackend();
		var engine = new DecisionEngine(new FakeTokenizer(), backend);

		router.Attach(ModelKey.English, engine);

		router.Load(ModelKey.English).Should().BeSameAs(engine);
		backends.Should().BeEmpty(); // the loader was never invoked
	}

	[Test]
	public async Task PredictAsync_routes_then_loads_and_runs_the_chosen_checkpoint()
	{
		var (router, backends) = MakeRouter(maxLoaded: 2);
		var answers = await router.PredictAsync(State("Please refund my duplicate charge."), Questions);

		answers.Should().ContainKey("intent");
		backends.Should().ContainKey(ModelKey.English); // English text routes to English
		backends[ModelKey.English].PredictCallCount.Should().Be(1);
	}

	[Test]
	public async Task PredictAsync_honors_an_explicit_model_override()
	{
		var (router, _) = MakeRouter(maxLoaded: 2);
		var answers = await router.PredictAsync(
			State("Please refund my duplicate charge."), Questions, model: ModelKey.Multilingual);

		answers.Should().ContainKey("intent");
		router.Loaded.Should().Equal(ModelKey.Multilingual);
	}
}
