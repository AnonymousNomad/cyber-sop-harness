---
name: cyber-secret-custody
description: Governs secure custody of external API credentials, tokens, and machine secrets on Windows using DPAPI ProtectedData/Credential Manager, with consent, rotation, and egress controls. Use whenever an external provider credential, API key, or machine secret must be stored, loaded, rotated, or cleared.
---

# Cyber Secret Custody

## Directive

Handle every external API credential, API key, token, and machine secret with these controls:

- Storage: persist secrets via Windows DPAPI (`System.Security.Cryptography.ProtectedData`, `CurrentUser` scope, with a project-specific entropy) or Credential Manager. Never store secrets in plaintext files, JSON manifests, source code, or the selection store.
- Separation: the provider selection store persists metadata only (provider id, name, model, version, source, license status). Secrets live only in protected storage keyed by provider id.
- Loading: load the secret only when a provider action actually executes, inside the contained provider boundary; never pass the secret to the model, to prompts, to logs, or into evidence/provenance records.
- Redaction: redact secrets and secret-like patterns from every proposal, evidence, artifact, and audit record before persistence.
- Consent and egress: external calls require the engagement manifest and policy engine to authorize the target and the scope; an API key alone never authorizes a call. External calls stay off by default; the startup flow surfaces the provider disclosure (provider, model, data path, retention, resource estimate) before the user enables anything.
- Rotation and clearing: provide a documented rotate-and-clear path (delete protected blob, regenerate, re-store) and never re-issue an API key that was exposed in chat, logs, or artifacts.
- Escalation: when a secret was exposed, instruct the human to revoke/rotate it and record the exposure in `agent_notes.Md`; never silently reuse it.

## Rationale and Architectural Reason

Secrets are the highest-value artifact an attacker can steal from this system: a stored API key turns a model-output bug into full account compromise. Microsoft's DPAPI documentation is explicit that `ProtectedData`/`CurrentUser` encryption ties the secret to the user account on the machine and requires application-specific entropy to avoid cross-app decryption; that is the supported, locally-attestable option on this Windows host. Credential Manager is the alternative when the credential should be visible to the user in Windows credential UI.

Storing only metadata in the selection store (as the existing `ModelProviderSelectionStore` already does) keeps the secret surface minimal: a dump of project state contains provider ids and names, not usable credentials. Loading the secret only at execution time inside the provider boundary means the model and the evidence/provenance paths never see the raw value, so no prompt, log, artifact, or stamp can leak it by construction. Consent separated from credentials means a leaked key cannot, by itself, authorize a call; the policy engine still gates every external interaction.

## Threat Matrix

| Threat/trap | Likely complication/error | Required prevention/detection | Test |
|---|---|---|---|
| Plaintext secret on disk | API key in selection-store JSON or config | DPAPI/Credential Manager only; metadata-only store | Disk-scan for secret marker |
| Secret in logs/evidence | Token printed in provider logs or stamped records | Redaction filter before persistence | Redaction-scan test |
| Secret in model context | Key passed to prompt or completion | Load at execution only, inside provider boundary | Prompt-content assertion |
| Cross-app decryption | Another app reads the DPAPI blob | CurrentUser scope + application entropy | Entropy-mismatch reject test |
| Leaked key reused | Chat-pasted token silently used | Revoke + rotate; clear stored blob | Exposure-revocation test |
| Key alone authorizes call | Credential treated as authorization | Policy engine + engagement manifest still gate egress | Unauthorized-egress block test |
| No rotation path | Stale key cannot be replaced | Documented rotate-and-clear flow | Rotation test |
| Secret persisted in model selection store | Selection JSON gains a credential field | Store schema forbids secret fields; validator rejects them | Schema-rejection test |
