---
name: cyber-command-desk-parrot-foundation
description: Implements the Parrot-inspired two-line prompt, command routing, completion, history, highlighting, and safe input foundation for the Cyber SOP Harness command desk.
---

# Cyber Command Desk Foundation

## What And Why

Phase 1 reproduces the recognizable Parrot-style operator surface without turning Cyber SOP
Harness into a free-form shell. The desk is a governed REPL: input resolves through a fixed
verb registry, policy engine, permit issuer, broker, and evidence store. Visual familiarity
is presentation only; it never grants authority.

Read [`references/parrot-aesthetics.md`](references/parrot-aesthetics.md) before changing
prompt geometry, palette, completion behavior, or terminal profile defaults.

## Procedure

1. Create `CommandDeskState` containing operator display name, controller identity,
   engagement label, scope reference, risk class, provider/model identity, pending approvals,
   resource health, and emergency-stop state. Load it from typed application state, never from
   environment variables containing credentials.
2. Implement a two-line prompt inspired by Parrot:

   ```text
   ┌[csh]─[14:20-24/08]─[engagement/scope]
   └╼ operator❯
   ```

   Keep the frame red, host/controller cyan, time/state yellow, context/path magenta, and
   operator green. Replace Parrot's plain `$` with `❯`; retain its low-noise, compact feel.
3. Implement `ICommandDeskRenderer`, `IInputReader`, `IVerbRegistry`, and
   `CommandDeskRepl`. The renderer writes ANSI to stdout; diagnostics go to stderr.
4. Route every line through a static verb tree such as `doctor`, `engagement`, `model`,
   `proposal`, `action`, `evidence`, `report`, and `emergency`. Reject unknown verbs with a
   stable exit code and suggest the nearest valid verb without executing it.
5. Provide static completions for verbs/subcommands, dynamic completions for engagement IDs,
   target references, and evidence IDs from authorized state. Cap suggestions and never place
   secrets or raw unvalidated targets in completion state.
6. Store command history locally with secret redaction, engagement separation, append-only
   writes, a 10,000-entry default cap, and an explicit `--no-history` path for sensitive work.
7. Highlight only known safe token classes: verb, subcommand, flag, string, number, comment,
   and error. Treat all pasted/untrusted text as inert content.
8. Set the terminal title to `verb - Cyber Command Desk`; do not echo raw target data there.

## Code To Write

- Minimal ANSI theme renderer with `NO_COLOR`, non-TTY, narrow-width, and screen-reader fallbacks.
- Typed verb registry and parser with deterministic help/errors.
- Redacted history store and completion provider interfaces.
- Single-line and two-line prompt modes.
- Integration tests for unknown command, malformed flags, rejected dangerous paste, history redaction,
  `NO_COLOR`, non-TTY output, and emergency stop while input is active.

## Dependencies And Issues

- .NET 10 console application; avoid native curses in phase 1.
- Prefer built-in APIs and small MIT/Apache libraries. Add `ReadLine` only if its hooks support
  custom rendering; add `Spectre.Console` only after the governance core is complete.
- Parrot uses Zsh autosuggestions, autocomplete, and syntax highlighting. Reimplement only the
  behavior needed for the governed verb tree; Debian Zsh packages are not dependencies of the C# app.
- Windows/macOS/Linux terminals differ in ANSI support. Probe capabilities; never assume truecolor.

## Threat Matrix

| Threat | Control |
|---|---|
| Familiar prompt implies shell access | Fixed verb registry; no arbitrary process execution |
| Secret leakage through history | Redaction before persistence and per-engagement isolation |
| Terminal escape injection | Sanitize/control untrusted text before rendering |
| Sensitive paste execution | Confirm multiline/large paste; parse only after explicit acceptance |
| Completion leaks targets/secrets | Source from authorized typed state and apply retention limits |
| Model output impersonates operator | Label model output and route only through proposal parser |
| Emergency stop unavailable | Bind a global interrupt path outside model/provider readiness |

## Bugs And Pitfalls

- Do not copy Parrot's `MiscShowUnsafePasteDialog=false` behavior; keep a large-paste guard.
- Do not put secrets, bearer tokens, scope exclusions, or raw client data into titles/history.
- Do not let ANSI markup corrupt JSON mode; disable decoration when structured output is requested.
- Do not block emergency stop behind completion, model inference, network calls, or rendering locks.
- Do not claim Parrot affiliation; call the result “Parrot-inspired.”

## Gate

Phase 1 passes when the prompt matches the design at 80/110 columns, all verbs resolve through
the registry, unknown/dangerous inputs fail closed, sensitive history is redacted, fallbacks pass,
and emergency stop works during prompt/read/completion activity.
