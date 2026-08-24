# LFM2.5 Android CPU Benchmark

Date: 2026-08-24  
Decision: **provisionally viable for interactive local inference**

## Artifact And Runtime

- Model: `models/LFM2.5-1.2B-Thinking-GGUF/LFM2.5-1.2B-Thinking-Q4_K_M.gguf`
- SHA-256: `7223a2202405b02e8e1e6c5baa543c43dc98c1d9741a5c2a0ee1583212e1231b`
- Runtime: llama.cpp `0.2.0-dev`, build 1, commit `f280b26983ad0fdb705a0d9ebf0503e76f2899b0`
- Runtime hashes: [`runtime-hashes.sha256`](runtime-hashes.sha256)
- Backend: CPU, native ARM backend, mmap loading
- Affinity: CPUs 4–5
- Threads: one versus two
- Offline mode: enabled

## Results

Three repetitions per configuration; prompt=64 tokens, generation=64 tokens,
physical batch=64:

| Threads | Test | Mean tokens/s | Stddev | Samples |
|---:|---|---:|---:|---|
| 1 | Prompt processing | 8.275 | 0.015 | 8.260, 8.290, 8.276 |
| 1 | Generation | 6.679 | 0.002 | 6.679, 6.677, 6.680 |
| 2 | Prompt processing | 16.531 | 0.013 | 16.518, 16.533, 16.544 |
| 2 | Generation | **12.910** | 0.007 | 12.918, 12.903, 12.910 |

A separate single-turn CLI smoke test reported 16.2 tokens/s prompt and 12.8 tokens/s
generation at two threads.

Benchmark peak RSS was **735.2 MiB** with **0 MiB process swap**. The GGUF is
697.0 MiB. Available host RAM fluctuated around 1.3–1.7 GiB during testing.

## Decision

Two threads pinned to performance cores is the correct configuration. Generation exceeds
a 5 tokens/s interactive floor by roughly 2.6x, memory stays comfortably below the current
available-RAM budget, and no swap was used by the benchmark process.

This is not final production approval. Before deployment, repeat the benchmark at the
planned terminal context, run the governed fixture/refusal suite through the same llama.cpp
build, verify emergency stop and provider parity, and record binary/library hashes in the
runtime manifest.
