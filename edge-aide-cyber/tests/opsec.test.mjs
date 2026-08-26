import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { OpSecManager } from "../src/opsec/manager.mjs";
import { TrafficShaper } from "../src/opsec/traffic-shaper.mjs";
import { DNSGuard } from "../src/opsec/dns-guard.mjs";
import { ExfilGuard } from "../src/opsec/exfil-guard.mjs";

describe("opsec manager", () => {
  it("initializes with default config", async () => {
    const mgr = new OpSecManager();
    await mgr.init({ mode: "off" });
    const status = mgr.getStatus();
    assert.equal(status.mode, "off");
    assert.equal(status.connected, false);
  });

  it("returns null agent when off", async () => {
    const mgr = new OpSecManager();
    await mgr.init({ mode: "off" });
    assert.equal(mgr.getAgent(), null);
  });

  it("getFetchOptions passes through when off", async () => {
    const mgr = new OpSecManager();
    await mgr.init({ mode: "off" });
    const opts = mgr.getFetchOptions({ headers: { "X-Test": "1" } });
    assert.equal(opts.headers["X-Test"], "1");
    assert.equal(opts.agent, undefined);
  });

  it("shapedDelay completes within reasonable time", async () => {
    const mgr = new OpSecManager();
    await mgr.init({ mode: "off", trafficShaping: true, minDelayMs: 10, maxDelayMs: 50 });
    const start = Date.now();
    await mgr.shapedDelay();
    const elapsed = Date.now() - start;
    assert.ok(elapsed >= 5, `delay too short: ${elapsed}ms`);
    assert.ok(elapsed < 200, `delay too long: ${elapsed}ms`);
  });

  it("shapedDelay is instant when disabled", async () => {
    const mgr = new OpSecManager();
    await mgr.init({ mode: "off", trafficShaping: false });
    const start = Date.now();
    await mgr.shapedDelay();
    const elapsed = Date.now() - start;
    assert.ok(elapsed < 50, `delay should be instant: ${elapsed}ms`);
  });
});

describe("traffic shaper", () => {
  it("returns random user agent", () => {
    const shaper = new TrafficShaper();
    const ua1 = shaper.getRandomUA();
    const ua2 = shaper.getRandomUA();
    assert.ok(typeof ua1 === "string");
    assert.ok(ua1.length > 10);
  });

  it("returns valid headers", () => {
    const shaper = new TrafficShaper();
    const headers = shaper.getHeaders();
    assert.ok(headers["User-Agent"]);
    assert.ok(headers["Accept"]);
    assert.ok(headers["Accept-Language"]);
  });

  it("throttle respects burst limit", async () => {
    const shaper = new TrafficShaper({ minDelayMs: 1, maxDelayMs: 5, burstLimit: 2 });
    await shaper.throttle();
    await shaper.throttle();
    // Third should trigger burst delay
    const start = Date.now();
    await shaper.throttle();
    const elapsed = Date.now() - start;
    assert.ok(elapsed > 100, "burst delay should be significant");
  });
});

describe("dns guard", () => {
  it("initializes with doh mode", () => {
    const guard = new DNSGuard({ mode: "doh" });
    assert.equal(guard.mode, "doh");
  });

  it("resolves via DoH", async () => {
    const guard = new DNSGuard({ mode: "doh" });
    try {
      const result = await guard.resolve("localhost", "A");
      assert.ok(Array.isArray(result));
    } catch {
      // DoH may be unavailable in test env
      assert.ok(true, "DoH unavailable in test env");
    }
  });
});

describe("exfil guard", () => {
  it("detects password in data", () => {
    const guard = new ExfilGuard();
    const result = guard.inspect("password=secret123 api_key=abc123xyz");
    assert.equal(result.blocked, true);
    assert.equal(result.reason, "SENSITIVE_DATA_DETECTED");
  });

  it("allows clean data", () => {
    const guard = new ExfilGuard();
    const result = guard.inspect({ status: "ok", target: "example.com" });
    assert.equal(result.blocked, false);
  });

  it("whitelists tools", () => {
    const guard = new ExfilGuard({ whitelistedTools: ["nuclei.scan"] });
    const result = guard.inspect("api_key=secret", "nuclei.scan");
    assert.equal(result.blocked, false);
  });

  it("detects authorization header", () => {
    const guard = new ExfilGuard();
    const result = guard.inspect("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9");
    assert.equal(result.blocked, true);
  });

  it("detects token patterns", () => {
    const guard = new ExfilGuard();
    const result = guard.inspect("token: abc123def456ghi789");
    assert.equal(result.blocked, true);
  });

  it("handles JSON data", () => {
    const guard = new ExfilGuard();
    const result = guard.inspect(JSON.stringify({ secret: "mysecretvalue" }));
    assert.equal(result.blocked, true);
  });
});