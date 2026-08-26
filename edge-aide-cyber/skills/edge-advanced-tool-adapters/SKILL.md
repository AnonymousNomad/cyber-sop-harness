# Edge Advanced Tool Adapters

## What To Do
Implement typed tool adapters for common bug bounty and penetration testing tools. Each adapter wraps a CLI tool with the governance layer (policy engine -> permit -> execute -> sanitize -> evidence).

## Why
The current tool registry only has `dns.reverse` and `http.headers`. Professional bug bounty work requires port scanning, vulnerability scanning, fuzzing, subdomain enumeration, and technology fingerprinting.

## Adapters To Implement

### nmap.scan (R2)
Network port scanner. Use `spawn('nmap', ['-sT', '-oX', '-', target])` with 120s timeout. Parse XML output for structured host/port data.

### nuclei.scan (R2)
Template-based vulnerability scanner. Use `spawn('nuclei', ['-u', target, '-jsonl', '-silent'])` with 300s timeout. Parse JSONL output for findings.

### ffuf.fuzz (R2)
Web content/directory fuzzer. Use `spawn('ffuf', ['-u', target, '-w', wordlist, '-o', '/dev/stdout', '-of', 'json', '-s'])`.

### httpx.probe (R1)
HTTP technology fingerprinting. Use `spawn('httpx', ['-json', '-silent', '-tech-detect'])` with stdin input.

### subfinder.enum (R1)
Subdomain enumeration. Use `spawn('subfinder', ['-d', domain, '-silent', '-json'])` with 120s timeout.

### whois.lookup (R1)
Domain registration info. Use `spawn('whois', [domain])` with 30s timeout.

### curl.inspect (R1)
Generic HTTP inspection. Use `spawn('curl', ['-sI', '-o', '/dev/stdout', url])` with 15s timeout.

## Threat Matrix

| Threat | Impact | Mitigation |
|---|---|---|
| Command injection via target | RCE | Use spawn with args array, never shell |
| Tool not installed | Crash | Check `which` before registration |
| Tool output exceeds memory | OOM | Stream + truncate at 64KB |
| Tool hangs | Server freeze | Timeout per tool (60-300s) |

## Dependencies
- nmap, nuclei, ffuf, httpx, subfinder, whois, curl must be installed
- Termux: `pkg install nmap curl whois` + Go tools via `go install`

## Pitfalls
- Always use `spawn` not `exec` to prevent shell injection
- nmap SYN scan (-sS) requires root; use -sT for unprivileged
- nuclei needs template downloads; ffuf needs wordlists
- All tool output must pass through output sanitizer
