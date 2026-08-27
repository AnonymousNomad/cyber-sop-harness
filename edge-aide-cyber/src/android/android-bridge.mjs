/**
 * Android Bridge — interfaces with Android tools from the Linux container.
 */

import { execSync } from "node:child_process";

export class AndroidBridge {
  getClipboard() {
    try {
      return execSync("termux-clipboard-get 2>/dev/null", { timeout: 3000 }).toString().trim();
    } catch { return ""; }
  }

  setClipboard(text) {
    try {
      execSync(`termux-clipboard-set "${text.replace(/"/g, '\\"')}" 2>/dev/null`, { timeout: 3000 });
      return true;
    } catch { return false; }
  }

  notify(title, message) {
    try {
      execSync(`termux-notification -t "${title}" -c "${message}" 2>/dev/null`, { timeout: 3000, stdio: "ignore" });
    } catch {}
  }

  vibrate(ms = 200) {
    try {
      execSync(`termux-vibrate -d ${ms} 2>/dev/null`, { timeout: 3000, stdio: "ignore" });
    } catch {}
  }

  getBattery() {
    try {
      const out = execSync("dumpsys battery 2>/dev/null", { timeout: 5000 }).toString();
      const level = out.match(/level.*?(\d+)/);
      return { level: level ? parseInt(level[1]) : -1 };
    } catch { return { level: -1 }; }
  }

  hasTermuxAPI() {
    try { execSync("which termux-clipboard-get", { stdio: "ignore", timeout: 3000 }); return true; }
    catch { return false; }
  }
}