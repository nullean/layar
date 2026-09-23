# Agent Configuration

## Project

Layar — a .NET port of [Laya](https://github.com/NandhaKishorM/laya), a non-autoregressive typed
decision engine. See README.md for the package layout and measured numbers.

## Build & Test

```bash
dotnet build layar.slnx
dotnet run --project tests/Laya.Tests/Laya.Tests.csproj -c Release
```

## Conventions

- .NET 10, C# latest, file-scoped namespaces, `var` everywhere, tab indentation, Allman braces
- `[GeneratedRegex]` with a 2000ms timeout for every regex (ReDoS protection)
- Prefer `Span<T>`/`ReadOnlySpan<T>` and `ArrayPool<T>` over intermediate allocations in
  `Laya.Core`'s hot paths (`SequenceBuilder`, `ProbabilityMath`); `System.Numerics.Tensors.TensorPrimitives`
  for the softmax/entropy math rather than hand-rolled SIMD loops
- `FrozenDictionary`/`FrozenSet`/`SearchValues<char>` for static lookup tables (`Laya.Core.Lang`'s
  stopword lists and diacritic set)
- JSON is read/written via `JsonDocument`/`JsonElement`/`JsonObject` (the DOM APIs), never
  `JsonSerializer.Deserialize<T>`/`Serialize<T>` without a source-generated context — keeps
  `Laya.Core`/`Laya.Tokenization` reflection-free and AOT-safe by construction
- MIT license, no per-file headers

## Architecture

```
state + questions -> SequenceBuilder (per question) -> SequenceBatch.Collate (one batch)
                   -> IDecisionBackend.PredictAsync (Onnx or TorchSharp)
                   -> ProbabilityMath -> Answer (Choice/Score/Noul)
```

`DecisionEngine` (Laya.Core) is the orchestrator tying these together — the direct port of the
Python package's `Agent.system_one`. `IDecisionBackend` is the seam between `Laya.Onnx` and
`Laya.TorchSharp`; both consume the exact same `DecisionRequest` produced by `Laya.Core` alone, with
no ML-specific code in Core itself.

### Correctness strategy

Every port in this repo is checked against the original Python package
(`~/Projects/laya`, a separate checkout of NandhaKishorM/laya with its own venv) as the oracle, not
against the README. `Laya.Tokenization`'s BPE engine was conformance-tested token-for-token against
`AutoTokenizer` across English and multilingual, punctuation-heavy, and multi-language text before
being trusted. The full pipeline (`Core` + `Tokenization` + each backend) was verified against
`Agent.predict()`'s actual output on a real multi-question batch — see README's "Measured" section
for the numbers; they match to float precision.

### Known rough edges (see README's Status section for the fuller list)

- **ONNX export must use `dynamo=True`.** The legacy TorchScript-based exporter
  (`torch.onnx.export(..., dynamo=False)`) bakes the traced example's batch size into a `Reshape`
  inside the encoder's attention block, despite `dynamic_axes` declaring batch dynamic — it silently
  produces a graph that only works for that one batch size. `tools/layar-export/export.py` uses the
  newer `torch.export`-based exporter (`dynamo=True` + `dynamic_shapes`), verified against batch
  sizes 1/2/3/5.
- **`SequenceBatch.Collate` always allocates at least 2 marker slots per batch,** even for a
  single-option question. The decision head's own top-2-vs-rest "act" feature branches on whether
  there are >= 2 markers; both exported backends trace that branch as a constant, so a real
  single-marker request must still take the traced path.
- **`ProbabilityMath.SoftmaxWithTemperature` subtracts the max before calling
  `TensorPrimitives.SoftMax`.** The action head's raw logits routinely run into the hundreds
  (verified: 1203.17 / -1437.54 on a real request) and `TensorPrimitives.SoftMax`'s own `exp()`
  overflows to `+Infinity` for inputs that large, turning `Infinity/Infinity` into `NaN` without the
  explicit max-subtraction.
- **ONNX Runtime's ~3x latency gap vs. TorchSharp isn't a `SessionOptions` misconfiguration.**
  Tried `IntraOpNumThreads` at 1/4/8, `GraphOptimizationLevel.ORT_ENABLE_ALL`, and
  `ExecutionMode.ORT_PARALLEL` against the real exported model — default settings (which already
  pick a sensible intra-op thread count) were at least as fast as every explicit override; more
  threads than the default made it worse (8 threads: +15%; `ORT_PARALLEL` with 2 inter-op threads:
  +30%), and 1 thread was catastrophic (5-6x slower). Whatever's behind the gap, it's not idle
  thread-pool headroom — worth profiling the actual op-level breakdown before assuming it's fixable
  at the `SessionOptions` level at all.
- **TorchSharp's native `libtorch` needs the consolidated native directory on
  `DYLD_LIBRARY_PATH`/`LD_LIBRARY_PATH`** when running via `dotnet run` from a loose build output,
  and this repo's `UseArtifactsOutput=true` changes where that directory actually lands
  (`.artifacts/bin/<project>/<config>/runtimes/<rid>/native/`, not the classic
  `bin/<config>/<tfm>/<rid>/cpu/`).
- **`TorchSharp-cpu` 0.107.0's `libtorch_cpu.dylib` hardcodes an absolute load path to homebrew's
  `libomp.dylib`** (`/opt/homebrew/opt/libomp/lib/libomp.dylib`), on macOS — confirmed by inspecting
  the actual `dlopen` failure. This is unrelated to `dotnet run` vs. `dotnet publish`, or to AOT vs.
  JIT: any process on a machine without that exact homebrew package installed hits the same missing
  dependency, even from a fully consolidated publish/AOT output that bundles its own copy of
  `libomp.dylib` right next to the binary — dyld doesn't search the executable's own directory for
  an absolute-path dependency by default. `brew install libomp` fixes it outright; the alternative,
  confirmed working, is pointing `DYLD_LIBRARY_PATH` at the directory containing the bundled
  `libomp.dylib` (dyld does fall back to `DYLD_LIBRARY_PATH`, searched by basename, even for an
  absolute-path dependency that failed to resolve).
- **NativeAOT now verifies on this development machine, via a `SDKROOT` override — not a fix to
  the Xcode Command Line Tools SDK itself.** Its default SDK
  (`MacOSX27.0.sdk`, a macOS-27-beta CLT installed ahead of the actual OS, 26.6.1 — i.e. this
  machine is enrolled in the beta/developer update channel) ships malformed `.tbd` stub files that
  `clang`/`ld` cannot parse. Critically, plain `xcrun --show-sdk-path` (and `-sdk macosx`) still
  resolve to that broken SDK by default — the working one has to be named explicitly:
  `SDKROOT=$(xcrun --sdk macosx26.5 --show-sdk-path) dotnet publish ...`. With that,
  `examples/layar-aot-smoketest` and `examples/layar-torchsharp-aot-smoketest` both publish and run
  correctly, and — beyond what the committed smoketests check, since a real checkpoint can't ship in
  CI — a NativeAOT binary referencing `Laya.TorchSharp` was verified end-to-end against a real
  exported checkpoint: it loads the TorchScript module and produces the same prediction as the JIT
  path. `Laya.TorchSharp` is AOT-compatible; the real Xcode CLT is still broken and this SDKROOT
  override is a workaround, not a fix for it. CI's `macos-latest` runner is unaffected either way.
