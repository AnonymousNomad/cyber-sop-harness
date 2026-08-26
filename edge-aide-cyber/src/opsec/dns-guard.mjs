/**
 * DNS Guard — prevents DNS leaks via DoH or Tor SOCKS5h routing.
 */

export class DNSGuard {
  #mode;
  #dohUrl = "https://dns.google/dns-query";
  #agent;

  constructor({ mode = "doh", agent = null } = {}) {
    this.#mode = mode;
    this.#agent = agent;
  }

  async resolve(hostname, type = "A") {
    if (this.#mode === "doh" || this.#mode === "tor") {
      return this.#resolveDoH(hostname, type);
    }
    const dns = await import("node:dns/promises");
    const fn = type === "MX" ? "resolveMx" : type === "AAAA" ? "resolve6" : "resolve4";
    return dns[fn](hostname);
  }

  async #resolveDoH(hostname, type) {
    const opts = { headers: { Accept: "application/dns-json" }, signal: AbortSignal.timeout(5000) };
    if (this.#agent) opts.agent = this.#agent;
    const res = await fetch(`${this.#dohUrl}?name=${hostname}&type=${type}`, opts);
    const data = await res.json();
    return data.Answer?.map(a => ({ type: a.type, data: a.data, ttl: a.TTL })) || [];
  }

  get mode() { return this.#mode; }
}