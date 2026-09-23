using Laya.Core;
using Laya.Core.Lang;
using Laya.TorchSharp;

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

// Same pure-logic checks as examples/layar-aot-smoketest — the point of *this* smoketest is
// exercising the Laya.TorchSharp/TorchSharp-cpu dependency chain under NativeAOT (does the whole
// graph link and trim?), not re-testing Core/Tokenization logic a second time.
var english = LanguageAnalyzer.Analyse("Please refund my duplicate charge from March.");
Check("english text detected", english.IsEnglish);

float[] logits = [1203.17f, -1437.54f];
var probs = new float[2];
ProbabilityMath.SoftmaxWithTemperature(logits, 2, 1f, probs);
Check("softmax handles extreme-magnitude logits without NaN", !float.IsNaN(probs[0]) && probs[0] > 0.99f);

// A real checkpoint can't ship in CI (see NOTICE.md/README on checkpoint distribution), but
// constructing against a path that doesn't exist still forces the real thing this smoketest exists
// to check: TorchSharpDecisionBackend's constructor calls jit.load, which must first dlopen
// libLibTorchSharp.dylib/.so/.dll and link the whole libtorch dependency chain under PublishAot
// before it ever gets to check the file. A FileNotFoundException here means that link succeeded —
// a DllNotFoundException, or a crash, would mean it didn't. Loading a real checkpoint through this
// backend and getting a correct prediction was verified separately, by hand (see AGENTS.md).
try
{
	using var backend = new TorchSharpDecisionBackend("/nonexistent/layar-aot-smoketest/model.pt");
	Check("TorchSharpDecisionBackend under AOT (expected FileNotFoundException, got none)", false);
}
catch (FileNotFoundException)
{
	Check("TorchSharpDecisionBackend links and reaches file-not-found under AOT", true);
}

Console.WriteLine();
Console.WriteLine($"TorchSharp AOT smoke test: {pass} passed, {fail} failed");
return fail > 0 ? 1 : 0;
