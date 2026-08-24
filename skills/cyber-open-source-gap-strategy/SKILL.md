---
name: cyber-open-source-gap-strategy
description: Positions Cyber SOP Harness against open-source security tools, selects integrations instead of duplicating them, and converts ecosystem gaps into governed terminal workflows, evidence, replay, reporting, and model-provider requirements.
---

# Cyber Open Source Gap Strategy

## What And Why

Nmap, ZAP, Nuclei, Semgrep, Trivy, Prowler, ffuf, sqlmap, CTF benchmarks, and security LLMs already cover many point tasks. Cyber SOP Harness wins by providing the missing professional control plane: authorization, permits, containment, evidence, independent verification, coverage, replay, reporting, and model governance.

## Integration Rule

Prefer a typed adapter around a mature tool over writing a weaker replacement. Each integration must declare capability, targets, privilege, side effects, network destinations, output limits, timeout, credential handle, cleanup, rollback, risk class, and evidence schema. The model may propose an adapter invocation; it cannot invoke the tool directly.

## Gap-To-Feature Map

| Ecosystem gap | Build |
|---|---|
| Authorization scattered in tickets/chat | Signed engagement manifest and scope evaluator |
| No binding between approval and exact action hash | One-use permit verifier |
| Tool sprawl without workflow state | Methodology compiler and workflow graph |
| Raw logs separated from conclusions | Hash-chained raw/redacted evidence store |
| Findings advanced by assertion | Independent verifier and finding lifecycle |
| No repeatable lab replay | Frozen fixture/environment catalog and replay package |
| Unknown methodology coverage | Coverage ledger with UNKNOWN/BLOCKED states |
| Model/provider swaps alter behavior | Provider parity and pinned runtime manifests |
| Sensitive engagements forced to cloud | Loopback local provider and no automatic fallback |
| Stop depends on agent health | Local watchdog and emergency stop |
| Reports lack provenance | Report gate over verified evidence and signatures |

## Priorities

1. Terminal verbs and deterministic exit codes.
2. Engagement manifest validation and policy denial fixtures.
3. Evidence journal/replay integrity tests.
4. First two adapters: read-only HTTP fetch and local static/file analyzer, both restricted to owned fixtures.
5. Web/API SOP compiler and coverage ledger.
6. Report builder with provenance and redaction.
7. Remote terminal client with device revocation.

## Dependencies

Existing project schemas and core engines; mature external tools only through manifests; container/VM isolation; OpenAPI or typed adapter definitions; SBOM/provenance tooling; benchmark fixtures; and platform process/network controls.

## Threat Matrix

| Threat | Control |
|---|---|
| Reinventing unsafe exploit automation | Scope product to governance/orchestration |
| Tool plugin expands authority | Manifest allowlist and policy recheck |
| Dependency/tool supply-chain attack | Pin versions/hashes, SBOM, staged updates |
| Output injection from tools | Treat results as evidence, escape display, parse structurally |
| Evidence leakage | Separate raw/redacted stores and release gate |
| False compatibility claims | Publish tested versions/platforms and failing cases |
| Legal misuse | No unauthorized scanning, evasion, or destructive default capabilities |

## Pitfalls

Do not bundle offensive payloads or default credentials. Do not wrap interactive tools that bypass typed envelopes. Do not trust plugin descriptions without behavioral tests. Do not call the project “Parrot-like” if unrestricted shell access remains possible.

## Gate

Claim differentiation only after fixtures prove authorization denial, permit expiry/consumption, containment failure blocking, evidence tamper detection, independent verification, coverage honesty, provider parity, redaction, and report provenance.
