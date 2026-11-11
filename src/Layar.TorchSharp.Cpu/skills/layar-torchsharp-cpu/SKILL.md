---
name: layar-torchsharp-cpu
description: >-
  Use when setting up Layar from scratch in a .NET app on TorchSharp/libtorch, CPU only: Layar.TorchSharp.Cpu is the
  one-package install that brings Layar, Layar.Tokenization, Layar.TorchSharp and the TorchSharp-cpu runtime.
---

# Layar.TorchSharp.Cpu

A bundle with no code of its own: it depends on `Layar`, `Layar.Tokenization`, `Layar.TorchSharp` and
`TorchSharp-cpu`. For CUDA, install `Layar.TorchSharp` with the matching `TorchSharp-cuda-*` package instead.

## The calls you need

```csharp
using Laya.Core;
using Laya.Tokenization;
using Laya.TorchSharp;

using var cache = new CheckpointCache(cacheDir, GitHubReleaseCheckpointUrls.Create("nullean", "layar", "models-1.0.0"));
var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(await cache.EnsureTokenizerAsync("english"));
var backend = new TorchSharpDecisionBackend(await cache.EnsureTorchScriptModelAsync("english"));
using var engine = new DecisionEngine(tokenizer, backend, maxLen: 1024, headMaxLen: 256);

ModerationResult moderation = await engine.PredictAsync(comment, ModerationSchema.Instance);
```

## Gotchas

- The namespaces are `Laya.Core`, `Laya.TorchSharp` and `Laya.Tokenization`, not `Layar.*`.
- On macOS, libtorch loads homebrew's `libomp.dylib` by absolute path: `brew install libomp`, or point
  `DYLD_LIBRARY_PATH` at the bundled copy next to the binary.
- The `layar` skill (from the `Layar` package) covers questions, answers and their gotchas.

Docs: https://layar.nullean.net
