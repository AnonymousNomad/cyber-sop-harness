# Edge Tool Adapter Framework

## What To Do
Create a typed adapter registry where each security tool is wrapped in a standardized interface. Adapters are frozen at startup, enforce scope independently, and produce structured results.

## Why
The adapter pattern ensures every tool call goes through the same safety pipeline: policy check → permit verification → independent scope recheck → execution → output sanitization → evidence recording. No direct shell access from the model.

## Code Guidance
```javascript
// src/tools/registry.mjs
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

export function createAdapterRegistry(adapters) {
  const frozen = Object.freeze(new Map(adapters.map(a => [a.name, a])));

  return {
    get(name) { return frozen.get(name) || null; },
    list() { return [...frozen.values()].map(a => ({ name: a.name, capability: a.capability, riskLevel: a.riskLevel })); },
    has(name) { return frozen.has(name); },
    get size() { return frozen.size; },
  };
}

export function defineAdapter({ name, capability, riskLevel, binaryPath, buildArgs, parseOutput, maxOutputBytes = 65536 }) {
  return {
    name,
    capability,
    riskLevel,
    async execute(params, permit, scopeEvaluator) {
      // Independent scope check (defense in depth)
      const scopeCheck = scopeEvaluator(params.target);
      if (!scopeCheck.allowed) {
        return { ok: false, error: 'SCOPE_VIOLATION', detail: scopeCheck.reason };
      }

      const args = buildArgs(params);
      try {
        const { stdout, stderr } = await execFileAsync(binaryPath, args, {
          timeout: 30000,
          maxBuffer: maxOutputBytes,
          env: { ...process.env, PATH: process.env.PATH },
        });
        const parsed = parseOutput(stdout);
        return { ok: true, data: parsed, raw: stdout.slice(0, maxOutputBytes) };
      } catch (err) {
        if (err.killed) return { ok: false, error: 'TIMEOUT' };
        if (err.code === 'ENOENT') return { ok: false, error: 'BINARY_NOT_FOUND', detail: binaryPath };
        return { ok: false, error: 'EXECUTION_ERROR', detail: err.message };
      }
    },
  };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Command injection via parameters | Arbitrary code execution | Always use `execFile`, never `exec` |
| Dynamic adapter registration at runtime | Unauthorized tool injected | Registry frozen with Object.freeze |
| Adapter returns unbounded output | Memory exhaustion on edge | maxBuffer + explicit truncation |
| Binary path tampered between startup and execution | Wrong binary runs | Resolve path at init; store absolute path |
| Adapter crashes and takes down daemon | Loss of governance | Wrap in try/catch; return structured error |

## Dependencies
- Node.js built-in `child_process`

## Pitfalls & Bugs
- `execFile` doesn't invoke a shell, which prevents injection but also means no pipes or redirects. Chain tools via separate calls.
- Some tools write to stderr even on success; don't treat non-empty stderr as an error.
- `maxBuffer` limits apply to stdout+stderr combined; set generously but not unlimited.
- On Termux, some binaries are at `/data/data/com.termux/files/usr/bin/` not `/usr/bin/`. Use `which` to resolve.
- Timeout kills the process but may leave zombie processes; check for this in testing.
