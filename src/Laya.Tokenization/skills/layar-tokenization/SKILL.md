---
name: layar-tokenization
description: >-
  Use when Layar needs its tokenizer, or C# code needs to BPE-encode text with a Hugging Face tokenizer.json (byte-level
  GPT-2 style or Metaspace/SentencePiece style) without Python or another tokenizer package.
---

# Layar.Tokenization

`using Laya.Tokenization;` `HuggingFaceBpeTokenizer` is the `ITokenizer` a `DecisionEngine` takes. It reads a
checkpoint's `tokenizer/` directory directly. It covers the two tokenizer families Layar's checkpoints use; it is not
a general Hugging Face tokenizer.

## The calls you need

```csharp
var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(tokenizerDir); // tokenizer.json + tokenizer_config.json

var ids = new int[tokenizer.MaxTokenCount(text)];  // an upper bound, not the exact count
var count = tokenizer.Encode(text, ids);
ReadOnlySpan<int> tokens = ids.AsSpan(0, count);
```

## Gotchas

- `FromDirectory` takes the directory, not the `tokenizer.json` path, and needs `tokenizer_config.json` beside it.
  `CheckpointCache.EnsureTokenizerAsync` returns exactly that directory.
- Size the buffer with `MaxTokenCount`, then slice to what `Encode` returns.
- `Encode` adds no special tokens. `ClsTokenId`, `SepTokenId`, `PadTokenId` and `MaskTokenId` are there for callers
  that frame sequences themselves; `DecisionEngine` already does.

Docs: https://layar.nullean.net
