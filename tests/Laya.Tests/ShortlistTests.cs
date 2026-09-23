using AwesomeAssertions;
using Laya.Core;

namespace Laya.Tests;

public class ShortlistTests
{
	private static ChoiceQuestion Question(params string[] labels) =>
		new("", labels.Select(l => new ChoiceOption(l, null)).ToArray());

	[Test]
	public async Task Passthrough_when_k_is_at_least_the_option_count_and_never_calls_the_provider()
	{
		var question = Question("a", "b", "c");
		var provider = new FakeEmbeddingProvider(new Dictionary<string, float[]>());

		var result = await Shortlist.RankChoiceAsync("state text", question, provider, k: 20);

		result.Passthrough.Should().BeTrue();
		result.Labels.Should().Equal("a", "b", "c");
		result.Scores.Should().BeNull();
		result.OptionCount.Should().Be(3);
		provider.CallCount.Should().Be(0);
	}

	[Test]
	public async Task Ranks_by_cosine_similarity_best_first()
	{
		var question = Question("a", "b", "c", "d");
		var provider = new FakeEmbeddingProvider(new Dictionary<string, float[]>
		{
			["state"] = [1f, 0f],
			["a"] = [1f, 0f], // cos = 1.0
			["b"] = [0f, 1f], // cos = 0.0
			["c"] = [-1f, 0f], // cos = -1.0
			["d"] = [0.70710677f, 0.70710677f], // cos ~= 0.7071
		});

		var result = await Shortlist.RankChoiceAsync("state", question, provider, k: 2);

		result.Passthrough.Should().BeFalse();
		result.OptionCount.Should().Be(4);
		result.Labels.Should().Equal("a", "d");
		result.Scores![0].Should().BeApproximately(1f, 1e-4f);
		result.Scores![1].Should().BeApproximately(0.7071f, 1e-3f);
		provider.CallCount.Should().Be(1); // one batched call, not one per option
	}

	[Test]
	public async Task Ties_keep_the_earlier_label()
	{
		var question = Question("first", "second", "third");
		var provider = new FakeEmbeddingProvider(new Dictionary<string, float[]>
		{
			["q"] = [1f, 0f],
			["first"] = [1f, 0f], // cos = 1.0
			["second"] = [1f, 0f], // cos = 1.0, tied with "first"
			["third"] = [0f, 1f], // cos = 0.0
		});

		var result = await Shortlist.RankChoiceAsync("q", question, provider, k: 2);

		result.Labels.Should().Equal("first", "second");
	}

	[Test]
	public async Task Zero_query_vector_scores_everything_zero_and_keeps_original_order()
	{
		var question = Question("a", "b", "c");
		var provider = new FakeEmbeddingProvider(new Dictionary<string, float[]>
		{
			["q"] = [0f, 0f],
			["a"] = [1f, 0f],
			["b"] = [0f, 1f],
			["c"] = [1f, 1f],
		});

		var result = await Shortlist.RankChoiceAsync("q", question, provider, k: 2);

		result.Labels.Should().Equal("a", "b"); // stable sort over all-zero scores = original order
		result.Scores.Should().OnlyContain(s => s == 0f);
	}

	[Test]
	public async Task NaN_and_infinite_vector_components_are_sanitized_to_zero_not_propagated()
	{
		var question = Question("a", "b");
		var provider = new FakeEmbeddingProvider(new Dictionary<string, float[]>
		{
			["q"] = [1f, 0f],
			["a"] = [float.NaN, float.PositiveInfinity], // sanitizes to [0,0] -> zero norm -> sim 0
			["b"] = [1f, 0f], // cos = 1.0
		});

		var result = await Shortlist.RankChoiceAsync("q", question, provider, k: 1);

		result.Labels.Should().Equal("b");
		result.Scores![0].Should().BeApproximately(1f, 1e-4f);
	}

	[Test]
	public async Task Instructions_are_prefixed_onto_the_query_text()
	{
		var question = new ChoiceQuestion("classify this", [new("a", null), new("b", null)]);
		var provider = new FakeEmbeddingProvider(new Dictionary<string, float[]>
		{
			["classify this\nstate"] = [1f, 0f],
			["a"] = [1f, 0f],
			["b"] = [0f, 1f],
		});

		var result = await Shortlist.RankChoiceAsync("state", question, provider, k: 1);

		result.Labels.Should().Equal("a");
	}

	[Test]
	public async Task Duplicate_labels_throw()
	{
		var question = Question("a", "a");
		var provider = new FakeEmbeddingProvider(new Dictionary<string, float[]>());

		var act = () => Shortlist.RankChoiceAsync("state", question, provider, k: 1).AsTask();
		await act.Should().ThrowAsync<ArgumentException>();
	}

	[Test]
	public async Task Apply_rebuilds_the_question_with_only_the_ranked_options_in_ranked_order()
	{
		var question = new ChoiceQuestion("ins", [new("a", "desc-a"), new("b", "desc-b"), new("c", "desc-c")]);
		var result = new Shortlist.Result(["c", "a"], [0.9f, 0.5f], Passthrough: false, OptionCount: 3);

		var reduced = Shortlist.Apply(question, result);

		reduced.Instructions.Should().Be("ins");
		reduced.Options.Select(o => o.Label).Should().Equal("c", "a");
		reduced.Options[0].Description.Should().Be("desc-c");
	}

	[Test]
	public async Task Apply_is_a_no_op_for_a_passthrough_result()
	{
		var question = Question("a", "b");
		var passthrough = new Shortlist.Result(["a", "b"], null, Passthrough: true, OptionCount: 2);

		Shortlist.Apply(question, passthrough).Should().BeSameAs(question);
	}

	[Test]
	public async Task PredictShortlistAsync_reduces_a_high_cardinality_choice_and_forwards_others_unchanged()
	{
		var backend = new FakeDecisionBackend();
		var engine = new DecisionEngine(new FakeTokenizer(), backend, maxLen: 128, headMaxLen: 64);
		var provider = new FakeEmbeddingProvider(new Dictionary<string, float[]>
		{
			["state"] = [1f, 0f],
			["a"] = [1f, 0f],
			["b"] = [0f, 1f],
			["c"] = [-1f, 0f],
		});

		var questions = new Dictionary<string, Question>
		{
			["choice"] = Question("a", "b", "c"), // 3 options, k=1 -> shortlisted to just "a"
			["urgency"] = new ScoreQuestion("how urgent?", ["low", "high"]), // not a choice, untouched
		};

		var (answers, shortlists) = await engine.PredictShortlistAsync("state", questions, provider, k: 1);

		shortlists.Should().ContainKey("choice");
		shortlists["choice"].Labels.Should().Equal("a");
		shortlists.Should().NotContainKey("urgency"); // non-choice questions are never shortlisted
		answers.Should().ContainKey("choice");
		answers.Should().ContainKey("urgency");
	}
}
