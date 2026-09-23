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
| `Layar.Core` | Question/answer types, sequence building, calibration math, language/script routing. No ML dependency; AOT-safe. |
| `Layar.Tokenization` | A from-scratch BPE tokenizer (byte-level GPT-2-style + Metaspace/SentencePiece-style) reading a Hugging Face `tokenizer.json` directly. No external tokenizer package, no Python at runtime. |
| `Layar.Onnx` | `IDecisionBackend` over ONNX Runtime. |
| `Layar.TorchSharp` | `IDecisionBackend` over TorchSharp/libtorch. |
| `Layar.Cli` | `predict` and `benchmark` commands. |

## Quickstart

```csharp
var tokenizer = HuggingFaceBpeTokenizer.FromDirectory("path/to/checkpoint/tokenizer");
using var backend = new OnnxDecisionBackend("path/to/checkpoint/model.onnx"); // or TorchSharpDecisionBackend(...model.pt)
var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

var answers = await engine.PredictAsync(
    "Hi, we were billed twice for March. Please refund the duplicate today.",
    Presets.TriageQuestions());
```

A checkpoint directory (`model.onnx`, `model.pt`, `tokenizer/`, `config.json`) is produced by
`tools/layar-export/export.py`, a maintainer-run Python script that loads the original `laya`
package's checkpoint and exports it — not shipped with the .NET library.

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

This is a working, numerically-verified port of the core inference path (Core, Tokenization, Onnx,
TorchSharp, a minimal Cli), including the full model-lifecycle Router (`RouterEngine`: load,
preload, unload, attach, LRU eviction — the pure routing *decision* was ported first; the loading
side wraps it) and structured/non-string criteria (`CriterionText`, matching Python's
`render_criterion`). Tokenizer conformance and full-pipeline parity against the Python oracle are
committed regression tests, not one-off scratchpad checks — see `tests/Laya.Tests`.

Not yet done: `Shortlist` (the embedding-based shortlist for >20-option questions — needs a design
decision on what an `embed_fn` abstraction looks like in a strongly-typed API, which Python leaves
entirely to the caller) and the full docs site. ONNX Runtime's ~2x latency gap vs. TorchSharp was
investigated (`SessionOptions` thread/execution-mode tuning, see "Measured" above) and isn't a
quick fix — see `AGENTS.md`'s rough edges. NativeAOT compatibility for `Layar.Onnx`/`Layar.Core` is
designed for but not locally verified on this machine (its Xcode Command Line Tools SDK is
currently broken, unrelated to this project — see `AGENTS.md`); CI's `macos-latest` runner should
verify it properly. Filed [dotnet/TorchSharp#1581](https://github.com/dotnet/TorchSharp/issues/1581)
asking about Metal/MPS backend availability.
