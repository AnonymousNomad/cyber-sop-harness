# Edge VPN Integration

## What To Do
Integrate VPN as an alternative anonymization layer. Support WireGuard and OpenVPN configurations for operators who prefer VPN over Tor for lower latency.

## Why
Tor adds significant latency (1-3s per request). For time-sensitive scanning (e.g., race conditions, token replay), a VPN provides faster anonymization with acceptable trade-offs.

## Code Guidance
```javascript
// src/opsec/vpn-manager.mjs
export class VPNManager {
  async connect(configPath) { /* start wireguard/openvpn */ }
  async disconnect() { /* stop VPN */ }
  async checkIP() { /* verify IP changed */ }
  getStatus() { return { connected, ip, interface }; }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| VPN kill switch not enabled | Traffic leaks on disconnect | Enable kill switch in config |
| VPN logs operator IP | Deanonymization | Use no-log VPN provider |
| DNS leak over VPN | ISP sees queried domains | Route DNS through VPN tunnel |

## Dependencies
- WireGuard or OpenVPN installed on device
- VPN configuration file from provider

## Pitfalls
- Termux may not support WireGuard kernel module — use userspace implementation
- VPN connection may drop on WiFi switch — implement auto-reconnect
- Some VPN providers block port scanning — test compatibility