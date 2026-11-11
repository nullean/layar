# Layar.Cli

![nullean](https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png)

The `layar` command: run Layar's triage preset (intent, urgency, frustration, refund, churn risk) against a checkpoint
from the command line, and benchmark the ONNX and TorchSharp backends against each other.

[![NuGet](https://img.shields.io/nuget/v/Layar.Cli?style=flat-square)](https://www.nuget.org/packages/Layar.Cli)

## Install

```sh
dotnet tool install --global Layar.Cli
```

## Quick start

```console
$ layar
layar predict --model <dir> --backend onnx|torchsharp --state <text>
layar benchmark --model <dir> --state <text> [--iterations N]

<dir> is a directory produced by tools/layar-export (containing model.onnx,
model.pt, tokenizer/, config.json).
```

```sh
layar predict --model ./checkpoints/english --backend onnx --state "We were billed twice for March."
layar benchmark --model ./checkpoints/english --state "We were billed twice for March." --iterations 50
```

`--backend` defaults to `onnx` and `--iterations` to 20. The CLI does not download checkpoints. `benchmark` loads both
backends, so it needs `model.onnx` and `model.pt`, and prints the average milliseconds per call and the difference in
confidence per question. For other questions, write code against the `Layar` package.

## Learn more

- Documentation: https://layar.nullean.net
- Source and issues: https://github.com/nullean/layar
