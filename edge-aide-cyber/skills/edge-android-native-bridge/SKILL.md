# Edge Android Native Bridge

## What To Do
Build a bridge between the Linux container and Android's native tools. Use `am` (Activity Manager) to keep the Linux app in foreground, `termux-api`-style commands for hardware access, and Android intents for clipboard, notifications, and file sharing.

## Why
The Linux container can interact with Android via `am` and `cmd` commands. This bridge enables: keeping the app alive (foreground notification), accessing clipboard, sending notifications, and sharing files between Linux and Android.

## Code Guidance

```javascript
// src/android/native-bridge.mjs
import { execSync, exec } from 'node:child_process';

export class AndroidBridge {
  // Keep app alive by starting a foreground notification
  async keepAlive() {
    try {
      // Start a foreground service to prevent OOM kill
      execSync(
        'am start-foreground-service -n com.sec.android.app.samsunglinux/.ForegroundService',
        { stdio: 'ignore' }
      );
      return true;
    } catch {
      return false;
    }
  }

  // Get clipboard content
  getClipboard() {
    try {
      return execSync('termux-clipboard-get 2>/dev/null || am broadcast -a clipper.get 2>/dev/null')
        .toString().trim();
    } catch { return ''; }
  }

  // Set clipboard content
  setClipboard(text) {
    try {
      execSync(`termux-clipboard-set "${text.replace(/"/g, '\"')}" 2>/dev/null`);
      return true;
    } catch { return false; }
  }

  // Send notification
  notify(title, message) {
    try {
      execSync(
        `termux-notification -t "${title}" -c "${message}" 2>/dev/null || ` +
        `am broadcast -a android.intent.action.SEND -t text/plain --es android.intent.extra.TEXT "${message}" 2>/dev/null`,
        { stdio: 'ignore' }
      );
    } catch {}
  }

  // Vibrate
  vibrate(ms = 200) {
    try {
      execSync(`termux-vibrate -d ${ms} 2>/dev/null || cmd vibrator ${ms} 2>/dev/null`, { stdio: 'ignore' });
    } catch {}
  }

  // Get battery status
  getBattery() {
    try {
      const out = execSync('termux-battery-status 2>/dev/null || dumpsys battery 2>/dev/null').toString();
      const pct = out.match(/level.*?(\d+)/);
      return { level: pct ? parseInt(pct[1]) : -1, raw: out };
    } catch { return { level: -1 }; }
  }

  // Check if termux-api is available
  hasTermuxAPI() {
    try {
      execSync('which termux-clipboard-get', { stdio: 'ignore' });
      return true;
    } catch { return false; }
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| `am` command fails | Cannot keep alive | Fallback to heartbeat file |
| Clipboard contains sensitive data | Data leak | Sanitize before storing |
| Notification spam | User annoyed | Rate limit notifications |

## Dependencies
- `am` command (available)
- `termux-api` package (optional, for full hardware access)
- Android app context

## Pitfalls
- `am start-foreground-service` needs the exact package name
- Samsung Linux package name may vary by OneUI version
- `termux-api` must be installed separately in the Linux environment
- Battery status via `dumpsys` may be denied by seccomp
- Notifications may not appear if the app is fully killed