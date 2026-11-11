---
name: layar-onnx
description: >-
  Use when a Layar DecisionEngine should run on ONNX Runtime: construct the ONNX decision backend from an exported
  model.onnx checkpoint.
---

# Layar.Onnx

`using Laya.Onnx;` `OnnxDecisionBackend` is the `IDecisionBackend` over ONNX Runtime. It is the default backend:
ONNX Runtime needs no choice of native package. For TorchSharp instead, use `Layar.TorchSharp`; both answer the same
requests with the same numbers.

## The calls you need

```csharp
var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(Path.Combine(checkpointDir, "tokenizer"));
var backend = new OnnxDecisionBackend(Path.Combine(checkpointDir, "model.onnx"));
using var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

var triage = await engine.PredictAsync(message, TriageSchema.Instance);
```

Pass `Microsoft.ML.OnnxRuntime.SessionOptions` as the second argument to tune the session.

## Gotchas

- Some exports write their weights to `model.onnx.data`. It must sit next to `model.onnx`;
  `CheckpointCache.EnsureOnnxModelAsync` downloads it when it exists.
- The engine disposes the backend. Do not wrap the backend in its own `using` as well.
- Tuning `SessionOptions` (thread counts, graph optimisation, parallel execution) did not make it faster than the
  defaults in Layar's measurements, and one intra-op thread was five to six times slower.

Docs: https://layar.nullean.net
