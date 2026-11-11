# Layar

![nullean](https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png)

Typed decisions (`choice`, `score`, `noul`) over state, answered in one forward pass by a small transformer decision
head: a .NET port of Laya (https://github.com/NandhaKishorM/laya). This package holds the question and answer types,
`DecisionEngine`, the presets and `CheckpointCache`. It has no ML dependency and runs nothing on its own.

[![NuGet](https://img.shields.io/nuget/v/Layar?style=flat-square)](https://www.nuget.org/packages/Layar)

## Install

```sh
dotnet add package Layar
dotnet add package Layar.Tokenization
dotnet add package Layar.Onnx   # or Layar.TorchSharp
```

Or one bundle: `Layar.Onnx.Cpu` or `Layar.TorchSharp.Cpu`.

## Quick start

The namespaces are `Laya.*`; the packages are `Layar.*`. Three question types, keyed by an id you choose:

```csharp
using Laya.Core;

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

A preset schema binds the answers into a record, with no string ids:

```csharp
TriageResult triage = await engine.PredictAsync(message, TriageSchema.Instance);
// also GuardSchema.Instance, ModerationSchema.Instance, ModelRouterSchema.Instance, new EmailTriageSchema(categories)
```

The engine owns the backend: disposing the engine disposes it. Checkpoints are not in the package; `CheckpointCache`
downloads one on first use, though none has been published yet.

## Learn more

- Documentation: https://layar.nullean.net
- Source and issues: https://github.com/nullean/layar
