# Edge AIDE Cybersecurity Workbench — Integration Architecture

## What This Is

A single-process Node.js server that runs on Android tablets (Termux), iPads (a-Shell/iSH), and low-resource Linux devices. It combines AIDE Sovereign Workbench's harness architecture with Cyber SOP Harness's governance model into a cybersecurity operations terminal.

**Not a port. Not a fork. A translation of the same architecture to edge constraints.**

## Source Repositories

| Repo | Role | Key assets ported |
|---|---|---|
| `aide-sovereign-workbench` | Architecture blueprint | Cipher state bus, Credo+Veritas governance, orchestrator loop, memory spine, provider manager, scaffold system |
| `cyber-sop-harness` | Security domain logic | Policy engine concepts, permit lifecycle, scope evaluation, evidence journal, tool adapter pattern, Parrot terminal aesthetic |

## Device Profile (Samsung/Ferrell tablet)

```
SoC: ARMv9/aarch64, no discrete GPU
RAM: 7.2 GiB total, ~3 GiB usable for inference
Storage: ~12 GiB free
CPU: 8 cores (4 efficiency + 3 performance + 1 prime)
Optimal training config: 2 threads pinned to CPUs 4-5 (Cortex-A720)
Android allowed mask: CPUs 0-6 (CPU7 prime excluded)
```

## Runtime Stack

```
┌─────────────────────────────────────────────┐
│           Browser (Chrome/Samsung Internet)  │
│  Terminal UI · Workflow Views · Dashboard    │
│  Monaco-lite editor · Evidence viewer        │
└──────────────────────┬──────────────────────┘
                       │ HTTP/WS on 127.0.0.1:PORT
┌──────────────────────▼──────────────────────┐
│         Node.js Daemon (single process)      │
│                                              │
│  ┌─────────┐  ┌──────────┐  ┌───────────┐   │
│  │ Terminal │  │ Governance│  │ Model     │   │
│  │ Router   │  │ Core     │  │ Layer     │   │
│  ├─────────┤  ├──────────┤  ├───────────┤   │
│  │ Command  │  │ Policy   │  │ llama.cpp │   │
│  │ parser   │  │ engine   │  │ HTTP API  │   │
│  │ SOP      │  │ Permits  │  │ Context   │   │
│  │ router   │  │ Scope    │  │ manager   │   │
│  │ Tool     │  │ Evidence │  │ Cipher    │   │
│  │ dispatch │  │ chain    │  │ state bus │   │
│  └─────────┘  └──────────┘  └───────────┘   │
│                                              │
│  ┌──────────────────────────────────────────┐│
│  │        Tool Adapters (typed)             ││
│  │  nmap · nuclei · ffuf · httpx · curl     ││
│  │  dns · whois · headers · screenshots     ││
│  └──────────────────────────────────────────┘│
└──────────────────────────────────────────────┘
```

## What Gets Ported From AIDE

| AIDE component | Edge translation | Why this way |
|---|---|---|
| Node daemon (`ws`, `zod`) | Same stack, lighter deps | Node runs natively in Termux; WebSocket is proven |
| Browser frontend (`app.js` + `index.html`) | Single HTML + vanilla JS, no build step | No bundler on device; instant load; touch-friendly |
| Monaco editor | Optional lazy-load from CDN or local file | Heavy (~2MB) but works in mobile Chrome; not required for terminal mode |
| Cipher state bus (`.aide/cipher-state.jsonl`) | Identical JSONL format, path changed to `.edge-cyber/state.jsonl` | Proven append-only event log; zero dependencies |
| Harness orchestrator (intake→guard→retrieve→plan→propose→verify→revise→test→review→learn) | Same pipeline, security-domain stages replace code-editing stages | The closed-loop architecture is the core IP |
| Credo+Veritas governance | Security credo replaces developer credo; same oath structure | "Protect the operator" > "Protect the developer" |
| Memory spine (Helix X1) | Simplified: pinned blocks + day digests, no LSP integration | Edge doesn't need language intelligence |
| Provider manager (6 providers) | Local llama.cpp only + optional remote (consent-gated) | Edge = offline-first by default |
| Scaffold v2.1 + learned injection | Same concept, security SOPs instead of coding patterns | Cipher learns which SOPs the operator prefers |
| Model Hub (HF search/download) | Simplified: local GGUF import + hash verification | No HF API dependency on device |

## What Gets Ported From Cyber SOP Harness

| Harness component | Edge translation | Why this way |
|---|---|---|
| Policy engine (.NET) | Pure JS module, same schema contracts | Single process, no cross-runtime calls |
| Permit issuer (one-use, expiring) | JS class with Map-based storage + TTL checks | Same semantics, no persistence needed for active permits |
| Scope evaluator (CIDR, domain, URL matching) | JS module using `net.isIP()` and `URL` parsing | Native Node modules cover all scope checks |
| Durable evidence store (hash-chained JSONL) | Same SHA-256 chain, stored under `.edge-cyber/evidence/` | Append-only, tamper-detecting, replayable |
| Tool adapters (`IToolAdapter`) | Typed JS objects with `{name, capability, execute(params, permit)}` | Same contract, no interface ceremony needed |
| Engagement manifest (JSON schema) | Same schema, loaded at startup | Authorization is non-negotiable |
| Secret custody (DPAPI/AES-GCM) | AES-256-GCM via Node crypto module, passphrase-derived key | DPAPI unavailable outside Windows; passphrase is universal |

## What Does NOT Get Ported

| Excluded | Reason |
|---|---|
| .NET runtime | Cannot run natively on Android/Termux without Mono/AOT complexity |
| LSP/DAP managers | Edge terminal is not a code IDE; Monaco optional |
| Desktop control (bounded domain) | Tablet OS sandbox prevents window management |
| Training room | Fine-tuning happens on desktop, artifacts deployed to edge |
| Git integration | Termux has git but the cyber workbench doesn't need commit workflows |

## Model Strategy

| Role | Model | Quant | Size | Notes |
|---|---|---|---|---|
| Primary terminal controller | LFM2.5-1.2B-Thinking | Q4_K_M | 0.68 GiB | Function calling, 12.91 tok/s gen, 32K ctx |
| Fallback micro controller | TinyLiquid hybrid25m (custom) | fp16 | ~56 MiB | 292 tok/s pretrain, narrow SOP output |
| Future cipher quantized | User's cipher model | Q4_K_M target | TBD | Quantize when base weights available |
| Gateway reviewer (optional) | DeepSeek-R1-Distill-Qwen-7B | Q4_K_M | ~4 GiB | Only if RAM allows; analysis-only role |

## Trust Boundaries

```
Browser ←→ [loopback only] ←→ Node daemon ←→ [child_process] ←→ Tool binaries
                                    │
                                    ├──→ llama.cpp server (loopback only)
                                    └──→ Filesystem (workspace jail)
```

No network egress without explicit consent + evidence record.
Tool execution requires: valid engagement manifest → policy check → one-use permit → typed adapter → evidence write.
Model proposals are always untrusted input until parsed and policy-checked.
