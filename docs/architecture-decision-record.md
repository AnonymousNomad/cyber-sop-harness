# Architecture Decision Record 0001

Title: Mobile control plane with PC/Linux execution plane
Date: 2026-08-17
Status: Accepted for Phase 1 planning

## Context

The product must support iPhone, Android, and PC hardware while allowing models and tools to be swapped. Security tools require durable processes, local credentials, process control, network policy, and sandboxing. iOS applications are sandboxed and background work is system-managed. Android is more capable but still sandboxed and subject to lifecycle and background restrictions.

## Decision

Use a thin mobile control client and a desktop execution gateway. The gateway owns the policy gate, model/provider routing, credentials, typed tool broker, worker lifecycle, evidence capture, replay, and verifier. Workers run on Windows first and Linux later.

Use an external policy decision before every action. Use one-use permits bound to run, action, target, scope, policy version, worker identity, expiry, and approval.

Use Flutter as the initial cross-platform UI candidate. Keep the UI outside the security boundary. The gateway and worker remain separate processes with explicit IPC and access control.

## Alternatives Rejected

### Phone as executor

Rejected because iOS cannot provide arbitrary shell/daemon behavior and Android lifecycle/sandbox constraints make it unreliable as a durable authoritative executor.

### Model as policy authority

Rejected because a model can hallucinate, be prompt-injected, or misinterpret scope. Authorization must be external and deterministic.

### Direct model-to-tool integration

Rejected because unrestricted tools hide side effects, make policy auditing difficult, and weaken containment.

### Cloud-only execution

Rejected as the default because sensitive evidence, provider retention, connectivity, latency, and customer deployment constraints require local/on-premise operation.

### Generic shell as the first tool interface

Rejected because arbitrary command execution defeats capability declarations and makes it difficult to enforce least privilege.

## Consequences

Positive:

- PC resources support real tools and workers.
- Mobile remains useful without becoming privileged.
- Providers and tools can be swapped behind stable contracts.
- Evidence and policy are centralized.
- Offline/local deployments remain possible.

Negative:

- The system requires a gateway and worker lifecycle.
- Mobile operation depends on authenticated connectivity for live state.
- Cross-platform packaging is more complex than a single CLI.
- A secure broker and evidence store must be built before broad methodology coverage.

## Verification Required Later

- Mobile approval replay and revocation
- Gateway authentication and device pairing
- Worker kill without mobile/provider availability
- Relay failure revokes relay-dependent permits, stops every active worker, preserves evidence, and requires fresh permits after reauthentication
- Cross-engagement data isolation
- Model/provider swap invariance
- Tool adapter capability enforcement
- Windows and Linux containment tests
