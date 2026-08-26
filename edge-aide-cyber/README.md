# Edge AIDE Cybersecurity Workbench

**A governed AI-assisted cybersecurity operations terminal for edge devices.**

Runs on Android tablets (Termux), iPads (a-Shell), and low-resource Linux devices. Combines AIDE Sovereign Workbench's harness architecture with Cyber SOP Harness's governance model into a single-process Node.js server with a Parrot OS-style terminal interface.

**Not a port. Not a fork. A translation of the same architecture to edge constraints.**

## What It Does

- **Governed tool dispatch** — every tool action goes through policy engine → permit issuer → evidence chain
- **SOP methodology engine** — executable bug bounty workflows with coverage tracking
- **Local + remote model support** — LFM2.5 on-device, Cohere North Mini Code via LAN
- **Evidence journal** — SHA-256 hash-chained JSONL, tamper-detectable, replayable
- **Auto-debug** — file watcher with syntax detection and automatic fix suggestions
- **Voice assistant** — wake word detection with STT/TTS (optional)

## Quick Start

```bash
# On Termux
pkg install nodejs git
bash <(curl -s https://raw.githubusercontent.com/AnonymousNomad/edge-aide-cyber/main/install.sh)

# Or clone manually
git clone https://github.com/AnonymousNomad/edge-aide-cyber
cd edge-aide-cyber
npm install
npm start

# Open in browser
# http://127.0.0.1:7420
```

## Architecture

```
Browser (Chrome/Samsung Internet)
  Terminal UI · Workflow Views · Dashboard
      ↕ HTTP/WS on 127.0.0.1:7420
Node.js Daemon (single process)
  ├── Terminal Router (command parser, SOP router, tool dispatch)
  ├── Governance Core (policy engine, permits, scope, evidence, secrets)
  ├── Model Layer (llama.cpp HTTP, context manager, cipher state bus)
  ├── Tool Adapters (dns.reverse, http.headers, nmap, nuclei, ffuf, httpx)
  └── Auto-Debug (file watcher, syntax detector, auto-fixer)
```

## Commands

| Command | Description |
|---|---|
| `/help` | Show all commands |
| `/status` | System status |
| `/device` | Device profile |
| `/model status` | Check llama.cpp connection |
| `/model pin <path> <sha256>` | Pin model file with hash verification |
| `/ask <query>` | Ask the model a question |
| `/tools` | List available tool adapters |
| `/tool <name> <target>` | Execute governed tool |
| `/sop list` | List available SOPs |
| `/sop load <id>` | Load an SOP methodology |
| `/sop run` | Execute next SOP step |
| `/sop approve` | Approve pending SOP step |
| `/sop status` | Show SOP progress |
| `/engage load [path]` | Load engagement.json |
| `/engage status` | Show current engagement |
| `/finding add <title>` | Record a finding |
| `/finding list` | List all findings |
| `/autodebug status` | Debugger status |
| `/autodebug auto on` | Enable auto-fix |
| `/voice` | Voice capabilities |
| `/history` | Command history |
| `/clear` | Clear terminal |

## Governance Flow

```
/tool <name> <target>
  → policy engine evaluates against engagement manifest
    → DENY: reason + evidence record
    → ALLOW: permit issued (one-use, 30s TTL) → adapter executes → output sanitized → evidence chain records
```

Without an `engagement.json`, all tool actions are denied (fail-closed).

## Environment Variables

| Variable | Purpose | Default |
|---|---|---|
| `PORT` | Server port | `7420` |
| `LLAMA_HOST` | Local llama.cpp URL | `http://127.0.0.1:8081` |
| `REMOTE_MODEL_HOST` | Laptop running North Mini Code | — |
| `REMOTE_MODEL_NAME` | Model name for remote | `north-mini-code` |
| `REMOTE_MODEL_KEY` | API key for remote | — |
| `VAULT_PASSPHRASE` | Enables encrypted secret storage | — |

## Model Strategy

| Role | Model | Size | Runs On |
|---|---|---|---|
| Local edge | LFM2.5-1.2B-Thinking Q4_K_M | 698 MB | Tablet |
| Remote gateway | Cohere North Mini Code 30.5B MoE Q4_K_M | ~18 GB | Laptop |

## Testing

```bash
npm test          # Run all 142 tests (offline, deterministic)
npm run lint      # Lint (placeholder)
```

## Project Structure

```
edge-aide-cyber/
├── src/
│   ├── server.mjs              # Main daemon
│   ├── lib/
│   │   ├── device-profile.mjs  # Hardware detection
│   │   ├── file-boundary.mjs   # Workspace jail
│   │   ├── ws-protocol.mjs     # WebSocket protocol
│   │   └── security-audit.mjs  # Pre-flight checks
│   ├── model/
│   │   ├── provider.mjs        # llama.cpp HTTP client
│   │   ├── context-manager.mjs # Token budget
│   │   ├── cipher-state.mjs    # Append-only event log
│   │   └── voice-assistant.mjs # STT/TTS
│   ├── governance/
│   │   ├── policy-engine.mjs   # Action evaluation
│   │   ├── permit-issuer.mjs   # One-use permits
│   │   ├── scope-evaluator.mjs # CIDR/domain/URL matching
│   │   ├── evidence-chain.mjs  # SHA-256 hash chain
│   │   └── secret-vault.mjs    # AES-256-GCM encryption
│   ├── tools/
│   │   ├── registry.mjs        # Adapter registry
│   │   ├── sanitizer.mjs       # Output sanitization
│   │   └── adapters/           # dns.reverse, http.headers
│   ├── sop/
│   │   ├── compiler.mjs        # SOP DAG validation
│   │   └── coverage.mjs        # Step completion tracking
│   └── autodebug/
│       ├── watcher.mjs         # File change detection
│       ├── detector.mjs        # Syntax analysis
│       ├── fixer.mjs           # Auto-fix suggestions
│       └── notifier.mjs        # UI notifications
├── sops/
│   └── recon-basic.sop.json    # Built-in recon SOP
├── public/
│   └── index.html              # Terminal UI (Parrot OS aesthetic)
├── tests/                      # 142 deterministic tests
├── skills/                     # 29 development skills
├── install.sh                  # Termux installer
├── ARCHITECTURE.md             # Integration architecture
├── ROADMAP.md                  # Phased development plan
└── package.json
```

## Security

- **Fail-closed by default**: no engagement manifest = all tool actions denied
- **Loopback only**: server binds to 127.0.0.1, never 0.0.0.0
- **One-use permits**: each tool action requires a fresh, expiring permit
- **Evidence chain**: every action recorded with SHA-256 hash chain
- **Workspace jail**: all file operations confined to project root
- **Model verification**: SHA-256 hash pinning before model load
- **Output sanitization**: all tool output sanitized before display

## License

MIT — Authorized defensive testing only.
