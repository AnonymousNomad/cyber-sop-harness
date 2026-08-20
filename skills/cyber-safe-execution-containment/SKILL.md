---
name: cyber-safe-execution-containment
description: Defines sandboxing, least privilege, credential handling, rate limiting, process containment, kill switches, rollback, cleanup, and failure recovery for authorized cybersecurity tools. Use whenever implementing or changing execution capabilities.
---

# Cyber Safe Execution and Containment

## Tool Boundary

Every tool must have a manifest declaring capability, allowed targets, required privilege, read/write behavior, network destinations, data classes, runtime limit, output limit, process limit, approval requirement, and cleanup behavior.

Do not expose unrestricted shell execution by default.

## Credentials

Use short-lived, least-privilege handles. Do not expose raw credentials to the model, pass them through command-line arguments, or write them to logs or evidence.

Revoke credentials when a run stops, scope changes, a phase ends, a credential anomaly occurs, or a worker is destroyed.

## Windows

Use low-privilege worker accounts, Job Objects, explicit firewall egress, Windows Sandbox or Hyper-V for high-risk work, disposable worker state, and process-tree termination.

Windows Sandbox networking must be disabled unless the engagement explicitly requires it.

## Linux

Use unprivileged users, read-only filesystems, namespaces, `NoNewPrivileges`, dropped capabilities, seccomp, Landlock, cgroups, and network allowlists. If required isolation is unavailable, block execution rather than silently reducing safety.

## Kill and Recovery

The kill path must work without the model, mobile client, primary UI, remote provider, or a healthy worker.

Relay loss is a stop trigger. On relay loss, revoke relay-dependent permits, deny new actions, transition every active worker to `STOPPING`, stop all active work through the local watchdog, preserve evidence, close connections, and transition affected runs to `STOPPED` after cleanup. Resumption requires fresh authentication, scope validation, policy-version comparison, and new permits.

On stop:

1. Deny new permits.
2. Terminate the worker tree.
3. Close network connections.
4. Preserve evidence.
5. Revoke credentials.
6. Restore approved state.
7. Record the stop reason.

## Acceptance Tests

Use local fixtures and fake targets to verify process-tree termination, egress blocking, credential revocation, resource ceilings, worker destruction, sandbox setup failure, policy-engine failure, and cleanup after interrupted execution.

Because a prompt cannot contain a compromised tool, containment must exist outside the model.
