# Layar.TorchSharp.Cpu

![nullean](https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png)

Layar in one package, running on TorchSharp/libtorch, CPU only: typed decisions over state in one forward pass. It has
no code of its own and installs `Layar`, `Layar.Tokenization`, `Layar.TorchSharp` and the `TorchSharp-cpu` native
runtime.

[![NuGet](https://img.shields.io/nuget/v/Layar.TorchSharp.Cpu?style=flat-square)](https://www.nuget.org/packages/Layar.TorchSharp.Cpu)

## Install

```sh
dotnet add package Layar.TorchSharp.Cpu
```

Need CUDA? Install `Layar.TorchSharp` with the matching `TorchSharp-cuda-*` package instead.

## Quick start

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

The checkpoints are not in the package; no `models-*` release has been published yet. On macOS, libtorch loads
homebrew's `libomp.dylib` by absolute path: `brew install libomp`, or point `DYLD_LIBRARY_PATH` at the bundled copy
next to the binary.

## Learn more

- Documentation: https://layar.nullean.net
- Source and issues: https://github.com/nullean/layar
