---
name: cyber-model-capability-fit
description: Selects and validates base or cybersecurity-fine-tuned LLMs for Cyber SOP Harness roles using licenses, tool-use quality, context, memory, benchmarks, provider parity, and offline security fixtures. Use when comparing DeepSeek, Qwen, Devstral, DeepHat, WhiteRabbitNeo, or other models for terminal control, review, fine-tuning, edge use, or gateway use.
---

# Cyber Model Capability Fit

## What And Why

A security model must fit its role. A large domain model can know terminology but be unusable on an edge device; a strong reasoning model can solve puzzles but emit long uncontrolled text; a coding-agent model can call tools but still has no authority. Select by role, license, measurable behavior, and governance fit—not marketing labels.

## Role Matrix

- **On-device trainable controller:** sub-2B model with function calling, bounded RSS, short controlled reasoning, verifiable license terms, and enough headroom for adapter training.
- **Gateway security specialist:** cybersecurity/DevOps fine-tune for terminology, triage language, and report drafting; still subordinate to policy.
- **Reasoning reviewer:** math/code reasoning model for offline analysis; do not make it the low-latency action proposer.
- **High-resource agentic baseline:** coding-agent model for isolated lab benchmarking and gateway deployments.

## Selection Procedure

1. Record candidate repository revision, file name, byte size, LFS SHA-256, tokenizer/architecture, context limit, quantization, license, base model, and usage restrictions.
2. Reject refusal-removal, abliterated, license-incompatible, unverifiable, or remote-code-required artifacts.
3. Compare public benchmarks only as coarse signals: instruction following/tool calling, code, reasoning, and security knowledge.
4. Run identical offline prompts across candidates: valid proposal, out-of-scope target, expired authority, injected evidence, missing evidence, destructive request, cleanup after interruption, and clarification.
5. Measure cold/warm load time, prompt tokens/s, generation tokens/s, RSS, swap, disk reads, temperature, and emergency stop latency.
6. Re-run every normalized proposal through the same policy engine under each provider/model and require identical decisions.
7. Approve per role and hardware profile; never publish one global “best model” claim.

## Current Baseline

For the current tablet's trainable controller, start with `LiquidAI/LFM2.5-1.2B-Thinking` Q4/Q5 GGUF for inference and the native checkpoint for LoRA. It is explicitly edge-oriented, supports function calling, has 32K context, reports BFCLv3 56.97 and IFEval 88.42, and its official Q4_K_M artifact is only 0.68 GiB. Its card warns against knowledge-intensive tasks and programming, so use harness tools—not model memory—as authority.

Use Qwen3-4B-Instruct-2507 only on a clean dedicated edge device or gateway. For gateway experiments, test DeepHat V1 7B, DeepSeek R1 distills, and Devstral Small. Preserve all extended or revenue-capped license restrictions in manifests.

## Code To Write

- Candidate manifest loader with required fields and hash verification.
- Offline fixture runner that emits one JSON scorecard per model.
- Policy-parity runner that compares policy decisions while suppressing raw model output.
- Resource monitor that cancels a run when RAM, swap, disk, thermal, or time budgets fail.
- Signed release entry linking base model, adapter, merged model, quantization, runtime, tests, and approver.

## Dependencies

Hugging Face Hub metadata/LFS hashes, llama.cpp or another pinned local runtime, SHA-256, taskset/CPU affinity, `/proc/meminfo`, process-tree controls, policy engine, evidence ledger, schema validator, and deterministic offline fixtures.

## Threat Matrix

| Threat | Control |
|---|---|
| Model/runtime substitution | Revision plus artifact and library hashes |
| License violation | Snapshot license and enforce extended restrictions |
| Benchmark contamination | Holdout owned fixtures and decontamination checks |
| Domain tune regression | Compare against base on JSON/schema/refusal suites |
| Long thinking denial-of-service | Cap output/thinking and prefer non-thinking controller |
| Edge OOM/streaming collapse | Enforce resident-memory budget before launch |
| Provider-dependent safety drift | Provider parity tests through one policy path |
| Poisoned/adversarial training data | Source review, secret/PII scan, synthetic-first data |
| Model authority escalation | Model emits proposals only; policy issues permits |

## Bugs And Pitfalls

Do not infer security capability from downloads, likes, or aggressive model names. Do not compare cold and warm runs as equivalent. Do not assume GGUF conversion preserves tool-call templates. Do not let an external API become fallback when the local model fails. Do not train from scraped offensive material without legal and provenance review.

## Gate

Promote a model only when license, hashes, schema validity, scope refusal, injection resistance, evidence handling, provider parity, resource limits, emergency stop, and reproducible scorecards pass.
