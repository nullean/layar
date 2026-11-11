# Layar.Onnx.Cpu

![nullean](https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png)

Layar in one package, running on ONNX Runtime: typed decisions over state in one forward pass. It has no code of its
own and installs `Layar`, `Layar.Tokenization` and `Layar.Onnx`. This is the default install, since ONNX Runtime
needs no choice of native package the way TorchSharp does.

[![NuGet](https://img.shields.io/nuget/v/Layar.Onnx.Cpu?style=flat-square)](https://www.nuget.org/packages/Layar.Onnx.Cpu)

## Install

```sh
dotnet add package Layar.Onnx.Cpu
```

Prefer libtorch? Install `Layar.TorchSharp.Cpu` instead, or the individual `Layar.*` packages to run both backends side
by side.

## Quick start

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

The checkpoints are not in the package. `CheckpointCache` downloads one on first use (hundreds of megabytes); no
`models-*` release has been published yet.

## Learn more

- Documentation: https://layar.nullean.net
- Source and issues: https://github.com/nullean/layar
