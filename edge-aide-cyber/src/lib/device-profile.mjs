import os from 'node:os';
import fs from 'node:fs';

export function captureDeviceProfile() {
  const cpus = os.cpus();
  return Object.freeze({
    arch: os.arch(),
    platform: os.platform(),
    totalMemBytes: os.totalmem(),
    freeMemBytes: sampleFreeMem(),
    cpuCount: cpus.length,
    cpuModel: cpus[0]?.model?.trim() || 'unknown',
    cpuSpeedMhz: cpus[0]?.speed || 0,
    hostname: os.hostname(),
    uptimeSeconds: Math.floor(os.uptime()),
    nodeVersion: process.version,
    pid: process.pid,
    allowedCpus: readAllowedCpus(),
  });
}

function sampleFreeMem() {
  const samples = [];
  for (let i = 0; i < 3; i++) {
    samples.push(os.freemem());
    if (i < 2) Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 50);
  }
  return Math.min(...samples);
}

function readAllowedCpus() {
  try {
    return fs.readFileSync('/sys/fs/cgroup/cpuset/cpuset.cpus', 'utf8').trim();
  } catch {
    try {
      return fs.readFileSync('/sys/fs/cgroup/cpuset.cpus.effective', 'utf8').trim();
    } catch {
      return 'unknown';
    }
  }
}
