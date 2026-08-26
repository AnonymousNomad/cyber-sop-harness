# Edge Security Hardening

## What To Do
Systematically review and harden every attack surface: WebSocket API, file operations, tool execution, model interaction, secret storage. Document findings as accepted risks or fixes.

## Why
This is a security tool. It must demonstrate the same rigor it demands from engagements. A vulnerability in the harness itself undermines all governance guarantees.

## Hardening Checklist
1. **Input validation**: Every WS message field validated with zod schema
2. **Path traversal**: All file paths resolved through boundary jail; test with `../../`, URL-encoded, double-encoded, null bytes
3. **Command injection**: All child process calls use `execFile`, never `exec`
4. **XSS**: All dynamic content rendered via `textContent`, never `innerHTML`
5. **CSRF**: WebSocket connections from non-loopback rejected
6. **ReDoS**: All regex patterns tested with pathological inputs; set length limits before regex
7. **Secret exposure**: Grep codebase for hardcoded credentials; verify sanitizer catches all formats
8. **Dependency audit**: `npm audit --production`; triage all findings
9. **Rate limiting**: Max 3 concurrent WS connections; max 1 MiB message size
10. **Security headers**: CSP, X-Frame-Options, X-Content-Type-Options on HTTP responses

## Threat Matrix
| Attack Vector | Test Method | Expected Result |
|---|---|---|
| Path traversal | `{"path":"../../etc/passwd"}` | Rejected by boundary |
| Command injection | Target with `$(whoami)` | Passed as literal to execFile |
| XSS | Tool output with `<script>` tags | Rendered as escaped text |
| Oversized message | 10 MB JSON over WS | Rejected at maxPayload |
| Nonce reuse in AES-GCM | Inspect vault code | Fresh random IV per operation |

## Dependencies
- Node.js built-in crypto (for AES-GCM verification)
- npm audit (for dependency scanning)

## Pitfalls
- Security review is not one-time; re-run after every new adapter or API route
- Automated scanners miss logic bugs; manual review still necessary
- Test on target browser (Samsung Internet), not just Chrome desktop
