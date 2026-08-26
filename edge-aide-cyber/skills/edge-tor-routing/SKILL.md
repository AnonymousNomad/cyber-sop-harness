# Edge Tor Routing Integration

## What To Do
Integrate Tor as the primary anonymization layer for all outbound network operations. Manage Tor daemon lifecycle (start/stop/restart), circuit refresh, and connection health monitoring.

## Why
Tor provides the strongest anonymization for network operations by routing traffic through multiple encrypted relays. Bug bounty testers need this to prevent IP attribution during reconnaissance and vulnerability scanning phases.

## Code Guidance

```javascript
// src/opsec/tor-manager.mjs
import { spawn, exec } from 'node:child_process';
import { promisify } from 'node:util';
import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

const execAsync = promisify(exec);

export class TorManager {
  #process = null;
  #dataDir;
  #socksPort;
  #controlPort;
  #healthCheckInterval;

  constructor({ dataDir, socksPort = 9050, controlPort = 9051 }) {
    this.#dataDir = dataDir;
    this.#socksPort = socksPort;
    this.#controlPort = controlPort;
  }

  async start() {
    // Write minimal torrc
    const torrc = `
SocksPort ${this.#socksPort}
ControlPort ${this.#controlPort}
DataDirectory ${join(this.#dataDir, 'tor-data')}
Log notice stdout
RunAsDaemon 0
`;

    await writeFile(join(this.#dataDir, 'torrc'), torrc.trim());
    await execAsync(`mkdir -p ${join(this.#dataDir, 'tor-data')}`);

    this.#process = spawn('tor', ['-f', join(this.#dataDir, 'torrc')], {
      stdio: ['ignore', 'pipe', 'pipe'],
    });

    // Wait for bootstrap
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error('Tor bootstrap timeout')), 60000);
      this.#process.stdout.on('data', (data) => {
        const line = data.toString();
        if (line.includes('Bootstrapped 100%')) {
          clearTimeout(timeout);
          resolve();
        }
      });
      this.#process.on('error', (err) => {
        clearTimeout(timeout);
        reject(err);
      });
    });
  }

  async stop() {
    if (this.#process) {
      this.#process.kill('SIGTERM');
      this.#process = null;
    }
  }

  async refreshCircuit() {
    // Send NEWNYM signal to Tor control port
    // Requires cookie authentication or password
    try {
      await execAsync(`echo 'SIGNAL NEWNYM' | nc 127.0.0.1 ${this.#controlPort}`);
      return true;
    } catch {
      return false;
    }
  }

  async checkHealth() {
    try {
      const res = await fetch('https://check.torproject.org/api/ip', {
        agent: new (await import('socks-proxy-agent')).SocksProxyAgent(
          `socks5h://127.0.0.1:${this.#socksPort}`
        ),
        signal: AbortSignal.timeout(10000),
      });
      const data = await res.json();
      return { connected: true, ip: data.IP, isTor: data.IsTor };
    } catch {
      return { connected: false, ip: null, isTor: false };
    }
  }

  get socksPort() { return this.#socksPort; }
  get isRunning() { return this.#process !== null; }
}
```

## Threat Matrix

| Threat | Impact | Mitigation |
|---|---|---|
| Tor daemon crashes mid-operation | Traffic leaks to clearnet | Health check interval + auto-restart |
| Bootstrap timeout | Tool execution blocked | 60s timeout with clear error message |
| Tor data directory permissions | Daemon fails to start | chmod 700 on data dir |
| Circuit too old, fingerprintable | Timing correlation possible | Auto-refresh circuit every 10min |
| Control port unauthenticated | Anyone can manipulate Tor | Bind to 127.0.0.1 only, cookie auth |

## Dependencies
- Tor daemon (`pkg install tor` in Termux)
- `socks-proxy-agent` npm package
- Port 9050 available (SOCKS) and 9051 (control)
- File system write access for torrc and data directory

## Pitfalls & Bugs
- Termux Tor package may be outdated; verify version
- Android may kill background Tor process; implement PID file + watchdog
- Tor bootstrap can take 30-60s on first start; show progress to user
- `SIGNAL NEWNYM` requires control port authentication; may need cookie auth setup
- Some countries block Tor bridges; may need obfs4 bridges config
- Tor exit nodes change; re-check IP after circuit refresh
- Do NOT route model API keys through Tor — use direct connection for local inference
