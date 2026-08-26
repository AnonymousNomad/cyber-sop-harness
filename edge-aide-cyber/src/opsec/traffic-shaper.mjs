/**
 * Traffic Shaper — randomizes timing and User-Agent to prevent fingerprinting.
 */

const USER_AGENTS = [
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/125.0.0.0 Safari/537.36",
  "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 Safari/605.1.15",
  "Mozilla/5.0 (X11; Linux x86_64; rv:128.0) Gecko/20100101 Firefox/128.0",
  "Mozilla/5.0 (iPad; CPU OS 17_5 like Mac OS X) AppleWebKit/605.1.15 Safari/605.1.15",
];

export class TrafficShaper {
  #minDelay;
  #maxDelay;
  #burstLimit;
  #recentRequests = [];

  constructor({ minDelayMs = 200, maxDelayMs = 2000, burstLimit = 5 } = {}) {
    this.#minDelay = minDelayMs;
    this.#maxDelay = maxDelayMs;
    this.#burstLimit = burstLimit;
  }

  async throttle() {
    const now = Date.now();
    this.#recentRequests = this.#recentRequests.filter(t => now - t < 60000);
    if (this.#recentRequests.length >= this.#burstLimit) {
      await new Promise(r => setTimeout(r, 3000 + Math.random() * 5000));
    }
    const delay = this.#minDelay + Math.random() * (this.#maxDelay - this.#minDelay);
    await new Promise(r => setTimeout(r, delay));
    this.#recentRequests.push(Date.now());
  }

  getRandomUA() {
    return USER_AGENTS[Math.floor(Math.random() * USER_AGENTS.length)];
  }

  getHeaders() {
    return {
      "User-Agent": this.getRandomUA(),
      "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
      "Accept-Language": "en-US,en;q=0.9",
      "Accept-Encoding": "gzip, deflate, br",
    };
  }
}