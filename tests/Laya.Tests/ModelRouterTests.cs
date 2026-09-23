using System.Text.Json;
using AwesomeAssertions;
using Laya.Core.Routing;

namespace Laya.Tests;

public class ModelRouterTests
{
	private static JsonElement State(string text) => JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(text)).RootElement;

	[Test]
	public async Task English_state_routes_to_english()
	{
		var router = new ModelRouter();
		var decision = router.Route(State("Please refund my duplicate charge from March."));
		decision.Model.Should().Be(ModelKey.English);
	}

	[Test]
	public async Task Non_latin_state_routes_to_multilingual()
	{
		var router = new ModelRouter();
		var decision = router.Route(State("请退还我的重复收费"));
		decision.Model.Should().Be(ModelKey.Multilingual);
	}

	[Test]
	public async Task Explicit_model_wins_over_detection()
	{
		var router = new ModelRouter();
		var decision = router.Route(State("Please refund my duplicate charge."), model: ModelKey.Multilingual);
		decision.Model.Should().Be(ModelKey.Multilingual);
		decision.Detection.Should().BeNull();
	}

	[Test]
	public async Task Explicit_lang_skips_detection()
	{
		var router = new ModelRouter();
		var decision = router.Route(State("some text"), lang: "de");
		decision.Model.Should().Be(ModelKey.Multilingual);
		decision.Detection.Should().BeNull();
	}

	[Test]
	public async Task Unknown_script_falls_back_to_default()
	{
		var router = new ModelRouter(defaultModel: ModelKey.English);
		var decision = router.Route(State("12345 !!!"));
		decision.Model.Should().Be(ModelKey.English);
	}

	[Test]
	public async Task Typed_decisions_workflow_is_opt_in()
	{
		var router = new ModelRouter(autoTaskDetection: true);
		var ids = new[] { "action", "category", "churn_risk", "needs_human", "urgency" };
		var decision = router.Route(State("some ticket text"), questionIds: ids);
		decision.Model.Should().Be(ModelKey.TypedDecisions);
		decision.Workflow.Should().Be("customer_service");
	}

	[Test]
	public async Task Typed_decisions_workflow_is_ignored_when_not_opted_in()
	{
		var router = new ModelRouter(autoTaskDetection: false);
		var ids = new[] { "action", "category", "churn_risk", "needs_human", "urgency" };
		var decision = router.Route(State("Please help with my order today"), questionIds: ids);
		decision.Model.Should().Be(ModelKey.English);
	}
}
