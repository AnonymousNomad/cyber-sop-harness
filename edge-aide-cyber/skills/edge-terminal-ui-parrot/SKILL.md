# Edge Terminal UI — Parrot OS Aesthetic

## What To Do
Build a green-on-black terminal interface in the browser that replicates Parrot OS's visual identity: two-line prompt, Konsole color palette, blinking block cursor, and monospace typography.

## Why
Security professionals are familiar with Parrot's terminal look. Replicating it reduces cognitive load and signals "this is a serious tool." The two-line prompt provides context (host, time, directory) without cluttering the command line.

## Code Guidance
```html
<!-- index.html -->
<style>
  :root {
    --bg: #000000;
    --fg: #18F018;
    --fg-intense: #54FF54;
    --red: #FA4B4B;
    --green: #18B218;
    --yellow: #B26818;
    --blue: #1818B2;
    --magenta: #B218B2;
    --cyan: #18B2B2;
    --cursor: #FA4B4B;
  }

  * { box-sizing: border-box; margin: 0; padding: 0; }

  body {
    background: var(--bg);
    color: var(--fg);
    font-family: 'Fira Code', 'JetBrains Mono', 'Courier New', monospace;
    font-size: 14px;
    line-height: 1.4;
    height: 100dvh;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }

  #terminal-output {
    flex: 1;
    overflow-y: auto;
    padding: 8px 12px;
    white-space: pre-wrap;
    word-break: break-word;
  }

  #terminal-input-area {
    padding: 4px 12px 12px;
    border-top: 1px solid #1a3a1a;
  }

  .prompt-line {
    color: var(--red);
  }
  .prompt-host { color: var(--cyan); }
  .prompt-time { color: var(--yellow); }
  .prompt-dir { color: var(--magenta); }
  .prompt-user { color: var(--green); }
  .prompt-dollar { color: var(--yellow); }

  #command-input {
    background: transparent;
    border: none;
    outline: none;
    color: var(--fg);
    font-family: inherit;
    font-size: inherit;
    width: 100%;
    caret-color: var(--cursor);
  }

  /* Blinking cursor */
  @keyframes blink { 0%,49% {opacity:1} 50%,100%{opacity:0} }
  .blinking-cursor {
    animation: blink 1s step-end infinite;
    color: var(--cursor);
  }

  /* Status bar */
  #status-bar {
    display: flex;
    gap: 16px;
    padding: 4px 12px;
    background: #0a0a0a;
    border-top: 1px solid #1a3a1a;
    font-size: 11px;
    color: var(--fg);
  }
  .status-item { white-space: nowrap; }
  .status-ok { color: var(--green); }
  .status-warn { color: var(--yellow); }
  .status-err { color: var(--red); }
</style>

<div id="terminal-output"></div>
<div id="terminal-input-area">
  <div class="prompt-line">
    ┌[<span class="prompt-host" id="hostname">edge</span>]─[<span class="prompt-time" id="time">00:00</span>]─[<span class="prompt-dir" id="cwd">~</span>]
  </div>
  <div>
    <span class="prompt-user">└╼</span><span class="prompt-dollar">operator$</span>
    <input id="command-input" autocomplete="off" autocapitalize="off" spellcheck="false" autofocus />
  </div>
</div>
<div id="status-bar">
  <span class="status-item status-ok" id="model-status">● model</span>
  <span class="status-item" id="engagement">no engagement</span>
  <span class="status-item" id="permits">0 permits</span>
  <span class="status-item" id="evidence-count">0 evidence</span>
  <span class="status-item" id="rss">-- MiB</span>
</div>
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| XSS via tool output rendered as HTML | Session hijack | Always use `textContent`, never `innerHTML` for dynamic content |
| Command history stored in localStorage | Credential exposure on shared device | Store server-side only, bounded to last N entries |
| Virtual keyboard hides input field | User cannot type | Scroll into view on focus; use `visualViewport` API |
| Screen reader announces sensitive output | Privacy in public | Add `aria-live="polite"` but not `aria-label` with content |

## Dependencies
- No external dependencies. Pure HTML/CSS/JS.
- Optional: Google Fonts CDN for Fira Code (falls back to system monospace)

## Pitfalls & Bugs
- Mobile browsers have different viewport heights when the address bar shows/hides. Use `100dvh` not `100vh`.
- The virtual keyboard changes viewport dimensions; listen to `resize` and `visualViewport.resize` events.
- Terminal scrollback should be capped (e.g., 500 lines) to prevent DOM bloat on long sessions.
- `autocapitalize="off"` and `spellcheck="false"` are essential for terminal input.
- Samsung Internet may render monospace fonts differently than Chrome; test both.
- The prompt line uses Unicode box-drawing characters (`┌`, `└╼`); ensure the font supports them.
