# Edge AIDE Cybersecurity Workbench — Phased Roadmap

## Skill Count

Twenty-nine skills across seven phases. 142 tests passing. Each skill is a standalone `SKILL.md` with: what to do, why, code guidance, threat matrix, dependencies, pitfalls, and bugs to watch for.

| Phase | Skills |
|---|---|
| Phase 0: Device Lock & Architecture | `edge-device-profile-lock`, `edge-architecture-decision` |
| Phase 1: Runtime Foundation | `edge-node-runtime`, `edge-file-boundary`, `edge-websocket-api` |
| Phase 2: Model Layer | `edge-model-runtime-integration`, `edge-context-budget-manager`, `edge-cipher-state-bus` |
| Phase 3: Governance Core | `edge-policy-engine-js`, `edge-permit-lifecycle-js`, `edge-scope-evaluator-js`, `edge-evidence-chain-js`, `edge-secret-custody-mobile` |
| Phase 4: Tool Adapters | `edge-tool-adapter-framework`, `edge-network-recon-adapters`, `edge-web-testing-adapters`, `edge-output-sanitizer` |
| Phase 5: Terminal UI | `edge-terminal-ui-parrot`, `edge-workflow-views`, `edge-touch-optimization` |
| Phase 6: SOP Engine & Bounty Workflows | `edge-sop-methodology-compiler`, `edge-bounty-workflow-sops`, `edge-coverage-ledger` |
| Phase 7: Security Hardening & Release | `edge-ci-cd-edge`, `edge-security-hardening`, `edge-release-packaging` |

---

## Phase 0: Device Profile & Architecture Lock
**Status:** Complete (this document)
**Skills:** 2

Deliverables:
- Device hardware profile measured and locked
- Architecture decision record written
- Trust boundary diagram
- Model selection justified by benchmarks
- Excluded components documented with reasons

Gate:
- Every excluded component has a written reason
- Model choice is backed by measured tok/s and RSS on the target device
- No assumption about GPU or x86 availability

---

## Phase 1: Runtime Foundation
**Status:** Next up
**Skills:** 3 (`edge-node-runtime`, `edge-file-boundary`, `edge-websocket-api`)

Deliverables:
- Single-process Node.js server (ESM) that starts in Termux
- Workspace jail: all file operations confined to project root
- WebSocket + HTTP server on loopback only (127.0.0.1)
- Health endpoint returning device profile, model status, governance status
- Graceful shutdown with state flush

Forbidden:
- Listening on 0.0.0.0 or any non-loopback interface
- File operations outside the workspace jail
- Arbitrary shell execution from API routes
- Loading any model before governance is ready

Gate:
- Server starts and responds to `/api/health` in < 2s cold start
- Path traversal attempts are rejected with controlled errors
- SIGTERM/SIGINT produce clean exit with journal flush
- All tests pass offline (no network required)

Pitfalls:
- Termux Node.js may have different `os.cpus()` output; parse defensively
- Android's app lifecycle can kill background processes; implement PID file + restart detection
- `fs.watch()` is unreliable on Android; use polling with configurable interval

---

## Phase 2: Model Layer
**Status:** Blocked by Phase 1
**Skills:** 3 (`edge-model-runtime-integration`, `edge-context-budget-manager`, `edge-cipher-state-bus`)

Deliverables:
- llama.cpp HTTP client (loopback-only connection to llama-server)
- Context budget manager (token counting, truncation strategy)
- Cipher state bus ported from AIDE (append-only JSONL event log)
- Model pinning: SHA-256 verification before load
- Provider abstraction: local GGUF as default, remote API as explicit opt-in
- Prompt template system matching LFM2.5's chat format

Forbidden:
- Connecting to llama.cpp on non-loopback address without explicit consent gate
- Loading a model without hash verification
- Sending context to remote provider without consent + evidence record
- Exceeding context budget silently (must truncate or reject)

Gate:
- LFM2.5 Q4_K_M loads in < 5s, generates at ≥ 10 tok/s on CPUs 4-5
- Hash mismatch aborts startup with clear error
- Context overflow produces structured rejection, not silent truncation
- State bus persists across daemon restarts

