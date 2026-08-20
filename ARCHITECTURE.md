# Architecture Decision

Status: planning baseline
Date: 2026-08-17

## Topology

```text
iPhone / Android control client
              |
      authenticated control API
              |
       engagement preflight
              |
       model/provider adapter
              |
    typed action proposal
              |
       external policy gate
              |
        one-use permit
              |
         typed tool broker
              |
      sandboxed PC/Linux worker
              |
       authorized target or lab
              |
  append-only evidence and verifier
```

## Plane Responsibilities

### Mobile control plane

Mobile displays proposed actions, target, scope, risk, duration, approvals, evidence summaries, and run state. It can request pause or stop. It does not own execution authority, cloud provider keys, arbitrary commands, or scope expansion.

### Desktop gateway

The desktop gateway owns durable execution, provider access, authorization, policy checks, tool adapters, credential handles, worker lifecycle, evidence capture, and local model access.

### Policy gate

The policy gate is independent of the model and runs both during engagement preflight and immediately before every action. It evaluates authorization, target scope, redirects, risk, credentials, rate limits, time windows, approvals, and worker identity. It returns `ALLOW`, `BLOCK`, or `APPROVAL_REQUIRED`. Only `ALLOW` produces a one-use permit.

### Model adapter

Models emit structured proposals. They do not authorize actions and do not directly execute tools. Provider, model, configuration, data-retention policy, and tool-call mode are recorded.

### Tool broker

Tools are registered typed capabilities. Each manifest declares targets, side effects, privileges, network destinations, data classes, limits, evidence outputs, and cleanup behavior.

### Worker

The worker executes a one-use permit inside a restricted environment. Windows uses Job Objects and Windows Sandbox or Hyper-V for untrusted/high-risk work. Linux uses unprivileged workers, namespaces, cgroups, seccomp, Landlock, and VMs where required.

Seccomp is not a complete sandbox by itself. Landlock support must be detected at runtime and treated as an additional restriction layer.

## Trust Boundaries

1. Model output to policy: proposals are untrusted.
2. Target content to model: responses, files, and pages are untrusted data, never instructions.
3. Tool adapter to worker: undeclared capabilities fail closed.
4. Worker to evidence store: workers append evidence but cannot rewrite authoritative history.
5. Mobile to gateway: device identity, operator identity, approval binding, expiry, and nonce are validated.
6. Provider to engagement: data classification and provider policy determine what may leave the execution boundary.
7. Engagement to engagement: credentials, context, memory, logs, evidence, and artifacts are isolated.

## Platform Constraints

Apple documents that third-party iOS/iPadOS applications are sandboxed, cannot modify system resources or escalate privileges, and may perform background processing only through system APIs. Apple App Review also restricts downloading, installing, or executing code that changes application functionality.

Android provides per-application sandboxing but restricts background execution and can suspend or kill processes. Android VPN capabilities are not equivalent to unrestricted host access.

Therefore, phones are control clients, not authoritative pentest executors.

## Transport and Identity

Support explicit LAN pairing and an optional outbound relay/private VPN. LAN presence is not authorization. Native OAuth uses external-browser authorization with PKCE according to RFC 8252.

Mobile provider credentials never ship in the app. Requests to cloud providers route through the desktop gateway.

If the relay is unavailable, the gateway must deny new remote approvals, revoke all permits that depend on the relay, and transition every active worker to `STOPPING`. The local watchdog must stop all active work because the control plane can no longer establish current authority. It must stop new actions, preserve local evidence, close connections, and transition affected runs to `STOPPED` after cleanup. Reconnection requires device authentication, scope revalidation, policy-version comparison, and fresh permits; queued actions are discarded rather than replayed.

## UI Recommendation

Flutter is the initial cross-platform UI recommendation because its official supported deployment matrix includes Android, iOS, Windows, Linux, macOS, and web. Tauri is an acceptable alternative if its frontend/Rust IPC permissions are tightly scoped.

The UI framework is not a security boundary. The gateway, policy gate, worker, and evidence store are the security boundaries.

## Offline Behavior

Offline desktop operation may use local models and local tools. Offline mobile operation may display cached state and perform local approval UI, but high-impact actions must not queue silently for later execution. All queued actions require expiry and a fresh policy check at execution.
