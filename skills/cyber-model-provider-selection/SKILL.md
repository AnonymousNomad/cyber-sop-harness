---
name: cyber-model-provider-selection
description: Defines the one-time model setup wizard and policy-safe selection among a verified bundled/local model, a user local model/endpoint, and an explicitly enabled external API.
---

# Cyber Model Provider Selection

## Directive

Implement a `ModelProviderSelection` contract and startup flow with exactly three explicit choices:

- `VerifiedLocal`: use a verified installed/bundled model and local runtime;
- `UserLocal`: use a user-selected local GGUF or loopback-compatible endpoint;
- `ExternalApi`: use a user-approved external API.

The wizard must:

- show provider name, model/version, source, license status, local/remote data path, retention warning, and resource estimate before selection;
- default to no external egress and never silently download or switch providers;
- persist only non-secret provider metadata and a selection ID;
- store API keys only in platform secure storage, pass them through headers/environment-safe memory, and never write them to prompts, logs, evidence, crash reports, or provenance stamps;
- validate local paths, endpoint health, model identity, runtime identity, and policy compatibility before readiness;
- send every proposal through the same `ActionEnvelope`, policy, permit, typed broker, evidence, and provenance path regardless of provider;
- permit provider changes only through an explicit user action and invalidate active provider-bound sessions/permits;
- provide a visible `OFFLINE`, `LOCAL`, or `EXTERNAL` status and an auditable selection event.

No provider may authorize actions, expand scope, invoke tools directly, or mark findings verified.

## Rationale and Architectural Reason

A provider abstraction prevents model choice from changing security behavior. The local model is useful for offline privacy and predictable cost, while a user local endpoint supports existing installations and an external API supports capable hardware-independent use. These choices have different trust and data-flow risks, so the UI must make the distinction explicit and the gateway must record it. The policy engine must see a normalized action, not provider-specific authority. Persisting only a selection ID avoids turning configuration storage into a secret store.

An explicit no-fallback rule is essential for cybersecurity data: if a local model fails, sending the prompt to a cloud provider would be a privacy and authorization violation. Provider identity, retention, and egress state belong in evidence and signed provenance.

## Threat Matrix

| Threat/trap | Likely complication/error | Required prevention/detection | Test |
|---|---|---|---|
| Silent cloud fallback | Local model crash sends target data to API | No automatic fallback; explicit consent and separate state | Kill local server and assert no request leaves host |
| API key leakage | Key in logs, command line, prompt, dump, or evidence | Secure storage, redaction, headers only, secret canary | Log/evidence scan |
| Provider policy drift | Different provider changes action decision | Re-run same normalized action through policy; compare decision | Provider parity test |
| Endpoint impersonation | User points to an unrelated local service | Health plus model identity, auth, and TLS/loopback validation | Wrong-server rejection |
| Stale selection | Deleted/replaced model still shown ready | Revalidate on every startup and before request | File replacement test |
| Scope expansion | Provider output includes a new target | Action schema and policy scope revalidation | Out-of-scope proposal test |
| Approval replay | Provider switch reuses old approval | Provider/session binding and fresh permit | Provider-switch replay test |
| Remote retention | API stores sensitive prompts | Show retention/data location and require opt-in | Consent/state test |
| Offline deception | UI says local while endpoint is remote | Classify endpoint and record network path | Endpoint classification test |
| Model tool authority | Server tools/MCP expose shell/filesystem | Disable tools/agent/MCP and reject tool-call mode | Startup flag/identity test |