Pitfalls:
- llama-server may not be running; implement health check + auto-start with timeout
- LFM2.5 uses a custom chat template; hardcoding ChatML will produce garbage
- Token counting must match llama.cpp's tokenizer, not an approximation
- Memory pressure can cause OOM kill; monitor RSS and refuse loads above threshold

---

## Phase 3: Governance Core
**Status:** Blocked by Phase 1
**Skills:** 5 (`edge-policy-engine-js`, `edge-permit-lifecycle-js`, `edge-scope-evaluator-js`, `edge-evidence-chain-js`, `edge-secret-custody-mobile`)

Deliverables:
- Policy engine in pure JS: evaluates action requests against engagement manifest
- Permit issuer: one-use, TTL-bounded permits bound to specific tool+target+operator
- Scope evaluator: CIDR ranges, domain wildcards, URL path prefixes, method restrictions
- Evidence chain: SHA-256 hash-chained JSONL, tamper-detecting, replayable
- Secret custody: AES-256-GCM encryption via passphrase-derived key, no plaintext secrets on disk
- Engagement manifest loader: strict schema validation at startup, fail-closed

Forbidden:
- Any tool execution without a valid permit
- Permit reuse (one-use enforced atomically)
- Evidence entries without previous-hash linkage
- Secrets stored unencrypted anywhere in the filesystem
- Policy bypass when manifest is missing, expired, or malformed

Gate:
- Missing manifest → all actions rejected
- Expired permit → execution refused with audit record
- Scope violation → policy denial with reason code
- Evidence chain verification passes after simulated tamper attempt is detected
- Secret round-trip encrypt→decrypt matches original value

Pitfalls:
- Race condition: two concurrent requests trying to use the same permit; use synchronous check-and-consume
- CIDR parsing in JS requires manual bit manipulation (no built-in library); write and test thoroughly
- Time-of-check-to-time-of-use gap between permit validation and execution; keep the window minimal
- AES-GCM nonce reuse is catastrophic; always generate fresh random IV per operation
- Manifest expiry check must use monotonic time, not wall clock, to avoid clock skew issues

---

## Phase 4: Tool Adapters
**Status:** Blocked by Phase 3
**Skills:** 4 (`edge-tool-adapter-framework`, `edge-network-recon-adapters`, `edge-web-testing-adapters`, `edge-output-sanitizer`)

Deliverables:
- Typed adapter framework: `{name, capability, riskLevel, execute(params, permit) → Result}`
- Network recon adapters: DNS lookup, whois, reverse DNS, port scan (nmap wrapper)
- Web testing adapters: HTTP header inspection, URL fetch (bounded), form discovery
- Output sanitizer: strip credentials, tokens, session IDs from all adapter outputs before evidence storage
- Adapter registry: frozen at startup, no dynamic registration during operation
- Each adapter enforces its own scope check independently of policy engine (defense in depth)

Forbidden:
- Shell injection through adapter parameters (always use `execFile`, never `exec`)
- Unbounded output capture (truncate at configurable limit, e.g., 64 KiB)
- Adapter executing without both policy approval AND independent scope recheck
- Storing raw unsanitized output in evidence chain
- Adding adapters at runtime without daemon restart

Gate:
- Every adapter rejects out-of-scope targets independently
- Output truncation works correctly at the byte boundary
- Sanitizer removes known secret patterns (Bearer tokens, AWS keys, session cookies)
- Adapter failure produces structured error, not crash
- nmap wrapper passes `--` before user arguments to prevent option injection

Pitfalls:
- `child_process.exec` concatenates into shell string — command injection vector; always use `execFile`
- nmap output format varies by version; parse with tolerant regex, never assume fixed columns
- DNS resolution on Android may use system resolver that ignores /etc/hosts; document this limitation
- Tool binaries may not be installed; check availability at startup and report missing tools
- Large nmap scans can consume significant memory; enforce scan-type restrictions based on device RAM

---

## Phase 5: Terminal UI
**Status:** Blocked by Phases 1–4
**Skills:** 3 (`edge-terminal-ui-parrot`, `edge-workflow-views`, `edge-touch-optimization`)

