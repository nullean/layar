<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png">
    <img src="https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-light-544x192.png" width="272" alt="nullean">
  </picture>
</p>

<h1 align="center">Layar</h1>

<p align="center">A .NET port of <a href="https://github.com/NandhaKishorM/laya">Laya</a>: typed decisions (<code>choice</code>, <code>score</code>, <code>noul</code>) over state, answered in one forward pass by a small transformer decision head. There is no text generation, so nothing to parse and nothing to hallucinate.</p>

<p align="center">
  <a href="https://github.com/nullean/layar/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/nullean/layar/ci.yml?branch=main&style=flat-square&label=ci" alt="CI"></a>
  <a href="https://layar.nullean.net"><img src="https://img.shields.io/badge/docs-layar.nullean.net-20a0a0?style=flat-square" alt="Docs"></a>
</p>

```csharp
var tokenizer = HuggingFaceBpeTokenizer.FromDirectory("path/to/checkpoint/tokenizer");
var backend = new OnnxDecisionBackend("path/to/checkpoint/model.onnx"); // or new TorchSharpDecisionBackend(".../model.pt")
using var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

// TriageSchema.Instance is an IQuestionSchema<TriageResult>: a fixed question set plus how to bind the answers back
// into a record, so this returns a TriageResult directly, with no string-keyed lookup.
var triage = await engine.PredictAsync(
    "Hi, we were billed twice for March. Please refund the duplicate today.",
    TriageSchema.Instance);
Console.WriteLine($"{triage.Intent.Choice} (confidence {triage.Intent.Confidence:F2})");
```

Layar ships two interchangeable inference backends behind one interface, `IDecisionBackend`, so they can be benchmarked
against each other on the same requests:

