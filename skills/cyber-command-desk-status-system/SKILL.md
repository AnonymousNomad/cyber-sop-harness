---
name: cyber-command-desk-status-system
description: Builds the Parrot-inspired color system, status rail, risk badges, resource gauges, and accessible fallbacks for the Cyber SOP Harness terminal desk.
---

# Cyber Command Desk Status System

## What And Why

Phase 2 turns Parrot's aesthetic into an operational security console. Color must communicate
authorization, risk, evidence, model identity, and resource health at a glance. The layout must
degrade safely on an edge tablet, over SSH, in `NO_COLOR`, and in machine-readable JSON mode.

## Visual Contract

- Background black `#000000`; normal foreground green `#18F018`.
- Structural frame and blocked state: red `#FA4B4B`; intense alert `#FF5454`.
- Safe/verified/pass: green `#18B218`; intense success `#54FF54`.
- Pending/approval/expiry/resource warning: yellow `#B26818`; intense warning `#FFFF54`.
- Recon/info/blue-team context: blue `#1818B2`; intense info `#5454FF`.
- Target/path/model variant: magenta `#E11EE1`; intense selected `#FF54FF`.
- Operator/runtime/controller: cyan `#18B2B2`; intense active `#54FFFF`.
- Muted metadata: gray `#B2B2B2`; high-contrast metadata white `#FFFFFF`.

Never rely on color alone. Pair every semantic color with text (`BLOCKED`, `R3`, `PENDING`,
`UNVERIFIED`, `LOCAL`) and a Unicode symbol that survives without icon fonts.

## Layout Contract

At 110 columns use:

```text
Cyber Command Desk │ engagement │ scope │ UTC clock
ACTION QUEUE              TRANSCRIPT / COMMAND OUTPUT         EVIDENCE + MODEL
┌ RECON                  ┌ > proposal submit ...             ┌ EV-001 verified
│ R1 dns.resolve         │ policy=ALLOW permit=...            │ source/hash/time
└ waiting approval       └ rendered output is inert          └ chain=valid
[ENG active] [SCOPE valid] [RISK R1] [MODEL local/lfm2.5] [RSS 735M] [STOP=F2]
```

At 80 columns collapse to one status line plus a scrolling transcript. Below 60 columns or in
non-TTY mode, print linear key/value records. In `--json` mode emit one JSON document and no UI.

## Code To Write

1. Define immutable `DeskTheme`, `DeskBadge`, `DeskPanel`, and `DeskStatus` records.
2. Map semantic states to color, symbol, label, and ARIA/title text in one table.
3. Render badges from typed enums, never by parsing model prose.
4. Clamp panel widths; truncate with an explicit ellipsis and stable ordering.
5. Refresh status from snapshots at most twice per second; freeze rendering during evidence export.
6. Add `--compact`, `--json`, `--no-color`, `--width`, and `--safe-render` overrides.
7. Test visible/invisible character sanitization, wide glyphs, overflow, invalid UTF-8 replacement,
   and redraw after terminal resize.

## Dependencies

- .NET 10 and ANSI/ECMA-48 output.
- Optional `Spectre.Console` for tables/trees if bundle size and startup latency remain acceptable.
- Optional `Terminal.Gui v2` only for the later multi-pane interactive desk.
- No remote font, daemon, telemetry, or network dependency for status rendering.

## Threat Matrix

| Threat | Control |
|---|---|
| Color makes unsafe action look safe | Risk label plus policy decision controls execution |
| Untrusted output escapes panel | Escape/control-character neutralization and bounded cell rendering |
| Status spoofing | Status comes only from signed/typed runtime state |
| Stale authorization appears active | Snapshot includes expiry and monotonic event ID |
| Low-resource device starvation | Throttled refresh, bounded redraw region, no animation by default |
| Accessibility failure | Text symbols, `NO_COLOR`, linear mode, contrast-checked palette |

## Bugs And Pitfalls

- Do not use green for “model said success”; green means independently verified.
- Do not animate progress during approval or emergency stop.
- Do not draw panels before checking terminal width/capability.
- Do not cache expired authorization as active.
- Do not let full-screen redraw hide a denial or confirmation prompt.

## Gate

Phase 2 passes when every badge has text/color/symbol parity, JSON mode contains zero ANSI,
narrow/no-color/non-TTY modes are readable, untrusted output cannot alter layout, and stale or
expired state changes within one refresh interval.
