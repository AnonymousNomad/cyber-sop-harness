#!/usr/bin/env python3
"""Evaluate a native LFM2.5 adapter against terminal-control fixtures."""

from __future__ import annotations

import argparse
import json
import math
import re
import time
from collections import Counter
import os
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-model", default="LiquidAI/LFM2.5-1.2B-Thinking")
    parser.add_argument("--adapter")
    parser.add_argument("--eval-file", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--max-new-tokens", type=int, default=256)
    return parser.parse_args()


def extract_json(text: str) -> tuple[object | None, str]:
    candidate = text.strip()
    fence = re.search(r"```(?:json)?\s*(.*?)```", candidate, flags=re.DOTALL)
    if fence:
        candidate = fence.group(1).strip()
    try:
        return json.loads(candidate), ""
    except json.JSONDecodeError as error:
        start = candidate.find("{")
        if start < 0:
            return None, f"no JSON object: {error.msg}"
        depth = 0
        in_string = False
        escaped = False
        for index in range(start, len(candidate)):
            char = candidate[index]
            if in_string:
                if escaped:
                    escaped = False
                elif char == "\\":
                    escaped = True
                elif char == '"':
                    in_string = False
                continue
            if char == '"':
                in_string = True
            elif char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    try:
                        return json.loads(candidate[start : index + 1]), ""
                    except json.JSONDecodeError as nested_error:
                        return None, f"malformed JSON object: {nested_error.msg}"
        return None, "unterminated JSON object"


def semantic_match(expected: dict, actual: dict) -> bool:
    if expected.get("decision") != actual.get("decision"):
        return False
    if expected.get("reason_code") != actual.get("reason_code"):
        return False
    expected_action = expected.get("action_request")
    actual_action = actual.get("action_request")
    if isinstance(expected_action, dict):
        if not isinstance(actual_action, dict):
            return False
        keys = ("capability_ref", "risk_class", "scope_ref", "authorization_ref", "target_ref")
        return all(expected_action.get(key) == actual_action.get(key) for key in keys)
    return expected_action == actual_action


def main() -> int:
    args = parse_args()
    try:
        available_ram_gib = (
            os.sysconf("SC_AVPHYS_PAGES") * os.sysconf("SC_PAGE_SIZE") / 2**30
        )
    except (AttributeError, ValueError):
        available_ram_gib = 0
    import torch

    if not torch.cuda.is_available() and available_ram_gib < 8:
        raise SystemExit(
            f"CPU has only {available_ram_gib:.1f} GiB available; "
            "native safetensors evaluation requires a larger host"
        )
    if torch.cuda.is_available():
        gpu_memory_gib = torch.cuda.get_device_properties(0).total_memory / 2**30
        if gpu_memory_gib < 6:
            raise SystemExit(f"GPU has {gpu_memory_gib:.1f} GiB; 6 GiB required")

    import jsonschema
    from peft import PeftModel
    from transformers import AutoModelForCausalLM, AutoTokenizer

    schema_path = Path(__file__).with_name("response.schema.json")
    action_schema_path = Path(__file__).parents[2] / "schemas/action-request.schema.json"
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    action_schema = json.loads(action_schema_path.read_text(encoding="utf-8"))
    schema["properties"]["action_request"]["oneOf"][1] = action_schema
    validator = jsonschema.Draft202012Validator(schema)
    rows = [json.loads(line) for line in args.eval_file.read_text(encoding="utf-8").splitlines()]

    tokenizer = AutoTokenizer.from_pretrained(args.base_model, use_fast=True)
    if tokenizer.pad_token_id is None:
        tokenizer.pad_token = tokenizer.eos_token
    model = AutoModelForCausalLM.from_pretrained(
        args.base_model,
        torch_dtype=torch.float16 if torch.cuda.is_available() else torch.float32,
        device_map="cuda" if torch.cuda.is_available() else "cpu",
        attn_implementation="sdpa",
    )
    if args.adapter:
        model = PeftModel.from_pretrained(model, args.adapter)
    model.eval()

    results = []
    confusion = Counter()
    latency_seconds = []
    for index, row in enumerate(rows):
        prompt_messages = row["messages"][:-1]
        expected = json.loads(row["messages"][-1]["content"])
        prompt = tokenizer.apply_chat_template(
            prompt_messages,
            tokenize=False,
            add_generation_prompt=True,
        )
        inputs = tokenizer(prompt, return_tensors="pt").to(model.device)
        started = time.monotonic()
        with torch.inference_mode():
            generated = model.generate(
                **inputs,
                max_new_tokens=args.max_new_tokens,
                do_sample=True,
                temperature=0.05,
                top_k=50,
                repetition_penalty=1.05,
                pad_token_id=tokenizer.pad_token_id,
            )
        latency_seconds.append(time.monotonic() - started)
        output_ids = generated[0, inputs["input_ids"].shape[1] :]
        output = tokenizer.decode(output_ids, skip_special_tokens=True)
        parsed, parse_error = extract_json(output)
        schema_errors = []
        if isinstance(parsed, dict):
            schema_errors = [
                f"{error.json_path}: {error.message}"
                for error in sorted(validator.iter_errors(parsed), key=lambda item: item.json_path)
            ]
        valid = isinstance(parsed, dict) and not schema_errors
        match = valid and semantic_match(expected, parsed)
        label = row.get("label", "unknown")
        predicted = parsed.get("reason_code") if isinstance(parsed, dict) else "INVALID"
        confusion[(label, predicted)] += 1
        result = {
            "example_id": row.get("example_id"),
            "label": label,
            "valid": valid,
            "semantic_match": match,
            "parse_error": parse_error,
            "schema_errors": schema_errors,
            "expected_reason_code": expected.get("reason_code"),
            "actual_reason_code": predicted if predicted != "INVALID" else None,
            "latency_seconds": latency_seconds[-1],
            "output": output,
        }
        results.append(result)
        print(f"[{index + 1}/{len(rows)}] {result['example_id']} valid={valid} match={match}")

    total = len(results)
    report = {
        "base_model": args.base_model,
        "adapter": args.adapter or "(merged/base)",
        "eval_file": str(args.eval_file),
        "total": total,
        "schema_valid": sum(item["valid"] for item in results),
        "semantic_match": sum(item["semantic_match"] for item in results),
        "schema_valid_rate": sum(item["valid"] for item in results) / total if total else 0,
        "semantic_match_rate": sum(item["semantic_match"] for item in results) / total if total else 0,
        "mean_latency_seconds": sum(latency_seconds) / len(latency_seconds) if latency_seconds else math.nan,
        "confusion": [
            {"label": label, "predicted": predicted, "count": count}
            for (label, predicted), count in sorted(confusion.items())
        ],
        "results": results,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(
        f"schema_valid={report['schema_valid']}/{total} "
        f"semantic_match={report['semantic_match']}/{total} report={args.report}"
    )
    return 0 if report["schema_valid"] == total and report["semantic_match"] == total else 1


if __name__ == "__main__":
    raise SystemExit(main())
