---
name: layar-onnx-cpu
description: >-
  Use when setting up Layar from scratch in a .NET app on ONNX Runtime: Layar.Onnx.Cpu is the one-package install that
  brings Layar, Layar.Tokenization and Layar.Onnx.
---

# Layar.Onnx.Cpu

A bundle with no code of its own: it depends on `Layar`, `Layar.Tokenization` and `Layar.Onnx`. It is the default
install. Prefer `Layar.TorchSharp.Cpu` for libtorch, or the individual packages to run both backends side by side.

## The calls you need

```csharp
using Laya.Core;
using Laya.Onnx;
using Laya.Tokenization;

using var cache = new CheckpointCache(cacheDir, GitHubReleaseCheckpointUrls.Create("nullean", "layar", "models-1.0.0"));
var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(await cache.EnsureTokenizerAsync("english"));
var backend = new OnnxDecisionBackend(await cache.EnsureOnnxModelAsync("english"));
using var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

GuardResult guard = await engine.PredictAsync(userPrompt, GuardSchema.Instance);
bool block = guard.Jailbreak.Noul > 0.5f || guard.PromptInjection.Noul > 0.5f;
```

## Gotchas

- The namespaces are `Laya.Core`, `Laya.Onnx` and `Laya.Tokenization`, not `Layar.*`.
- The checkpoints are downloaded on first use (hundreds of megabytes) and cached under `cacheDir`; they are not in the
  package.
- The `layar` skill (from the `Layar` package) covers questions, answers and their gotchas.

Docs: https://layar.nullean.net
