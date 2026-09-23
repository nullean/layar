using AwesomeAssertions;
using Laya.Core;

namespace Laya.Tests;

public class QuestionSchemaTests
{
	[Test]
	public async Task DecisionEngine_PredictAsync_with_a_schema_binds_the_typed_result()
	{
		using var engine = new DecisionEngine(new FakeTokenizer(), new FakeDecisionBackend());

		var triage = await engine.PredictAsync("customer message", TriageSchema.Instance);

		// FakeDecisionBackend always picks the first option/highest logit deterministically.
		triage.Intent.Choice.Should().Be("refund");
		triage.IsUrgent.Should().NotBeNull();
		triage.Frustration.Should().NotBeNull();
	}

	[Test]
	public async Task Schema_Questions_match_the_equivalent_Presets_dictionary()
	{
		TriageSchema.Instance.Questions.Keys.Should().BeEquivalentTo(Presets.TriageQuestions().Keys);
		GuardSchema.Instance.Questions.Keys.Should().BeEquivalentTo(Presets.GuardQuestions().Keys);
		ModerationSchema.Instance.Questions.Keys.Should().BeEquivalentTo(Presets.ModerationQuestions().Keys);
		ModelRouterSchema.Instance.Questions.Keys.Should().BeEquivalentTo(Presets.RouterQuestions().Keys);
	}

	[Test]
	public async Task EmailTriageSchema_forwards_caller_supplied_categories()
	{
		var categories = new Dictionary<string, string> { ["legal"] = "contracts, compliance" };
		var schema = new EmailTriageSchema(categories);

		var category = (ChoiceQuestion)schema.Questions["category"];

		category.Options.Should().ContainSingle(o => o.Label == "legal");
	}

	[Test]
	public async Task Bind_matches_the_dictionary_based_AsTriage_extension()
	{
		using var engine = new DecisionEngine(new FakeTokenizer(), new FakeDecisionBackend());

		var answers = await engine.PredictAsync("customer message", Presets.TriageQuestions());
		var viaDictionary = answers.AsTriage();
		var viaSchema = TriageSchema.Instance.Bind(answers);

		viaSchema.Should().BeEquivalentTo(viaDictionary);
	}
}
