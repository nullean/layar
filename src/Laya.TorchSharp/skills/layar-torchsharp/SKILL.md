---
name: layar-torchsharp
description: >-
  Use when a Layar DecisionEngine should run on TorchSharp/libtorch: construct the TorchSharp decision backend from an
  exported model.pt TorchScript checkpoint, on CPU or CUDA.
---

# Layar.TorchSharp

`using Laya.TorchSharp;` `TorchSharpDecisionBackend` is the `IDecisionBackend` over libtorch, loading the checkpoint's
TorchScript module. This package carries no native libtorch: add `TorchSharp-cpu` (or a `TorchSharp-cuda-*` package),
or install `Layar.TorchSharp.Cpu`, which bundles the CPU runtime.

## The calls you need

```csharp
var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(Path.Combine(checkpointDir, "tokenizer"));
var backend = new TorchSharpDecisionBackend(Path.Combine(checkpointDir, "model.pt")); // DeviceType.CPU by default
using var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

var answers = await engine.PredictAsync(message, Presets.ModerationQuestions());
```

## Gotchas

- Without a native libtorch package the constructor fails at runtime, not at build time.
- On macOS, `TorchSharp-cpu`'s `libtorch_cpu.dylib` loads homebrew's `libomp.dylib` by absolute path. Run
  `brew install libomp`, or set `DYLD_LIBRARY_PATH` to the directory holding the bundled `libomp.dylib` (next to a
  published binary).
- Running from a loose build output with `dotnet run` needs the native directory
  (`.artifacts/bin/<project>/<config>/runtimes/<rid>/native/`) on `DYLD_LIBRARY_PATH` / `LD_LIBRARY_PATH`.
- The engine disposes the backend. Do not dispose it separately.

Docs: https://layar.nullean.net
