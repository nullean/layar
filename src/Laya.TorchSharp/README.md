# Layar.TorchSharp

![nullean](https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png)

The TorchSharp (libtorch) backend for Layar: it loads the checkpoint's `torch.jit.trace`-exported TorchScript module.
One of two interchangeable `IDecisionBackend` implementations (the other is `Layar.Onnx`), so the two can be
benchmarked against each other on the same requests.

[![NuGet](https://img.shields.io/nuget/v/Layar.TorchSharp?style=flat-square)](https://www.nuget.org/packages/Layar.TorchSharp)

## Install

```sh
dotnet add package Layar.TorchSharp
dotnet add package TorchSharp-cpu   # or a TorchSharp-cuda-* package
```

This package carries no native libtorch. `Layar.TorchSharp.Cpu` bundles it with the CPU runtime, `Layar` and
`Layar.Tokenization`.

## Quick start

```csharp
using Laya.Core;
using Laya.Tokenization;
using Laya.TorchSharp;

var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(Path.Combine(checkpointDir, "tokenizer"));
var backend = new TorchSharpDecisionBackend(Path.Combine(checkpointDir, "model.pt")); // DeviceType.CPU by default
using var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

var answers = await engine.PredictAsync(message, Presets.ModerationQuestions());
```

On macOS, `TorchSharp-cpu`'s `libtorch_cpu.dylib` loads homebrew's `libomp.dylib` by absolute path: run
`brew install libomp`, or set `DYLD_LIBRARY_PATH` to the directory holding the bundled `libomp.dylib`.

## Learn more

- Documentation: https://layar.nullean.net
- Source and issues: https://github.com/nullean/layar
