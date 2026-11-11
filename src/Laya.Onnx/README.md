# Layar.Onnx

![nullean](https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png)

The ONNX Runtime backend for Layar: one of two interchangeable `IDecisionBackend` implementations (the other is
`Layar.TorchSharp`), so the two can be benchmarked against each other on the same requests.

[![NuGet](https://img.shields.io/nuget/v/Layar.Onnx?style=flat-square)](https://www.nuget.org/packages/Layar.Onnx)

## Install

```sh
dotnet add package Layar.Onnx
```

Or `Layar.Onnx.Cpu`, which bundles it with `Layar` and `Layar.Tokenization`.

## Quick start

```csharp
using Laya.Core;
using Laya.Onnx;
using Laya.Tokenization;

var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(Path.Combine(checkpointDir, "tokenizer"));
var backend = new OnnxDecisionBackend(Path.Combine(checkpointDir, "model.onnx"));
using var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

var triage = await engine.PredictAsync(message, TriageSchema.Instance);
```

Pass `Microsoft.ML.OnnxRuntime.SessionOptions` as the second constructor argument to tune the session. A
`model.onnx.data` weights file, when the export has one, must sit next to `model.onnx`.

## Learn more

- Documentation: https://layar.nullean.net
- Source and issues: https://github.com/nullean/layar
