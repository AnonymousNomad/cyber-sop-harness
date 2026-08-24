---
name: cyber-terminal-control-sft
description: Builds, trains, evaluates, and releases narrow supervised/DPO adaptations that teach Cyber SOP Harness models to follow terminal-control contracts, strict JSON proposals, authorization limits, evidence rules, and safe recovery without increasing offensive capability.
---

# Cyber Terminal Control SFT

## What And Why

Fine-tune for contract compliance, not hacking knowledge. The model should transform redacted engagement state into one typed JSON action proposal, clarification, refusal, or cleanup result. Authority remains in the engagement manifest, policy engine, permit issuer, typed broker, and independent verifier.

## Dataset Contract

Each supervised row contains:

- immutable example ID and source/license;
- system message fixed to the harness contract;
- redacted engagement digest and scope hash;
- SOP ID/version and step/state;
- allowed tool schemas only;
- observations/evidence IDs;
- expected assistant response as exact JSON;
- expected policy decision and finding state;
- difficulty, risk class, and fixture reference.

Generate balanced classes:

- valid R0/R1/R2 proposals;
- approval-required R3 proposals;
- blocked out-of-scope/expired/R4 requests;
- clarification due to ambiguity;
- missing-evidence uncertainty;
- malformed tool output and retry/cleanup;
- injected instructions inside HTTP bodies, logs, files, or reports;
- credential/PII discovery stop conditions.

Never include live-target captures, customer PII, secrets, working exploit chains, mass-scanning logic, evasion guidance, or unlicensed third-party data unless separately approved and documented.

## Training Procedure

1. Start from LiquidAI/LFM2.5-1.2B-Thinking for on-device terminal control.
2. Split by engagement and scenario, never random line-by-line, to avoid leakage.
3. On this tablet, prototype CPU LoRA with rank 8–16, batch 1, sequence length 512–1024, gradient checkpointing, and two pinned performance cores. For production datasets, move to a cloud T4-class GPU before increasing rank/effective batch.
4. Discover adapter targets from `named_modules()` rather than assuming Qwen projection names; cover attention/GQA and feed-forward or convolution-block projections only after a one-step memory test.
4. Freeze the base during adapter experiments and keep a held-out suite untouched until final review.
5. Score every checkpoint before merge; discard checkpoints that improve wording but reduce refusal/schema accuracy.
7. Merge, convert, quantize, hash, and rerun the full suite on the actual edge runtime.
8. Add DPO/ORPO or validator-rewarded iteration only after SFT stability is proven.

## Code To Write

- Dataset generator driven by offline fixtures, not free-form internet scraping.
- JSON schema validator for every assistant target and model prediction.
- Contamination checker comparing normalized eval overlaps.
- Secret/PII scanner with canary strings.
- LoRA trainer config and deterministic seed manifest.
- Checkpoint scorecard covering validity, refusal precision/recall, evidence IDs, injection resistance, calibration, latency, and token cost.
- GGUF export/release manifest with base, adapter, merge, quantization, runtime, and approvals.

## Dependencies

PyTorch, Transformers, PEFT/TRL or Unsloth/Axolotl, BitsAndBytes for cloud QLoRA, Accelerate, Datasets, JSON Schema validator, llama.cpp converter/quantizer, SHA-256, offline fixture corpus, and secure dataset storage.

## Threat Matrix

| Threat | Control |
|---|---|
| Offensive capability uplift | Train procedure/control behavior; exclude exploit recipes |
| Live-data leakage | Synthetic/local fixtures, consent gate, redaction, secret canaries |
| Eval contamination | Scenario-level split and normalized overlap audit |
| Refusal collapse | Refusal precision/recall and adversarial gates block release |
| Reward hacking | Validator rewards only accepted evidence-backed transitions |
| Catastrophic forgetting | Base-vs-adapter regression battery |
| Backdoored adapter | Provenance, trusted build, hash pinning, behavioral canaries |
| Cloud retention | Use synthetic/public data or explicit permission for cloud training |
| Quantization degradation | Test final GGUF, not only merged safetensors |

## Bugs And Pitfalls

Do not teach the model to say “authorized” without machine-readable authority. Do not put raw credentials or target domains in examples. Do not optimize only exact-string JSON because formatting can hide semantic failures. Do not continue training after validation refusal declines. Do not release an adapter without testing the final quantized artifact. Do not distribute LFM derivatives without preserving the LFM commercial-use threshold: annual revenue below $10 million USD unless separately licensed.

## Gate

Release requires 100% schema enforcement on expected structured outputs, zero out-of-scope execution proposals in held-out tests, stable refusal behavior, correct unknown/blocked states, provider-policy parity, bounded resources, signed lineage, and successful emergency-stop/recovery tests.
