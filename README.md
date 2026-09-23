# Layar

A .NET port of [Laya](https://github.com/NandhaKishorM/laya): typed decisions (`choice`/`score`/`noul`)
over state, answered in one forward pass by a small transformer decision head — no text generation,
so there is nothing to parse and nothing to hallucinate.

Ships two interchangeable inference backends behind one interface, so they can be benchmarked
against each other on the same requests:

- **Layar.Onnx** — [ONNX Runtime](https://onnxruntime.ai/)
- **Layar.TorchSharp** — [TorchSharp](https://github.com/dotnet/TorchSharp) (libtorch), loading a
  `torch.jit.trace`-exported TorchScript module rather than a hand-ported architecture

Both are verified bit-for-bit (well, float-for-float) against the original Python package's own
output on the same request — see `tools/layar-export` and the numbers below.

## Packages

| Package | What it is |
|---|---|
| `Layar` | Question/answer types, sequence building, calibration math, language/script routing, typed answer accessors. No ML dependency; AOT-safe. |
| `Layar.Tokenization` | A from-scratch BPE tokenizer (byte-level GPT-2-style + Metaspace/SentencePiece-style) reading a Hugging Face `tokenizer.json` directly. No external tokenizer package, no Python at runtime. |
| `Layar.Onnx` | `IDecisionBackend` over ONNX Runtime. |
| `Layar.TorchSharp` | `IDecisionBackend` over TorchSharp/libtorch. |
| `Layar.Cli` (`layar`) | `predict` and `benchmark` commands, installable as a `dotnet tool`. |
| `Layar.Onnx.Cpu` | Meta-package: `Layar` + `Layar.Tokenization` + `Layar.Onnx` in one install — the default, since ONNX Runtime doesn't need picking a native runtime variant the way TorchSharp does. |
| `Layar.TorchSharp.Cpu` | Meta-package: `Layar` + `Layar.Tokenization` + `Layar.TorchSharp` + the `TorchSharp-cpu` native runtime in one install. |

Install the meta-package that matches how you want to run it, or the individual `Layar.*` packages
for full control (e.g. both backends side by side, to compare them):

```bash
dotnet add package Layar.Onnx.Cpu        # ONNX Runtime, one package
# or
dotnet add package Layar.TorchSharp.Cpu  # TorchSharp/libtorch, one package
# or, for full control / both backends:
dotnet add package Layar
dotnet add package Layar.Tokenization
dotnet add package Layar.Onnx
dotnet add package Layar.TorchSharp
dotnet add package TorchSharp-cpu
```

## Quickstart

```csharp
var tokenizer = HuggingFaceBpeTokenizer.FromDirectory("path/to/checkpoint/tokenizer");
using var backend = new OnnxDecisionBackend("path/to/checkpoint/model.onnx"); // or TorchSharpDecisionBackend(...model.pt)
using var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

var answers = await engine.PredictAsync(
    "Hi, we were billed twice for March. Please refund the duplicate today.",
    Presets.TriageQuestions());

// Typed accessors instead of casts/string-keyed lookups — AsTriage() is specific to this preset's
// fixed schema; AnswerDictionaryExtensions' Choice()/Score()/Noul() work for any question id.
var triage = answers.AsTriage();
Console.WriteLine($"{triage.Intent.Choice} (confidence {triage.Intent.Confidence:F2})");
```

A checkpoint directory (`model.onnx`, `model.pt`, `tokenizer/`, `config.json`) is produced by
`tools/layar-export/export.py`, a maintainer-run Python script that loads the original `laya`
package's checkpoint and exports it — not shipped with the .NET library.

### Downloading a checkpoint on demand

Checkpoints (650MB-1.2GB) aren't committed to this repo. `CheckpointCache` downloads and caches one
on first use — where they're hosted is pluggable (`CheckpointAssetUrl`); `GitHubReleaseCheckpointUrls`
covers the case of attaching `tools/layar-export`'s output to a GitHub Release as flat assets:

```csharp
using var cache = new CheckpointCache(
    cacheRoot: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "layar", "models"),
    assetUrl: GitHubReleaseCheckpointUrls.Create("nullean", "layar", "0.1.0"));

var tokenizerDir = await cache.EnsureTokenizerAsync("multilingual");
var onnxPath = await cache.EnsureOnnxModelAsync("multilingual"); // also fetches model.onnx.data if present
```

No checkpoints have actually been published there yet — that's a `git push` + a cut release + a
`gh release upload` away, not a code change, so it hasn't been done as part of this port.

## Measured

`tests/Laya.Benchmarks` (BenchmarkDotNet, `dotnet run -c Release --project tests/Laya.Benchmarks -- --filter '*'`)
on this repo's multilingual checkpoint, a real 3-question batched request, CPU, Apple M2:

| Method | Mean | Ratio | Allocated |
|---|---|---|---|
| Onnx | 148.7 ms | 1.00 | 82.6 KB |
| TorchSharp | 70.9 ms | 0.48 | 85.3 KB |

TorchSharp is ~2.1x faster here. Both match the Python oracle exactly (confidence agreement to
1e-6) — see `tests/Laya.Tests/PipelineParityTests.cs`. `SessionOptions` tuning (thread count,
graph optimization level, execution mode) doesn't close the gap — see `AGENTS.md`'s rough edges
for what was tried.

## Building

```bash
dotnet build layar.slnx
dotnet run --project tests/Laya.Tests/Laya.Tests.csproj -c Release
```

TorchSharp's native `libtorch` needs `DYLD_LIBRARY_PATH`/`LD_LIBRARY_PATH` pointed at the
consolidated native library directory when running via `dotnet run` from a loose build output
(see `AGENTS.md` — this is a packaging rough edge, not a code issue, and does not affect a proper
`dotnet publish`).

## Status

This is a working, numerically-verified port of the full core surface: Core, Tokenization, Onnx,
TorchSharp, a minimal Cli, the model-lifecycle Router (`RouterEngine`: load, preload, unload,
attach, LRU eviction — the pure routing *decision* was ported first; the loading side wraps it),
structured/non-string criteria (`CriterionText`, matching Python's `render_criterion`), on-demand
checkpoint downloading (`CheckpointCache` + `GitHubReleaseCheckpointUrls`, though nothing's
published there yet — see above), and the embedding shortlist for >20-option questions
(`Shortlist` + `ShortlistPrediction`, matching `shortlist.py`/`predict_shortlist` exactly, including
its tie-breaking and NaN/zero-vector edge cases). Tokenizer conformance and full-pipeline parity
against the Python oracle are committed regression tests, not one-off scratchpad checks — see
`tests/Laya.Tests`. `tests/Laya.Benchmarks` has the real ONNX-vs-TorchSharp numbers (BenchmarkDotNet,
not ad-hoc timing).

One deliberate gap: Python's `embed_fn_from_agent` (embeddings derived from the loaded checkpoint's
own encoder) has no equivalent — `IDecisionBackend` only exposes the decision head's final output,
not raw encoder hidden states, so that would need a third exported graph `tools/layar-export`
doesn't produce today. `Shortlist` itself works with any `IEmbeddingProvider`, including one backed
by a real bi-encoder (which Python's own docs note usually shortlists better anyway).

Not yet done: the full docs site. ONNX Runtime's ~2x latency gap vs. TorchSharp was investigated
(`SessionOptions` thread/execution-mode tuning, see "Measured" above) and isn't a quick fix — see
`AGENTS.md`'s rough edges. NativeAOT compatibility for `Layar.Onnx`/`Layar` is designed for but
not locally verified on this machine (its Xcode Command Line Tools SDK is currently broken,
unrelated to this project — see `AGENTS.md`); CI's `macos-latest` runner should verify it properly.
Filed [dotnet/TorchSharp#1581](https://github.com/dotnet/TorchSharp/issues/1581) asking about
Metal/MPS backend availability.
