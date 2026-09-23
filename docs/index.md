# Layar

A .NET port of [Laya](https://github.com/NandhaKishorM/laya): typed decisions (`choice`, `score`,
`noul`) answered over state in a single forward pass by a small transformer decision head — no
text generation, so there's nothing to parse and nothing to hallucinate.

Layar ships two interchangeable inference backends behind one interface (`IDecisionBackend`), so
they can be benchmarked against each other on the same requests instead of picking one on faith:

- **Layar.Onnx** runs the model through [ONNX Runtime](https://onnxruntime.ai/).
- **Layar.TorchSharp** runs it through [TorchSharp](https://github.com/dotnet/TorchSharp)
  (libtorch), by loading a `torch.jit.trace`-exported TorchScript module rather than
  hand-reimplementing the transformer architecture in C#.

Both are verified against the original Python package's own output on real requests, not just
against each other — see [Getting started](getting-started/index.md) and the project
[README](https://github.com/nullean/layar#readme) for the measured numbers.

## Packages

| Package | What it is |
|---|---|
| `Layar` | Question/answer types, sequence building, calibration math, language/script routing, the embedding shortlist for high-cardinality questions. No ML dependency; designed to be AOT-safe. |
| `Layar.Tokenization` | A from-scratch BPE tokenizer (byte-level GPT-2-style and Metaspace/SentencePiece-style) that reads a Hugging Face `tokenizer.json` directly. No external tokenizer package, no Python at runtime. |
| `Layar.Onnx` | `IDecisionBackend` over ONNX Runtime. |
| `Layar.TorchSharp` | `IDecisionBackend` over TorchSharp/libtorch. |
| `Layar.Cli` (`layar`) | `predict` and `benchmark` commands, installable as a `dotnet tool`. |
| `Layar.Onnx.Cpu` / `Layar.TorchSharp.Cpu` | Meta-packages bundling `Layar` + `Layar.Tokenization` + one backend (plus its native runtime for TorchSharp) in a single install. |

## Why a port at all

The Python package is small (under 2,000 lines) and already runs comfortably on CPU — no GPU
required, confirmed by measuring it directly rather than assuming. Layar exists to answer a
narrower question: what does this actually cost in a strongly-typed, AOT-oriented .NET service,
and does one backend meaningfully beat the other once both are held to the same numeric bar as the
original model? The `README`'s "Measured" section has the current answer; it's expected to keep
changing as both backends get tuned further.

## Status

Layar is under active development. The core inference path — sequence building, calibration,
language routing, both backends, the model-lifecycle router, the embedding shortlist — is ported
and verified against the Python package's own output. What isn't finished yet, and why, is tracked
plainly in the project [README](https://github.com/nullean/layar#readme)'s Status section rather
than glossed over here.
