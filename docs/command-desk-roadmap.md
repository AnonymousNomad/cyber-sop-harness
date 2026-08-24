# Professional Command Desk Roadmap

Status: research-based implementation plan  
Date: 2026-08-24  
Design influence: official Parrot OS shell/terminal configuration, adapted for governed security work

## Research Summary

Official Parrot sources use a compact two-line boxed prompt:

```text
┌[host]─[time-date]─[path]
└╼user$
```

Its structure is red; identity is cyan; time is yellow; path is magenta; operator is green. The
Konsole profile is green-on-black at 90% opacity with unlimited scrollback and a red cursor. Zsh
adds autosuggestions, autocomplete, syntax highlighting, security helper aliases, colored listing,
and command-title updates. Exact palette/layout evidence and source commits are in
`../skills/cyber-command-desk-parrot-foundation/references/parrot-aesthetics.md`.

Cyber SOP Harness should preserve the look but replace generic filesystem context with operational
state: engagement, scope, risk, approval, model/provider, evidence, resources, and emergency stop.
It must not become a free-form shell.

## Phases

### Phase A — Governed Shell Feel

Implement the two-line prompt, ANSI theme, fixed verb registry, deterministic errors, static
completions, redacted history, safe paste guard, `NO_COLOR`, non-TTY, and JSON modes. Keep rendering
separate from authority.

Skills: `cyber-command-desk-parrot-foundation`, `cyber-terminal-control-plane`.

### Phase B — Operational Status Layer

Add semantic badges for scope, risk, approval, provider/model, evidence, RSS/memory, and stop state.
Build the 110-column three-panel layout, 80-column compact fallback, linear accessibility mode, and
JSON-only mode. Refresh from typed snapshots and sanitize every untrusted cell.

Skills: `cyber-command-desk-status-system`, `cyber-terminal-control-plane`.

### Phase C — Bug-Bounty Work Views

Implement preflight, engagement, targets, proposals, actions, evidence, findings, report, and
emergency views. Each view is a read model over existing governance stores; mutations traverse the
parser, policy engine, permit issuer, broker, rollback ledger, and evidence store.

Skills: `cyber-command-desk-workflow-views`, `cyber-bounty-sop-terminal-workflow`,
`cyber-terminal-control-plane`.

### Phase D — Interactive Hardening

Add resize-aware redraw, keyboard shortcuts, completion caching, worker progress, cancellation,
screen-reader/low-bandwidth profiles, and performance budgets. Emergency stop must work during every
render/input/inference state.

Skills: all three command-desk skills plus `cyber-safe-execution-containment`.

### Phase E — Release Assurance

Run golden-render tests at 60/80/110/160 columns, injection/redaction suites, offline fixture
engagements, policy denial cases, R3 approval enforcement, evidence tamper rejection, report
redaction, provider-parity tests, latency measurements, and emergency-stop chaos tests.

Skills: `cyber-evaluation-reporting-release`, `cyber-project-governance`, and all command-desk skills.

## Non-Goals

- No arbitrary shell/command execution from the prompt.
- No cloud fallback or model-owned tool authority.
- No screenshots/animations that leak target data.
- No color-only risk communication.
- No copying Parrot's disabled unsafe-paste warning.
