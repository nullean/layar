using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Laya.Core;

namespace Laya.Tests;

public class CriterionTextTests
{
	private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

	[Test]
	public async Task String_passes_through_unquoted()
	{
		CriterionText.Render(Parse("\"invoices, payments\"")).Should().Be("invoices, payments");
	}

	[Test]
	public async Task Object_renders_with_python_style_spacing()
	{
		// json.dumps({"keywords": ["asap", "now"], "weight": 2}, separators=(", ", ": "))
		var text = CriterionText.Render(Parse("""{"keywords": ["asap", "now"], "weight": 2}"""));
		text.Should().Be("""{"keywords": ["asap", "now"], "weight": 2}""");
	}

	[Test]
	public async Task Bool_renders_lowercase_not_ToString_casing()
	{
		CriterionText.Render(Parse("true")).Should().Be("true");
		CriterionText.Render(Parse("false")).Should().Be("false");
	}

	[Test]
	public async Task Number_renders_as_json_literal()
	{
		CriterionText.Render(Parse("42")).Should().Be("42");
	}

	[Test]
	public async Task RenderOrNull_treats_missing_and_empty_string_as_no_description()
	{
		CriterionText.RenderOrNull((JsonElement?)null).Should().BeNull();
		CriterionText.RenderOrNull(Parse("null")).Should().BeNull();
		CriterionText.RenderOrNull(Parse("\"\"")).Should().BeNull();
	}

	[Test]
	public async Task ChoiceOption_FromCriterion_renders_structured_criteria()
	{
		var option = ChoiceOption.FromCriterion("urgent", Parse("""{"keywords": ["asap", "now"], "weight": 2}"""));
		option.Label.Should().Be("urgent");
		option.Description.Should().Be("""{"keywords": ["asap", "now"], "weight": 2}""");
	}

	[Test]
	public async Task ChoiceOption_FromCriterion_with_null_omits_description()
	{
		var option = ChoiceOption.FromCriterion("other", (JsonElement?)null);
		option.Description.Should().BeNull();
	}

	[Test]
	public async Task JsonNode_construction_side_matches_JsonElement_parse_side()
	{
		// Built in code with JsonObject/JsonArray (the mutable DOM Email.cs already uses), not
		// parsed from external JSON text.
		var node = new JsonObject
		{
			["keywords"] = new JsonArray("asap", "now"),
			["weight"] = 2,
		};
		var option = ChoiceOption.FromCriterion("urgent", node);
		option.Description.Should().Be("""{"keywords": ["asap", "now"], "weight": 2}""");
	}
}
