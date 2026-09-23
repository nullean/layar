# Notice

Layar's own code is MIT-licensed (see [LICENSE.txt](LICENSE.txt)). This file covers everything
Layar incorporates, ports the design of, or redistributes that isn't Layar's own original work —
checked directly against each upstream project's declared license before anything here was written
or released, not assumed.

## The `laya` Python package

Layar is a .NET port of [`laya`](https://github.com/NandhaKishorM/laya) by
[Convai Innovations](https://github.com/convaiinnovations) (author: NandhaKishorM) — the
question/answer model, sequence-building layout, calibration math, and language routing are direct
ports of that project's design, re-implemented from scratch in C# (no source was copied). Licensed
under the [Apache License 2.0](https://github.com/NandhaKishorM/laya/blob/main/LICENSE); no NOTICE
file is published there to carry forward.

```
Copyright NandhaKishorM / Convai Innovations
Licensed under the Apache License, Version 2.0
```

## Model checkpoints

`tools/layar-export/export.py` exports checkpoints from
[`convaiinnovations/laya`](https://huggingface.co/convaiinnovations/laya) on Hugging Face (the
english, multilingual, and typed-decisions checkpoints all live in that one repo, as subfolders) to
ONNX and TorchScript. **`release-models.yml` redistributes those exports** as GitHub Release
assets — this is the part that actually needs a license check, since it's Layar publishing a
derivative form of someone else's weights, not just calling their code.

That checkpoint's model card declares [Apache License 2.0](https://huggingface.co/convaiinnovations/laya)
and explicitly tags `commercial-use`, confirmed against the model card's own metadata rather than
assumed from the code license. The only change made to the weights themselves is the export/format
conversion (`torch.onnx.export` and `torch.jit.trace` — see `tools/layar-export/export.py`); no
retraining or fine-tuning happens in this repo.

```
Copyright convaiinnovations
Licensed under the Apache License, Version 2.0
```

### Base encoder architectures

For provenance, not because Layar redistributes these separately (they're embedded in the
checkpoint above, already covered by its own Apache-2.0 license):

- English checkpoint: [ModernBERT-large](https://huggingface.co/answerdotai/ModernBERT-large)
  (answerdotai) — Apache License 2.0.
- Multilingual checkpoint: [mmBERT-base](https://huggingface.co/jhu-clsp/mmBERT-base) (jhu-clsp) —
  MIT License.

## Third-party NuGet dependencies

Runtime dependencies of the published `Layar.*` packages, license verified against each package's
own NuGet metadata (not the project's marketing page):

| Package | License | Project |
|---|---|---|
| Microsoft.ML.OnnxRuntime | MIT | https://github.com/microsoft/onnxruntime |
| TorchSharp | MIT | https://github.com/dotnet/TorchSharp |
| TorchSharp-cpu | MIT (wrapper); bundles libtorch under PyTorch's own BSD-style license | https://github.com/dotnet/TorchSharp, https://github.com/pytorch/pytorch |
| TorchSharp.PyBridge | MIT | https://github.com/shaltielshmid/TorchSharp.PyBridge |
| System.Numerics.Tensors | MIT | https://dot.net |

None of these require reproducing their license text here beyond this notice — MIT and the
PyTorch BSD-style license both only require retaining the copyright/license notice in
distributions that include their own source or binaries, which NuGet's own package metadata already
carries for consumers of `Layar.Onnx.Cpu` / `Layar.TorchSharp.Cpu`.
