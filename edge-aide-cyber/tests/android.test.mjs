import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { DeviceEnvironment } from "../src/android/device-environment.mjs";
import { MemoryGuardian } from "../src/android/memory-guardian.mjs";
import { KernelTuner } from "../src/android/kernel-tuner.mjs";
import { ProcessPriority } from "../src/android/process-priority.mjs";
import { SignalHandler } from "../src/android/signal-handler.mjs";
import { StorageOptimizer } from "../src/android/storage-optimizer.mjs";
import { AutoRecovery } from "../src/android/auto-recovery.mjs";

describe("device environment", () => {
  it("detects device profile", async () => {
    const env = new DeviceEnvironment();
    const profile = await env.detect();
    assert.ok(profile.kernel);
    assert.ok(profile.arch);
    assert.ok(profile.cpuCount > 0);
    assert.ok(profile.totalMemKB > 0);
    assert.ok(typeof profile.nodeVersion === "string");
  });

  it("summarizes profile", async () => {
    const env = new DeviceEnvironment();
    await env.detect();
    const summary = env.summarize();
    assert.ok(summary.includes("Kernel:"));
    assert.ok(summary.includes("RAM:"));
  });
});

describe("memory guardian", () => {
  it("reads memory status", () => {
    const guardian = new MemoryGuardian();
    const status = guardian.getMemoryStatus();
    assert.ok(status.totalMB > 0);
    assert.ok(status.freeMB >= 0);
    assert.ok(status.availableMB >= 0);
    assert.ok(typeof status.pressure === "string");
  });

  it("fires warning callback on low memory", () => {
    return new Promise((resolve) => {
      const guardian = new MemoryGuardian({ thresholdMB: 999999, checkIntervalMs: 50 });
      let called = false;
      guardian.start((event) => {
        if (called) return;
        called = true;
        assert.equal(event.level, "warning");
        guardian.stop();
        resolve();
      });
    });
  });

  it("starts and stops cleanly", () => {
    const guardian = new MemoryGuardian({ checkIntervalMs: 100 });
    guardian.start(() => {});
    const status = guardian.getStatus();
    assert.ok(status);
    guardian.stop();
  });
});

describe("kernel tuner", () => {
  it("applies balanced profile", () => {
    const tuner = new KernelTuner();
    const results = tuner.tune("balanced");
    assert.ok(Array.isArray(results));
    assert.ok(results.length > 0);
    results.forEach(r => {
      assert.ok(typeof r.path === "string");
      assert.ok(typeof r.value === "string");
      assert.ok(typeof r.ok === "boolean");
    });
  });

  it("reports active profile", () => {
    const tuner = new KernelTuner();
    tuner.tune("memory_saver");
    assert.equal(tuner.activeProfile, "memory_saver");
  });

  it("gets status of applied params", () => {
    const tuner = new KernelTuner();
    tuner.tune("balanced");
    const status = tuner.getStatus();
    assert.ok(typeof status === "object");
  });
});

describe("process priority", () => {
  it("sets high priority", () => {
    const pp = new ProcessPriority();
    const result = pp.setHighPriority();
    assert.ok(typeof result === "boolean");
  });

  it("pins to performance cores", () => {
    const pp = new ProcessPriority();
    const result = pp.pinToPerformanceCores();
    assert.ok(typeof result === "boolean");
  });

  it("optimizes all settings", () => {
    const pp = new ProcessPriority();
    const results = pp.optimizeAll();
    assert.ok(typeof results.nice === "boolean");
    assert.ok(typeof results.cpuAffinity === "boolean");
    assert.ok(typeof results.ioPriority === "boolean");
  });
});

describe("signal handler", () => {
  it("installs without error", () => {
    const handler = new SignalHandler(() => {});
    handler.install();
    assert.ok(true);
  });

  it("registers custom handlers", () => {
    const handler = new SignalHandler(() => {});
    handler.install();
    handler.on("reinit", () => {});
    handler.on("reload", () => {});
    assert.ok(true);
  });
});

describe("storage optimizer", () => {
  it("gets disk usage", async () => {
    const optimizer = new StorageOptimizer("/tmp");
    const usage = await optimizer.getDiskUsage();
    assert.ok(usage.totalMB > 0);
    assert.ok(typeof usage.percentUsed === "number");
  });

  it("cleans without error on empty dir", async () => {
    const optimizer = new StorageOptimizer("/tmp/nonexistent");
    const actions = await optimizer.cleanup();
    assert.ok(Array.isArray(actions));
  });
});

describe("auto recovery", () => {
  it("returns null when no checkpoints exist", async () => {
    const recovery = new AutoRecovery("/tmp/nonexistent-checkpoints");
    const result = await recovery.attemptRecovery();
    assert.equal(result, null);
  });

  it("reports recovery count", () => {
    const recovery = new AutoRecovery("/tmp");
    assert.equal(recovery.recoveryCount, 0);
  });
});