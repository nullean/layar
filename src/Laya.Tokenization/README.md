# Layar.Tokenization

![nullean](https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png)

A from-scratch BPE tokenizer for Layar, covering the two families its checkpoints use: byte-level GPT-2-style BPE and
Metaspace/SentencePiece-style BPE. It reads a Hugging Face `tokenizer.json` directly, with no external tokenizer
package and no Python at runtime. It is not a general Hugging Face tokenizer.

[![NuGet](https://img.shields.io/nuget/v/Layar.Tokenization?style=flat-square)](https://www.nuget.org/packages/Layar.Tokenization)

## Install

```sh
dotnet add package Layar.Tokenization
```

## Quick start

```csharp
using Laya.Tokenization;

var tokenizer = HuggingFaceBpeTokenizer.FromDirectory(tokenizerDir); // tokenizer.json + tokenizer_config.json

var ids = new int[tokenizer.MaxTokenCount(text)];  // an upper bound, not the exact count
var count = tokenizer.Encode(text, ids);
ReadOnlySpan<int> tokens = ids.AsSpan(0, count);
```

`FromDirectory` takes the directory, not the `tokenizer.json` path. `Encode` adds no special tokens;
`DecisionEngine` frames the sequences itself.

## Learn more

- Documentation: https://layar.nullean.net
- Source and issues: https://github.com/nullean/layar