- **Layar.Onnx** runs the model on [ONNX Runtime](https://onnxruntime.ai/).
- **Layar.TorchSharp** runs it on [TorchSharp](https://github.com/dotnet/TorchSharp) (libtorch), loading a
  `torch.jit.trace`-exported TorchScript module rather than a hand-ported architecture.

Both are verified float-for-float against the original Python package's own output on the same request: see
`tools/layar-export` and [Measured](#measured).

## Install

Layar is not on nuget.org yet. Once it is, install the bundle that matches how you want to run it:

```sh
dotnet add package Layar.Onnx.Cpu        # ONNX Runtime, one package
# or
dotnet add package Layar.TorchSharp.Cpu  # TorchSharp/libtorch, one package
```

Or the individual `Layar.*` packages for full control, for example to run both backends side by side:

```sh
dotnet add package Layar
dotnet add package Layar.Tokenization
dotnet add package Layar.Onnx
dotnet add package Layar.TorchSharp
dotnet add package TorchSharp-cpu
```

## Quick start

The namespaces are `Laya.*` (`Laya.Core`, `Laya.Tokenization`, `Laya.Onnx`, `Laya.TorchSharp`); the packages are
`Layar.*`. A preset schema such as `TriageSchema` binds the answers into a record, as above. A question set you compose
yourself has no fixed shape to bind to, so it stays on the dictionary path, with typed accessors:

```csharp
var answers = await engine.PredictAsync("...", Presets.EmailQuestions(myCategories));
var category = answers.Choice("category");
```

A checkpoint directory (`model.onnx`, `model.pt`, `tokenizer/`, `config.json`) is produced by
`tools/layar-export/export.py`, a maintainer-run Python script that loads the original `laya` package's checkpoint and
exports it. It is not shipped with the .NET library.

### Downloading a checkpoint on demand

Checkpoints (650 MB to 1.2 GB) are not committed to this repository. `CheckpointCache` downloads and caches one on
first use. Where they are hosted is pluggable (`CheckpointAssetUrl`); `GitHubReleaseCheckpointUrls` covers
`tools/layar-export`'s output attached to a GitHub Release as flat assets:

```csharp
using var cache = new CheckpointCache(
    cacheRoot: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "layar", "models"),
    assetUrl: GitHubReleaseCheckpointUrls.Create("nullean", "layar", "models-1.0.0"));

var tokenizerDir = await cache.EnsureTokenizerAsync("multilingual");
var onnxPath = await cache.EnsureOnnxModelAsync("multilingual"); // also fetches model.onnx.data if present
```

No checkpoints have been published there yet: that needs a `models-1.0.0` tag, not a code change. The checkpoint itself
is Apache-2.0 and explicitly tagged for commercial use; [NOTICE.md](NOTICE.md) has the licence check behind
redistributing it.

### Command line

`Layar.Cli` installs the `layar` command, which runs the triage preset against a checkpoint directory:

```console
$ layar
layar predict --model <dir> --backend onnx|torchsharp --state <text>
layar benchmark --model <dir> --state <text> [--iterations N]

<dir> is a directory produced by tools/layar-export (containing model.onnx,
model.pt, tokenizer/, config.json).
```

## Packages

| Package | What it is |
|---|---|
| [`Layar`](src/Layar.Core/README.md) | Question and answer types, sequence building, calibration math, language and script routing, typed answer accessors. No ML dependency; AOT-safe. |
| [`Layar.Tokenization`](src/Laya.Tokenization/README.md) | A from-scratch BPE tokenizer (byte-level GPT-2-style, and Metaspace/SentencePiece-style) reading a Hugging Face `tokenizer.json` directly. No external tokenizer package, no Python at runtime. |
| [`Layar.Onnx`](src/Laya.Onnx/README.md) | `IDecisionBackend` over ONNX Runtime. |
| [`Layar.TorchSharp`](src/Laya.TorchSharp/README.md) | `IDecisionBackend` over TorchSharp/libtorch. |
| [`Layar.Cli`](src/Laya.Cli/README.md) | The `layar` command: `predict` and `benchmark`, installable as a `dotnet tool`. |
| [`Layar.Onnx.Cpu`](src/Layar.Onnx.Cpu/README.md) | Bundle: `Layar`, `Layar.Tokenization` and `Layar.Onnx` in one install. The default, since ONNX Runtime needs no choice of native runtime the way TorchSharp does. |
| [`Layar.TorchSharp.Cpu`](src/Layar.TorchSharp.Cpu/README.md) | Bundle: `Layar`, `Layar.Tokenization`, `Layar.TorchSharp` and the `TorchSharp-cpu` native runtime in one install. |

Every package also carries an agent skill (`skills/<name>/SKILL.md`).

## Measured

`tests/Laya.Benchmarks` (BenchmarkDotNet, `dotnet run -c Release --project tests/Laya.Benchmarks -- --filter '*'`) on
this repository's multilingual checkpoint, a real 3-question batched request, CPU, Apple M2:

| Method | Mean | Ratio | Allocated |
|---|---|---|---|
| Onnx | 148.7 ms | 1.00 | 82.6 KB |
| TorchSharp | 70.9 ms | 0.48 | 85.3 KB |

TorchSharp is about 2.1x faster here. Both match the Python oracle exactly (confidence agreement to 1e-6): see
`tests/Laya.Tests/PipelineParityTests.cs`. `SessionOptions` tuning (thread count, graph optimization level, execution
mode) does not close the gap; `AGENTS.md`'s rough edges list what was tried.

## Status

A working, numerically verified port of the full core surface:

- Core, Tokenization, Onnx, TorchSharp and a minimal Cli.
- The model-lifecycle router (`RouterEngine`: load, preload, unload, attach, LRU eviction).
- Structured, non-string criteria (`CriterionText`, matching Python's `render_criterion`).
- On-demand checkpoint downloading (`CheckpointCache` and `GitHubReleaseCheckpointUrls`), though nothing is published
  there yet.
- The embedding shortlist for questions with more than 20 options (`Shortlist` and `ShortlistPrediction`, matching
  `shortlist.py`/`predict_shortlist` exactly, including its tie-breaking and NaN/zero-vector edge cases).

Tokenizer conformance and full-pipeline parity against the Python oracle are committed regression tests in
`tests/Laya.Tests`, not one-off checks.

One deliberate gap: Python's `embed_fn_from_agent` (embeddings from the loaded checkpoint's own encoder) has no
equivalent. `IDecisionBackend` only exposes the decision head's final output, not raw encoder hidden states, so it would
need a third exported graph that `tools/layar-export` does not produce today. `Shortlist` works with any
`IEmbeddingProvider`, including one backed by a real bi-encoder, which Python's own docs note usually shortlists better
anyway.

Not yet done: ONNX Runtime's roughly 2x latency gap against TorchSharp was investigated and is not a quick fix (see
`AGENTS.md`'s rough edges). [dotnet/TorchSharp#1581](https://github.com/dotnet/TorchSharp/issues/1581) asks about
Metal/MPS backend availability.

NativeAOT compatibility is verified by `examples/layar-aot-smoketest` (Onnx) and `examples/layar-torchsharp-aot-smoketest`
(TorchSharp). Beyond what those check, since a real checkpoint cannot ship in CI, a NativeAOT binary was confirmed
locally to load a real exported checkpoint through `Layar.TorchSharp` and produce a correct prediction. `Layar`,
`Layar.Tokenization`, `Layar.Onnx` and `Layar.TorchSharp` all declare `IsAotCompatible`.

## Building

```sh
mise run build
mise run test
```

TorchSharp's native `libtorch` needs `DYLD_LIBRARY_PATH`/`LD_LIBRARY_PATH` pointed at the consolidated native library
directory when running with `dotnet run` from a loose build output (see `AGENTS.md`). Separately, on macOS,
`TorchSharp-cpu`'s `libtorch_cpu.dylib` hardcodes an absolute load path to homebrew's `libomp.dylib`: `brew install
libomp` fixes it, or point `DYLD_LIBRARY_PATH` at wherever `libomp.dylib` already landed. A published or AOT output
bundles its own copy next to the binary; `AGENTS.md`'s rough edges explain why dyld still needs pointing at it.

### Releases

Two independent tag prefixes, so a library release never re-uploads unchanged model weights and a new checkpoint export
never forces a library version bump (MinVer's own pattern; see its README, "Can I version multiple projects in a single
repository independently?"):

| Tag | Triggers | Versions |
|---|---|---|
| `v<major.minor.patch>`, for example `v0.2.0`, created by publishing the release-drafter draft | the `release` job in `.github/workflows/ci.yml` | `Layar`, `Layar.Tokenization`, `Layar.Onnx`, `Layar.TorchSharp`, `Layar.Cli` and the bundles (`MinVerTagPrefix` is `v`) |
| `models-<major.minor.patch>`, for example `models-1.0.0` | `.github/workflows/release-models.yml` | the three checkpoints (english, multilingual, typed-decisions), exported fresh and uploaded as flat GitHub Release assets |

## Learn more

- Documentation: https://layar.nullean.net
- [Releases](https://github.com/nullean/layar/releases)
- [Contributing](https://github.com/nullean/.github/blob/main/CONTRIBUTING.md)

MIT licensed. Copyright Nullean and contributors.
