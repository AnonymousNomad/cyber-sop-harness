# Phase 3B Acceptance Matrix

Status: real local load passed; resource/redistribution gates remain open
Date: 2026-08-18

| Test | Required behavior | Current result |
|---|---|---|
| P3B-001 | Local/API provider selections validate and local selection cannot silently enable egress. | PASS |
| P3B-002 | Model/runtime/license manifest validates absolute paths, sizes, SHA-256 values, architecture, context, and resource limits. | PASS |
| P3B-003 | Model mutation is rejected before runtime launch. | PASS |
| P3B-004 | Pinned llama.cpp starts only on loopback with offline/no-tools/no-agent/no-web UI posture. | PASS |
| P3B-005 | `/health` and `/v1/models` identity pass before readiness. | PASS: alias `wrn-v3-7b-q4-k-m` |
| P3B-006 | Deterministic warmup succeeds within measured RAM/VRAM/latency budgets. | PASS: Q4 peak working set `4.42 GiB`, Q5 peak `5.06 GiB` (CPU-only, `-ngl 0`); VRAM `435 MiB/6.00 GiB` (GPU desktop baseline, model uses zero VRAM); probe latency Q4 `59.8s` / Q5 `67.6s` including warmup |
| P3B-007 | Runtime stops its process tree and releases its port. | PASS: project runtime released port 18080 |
| P3B-008 | Q4_K_M and Q5_K_M candidates are measured on the GTX 1060/16 GB host. | PASS: Q5_K_M staged (5,444,832,448 bytes, SHA-256 `6d7c235f2e79bc65c9cb4478b4460a8efdd1da13dd5185b49788693e1824bd58`, GGUF magic verified, resumable HTTP streaming after Xet proved unreliable); paired telemetry runs above; Q5 fences JSON output and is normalized by `ProposalTextNormalizer` before the strict parser |
| P3B-009 | License, quantizer provenance, runtime hash, model hash, and notices are release-verified. | PARTIAL: hashes/notices verified; redistribution approval pending |
| P3B-010 | Model output remains a strict typed proposal and cannot bypass policy, permits, tools, evidence, or provenance. | PASS: real proposal traversed policy, one-use permit, synthetic broker, evidence, and signed provenance |
| P3B-011 | Provider selection persists only non-secret metadata and loads atomically without file-lock leakage. | PASS |
| P3B-012 | Evidence journal is append-only, hash-chained, crash-safe (fsync per record), and recovers as VERIFIED/PARTIAL/CORRUPT with artifact hash verification. | PASS |
| P3B-013 | Secrets persist only through the platform protector (DPAPI on Windows; encrypted test protector elsewhere), never as plaintext, and reject tampering/traversal/wrong-entropy. | PASS |
| P3B-014 | Signing keys are protected at rest, bound to identity by fingerprint, separated between runtime evidence and release roles, rotatable with retired-key validity windows, and verifiable offline by public key. | PASS |
| P3B-015 | Startup wizard surfaces provider disclosure (provider/model/version, source, license, data path, retention, resource, OFFLINE/LOCAL/EXTERNAL egress), requires explicit egress acknowledgement, and blocks external selection without a stored secret. | PASS |
| P3B-016 | Evidence and workflow audit persist to the durable journal as the broker/state machine run, and recover as VERIFIED with artifact hashes intact. | PASS |
| P3B-017 | External API provider is consent-gated: no consent, no stored secret, or stale consent yields a controlled policy-blocked failure without any network request. | PASS |
| P3B-018 | The persisted provider selection drives the real start path: selection is loaded, the staged runtime manifest is resolved and validated (hashes, limits, absolute paths), the managed runtime starts, and readiness is reached only after health/identity/warmup gates. | PASS: `selection_237f25...` -> `WhiteRabbitNeo-V3-7B-GGUF` -> READY via CLI `run`; bootstrapper also fails closed on no-selection, missing manifest, tampered model, missing consent/secret, and non-loopback user-local endpoints |
| P3B-019 | The user-local loopback endpoint path and the bootstrapper start path tolerate a busy host with a bounded readiness budget and controlled failure (no raw stack dumps, no leaked processes). | PASS: 420s readiness budget; CLI returns controlled ERROR; project runtime leaves no llama-server processes after stop |
| P3B-020 | A consent-gated external provider proposal traverses the identical full chain as the local model: proposal -> policy -> one-use permit -> broker -> evidence -> signed provenance, with the secret only in the Authorization header. | PASS: loopback fixture with secret; `proposal=None`, policy Allow, permit consumed, evidence Success, provenance verified |
| P3B-021 | Code-fenced provider output (markdown fences) is normalized only at the presentation layer: an outer fence is stripped only when it wraps the entire object with no trailing prose, and the strict parser still rejects anything else. | PASS: fenced JSON parses after normalization; bare JSON unchanged; unclosed fences and trailing prose rejected |
| P3B-022 | The external endpoint is a first-class configured value: validated (https, or http only on loopback; no embedded credentials; no query/fragment), persisted outside the secret store, surfaced in `status`, and required (with the stored secret) before the external choice is offered or selectable. | PASS: endpoint set/show/clear CLI; non-loopback http, embedded credentials, query/fragment all rejected; external choice hidden without endpoint+secret; bootstrapper uses the selection endpoint |
| P3B-023 | The CLI `run` executes the real model probe through the full governed chain and persists durable evidence: policy -> one-use permit -> frozen fixture registry -> broker dispatch -> evidence + raw/redacted artifacts -> signed provenance -> workflow lifecycle (Planned..Reportable) with independent verification and report gate -> verified journal recovery. | PASS: real Q4 run returned POLICY Allow, PERMIT issued=True, DISPATCH executed, PROVENANCE verified=True, WORKFLOW Reportable, JOURNAL Verified events=1 audit=13; data/evidence.journal, artifacts/, keys/ persisted; zero leaked processes |

