# Local Model and Provider Research

Date: 2026-08-18
Status: research complete; implementation not started

## Decision

Adding an optional local model is viable as a Phase 3 workstream named **Phase 3B: Local Model Runtime and Provider Selection**. It must not be implemented as an automatic download, an unrestricted model agent, or a live-tool authority.

The first supported flow should be:

1. Detect an already-installed verified local model/runtime.
2. Offer an explicit setup choice: bundled/approved local model, user local model, or external API.
3. Keep API use disabled unless the user explicitly opts in.
4. Route every model output through the existing typed action envelope, policy engine, permit, tool broker, and evidence chain.
5. Keep the model unable to authorize, expand scope, invoke tools directly, or mark findings verified.

## Verified WhiteRabbitNeo Facts

The official Hugging Face model is `WhiteRabbitNeo/WhiteRabbitNeo-13B-v1` with repository revision `5ecae8d533223436512e31edc3f56bced00265f1`, `LlamaForCausalLM`, `license: llama2`, and custom Transformers code. Its repository contains six PyTorch weight shards and reports approximately 52,066,287,694 bytes of storage.

The official model card instructs Transformers users to set `trust_remote_code=True` and describes WhiteRabbitNeo as a cybersecurity model. That is not acceptable as the default application runtime because it executes model-repository Python code. The product should use a pinned, inspected GGUF artifact through a pinned llama.cpp runtime instead.

The official model card includes additional WhiteRabbitNeo restrictions, including lawful use, no military use, no unauthorized personal data, and no harmful/discriminatory use. The model card also states that users are responsible for outcomes. These terms must ship with any permitted distribution; a `license: llama2` tag alone is not sufficient redistribution approval.

## Quantized Artifact Facts

The third-party `QuantFactory/WhiteRabbitNeo-13B-v1-GGUF` repository reports `license: llama2`, base model `WhiteRabbitNeo/WhiteRabbitNeo-13B-v1`, GGUF architecture `llama`, and context length 16,384. Its API lists these measured files:

| Quantization | Bytes | Approximate size |
|---|---:|---:|
| Q2_K | 4,854,364,864 | 4.52 GiB |
| Q3_K_S | 5,659,083,584 | 5.27 GiB |
| Q3_K_M | 6,337,872,704 | 5.90 GiB |
| Q4_0 | 7,365,948,864 | 6.86 GiB |
| Q4_K_M | 7,866,070,464 | 7.33 GiB |
| Q5_K_M | 9,230,048,704 | 8.59 GiB |
| Q8_0 | 13,831,494,464 | 12.88 GiB |

These figures make Q3_K_S the first memory-probe candidate on this machine. Q4_K_M may be a quality candidate but leaves little RAM after the operating system, runtime, context, and application are counted. No performance or successful load claim is made until measured locally.

The quantization repository is not the original model publisher. Before bundling, record the exact quantizer repository revision, file SHA-256, model-card/license files, conversion provenance, and redistribution permission. A user-selected download is safer than shipping a third-party derivative until that review passes.

## Runtime Facts

The llama.cpp project is MIT licensed and supports GGUF, CPU/GPU hybrid inference, Windows builds, local model paths, an OpenAI-compatible server, `/health`, `--offline`, and binding to `127.0.0.1`. Its server also exposes optional tools, agent mode, MCP, file access, and shell execution; these must remain disabled.

Required launch posture:

```text
llama-server.exe -m <verified-model.gguf> --host 127.0.0.1 --port <owned-port> --offline --no-webui --no-agent
```

The exact supported flags must be verified against the pinned llama.cpp binary. The server must be started without the `--tools` flag, with `--no-repack` on this constrained host, no MCP configuration, no shell tool, no public bind address, no arbitrary model URL, and no prompt logging.

## Provider Choices

| Choice | Default | Required controls |
|---|---|---|
| Verified bundled/local model | Offered, not silently selected | license acceptance, file hash, runtime identity, health, model identity, warmup, resource gate |
| User local model or endpoint | Offered | explicit path/endpoint, health check, no secret logging, policy parity, local-only default |
| External API | Opt-in only | explicit egress consent, encrypted key storage, provider metadata, retention warning, no key in prompts/logs, policy parity |

The setup prompt should run once and be changeable later. It must never silently download weights or silently switch from local inference to an external API.

## Required Acceptance Battery

- License and redistribution manifest is present and hash-verified.
- Runtime binary revision and SHA-256 are pinned.
- Model file hash, size, architecture, context length, and source revision are verified before load.
- Startup binds only to loopback, disables tools/agent/MCP/web UI, and passes `/health`.
- Server identity matches the requested model and runtime manifest.
- Warmup and deterministic fixture completion succeed within measured RAM/VRAM/latency budgets.
- Clean stop terminates the model process tree and releases the port.
- User local endpoint and external API choices preserve the same policy decision for the same action.
- API keys never appear in logs, prompts, evidence, crash output, or provenance artifacts.
- Offline mode remains functional with network access disabled.
- Model output cannot bypass the typed action/policy/permit/evidence path.

## Sources

- WhiteRabbitNeo official model card: https://huggingface.co/WhiteRabbitNeo/WhiteRabbitNeo-13B-v1
- WhiteRabbitNeo model API and revision: https://huggingface.co/api/models/WhiteRabbitNeo/WhiteRabbitNeo-13B-v1
- WhiteRabbitNeo GGUF model card: https://huggingface.co/MaziyarPanahi/WhiteRabbitNeo-13B-v1-GGUF
- WhiteRabbitNeo GGUF file inventory: https://huggingface.co/api/models/QuantFactory/WhiteRabbitNeo-13B-v1-GGUF/tree/main?recursive=true
- llama.cpp server documentation: https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md
- llama.cpp build documentation: https://github.com/ggml-org/llama.cpp/blob/master/docs/build.md
- llama.cpp model/GGUF documentation: https://github.com/ggml-org/llama.cpp/blob/master/docs/models.md
- llama.cpp license: https://github.com/ggml-org/llama.cpp/blob/master/LICENSE
