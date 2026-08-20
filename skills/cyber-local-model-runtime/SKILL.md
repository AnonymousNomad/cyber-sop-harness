---
name: cyber-local-model-runtime
description: Governs safe local model discovery, verification, llama.cpp startup, health, identity, warmup, resource limits, chat transport, and process shutdown for the cybersecurity harness.
---

# Cyber Local Model Runtime

## Directive

Implement a `LocalModelRuntime` service that:

- accepts only a signed/pinned `ModelRuntimeManifest` containing model path, model SHA-256, model revision, runtime binary path, runtime SHA-256, runtime version, expected architecture, context limit, RAM/VRAM budget, and license references;
- verifies every file before execution and rejects missing, changed, oversized, or unapproved artifacts;
- starts a pinned llama.cpp server with `--host 127.0.0.1`, an owned port, offline mode, web UI disabled, agent disabled, MCP disabled, prompt logging disabled, and an explicit context/token budget; tools remain disabled by omission of the `--tools` flag;
- uses `ProcessStartInfo.ArgumentList`, never a shell string, and records the child PID/tree for shutdown;
- waits for `/health` and verifies `/v1/models` identity before exposing the provider as ready;
- performs a deterministic synthetic warmup and fails closed when latency, memory, health, identity, or output checks fail;
- exposes typed chat/proposal calls only, never direct tool execution;
- stops the full process tree, releases the port, and clears readiness on shutdown, crash, relay loss, or manifest mismatch;
- records runtime/model/provider metadata into the Phase 3 provenance/evidence path.

Do not use Transformers `trust_remote_code=True` in the shipped runtime. Do not pass llama.cpp `--tools`, `--agent`, MCP, public binds, arbitrary model URLs, or prompt logging flags.

## Rationale and Architectural Reason

The model is an untrusted proposal source, not an authority. A separate local server gives the gateway a typed, inspectable transport boundary and lets the existing policy engine remain independent of model choice. Loopback binding prevents accidental LAN exposure; offline mode prevents hidden egress; disabled tools keep the model from becoming a shell or filesystem authority. Hash and identity checks stop model/runtime substitution. Health plus warmup prevents the first real request from being the readiness test. Tree shutdown is required because a model process can create child processes or survive a root-process stop.

GGUF through pinned llama.cpp is preferred over executing repository Python because the official WhiteRabbitNeo card requires custom remote code for Transformers. The runtime must still treat the GGUF and binary as untrusted supply-chain artifacts until hashes, metadata, and license records pass.

## Threat Matrix

| Threat/trap | Likely complication/error | Required prevention/detection | Test |
|---|---|---|---|
| Model replacement | Same filename, different weights | SHA-256, source revision, architecture, and model identity check | Tampered GGUF rejected |
| Remote code execution | Transformers `trust_remote_code=True` or custom loader | GGUF-only runtime; no remote Python execution | Custom-code model path rejected |
| LAN exposure | Server binds `0.0.0.0` or CORS is permissive | Force loopback and verify bound endpoint | Port-scan/bind assertion |
| Hidden egress | Model URL, telemetry, MCP, tools, or API fallback | Offline flag, no URLs, no tools/agent/MCP, network-denied fixture | Network-disabled warmup |
| First-request failure | Health returns before weights/warmup complete | `/health`, `/v1/models`, deterministic warmup gate | Cold-start test |
| VRAM/RAM exhaustion | Q4/Q8 model swaps or context too large | Manifest budgets, measured telemetry, context cap, fail closed | Resource ceiling test |
| Child-process leak | Root process exits but descendants remain | Process-tree tracking and termination | Shutdown descendant test |
| Port collision | Stale server or another local service owns port | Owned-port allocation, identity check, cleanup | Collision/recovery test |
| Prompt leakage | Debug logging or crash output contains secrets | Disable prompt logging; redact diagnostics | Secret-canary log test |
| API fallback surprise | Local failure silently sends data to cloud | Explicit provider selection and consent; no automatic fallback | Local failure egress test |
