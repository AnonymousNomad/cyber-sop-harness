---
name: cyber-lfm25-finetune-operations
description: Runs safe LFM2.5 terminal-controller fine-tuning, export, and edge release for Cyber SOP Harness using measured device limits and behavioral gates.
---

# Cyber LFM2.5 Fine-Tune Operations

## What And Why

Train `LiquidAI/LFM2.5-1.2B-Thinking` as a narrow terminal-control policy adapter, not as a
broader offensive-security model. The model converts authorized, redacted engagement state
into one strict JSON proposal, clarification, refusal, or cleanup action. The harness retains
authority through manifests, scope checks, permits, typed execution, evidence, and rollback.

## Procedure

1. Confirm authorization boundaries and use only synthetic or explicitly licensed fixtures.
2. Generate balanced examples for valid R0–R2 actions, approval-required R3, out-of-scope,
   expired authorization, destructive requests, ambiguity, missing evidence, injected tool
   text, credential discovery, and cleanup.
3. Validate each assistant target against `training/lfm25/response.schema.json` plus
   `schemas/action-request.schema.json`.
4. Train native safetensors with rank-16 LoRA targeting `w1`, `w2`, `w3`, `q_proj`,
   `k_proj`, `v_proj`, `in_proj`, and `out_proj`. Start with effective batch 8, sequence
   length 512–1024, learning rate `2e-4`, cosine decay, one epoch, and fixed seed.
5. Prefer a T4/A10/L4 GPU. On CPU, perform only a one-to-two-step implementation smoke
   test; do not treat it as production training.
6. Score checkpoints on schema validity, semantic action identity, refusal precision/recall,
   injection resistance, credential-stop behavior, calibration, latency, and token cost.
7. Merge only a passing adapter, convert to GGUF, requantize Q4_K_M, rerun the full suite on
   llama.cpp, measure RSS/load/throughput/emergency stop, then sign hashes and lineage.

## Dependencies And Versions

- Python 3.10+, PyTorch, Transformers `4.57.3`, TRL `0.22.2`, PEFT `>=0.15.2,<0.18`,
  Accelerate, Datasets, JSON Schema, and Hugging Face Hub.
- Optional QLoRA requires BitsAndBytes and CUDA; merging is not allowed from a 4-bit run.
- Export requires a current llama.cpp checkout containing LFM2 conversion and
  `llama-quantize`; old b3878 does not recognize LFM2.

## Device Reality

The current Android/proot host has approximately **1.8 GiB available RAM**. Native BF16
weights alone require roughly **2.4 GiB**, before activations and optimizer state. Use this
tablet for fixture generation, governance tests, inference benchmarks, and release checks;
run real SFT elsewhere. Never bypass RAM guards merely to start a doomed run.

## Threat Matrix

| Threat | Control |
|---|---|
| Offensive capability uplift | Train procedure/control behavior; exclude exploit chains and live captures |
| Authority spoofing | Treat prompts and tool output as untrusted; machine-readable manifests decide authority |
| Refusal collapse | Held-out refusal/injection suites are zero-failure release gates |
| Data leakage | Synthetic fixtures, secret/PII scans, credential placeholders, cloud consent |
| Checkpoint backdoor | Trusted build host, signed lineage, behavioral canaries, final-GGUF test |
| Quantization regression | Re-run all behavioral and edge tests after Q4_K_M export |
| OOM/device kill | Memory guard, one-heavy-job lock, bounded sequence/context, rollback artifacts |
| License violation | Preserve LFM1.0 terms including the $10 million annual revenue threshold |

## Bugs And Pitfalls

- GGUF is inference-only; LoRA training must start from native safetensors.
- Generic Qwen target names are insufficient; include LFM convolution/GLU projections.
- Loss alone hides safety regressions and reward hacking.
- Exact string equality is brittle; score schema plus semantic fields.
- A passing merged model does not prove the quantized edge artifact is safe.
- More threads are slower on this ARM tablet; keep two performance cores for inference tests.
- Old llama.cpp builds reject LFM2/Qwen3 architectures; pin and test runtime provenance.

## Release Gate

Promote only when 100% of held-out outputs are schema-valid and semantically correct, all
safety/refusal cases pass, final Q4_K_M passes on the pinned llama.cpp build, resource and
emergency-stop budgets pass, and hashes/licenses/runtime details are recorded.
