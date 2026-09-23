using AwesomeAssertions;
using Laya.Core;

namespace Laya.Tests;

public class SequenceBuilderTests
{
	private static readonly ChoiceQuestion Choice = new(
		"Which department should handle this?",
		[
			new("billing", "invoices, payments"),
			new("technical", "bugs, outages"),
			new("other", null),
		]);

	[Test]
	public async Task Build_produces_one_marker_per_option()
	{
		var tok = new FakeTokenizer();
		var seq = SequenceBuilder.Build(tok, "the customer wants a refund", Choice, maxLen: 128, headMaxLen: 64);
		seq.MarkerPositions.Should().HaveCount(3);
	}

	[Test]
	public async Task Build_places_mask_token_at_every_marker_position()
	{
		var tok = new FakeTokenizer();
		var seq = SequenceBuilder.Build(tok, "the customer wants a refund", Choice, maxLen: 128, headMaxLen: 64);
		foreach (var marker in seq.MarkerPositions)
			seq.TokenIds[marker].Should().Be(tok.MaskTokenId);
	}

	[Test]
	public async Task Build_starts_with_cls_and_ends_with_sep()
	{
		var tok = new FakeTokenizer();
		var seq = SequenceBuilder.Build(tok, "the customer wants a refund", Choice, maxLen: 128, headMaxLen: 64);
		seq.TokenIds[0].Should().Be(tok.ClsTokenId);
		seq.TokenIds[^1].Should().Be(tok.SepTokenId);
	}

	[Test]
	public async Task Build_never_exceeds_max_len()
	{
		var tok = new FakeTokenizer();
		var longState = string.Join(' ', Enumerable.Repeat("word", 500));
		var seq = SequenceBuilder.Build(tok, longState, Choice, maxLen: 32, headMaxLen: 16);
		seq.TokenIds.Length.Should().BeLessThanOrEqualTo(32);
	}

	[Test]
	public async Task Build_keeps_all_markers_when_options_are_shrunk_for_budget()
	{
		var tok = new FakeTokenizer();
		var manyOptions = new ChoiceQuestion(
			"pick one",
			Enumerable.Range(0, 20).Select(i => new ChoiceOption($"option_{i}", "a fairly long description of this option")).ToArray());
		var seq = SequenceBuilder.Build(tok, "state text", manyOptions, maxLen: 256, headMaxLen: 64);
		seq.MarkerPositions.Should().HaveCount(20);
	}

	[Test]
	public async Task Noul_question_always_renders_false_then_true()
	{
		var noul = new NoulQuestion("Does the user threaten to cancel?");
		var options = noul.RenderOptions();
		options[0].Should().StartWith("false:");
		options[1].Should().StartWith("true:");
	}
}
