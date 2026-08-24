# Open-Source Gap Analysis

Status: product positioning and implementation backlog  
Date: 2026-08-23

## Existing Open Tools

The ecosystem already has capable point tools:

- Reconnaissance and attack surface discovery: Subfinder, Amass, ProjectDiscovery tools, Nmap.
- Web/API testing: OWASP ZAP, ffuf, Nuclei, sqlmap, Katana, httpx.
- Source and dependency analysis: Semgrep, OSV-Scanner, Trivy, Gitleaks, Grype.
- Cloud/configuration assurance: Prowler, ScoutSuite, Checkov.
- CTF/agent research: Cybench, NYU CTF benchmark, InterCode-CTF, AIRTBench, BoxPwnr traces.
- Local models: DeepHat, WhiteRabbitNeo, Seneca/Trendyol cybersecurity fine-tunes, Qwen/Devstral bases.

These tools are necessary but not sufficient for professional operations because they generally assume a human owns authorization, workflow state, evidence custody, severity judgment, cleanup, and disclosure.

## Product Gap

Cyber SOP Harness should not duplicate scanners or exploit frameworks. Its differentiated layer is governed orchestration:

| Missing capability in most open workflows | Harness answer |
|---|---|
| Machine-checkable authorization linked to exact actions | Signed engagement manifest plus scope evaluator |
| One-use permits bound to target, method, operator, risk, and expiry | Permit issuer and verifier |
| Free-form shell/model command chains | Typed capability registry and fail-closed broker |
| Scattered screenshots/logs that cannot be trusted | Append-only hash-chained raw/redacted evidence |
| Findings advanced by model confidence | Independent verifier and finding lifecycle |
| No replay after staff/tool changes | Deterministic fixtures and signed replay package |
| Coverage hidden in analyst notes | Versioned methodology compiler and coverage ledger |
| Model swaps silently changing behavior | Provider parity tests and pinned runtime manifests |
| No safe edge path for sensitive engagements | Local loopback runtime, no automatic cloud fallback |
| Emergency stop coupled to agent/UI health | Local watchdog independent of model/client/provider |
| Reports lacking provenance | Report gate over verified evidence and signed artifacts |

## First Three Build Priorities

1. **Terminal control plane:** implement `doctor`, `engagement validate`, `model pin`, `model serve`, `proposal submit`, `action status`, `evidence export`, `report build`, and `emergency stop` with deterministic exit codes.
2. **Offline scoring harness:** score candidate models against JSON validity, scope refusal, evidence IDs, prompt-injection resistance, provider parity, and resource limits.
3. **Methodology compiler:** compile web/API bug-bounty SOPs into executable graphs and emit coverage ledgers from local fixtures.

## Non-Goals

Do not ship unrestricted exploit automation, payload maximization, CAPTCHA/anti-abuse evasion, unauthorized scanning, credential attacks against live systems, or automatic cloud fallback. These increase legal risk and do not solve the professional evidence/governance gap.