## Current Evidence

- Phase 2 regression: `phase2_tests=passed count=10`.
- Phase 3B contract suite: `phase3_tests=passed count=19`.
- Phase 3B real runtime/provider suite: `phase3_tests=passed count=20` with the opt-in real model and policy-path tests.
- Bootstrapper suite: `phase3_tests=passed count=21` with the opt-in real model running through the persisted-selection start path.
- Durable evidence, secret custody, key custody, wizard, journal-mirror, and external-provider tests: 6 additional PASS rows (P3B-012..P3B-017).
- Bootstrapper/loopback/CLI run path: additional PASS rows P3B-018/P3B-019; CLI `run` smoke with the real staged model returned `READY` for `selection_237f25...`, a valid typed probe action (`None`, 313 tokens), and a clean stop with no leaked llama-server processes.
- External full-path test: `phase3_tests=passed count=21`; row P3B-020 PASS (external proposal -> policy -> permit -> broker -> evidence -> provenance with header-only secret).
- Telemetry: `run --telemetry` samples working set (process) and VRAM/GPU (nvidia-smi, graceful fallback). Q4: ready `4.37 GiB`, probe peak `4.42 GiB`, `59.8s`/361 tokens. Q5: ready `5.02 GiB`, probe peak `5.06 GiB`, `67.6s`/361 tokens. VRAM `435 MiB/6.00 GiB` both (CPU-only; desktop baseline only).
- Q5_K_M staged with notice + runtime manifest; Q4 selection restored as the active candidate after the paired run.
- Endpoint config: `phase3_tests=passed count=22` (TestExternalEndpointStore added, row P3B-022 PASS); CLI smoke verified set/show/clear, non-loopback http rejected, external choice unavailable without endpoint+secret.
- Governed CLI run: `phase3_tests=passed count=23` (TestSyntheticFixtureToolAdapter added, row P3B-023 PASS). Live real-model run produced POLICY Allow / PERMIT / DISPATCH executed / PROVENANCE verified=True / WORKFLOW Reportable / JOURNAL Verified events=1 audit=13, with artifacts and the DPAPI-protected runtime evidence key persisted under `data/`.
- Real proposal-to-policy path: model proposal was allowed only as a synthetic fixture action and produced verified evidence/provenance.
- Build gate: `0 Warning(s)`, `0 Error(s)`.
- Initial runtime probe found no llama.cpp executable or GGUF; both are now staged and hash-verified on E:.
- Real load: `wrn-v3-7b-q4-k-m` returned `READY` in `9.148` seconds using CPU-only llama.cpp `b10488`.

## Safety Boundary

The model was downloaded only for local evaluation and was not bundled as a release. The real load passed with the approved local manifest, preserved notices, exact hashes, and pinned llama.cpp binary. RAM/VRAM telemetry is now wired into `run --telemetry` and both quantizations are measured; redistribution approval remains the only open item. External APIs remain opt-in and never receive data through automatic fallback.
