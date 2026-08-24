# Model Strategy For Cyber SOP Harness

Status: research recommendation  
Date: 2026-08-23  
Research snapshots: [`research/model-candidates/`](../research/model-candidates/)

## Decision Summary

Use separate models for different trust and latency roles rather than seeking one universal security model.

| Role | Primary choice | Why |
|---|---|---|
| On-device trainable terminal controller | `LiquidAI/LFM2.5-1.2B-Thinking`, Q4_K_M | 1.17B hybrid edge model; 32K context; function calling; BFCLv3 56.97 and IFEval 88.42; official GGUF is only 0.68 GiB, making it the first practical on-device LoRA candidate |
| Dedicated-edge or gateway controller | `Qwen/Qwen3-4B-Instruct-2507`, Q4_K_M | Apache-2.0; 256K native context; stronger general/tool capacity, but its ~2.33 GiB Q4 artifact is still unsuitable for concurrent use on this Android host |
| Gateway security specialist | `DeepHat/DeepHat-V1-7B` Q4_K_M | Cybersecurity/DevOps fine-tune of Qwen2.5-Coder-7B; Apache-2.0 plus extended restrictions; useful domain vocabulary, but no independent security-agent benchmark is claimed in its card |
| Gateway reasoning reviewer | `deepseek-ai/DeepSeek-R1-Distill-Qwen-7B` | MIT license; strong public math/code/reasoning results; long thinking output makes it unsuitable as the interactive action proposer |
| High-end gateway coding agent | `mistralai/Devstral-Small-2507` | Apache-2.0; agentic coding focus; published SWE-Bench Verified result of 53.6%; too large for current tablet but strong where RAM/GPU exists |
| Existing security reference | WhiteRabbitNeo 2.5 | Already pinned locally; same general size/class as DeepHat but older and measured unusable on this tablet's current Android memory profile |

Do not use abliterated or refusal-removal variants. The terminal must retain policy-level refusal even if a model is less restrictive.

## Candidate Evidence

- **Qwen3-4B-Instruct-2507** publishes major gains over Qwen3-4B in instruction following, tool usage, knowledge, coding, and agent benchmarks. Its card reports BFCL-v3 **61.9** versus **57.6** for Qwen3-4B, plus improved LiveCodeBench and IFEval. It supports only non-thinking mode, which avoids long hidden reasoning during terminal operations.
- **DeepHat V1 7B** identifies itself as an offensive/defensive cybersecurity and DevOps model, inherits Qwen2.5-Coder-7B architecture, claims 131,072-token capability with YaRN, and uses Apache-2.0 plus extended usage restrictions. Its card provides no reproducible security-agent benchmark.
- **DeepSeek-R1-Distill-Qwen-7B** reports stronger reasoning/code benchmarks than small generic models, but recommends sampling settings and long generation lengths. Use it offline as an analysis reviewer or benchmark model, not as a low-latency typed-action controller.
- **Devstral Small 2507** explicitly targets agentic coding and tool calls, supports Mistral function calling, and has a 128K context. It requires gateway-class memory but is a strong open base for future authorized-lab agent benchmarks.
- **LFM2.5-1.2B-Thinking** is explicitly designed for on-device deployment, supports function calling, has day-one llama.cpp support, and reports 856 MB memory at Q4_0 plus 70 tok/s decode on a Snapdragon phone CPU. Its card warns against knowledge-intensive tasks and programming; that is acceptable for narrow SOP/control behavior where tools and policy provide authority.

## Edge Selection

For this tablet's on-device fine-tune target, pin `LiquidAI/LFM2.5-1.2B-Thinking-GGUF`, revision `74ddb49ac3dbd31c744afaa7061530c6466de6a0`, file `LFM2.5-1.2B-Thinking-Q4_K_M.gguf`, SHA-256 `7223a2202405b02e8e1e6c5baa543c43dc98c1d9741a5c2a0ee1583212e1231b`. Its LFM license permits commercial use only below an annual revenue threshold of **$10 million USD**; carry that restriction in every derivative manifest.

For a dedicated-edge or gateway controller, pin `unsloth/Qwen3-4B-Instruct-2507-GGUF`, revision `a06e946bb6b655725eafa393f4a9745d460374c9`, file `Qwen3-4B-Instruct-2507-Q4_K_M.gguf`, expected LFS SHA-256 `3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597`.

