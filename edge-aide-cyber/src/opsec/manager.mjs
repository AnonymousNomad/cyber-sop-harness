/**
 * OpSec Manager — central anonymity and security controller.
 * Routes all outbound traffic through Tor/VPN/proxy layers.
 */

export class OpSecManager {
  #config = {
    mode: 'off',
    torSocksPort: 9050,
    dnsOverTor: true,
    blockWebRTC: true,
    trafficShaping: true,
    minDelayMs: 200,
    maxDelayMs: 2000,
    exfilGuard: true,
  };

  #torAgent = null;
  #connected = false;

  async init(config = {}) {
    Object.assign(this.#config, config);

    if (this.#config.mode === 'tor') {
      await this.#initTor();
    }

    console.log(`  opsec: ${this.#config.mode} mode`);
    return this;
  }

  async #initTor() {
    try {
      const { SocksProxyAgent } = await import('socks-proxy-agent');
      this.#torAgent = new SocksProxyAgent(`socks5h://127.0.0.1:${this.#config.torSocksPort}`);

      const res = await fetch('https://check.torproject.org/api/ip', {
        // @ts-ignore
        agent: this.#torAgent,
        signal: AbortSignal.timeout(10000),
      });
      const data = await res.json();
      this.#connected = true;
      console.log(`  tor: connected via ${data.IP}`);
    } catch (err) {
      console.log(`  tor: not available (${err.message})`);
      this.#connected = false;
    }
  }

  getAgent() { return this.#torAgent; }

  getFetchOptions(opts = {}) {
    if (this.#torAgent) opts.agent = this.#torAgent;
    return opts;
  }

  async shapedDelay() {
    if (!this.#config.trafficShaping) return;
    const delay = this.#config.minDelayMs +
      Math.random() * (this.#config.maxDelayMs - this.#config.minDelayMs);
    await new Promise(r => setTimeout(r, delay));
  }

  getStatus() {
    return {
      mode: this.#config.mode,
      connected: this.#connected,
      dnsOverTor: this.#config.dnsOverTor,
      trafficShaping: this.#config.trafficShaping,
      exfilGuard: this.#config.exfilGuard,
    };
  }
}