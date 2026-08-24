#!/usr/bin/env python3
"""Train a governed terminal-control LoRA adapter for LFM2.5."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path


LORA_TARGETS = ["w1", "w2", "w3", "q_proj", "k_proj", "v_proj", "in_proj", "out_proj"]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-model", default="LiquidAI/LFM2.5-1.2B-Thinking")
    parser.add_argument("--train-file", type=Path, required=True)
    parser.add_argument("--eval-file", type=Path)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--merged-output-dir", type=Path)
    parser.add_argument("--epochs", type=float, default=1.0)
    parser.add_argument("--max-steps", type=int)
    parser.add_argument("--batch-size", type=int)
    parser.add_argument("--gradient-accumulation", type=int, default=8)
    parser.add_argument("--learning-rate", type=float, default=2e-4)
    parser.add_argument("--max-length", type=int, default=1024)
    parser.add_argument("--lora-r", type=int, default=16)
    parser.add_argument("--lora-alpha", type=int, default=16)
    parser.add_argument("--lora-dropout", type=float, default=0.05)
    parser.add_argument("--seed", type=int, default=20260823)
    parser.add_argument("--save-steps", type=int, default=25)
    parser.add_argument("--load-in-4bit", action="store_true")
    parser.add_argument("--merge", action="store_true")
    return parser.parse_args()


def available_ram_bytes() -> int:
    try:
        return os.sysconf("SC_AVPHYS_PAGES") * os.sysconf("SC_PAGE_SIZE")
    except (AttributeError, ValueError):
        return 0


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_rows(path: Path) -> None:
    import jsonschema

    schema_path = Path(__file__).with_name("response.schema.json")
    action_schema_path = Path(__file__).parents[2] / "schemas/action-request.schema.json"
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    action_schema = json.loads(action_schema_path.read_text(encoding="utf-8"))
    schema["properties"]["action_request"]["oneOf"][1] = action_schema
    validator = jsonschema.Draft202012Validator(schema)
    with path.open(encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, 1):
            row = json.loads(line)
            assistant_target = json.loads(row["messages"][-1]["content"])
            errors = sorted(validator.iter_errors(assistant_target))
            if errors:
                detail = "; ".join(error.message for error in errors)
                raise SystemExit(f"{path}:{line_number}: invalid assistant target: {detail}")


def main() -> None:
    args = parse_args()
    if args.base_model.lower().endswith(".gguf"):
        raise SystemExit("LoRA training requires native safetensors, not GGUF")
    if args.max_steps is None and args.epochs <= 0:
        raise SystemExit("Set --epochs greater than zero or --max-steps")
    if args.merge and not args.merged_output_dir:
        raise SystemExit("--merge requires --merged-output-dir")
    if args.merge and args.merged_output_dir.resolve() == args.output_dir.resolve():
        raise SystemExit("Adapter and merged output directories must differ")
    if args.merge and args.load_in_4bit:
        raise SystemExit("Merge requires a non-quantized training run")

    import torch
    from datasets import load_dataset
    from peft import LoraConfig, TaskType, get_peft_model, prepare_model_for_kbit_training
    from transformers import AutoModelForCausalLM, AutoTokenizer
    from trl import SFTConfig, SFTTrainer

    cuda_available = torch.cuda.is_available()
    device = "cuda" if cuda_available else "cpu"
    if cuda_available:
        gpu_memory_gib = torch.cuda.get_device_properties(0).total_memory / 2**30
        required_gib = 7.0 if args.load_in_4bit else 12.0
        if gpu_memory_gib < required_gib:
            raise SystemExit(
                f"GPU has {gpu_memory_gib:.1f} GiB; {required_gib:.1f} GiB required"
            )
    else:
        ram_gib = available_ram_bytes() / 2**30
        low_ram_override = os.environ.get("LFM25_ALLOW_LOW_RAM_CPU") == "1"
        if ram_gib < 8 and not low_ram_override:
            raise SystemExit(
                f"CPU has only {ram_gib:.1f} GiB available; use a GPU host or set "
                "LFM25_ALLOW_LOW_RAM_CPU=1 for a tiny implementation smoke test"
            )
        if ram_gib < 8 and (args.max_steps is None or args.max_steps > 2 or args.max_length > 128):
            raise SystemExit("Low-RAM CPU smoke test permits at most 2 steps at length 128")

    validate_rows(args.train_file)
    if args.eval_file:
        validate_rows(args.eval_file)

    train_dataset = load_dataset("json", data_files=str(args.train_file), split="train")
    keep_columns = ["messages"]
    train_dataset = train_dataset.remove_columns(
        [column for column in train_dataset.column_names if column not in keep_columns]
    )
    eval_dataset = None
    if args.eval_file:
        eval_dataset = load_dataset("json", data_files=str(args.eval_file), split="train")
        eval_dataset = eval_dataset.remove_columns(
            [column for column in eval_dataset.column_names if column not in keep_columns]
        )

    tokenizer = AutoTokenizer.from_pretrained(args.base_model, use_fast=True)
    if tokenizer.pad_token_id is None:
        tokenizer.pad_token = tokenizer.eos_token
    if cuda_available:
        major, _ = torch.cuda.get_device_capability(0)
        model_dtype = torch.bfloat16 if major >= 8 else torch.float16
    else:
        model_dtype = torch.float32

    quantization_config = None
    if cuda_available and args.load_in_4bit:
        from transformers import BitsAndBytesConfig

        quantization_config = BitsAndBytesConfig(
            load_in_4bit=True,
            bnb_4bit_quant_type="nf4",
            bnb_4bit_compute_dtype=model_dtype,
            bnb_4bit_use_double_quant=True,
        )

    model = AutoModelForCausalLM.from_pretrained(
        args.base_model,
        torch_dtype=model_dtype,
        device_map=device,
        quantization_config=quantization_config,
        low_cpu_mem_usage=True,
        attn_implementation="sdpa",
    )
    model.config.use_cache = False
    module_names = {name for name, _ in model.named_modules()}
    missing_targets = [name for name in LORA_TARGETS if name not in module_names]
    if missing_targets:
        raise SystemExit(f"LFM adapter targets missing from model: {missing_targets}")

    lora_config = LoraConfig(
        task_type=TaskType.CAUSAL_LM,
        inference_mode=False,
        r=args.lora_r,
        lora_alpha=args.lora_alpha,
        lora_dropout=args.lora_dropout,
        target_modules=LORA_TARGETS,
        bias="none",
        modules_to_save=None,
    )
    if quantization_config is not None:
        model = prepare_model_for_kbit_training(model)
    model.enable_input_require_grads()
    model = get_peft_model(model, lora_config)
    model.print_trainable_parameters()

    batch_size = args.batch_size or (1 if device == "cpu" else 2)
    save_steps = args.save_steps
    if args.max_steps is not None and args.max_steps < save_steps:
        save_steps = args.max_steps
    eval_strategy = "steps" if eval_dataset is not None else "no"
    training_config = SFTConfig(
        output_dir=str(args.output_dir),
        num_train_epochs=args.epochs,
        max_steps=args.max_steps or -1,
        per_device_train_batch_size=batch_size,
        gradient_accumulation_steps=args.gradient_accumulation,
        learning_rate=args.learning_rate,
        lr_scheduler_type="cosine",
        warmup_ratio=0.03,
        weight_decay=0.01,
        max_grad_norm=1.0,
        logging_steps=5,
        save_strategy="steps",
        save_steps=save_steps,
        save_total_limit=3,
        eval_strategy=eval_strategy,
        eval_steps=save_steps,
        load_best_model_at_end=eval_dataset is not None,
        metric_for_best_model="eval_loss",
        greater_is_better=False,
        report_to=[],
        seed=args.seed,
        bf16=cuda_available and model_dtype == torch.bfloat16,
        fp16=cuda_available and model_dtype == torch.float16,
        gradient_checkpointing=True,
        gradient_checkpointing_kwargs={"use_reentrant": False},
        dataloader_num_workers=0,
        remove_unused_columns=False,
        max_length=args.max_length,
        packing=False,
    )

    trainer = SFTTrainer(
        model=model,
        args=training_config,
        train_dataset=train_dataset,
        eval_dataset=eval_dataset,
        processing_class=tokenizer,
    )
    trainer.train()
    trainer.save_model(str(args.output_dir))
    tokenizer.save_pretrained(str(args.output_dir))

    dataset_hashes = {
        str(path): sha256_file(path)
        for path in (args.train_file, args.eval_file)
        if path is not None
    }
    manifest = {
        "base_model": args.base_model,
        "method": "LoRA",
        "targets": LORA_TARGETS,
        "lora_r": args.lora_r,
        "lora_alpha": args.lora_alpha,
        "learning_rate": args.learning_rate,
        "effective_batch": batch_size * args.gradient_accumulation,
        "max_length": args.max_length,
        "seed": args.seed,
        "device": device,
        "load_in_4bit": args.load_in_4bit,
        "dataset_sha256": dataset_hashes,
        "adapter_dir": str(args.output_dir),
    }
    (args.output_dir / "training-manifest.json").write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )

    if args.merge:
        merged_model = trainer.model.merge_and_unload()
        merged_model.save_pretrained(str(args.merged_output_dir), safe_serialization=True)
        tokenizer.save_pretrained(str(args.merged_output_dir))
        manifest["merged_dir"] = str(args.merged_output_dir)
        (args.output_dir / "training-manifest.json").write_text(
            json.dumps(manifest, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )


if __name__ == "__main__":
    main()
