# Getting started

## 1. Export a checkpoint

Layar doesn't ship model weights. `tools/layar-export/export.py` is a maintainer-run Python script
that loads a checkpoint through the original [`laya`](https://github.com/NandhaKishorM/laya)
package and exports it to the files Layar's backends actually load:

```bash
pip install laya torch onnx onnxscript  # laya's own deps, plus the ONNX export tooling
python tools/layar-export/export.py convaiinnovations/laya --subfolder multilingual \
    -o .artifacts/models/multilingual
```

This produces, under the output directory: `model.onnx` (+ `model.onnx.data` for larger
checkpoints), `model.pt` (a `torch.jit.trace`d TorchScript module), `tokenizer/` (the Hugging Face
tokenizer files), and `config.json` (max length, head budget, calibration temperatures).

If you'd rather not run Python at all, `Laya.Core.CheckpointCache` downloads an already-exported
checkpoint on demand from wherever one is hosted — see the project README's "Downloading a
checkpoint on demand" section.

## 2. Predict

```csharp
using Laya.Core;
using Laya.Onnx; // or: using Laya.TorchSharp;
using Laya.Tokenization;

var tokenizer = HuggingFaceBpeTokenizer.FromDirectory("path/to/checkpoint/tokenizer");
using var backend = new OnnxDecisionBackend("path/to/checkpoint/model.onnx");
var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

var triage = await engine.PredictAsync(
    "Hi, we were billed twice for March. Please refund the duplicate today.",
    TriageSchema.Instance);

Console.WriteLine($"{triage.Intent.Choice} (confidence {triage.Intent.Confidence:F2})");
```

`maxLen`/`headMaxLen` should match the checkpoint's `config.json` (`1024`/`256` for the
multilingual checkpoint at the time of writing; `512`/`192` for the English one).

`TriageSchema` is an `IQuestionSchema<TriageResult>` — a fixed question set plus how to bind the
answers back into a record, so `PredictAsync` returns `TriageResult` directly instead of a
string-keyed dictionary. It exists because `Presets.TriageQuestions()`'s shape is fixed at compile
time; a runtime-composed question set (e.g. `Presets.EmailQuestions(myCategories)` with your own
categories) has no fixed record to bind to and stays on the dictionary path:

```csharp
var answers = await engine.PredictAsync(state, Presets.EmailQuestions(myCategories));
var category = answers.Choice("category"); // typed accessor, any question id
```

## 3. Route between checkpoints, or shortlist a large label set

`RouterEngine` wraps checkpoint selection and lazy loading (mirroring the Python package's
`Router`): it inspects the state's script/language, picks the right checkpoint, and loads it on
first use.

```csharp
using Laya.Core.Routing;

using var router = new RouterEngine(
    loader: key => BuildEngineFor(key), // your own: pick a checkpoint dir per ModelKey, build a DecisionEngine
    maxLoaded: 2);

var triage = await router.PredictAsync(stateJson, TriageSchema.Instance);
```

For a `choice` question with more labels than fit in the token budget (more than ~20, depending on
the checkpoint), `Shortlist`/`PredictShortlistAsync` embeds the state and each option, keeps the
top matches, and predicts over just those:

```csharp
var (answers, shortlists) = await engine.PredictShortlistAsync(stateText, questions, myEmbeddingProvider);
```

`myEmbeddingProvider` is anything implementing `IEmbeddingProvider` — Layar doesn't ship one, the
same way the Python package leaves `embed_fn` entirely to the caller.

## 4. Compare the two backends yourself

```bash
dotnet run -c Release --project tests/Laya.Benchmarks -- --filter '*'
```

Runs both `Layar.Onnx` and `Layar.TorchSharp` against the same real request through
BenchmarkDotNet (proper warmup, memory diagnostics) — the numbers in the README's "Measured"
section came from exactly this command.

## Building from source

```bash
dotnet build layar.slnx
dotnet run --project tests/Laya.Tests -c Release
```

See the project [AGENTS.md](https://github.com/nullean/layar/blob/main/AGENTS.md) for the known
rough edges (native library loading on macOS, why ONNX export needs the `dynamo=True` exporter,
and what's blocked on this or that toolchain issue) before filing something as a bug.
