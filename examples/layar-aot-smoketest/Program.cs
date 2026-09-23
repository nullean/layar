using Laya.Core;
using Laya.Core.Lang;

var pass = 0;
var fail = 0;

void Check(string name, bool condition)
{
	Console.WriteLine(condition ? $"  OK  {name}" : $" FAIL {name}");
	if (condition)
		pass++;
	else
		fail++;
}

// Language detection
var english = LanguageAnalyzer.Analyse("Please refund my duplicate charge from March.");
Check("english text detected", english.IsEnglish);

var french = LanguageAnalyzer.Analyse("Bonjour, merci beaucoup pour votre aide rapide, nous avons besoin d'une réponse.");
Check("french text routed away from english", !french.IsEnglish);

// Calibration math
float[] logits = [1203.17f, -1437.54f];
var probs = new float[2];
ProbabilityMath.SoftmaxWithTemperature(logits, 2, 1f, probs);
Check("softmax handles extreme-magnitude logits without NaN", !float.IsNaN(probs[0]) && probs[0] > 0.99f);

var confidence = ProbabilityMath.ConfidenceFromProbabilities([1f, 0f, 0f, 0f]);
Check("confidence is 1.0 for a certain answer", MathF.Abs(confidence - 1f) < 1e-4f);

// Sequence building against a fake tokenizer (no external files needed for this smoketest)
var tokenizer = new SmoketestTokenizer();
var question = new ChoiceQuestion("Which department should handle this?",
[
	new("billing", "invoices, payments"),
	new("technical", "bugs, outages"),
]);
var sequence = SequenceBuilder.Build(tokenizer, "the customer wants a refund", question, maxLen: 128, headMaxLen: 64);
Check("sequence has one marker per option", sequence.MarkerPositions.Length == 2);
Check("sequence starts with CLS and ends with SEP",
	sequence.TokenIds[0] == tokenizer.ClsTokenId && sequence.TokenIds[^1] == tokenizer.SepTokenId);

// Presets and Email are pure data/text ports; check they at least construct.
var triage = Presets.TriageQuestions();
Check("triage preset has 5 questions", triage.Count == 5);

var cleaned = Email.CleanBody("Please help.\n\n--\nJohn Smith");
Check("email cleaning strips signature", cleaned == "Please help.");

Console.WriteLine();
Console.WriteLine($"AOT smoke test: {pass} passed, {fail} failed");
return fail > 0 ? 1 : 0;

internal sealed class SmoketestTokenizer : ITokenizer
{
	public int ClsTokenId => 1;
	public int SepTokenId => 2;
	public int MaskTokenId => 3;
	public int PadTokenId => 0;
	public string MaskToken => "[MASK]";
	public int MaxTokenCount(ReadOnlySpan<char> text) => text.Length + 1;

	public int Encode(ReadOnlySpan<char> text, Span<int> destination)
	{
		var count = 0;
		foreach (var ch in text)
		{
			if (char.IsWhiteSpace(ch))
				continue;
			if (count >= destination.Length)
				break;
			destination[count++] = 100 + ch;
		}
		return count;
	}
}
