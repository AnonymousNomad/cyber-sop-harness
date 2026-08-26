# Edge Release Packaging

## What To Do
Create a distributable package: source tarball with checksum, installation script for Termux, model download instructions with pinned hashes, and documentation bundle.

## Why
Users on edge devices need a one-command setup. The installation script handles Node.js checks, dependency installation, directory creation, and initial configuration.

## Installer Script Pattern
```bash
#!/bin/bash
set -euo pipefail
# 1. Check Node.js >= 18
# 2. npm install --production
# 3. Create workspace dirs (~/.edge-cyber/{data,evidence,secrets})
# 4. Check optional tools (nmap, whisper-cli)
# 5. Download model with hash verification
# 6. Print usage instructions
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| MITM during download | Malicious code | HTTPS + SHA-256 verification |
| Model tampered | Poisoned behavior | Pin hash in installer |
| Package includes secrets | Credential leak | .gitignore + exclusion list |

## Dependencies
- curl or wget for model download
- sha256sum for hash verification
- Node.js >= 18

## Pitfalls
- Termux `pkg` may prompt for confirmation; use `-y` flag
- Model URLs may change; pin to specific revision
- Large downloads need resume support (`curl -C -`)
- Script must be idempotent (safe to re-run)
