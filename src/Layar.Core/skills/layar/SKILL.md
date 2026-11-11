---
name: layar
description: >-
  Use when C# code needs a typed decision about some text (pick a category, score a level, answer yes/no with a
  probability) without generating text. Layar asks a set of typed questions over a state string and answers them all
  in one forward pass of a small decision model.
---

# Layar

`using Laya.Core;` (the namespaces are `Laya.*`, the packages `Layar.*`). Layar holds the question and answer types,
`DecisionEngine` and the presets; it runs nothing on its own. Add a backend (`Layar.Onnx` or `Layar.TorchSharp`) and
`Layar.Tokenization`, or one of the `Layar.Onnx.Cpu` / `Layar.TorchSharp.Cpu` bundles. Not for free-form text answers.

## The calls you need

Three question types, keyed by an id you choose:

```csharp
var questions = new Dictionary<string, Question>
{
    ["team"] = new ChoiceQuestion("Which team should handle `message`?",
    [
        new("billing", "invoices, refunds and payment methods"),
        new("support", "bugs, outages and how-to questions"),
    ]),
    ["anger"] = new ScoreQuestion("How angry is the customer?", ["calm", "annoyed", "furious"]),
    ["wants_refund"] = new NoulQuestion("Does the customer ask for money back?"),
};

// engine: new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256)
var answers = await engine.PredictAsync("I was charged twice, refund it now!", questions);
var team = answers.Choice("team");          // team.Choice, team.Probabilities["billing"], team.Confidence
var anger = answers.Score("anger");         // anger.Score is the expected level index (0..2), as a float
var refund = answers.Noul("wants_refund");  // refund.Noul is P(true), 0..1
```

A preset schema binds the answers into a record, so there are no string ids:

```csharp
TriageResult triage = await engine.PredictAsync(message, TriageSchema.Instance);
if (triage.IsUrgent.Noul > 0.5f) { /* ... */ }
// also GuardSchema.Instance, ModerationSchema.Instance, ModelRouterSchema.Instance, new EmailTriageSchema(categories)
```

Checkpoints are not in the package. `CheckpointCache` downloads one and returns local paths:

```csharp
using var cache = new CheckpointCache(cacheDir, GitHubReleaseCheckpointUrls.Create("nullean", "layar", "models-1.0.0"));
var tokenizerDir = await cache.EnsureTokenizerAsync("multilingual");
var onnxPath = await cache.EnsureOnnxModelAsync("multilingual");    // or EnsureTorchScriptModelAsync
```

Pick a `models-<version>` tag that exists on https://github.com/nullean/layar/releases.

## Gotchas

- `DecisionEngine` owns the backend: disposing the engine disposes the backend. Dispose one of them, not both.
- `maxLen` and `headMaxLen` default to 512 and 192. The CLI and the README pass 1024 and 256 for the exported
  checkpoints.
- `Confidence` comes from the answer's own distribution (for a noul, `max(p, 1 - p)`). `Action.ActProbability` is a
  separate output of the model; do not use one for the other.
- `answers.Choice("id")` throws `KeyNotFoundException` for an unknown id and `InvalidCastException` when the question
  had another type. Both messages name the id.
- A `ChoiceQuestion` option renders as `"label: description"`; the label is what `Choice` returns, so keep it short
  and stable.
- `Routing.RouterEngine` picks between the `english`, `multilingual` and `typed-decisions` checkpoints and loads them
  lazily through a `ModelLoader` you supply.

Docs: https://layar.nullean.net