Run only after the device has at least **3 GiB available RAM**, cap context to **8K** for initial tests and **16K** after stability, pin two threads to performance cores, and keep governance/evidence processes outside the inference worker. If throughput remains below one token per second, the correct fix is a dedicated clean-device profile, smaller model, desktop gateway, or consented external provider—not weakening the resource guard.

Measured on 2026-08 with llama.cpp `f280b26`: LFM2.5 Q4_K_M reached **16.53 tokens/s prompt**
and **12.91 tokens/s generation** on CPUs 4–5, peaking at **735.2 MiB RSS** with zero benchmark-process
swap. It is provisionally viable even at lower host availability, but production still requires the
context, behavior, hash, and emergency-stop gates in [`../benchmarks/lfm25/README.md`](../benchmarks/lfm25/README.md).

## Fine-Tuning Plan

Do not train a broad “hacker” model. Train narrow compliance with the terminal control contract:

1. Build supervised examples from owned lab fixtures and synthetic engagement manifests.
2. Each example supplies redacted engagement digest, SOP state, allowed tool schema, observations, and asks for exactly one strict JSON response.
3. Label valid proposals, clarification requests, blocked actions, cleanup steps, and refusals.
4. Add adversarial examples for out-of-scope targets, wildcard expansion, redirect changes, tenant confusion, expired authorization, prompt injection in tool output, missing evidence, and destructive R4 requests.
5. Preserve failed paths and recovery behavior so the model learns honest uncertainty rather than confident guessing.

Use parameter-efficient fine-tuning before full training:

- Base: `LiquidAI/LFM2.5-1.2B-Thinking` for on-device work; Qwen3-4B-Instruct-2507 only when training moves to cloud/GPU capacity.
- Method: CPU LoRA prototype with rank 8–16, batch 1, sequence length 512–1024, gradient checkpointing, two pinned performance cores, and a few hundred fixture examples. For production datasets, use Unsloth/TRL LoRA or NF4 QLoRA on a free/cloud T4-class GPU.
- Schedule: learning rate `1e-4`–`2e-4`, cosine decay, one to three epochs; increase effective batch only after memory behavior is measured.
- Export: merge only after review, convert to GGUF, quantize Q4_K_M, verify hashes, then run the full fixture suite.

Keep target data off cloud services unless the source is synthetic, licensed, or covered by explicit written permission. Record dataset lineage, licenses, decontamination checks, secret scans, PII review, eval overlap, training seed, adapter hash, merged model hash, and quantized artifact hash.

Operational commands and release gates live in [`../training/lfm25/README.md`](../training/lfm25/README.md); the governing skill is `cyber-lfm25-finetune-operations`.

## Evaluation Gates

A model cannot become the terminal default until it passes all layers:

1. **Contract:** valid JSON, schema conformance, no invented fields, no direct execution claims.
2. **Authority:** out-of-scope, expired, ambiguous, injected, and R4 proposals are refused or marked approval-required.
3. **Evidence:** every claim references an evidence ID; missing evidence yields `UNKNOWN`, not a conclusion.
4. **Knowledge:** CyberMetric-style questions measure security knowledge without granting live-target authority.
5. **Agent:** Cybench/NYU-CTF-class tasks run only in isolated owned containers with explicit safety controls.
6. **Regression:** identical normalized proposals produce the same policy decision across provider/model swaps.
7. **Safety:** prompt-injection, jailbreak, credential-canary, secret-leakage, and destructive-action suites pass.
8. **Edge:** cold/warm load time, tokens per second, RSS, swap, disk reads, temperature, and emergency stop meet published thresholds.

## Current Recommendation

For immediate development, wire LFM2.5-1.2B-Thinking into the local runtime as the on-device proposal engine and fine-tune it narrowly for terminal control. Keep Qwen3-4B-Instruct-2507 as the dedicated-edge upgrade, DeepSeek R1 distills as offline reviewers, DeepHat V1 7B as a gateway security experiment, and Devstral as the high-resource agentic baseline. Current public DeepSeek cybersecurity fine-tunes have sparse cards and no reproducible evaluation; treat them as research references, not product defaults.
