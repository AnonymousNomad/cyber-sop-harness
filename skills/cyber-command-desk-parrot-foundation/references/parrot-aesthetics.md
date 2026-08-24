# Parrot OS Terminal Source Findings

Research date: 2026-08-24. Sources are official ParrotSec GitHub repositories at these commits:

- `parrot-skel`: `0420beb95ed96d29919ff1418e10697b005d48a1`
- `parrot-core`: `525964d68c4d3eab46a62353ce37c92b3cdb95f4`
- `parrot-interface`: `9c94d417f9b0ef7c87a2c0648b8007e506eaec28`

## Shell Pattern

Parrot prints `Welcome to Parrot OS`, then uses this two-line Zsh prompt:

```text
┌[host]─[HH:MM-dd/mm]─[working-directory]
└╼user$
```

Zsh color roles are:

- red `┌`, `]─[`, and `└╼` structure;
- cyan hostname;
- yellow time/date;
- magenta working directory;
- green username;
- yellow `$`.

Fish uses the same geometry. The newer Parrot Core Zsh config adds `hex-encode`, `hex-decode`,
and `rot13` helpers, colored/common aliases, conditional loading of plugins, 10,000-command
history, and title updates using `<command> - Parrot Terminal`.

Recommended Parrot Zsh packages are `zsh-autocomplete`, `zsh-syntax-highlighting`, and
`zsh-autosuggestions`. Autocomplete starts after one character, tab uses menu selection, and
autosuggestions provide fish-like history hints.

## Konsole Green-On-Black Palette

Parrot profile defaults are 110 columns, unlimited scrollback, blinking block cursor with red
custom cursor, automatic copy of selected text with trailing-space trimming, underline recognition,
and line-character support.

Base colors:

| Role | Normal | Intense |
|---|---|---|
| Background | `#000000` | `#000000` |
| Foreground | `#18F018` | `#18F018` |
| Red | `#FA4B4B` | `#FF5454` |
| Green | `#18B218` | `#54FF54` |
| Yellow | `#B26818` | `#FFFF54` |
| Blue | `#1818B2` | `#5454FF` |
| Magenta | `#E11EE1` | `#FF54FF` |
| Cyan | `#18B2B2` | `#54FFFF` |
| Gray | `#B2B2B2` | `#FFFFFF` |

Faint variants are darker versions of each normal color.

## XFCE Terminal Behavior

The XFCE profile uses 90% background opacity, no menu bar or toolbar, underline non-blinking
cursor, unlimited scrolling, URL highlighting, mouse-wheel zoom, top tabs, and an 80×24 default
geometry. Parrot disables XFCE's unsafe-paste warning; Cyber SOP Harness must not copy that flaw.

## Adaptation Rule

Preserve the compact boxed prompt, dark green-on-black mood, strong structural red, and responsive
suggestions. Replace generic working directory/context with engagement/scope state. Add explicit
risk, approval, model/provider, evidence, and emergency indicators that Parrot's general-purpose
shell does not provide.
