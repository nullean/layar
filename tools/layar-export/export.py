#!/usr/bin/env python3
"""Exports a Laya checkpoint's decision model to ONNX and TorchScript, plus its tokenizer and
config, into a directory Laya.Onnx / Laya.TorchSharp can load.

Not shipped with the .NET library: this is a maintainer-run, one-time-per-checkpoint tool. It
depends on the original Python `laya` package (https://github.com/NandhaKishorM/laya) being
installed, since it loads the checkpoint through that package's own `Agent` rather than
re-implementing checkpoint loading here.

Usage:
    python export.py convaiinnovations/laya --subfolder multilingual -o ../../.artifacts/models/multilingual
    python export.py convaiinnovations/laya -o ../../.artifacts/models/english
"""
import argparse
import json
import os
import shutil

import torch


def build_example_inputs(agent):
    """A fixed two-question batch: enough markers (>=2 per question, see SequenceBatch.Collate's
    comment) and enough sequence length that both traced graphs generalize to real requests of
    varying length up to max_len (the transformer encoder itself has no fixed-shape state; only the
    model's own top-2-vs-rest branch is shape-sensitive, and that only cares about marker count)."""
    from laya.common import build_sequence, collate_items, QTYPES

    state = {"body": "Hi, we were billed twice for March. Please refund the duplicate today."}
    questions = [
        {"t": "choice", "ins": "Which department should handle this?",
         "crit": {"billing": "invoices, payments, refunds", "technical": "bugs, outages",
                   "sales": "pricing", "other": "everything else"}},
        {"t": "noul", "ins": "Does the user threaten to cancel?", "crit": None},
    ]
    max_len = agent.cfg.get("max_len", 512)
    head_max_len = agent.cfg.get("head_max_len", 192)
    items = []
    for q in questions:
        seq, markers = build_sequence(agent.tok, state, q, max_len, head_max_len)
        items.append({"ids": seq, "markers": markers, "qtype": QTYPES[q["t"]]})
    b = collate_items([items], agent.tok.pad_token_id)
    return (b["input_ids"], b["attention_mask"], b["marker_pos"], b["marker_mask"], b["qtype"])


def export_onnx(model, args, output_path):
    # The legacy TorchScript-based exporter (dynamo=False) bakes the traced example's batch size
    # into at least one Reshape inside the encoder's attention block, despite `dynamic_axes`
    # declaring batch dynamic -- it silently produces a graph that only works for that one batch
    # size. The newer torch.export-based exporter (dynamo=True) traces through torch.export
    # instead, which keeps the batch dimension genuinely symbolic; verified against batch sizes
    # 1/2/3/5 before relying on it here.
    torch.onnx.export(
        model, args, output_path,
        input_names=["input_ids", "attention_mask", "marker_pos", "marker_mask", "qtype"],
        output_names=["logits", "act_logits"],
        dynamic_shapes={
            "input_ids": {0: "batch", 1: "seq"},
            "attention_mask": {0: "batch", 1: "seq"},
            "marker_pos": {0: "batch", 1: "k"},
            "marker_mask": {0: "batch", 1: "k"},
            "qtype": {0: "batch"},
        },
        dynamo=True,
    )


def export_torchscript(model, args, output_path):
    traced = torch.jit.trace(model, args, check_trace=False)
    traced.save(output_path)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("model_id", help="e.g. convaiinnovations/laya")
    parser.add_argument("--subfolder", default=None, help="e.g. multilingual, typed-decisions")
    parser.add_argument("-o", "--output", required=True, help="output directory")
    args = parser.parse_args()

    import laya
    agent = laya.load(args.model_id, subfolder=args.subfolder, device="cpu")
    agent.model.eval()

    os.makedirs(args.output, exist_ok=True)
    example = build_example_inputs(agent)

    print("Exporting ONNX...")
    export_onnx(agent.model, example, os.path.join(args.output, "model.onnx"))

    print("Exporting TorchScript...")
    export_torchscript(agent.model, example, os.path.join(args.output, "model.pt"))

    print("Copying tokenizer...")
    src_model_dir = os.path.dirname(os.path.dirname(agent.tok.name_or_path)) \
        if os.path.isdir(agent.tok.name_or_path) else None
    # agent.tok was loaded from <model_dir>/tokenizer (or the HF encoder id as a fallback); find
    # the actual tokenizer directory back through the agent's own resolution instead of guessing.
    tok_dir = agent.tok.name_or_path
    if os.path.isdir(tok_dir):
        shutil.copytree(tok_dir, os.path.join(args.output, "tokenizer"), dirs_exist_ok=True)
    else:
        agent.tok.save_pretrained(os.path.join(args.output, "tokenizer"))

    config = {
        "max_len": agent.cfg.get("max_len", 512),
        "head_max_len": agent.cfg.get("head_max_len", 192),
        "temperature": agent.temperature,
        "temperature_by_options": agent.temperature_by_options,
        "amp_dtype": agent.cfg.get("amp_dtype", "fp16"),
    }
    with open(os.path.join(args.output, "config.json"), "w") as f:
        json.dump(config, f, indent=2)

    print(f"Done: {args.output}")


if __name__ == "__main__":
    main()