Deliverables:
- Parrot OS green-on-black terminal aesthetic (colors from research skill)
- Two-line prompt: `┌[host]─[time-date]─[dir]` / `└╼user$`
- Command input with history (up/down arrows), tab completion for commands
- Status bar: model status, active engagement, permit count, evidence entries, RSS usage
- Workflow views: Engagement overview, Active permits, Evidence timeline, Findings list, SOP progress
- Touch-friendly: large tap targets, swipe navigation between views, long-press for context menu
- Single-page app served from Node daemon, no build step required
- Responsive layout: portrait and landscape tablet orientations

Forbidden:
- Rendering raw HTML from model output or tool results (XSS prevention)
- Storing command history in localStorage (use server-side bounded buffer)
- Auto-submitting model proposals without human review
- Blocking the UI thread during model inference

Gate:
- Cold page load in mobile Chrome < 3s over loopback
- Terminal renders correctly in portrait and landscape
- Tab completion works for all registered commands
- Status bar updates in real-time via WebSocket
- XSS payload in tool output renders as escaped text, not executed

Pitfalls:
- Mobile virtual keyboard covers input field; scroll-into-view on focus
- WebSocket disconnect on Android screen lock; implement auto-reconnect with exponential backoff
- CSS grid/flexbox behaves differently in older Samsung Internet versions; test on target browser
- Terminal cursor positioning in a web `<textarea>` vs `<div contenteditable>` has quirks; use textarea
- Long output lines need horizontal scrolling, not word-wrap (terminal convention)

---

## Phase 6: SOP Methodology Engine & Bounty Workflows
**Status:** Blocked by Phases 1–5
**Skills:** 3 (`edge-sop-methodology-compiler`, `edge-bounty-workflow-sops`, `edge-coverage-ledger`)

Deliverables:
- SOP definition format: JSON graph of steps with preconditions, tools, expected outputs
- Methodology compiler: validates SOP graphs, detects cycles, resolves dependencies
- Pre-built bug bounty SOPs: reconnaissance, subdomain enumeration, vulnerability scanning, report writing
- Coverage ledger: tracks which SOP steps have been completed for current engagement
- SOP execution engine: walks the graph, dispatches tool calls, records evidence at each step
- Human-in-the-loop gates: steps marked `approval-required` pause for operator confirmation

Forbidden:
- Auto-executing destructive SOP steps without explicit approval
- Skipping coverage tracking (every step must be recorded)
- Modifying SOP definitions during an active engagement
- Using SOP output as evidence without independent verification

Gate:
- A complete recon SOP runs end-to-end against a local fixture target
- Cycle detection catches circular SOP references at compile time
- Approval-gated steps halt and wait indefinitely until approved or rejected
- Coverage ledger shows exact percentage of methodology completed
- SOP modification during active engagement is rejected

Pitfalls:
- SOP graphs grow complex quickly; keep initial SOPs to ≤ 15 nodes
- Preconditions referencing tool outputs need typed contracts, not string matching
- Timeout handling: what happens if a tool call takes 10 minutes? Must be configurable per step
- Concurrent engagements sharing the same target need separate coverage ledgers
- Report generation depends on evidence ordering; ensure chronological integrity

---

## Phase 7: Security Hardening & Release
**Status:** Blocked by all previous phases
**Skills:** 3 (`edge-ci-cd-edge`, `edge-security-hardening`, `edge-release-packaging`)

Deliverables:
- GitHub Actions CI: lint, unit tests, integration tests (offline), build artifact
- Security audit: `npm audit`, dependency pinning, supply-chain checks
- Penetration testing checklist: XSS, path traversal, command injection, CSRF, privilege escalation
- Release packaging: tarball with checksum, installation script for Termux, model download instructions
- Documentation: user guide, admin guide, threat model, changelog
- Version tagging and release notes

Forbidden:
- Releasing without passing the full test suite
- Shipping with known high-severity dependency vulnerabilities
- Publishing without threat model review
- Claiming "production ready" before live-target validation

Gate:
- CI passes on ubuntu-latest with zero warnings treated as errors
- All identified security findings are triaged and either fixed or documented as accepted risks
- Installation script tested on clean Termux environment
- Threat model reviewed and signed off
- Version tagged with signed git tag

Pitfalls:
- CI cannot test Android-specific behavior; maintain separate on-device test script
- npm packages can be yanked; lock exact versions and vendor critical deps
- Release tarball should not include `.git`, `node_modules`, or test fixtures with sensitive data
- Termux users may have different Node.js versions; document minimum version requirement
