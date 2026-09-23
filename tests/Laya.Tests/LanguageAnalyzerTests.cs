using AwesomeAssertions;
using Laya.Core.Lang;

namespace Laya.Tests;

public class LanguageAnalyzerTests
{
	[Test]
	public async Task English_text_is_detected_as_english()
	{
		var result = LanguageAnalyzer.Analyse(
			"Hi, we were billed twice for March. Please refund the duplicate today or we will cancel our plan.");
		result.Script.Should().Be("latin");
		result.IsEnglish.Should().BeTrue();
	}

	[Test]
	public async Task French_text_routes_away_from_english()
	{
		var result = LanguageAnalyzer.Analyse("Bonjour, merci beaucoup pour votre aide rapide, nous avons besoin d'une réponse.");
		result.Script.Should().Be("latin");
		result.IsEnglish.Should().BeFalse();
		result.Language.Should().Be("fr");
	}

	[Test]
	public async Task Non_latin_script_is_never_english()
	{
		var result = LanguageAnalyzer.Analyse("мы дважды сняли с вас деньги, пожалуйста, верните их");
		result.Script.Should().Be("cyrillic");
		result.IsEnglish.Should().BeFalse();
	}

	[Test]
	public async Task Empty_text_is_unknown_and_defaults_to_english()
	{
		var result = LanguageAnalyzer.Analyse("123 456 !!!");
		result.Script.Should().Be("unknown");
		result.IsEnglish.Should().BeTrue();
	}

	[Test]
	public async Task German_diacritics_alone_are_enough_even_without_a_clear_stopword_match()
	{
		// Verified against the Python oracle: no single language's stopwords clear the required
		// margin here, so `Language` stays undecided — but the diacritic rate alone (0.0341) is
		// enough to say "not safe for the English checkpoint", which is the bit that matters for
		// routing.
		var result = LanguageAnalyzer.Analyse(
			"Ich möchte gerne wissen, ob mein Geld zurückerstattet wird, denn ich bin sehr verärgert.");
		result.Script.Should().Be("latin");
		result.IsEnglish.Should().BeFalse();
	}
}
