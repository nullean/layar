---
name: layar-cli
description: >-
  Use when trying Layar from a shell, or comparing its ONNX and TorchSharp backends on a checkpoint: the layar dotnet
  tool runs the triage preset over a piece of text, or benchmarks both backends.
---

# Layar.Cli

`dotnet tool install -g Layar.Cli` installs the `layar` command. It runs the triage preset (intent, urgency,
frustration, refund, churn risk) only. For other questions, write code against the `Layar` package.

## The calls you need

```bash
layar predict --model ./checkpoints/english --backend onnx --state "We were billed twice for March."
layar predict --model ./checkpoints/english --backend torchsharp --state "..."
layar benchmark --model ./checkpoints/english --state "..." --iterations 50
```

`--model` is a checkpoint directory with `model.onnx`, `model.pt` and `tokenizer/`, as `tools/layar-export` writes
it. `--backend` defaults to `onnx`, and `--iterations` to 20.

## Gotchas

- The CLI does not download checkpoints; `--model` must already exist on disk.
- `benchmark` loads both backends, so it needs `model.onnx` and `model.pt`. It prints the average milliseconds per
  call and the difference in confidence per question.
- The TorchSharp backend on macOS needs `brew install libomp` (see the `layar-torchsharp` skill).

Docs: https://layar.nullean.net
