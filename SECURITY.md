# Security

## Reporting vulnerabilities

Do not open a public issue for a security finding. Report privately to the maintainer at `AnonymousNomad` on GitHub (or a private security advisory via the repo's Security tab). Include the harness version, the reproduction steps, and the observed behavior. Acknowledgment within 7 days; coordinated disclosure otherwise.

## Security model

Cyber SOP Harness is a governance and evidence layer for **authorized defensive testing only**. Its core property is **fail closed**:

- No action executes without a validated authorization manifest, scope, permit, and typed tool binding.
- No model proposal is trusted: proposals are parsed strictly, normalized, validated, and executed only through frozen typed tool adapters.
- Every execution produces durable evidence: a tamper-detecting journal, signed artifact hashes, and provenance keys protected by the operating system's DPAPI.
- Missing, ambiguous, or tampered state (selection, manifest, consent, endpoint, readiness) aborts startup with a controlled error — never a degraded run.
- Local model runtime connections require loopback endpoints and verified model manifests; remote model APIs require explicit consent plus a stored secret, and are hidden from the setup wizard unless both exist.
- No credentials, tokens, or target data are ever committed; all local state lives under `data/` and is git-ignored.

## Scope of responsibility

The harness does not protect against a malicious model that already holds valid authorization, and it does not make an unauthorized assessment authorized. Authorization is the operator's responsibility; containment, evidence, and verification are the harness's.