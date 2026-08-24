---
name: cyber-edge-model-capacity
description: Selects, pins, verifies, loads, and benchmarks local cybersecurity GGUF models on RAM-constrained ARM edge devices. Use when choosing model size/quantization, building llama.cpp manifests, preventing OOM, or deciding whether inference belongs locally or on a gateway/cloud provider.
---

# Cyber Edge Model Capacity

## What And Why

A security model is useful only if it runs within device limits without starving safety controls or leaking data to an unintended provider. Measure resident-memory behavior, storage bandwidth, context budget, startup latency, prompt speed, generation speed, and thermal stability before claiming edge readiness.

## Selection Workflow

1. Record total/free/available RAM, swap, storage reserve, CPU affinity, thermal state, and competing Android applications.
2. Prefer models whose weights plus KV cache and compute buffers fit in available RAM with at least 20% host headroom.
3. Choose the newest license-compatible base model first; then choose quality versus speed using measured tokens per second.
4. Pin repository revision, filename, byte size, LFS SHA-256, quantization method, tokenizer identity, context limit, and license evidence.
5. Build llama.cpp for the exact device with native ARM features and no network fetch support unless explicitly required.
6. Run identical offline prompts across candidate quants before selecting a daily driver.
7. Reject any configuration that causes swap thrash, OOM kills, unbounded RSS, lost CPU affinity, or starvation of governance processes.

## Runtime Manifest

Require model path, SHA-256, source revision, architecture, quantization, file size, approved context, RAM budget, license references, runtime path/hash/version, shared-library hashes, offline policy, loopback bind, tools-disabled state, and automatic-fallback prohibition.

Verify every hash immediately before launch. Treat a filename match alone as untrusted.

## Measured Edge Rule

If available RAM is smaller than model weight plus working set, mmap may still load the model but each token can stream weights from storage. This appears as normal load completion followed by extremely slow prompt and generation throughput. Do not solve this by lowering the RAM guard; move to a smaller model, close host applications in a dedicated profile, use a desktop gateway, or obtain explicit cloud consent.

## Measured Android Memory Cleanup

On 2026-08-23 the proot workspace used only about 2 MiB and its largest process was under
50 MiB RSS. Host reporting showed approximately **7.2 GiB physical RAM**, **5.5 GiB used**,
and **1.7–2.0 GiB available**. Therefore, clearing project files cannot create enough room
for native BF16 training.

Least-destructive reclaim behavior:

- `sync` and removal of project caches are safe but provide no material gain here.
- Writing `/proc/sys/vm/drop_caches` is denied from proot.
- `am compact system full` succeeds and completed, but changed available RAM by only about
  **19 MiB**.
- `am kill-all` fails with `SecurityException` because the host app lacks
  `android.permission.KILL_ALL_BACKGROUND_PROCESSES`.
- App-level `ActivityManager.killBackgroundProcesses()` also fails with
  `Permission Denial`; it identified Outlook, TextNow, and ADM as candidate third-party
  background consumers but could not trim them.
- Shizuku access timed out, preventing privileged `kill-all`, package inspection, or safe
  per-app force-stop.

Do not force-stop user apps without explicit approval. To unlock privileged cleanup, open and
connect Shizuku, disable battery optimization for both AnyClaw and Shizuku if Android blocks
the connection, then rerun a read-only memory report before selecting packages. Even then,
reserve at least 20% headroom before loading an edge model.

For professional terminal use, require sustained interactive performance and repeatable benchmarks after cold start and warm cache. A single successful response does not prove edge readiness.

## Dependencies

aarch64 toolchain, CMake, pinned llama.cpp, GGUF artifact, SHA-256/LFS metadata, `taskset`, `/proc/meminfo`, process-tree controls, secure clock/time source, and license/model-card snapshots.

## Threat Matrix

| Threat | Control |
|---|---|
| Model substitution | Revision plus LFS SHA-256 verification |
| Runtime substitution | Binary/library hashes and build provenance |
| OOM kill during analysis | Memory preflight, context cap, resource telemetry, fail-closed launch |
| Silent swap thrash | Monitor swap-in/out and cancel below performance floor |
| Hidden cloud fallback | Explicit provider selection and no automatic egress |
| License violation | Preserve extended restrictions with model card |
| Model tool authority | Disable tools/MCP/agent and expose chat proposals only |
| Prompt/target leakage | Loopback binding, disabled logging, secret canary tests |

## Bugs And Pitfalls

Do not compare cold and warm runs as equivalent. Do not trust model-card context length for RAM planning. Do not use Q4 labels as a quality guarantee. Do not run training and inference concurrently on a small device. Do not leave a loopback server running after engagement closure.

## Gate

Approve a local model only when hashes/licenses pass, clean-device benchmarks meet the operator's latency floor, RSS/swap remain bounded, emergency stop works, and provider parity tests show policy decisions unchanged.
