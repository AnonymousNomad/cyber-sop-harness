---
name: cyber-tool-adapter-expansion
description: Guides safe addition of new security tool adapters to the Cyber SOP Harness typed tool registry. Use when adding HTTP methods, DNS inspection, port scanning, content analysis, or any new tool capability that must execute through policy, permit, evidence, and cleanup gates.
---

# Cyber Tool Adapter Expansion

## What

Add new `IToolAdapter` implementations that execute through the full governance pipeline (policy → permit → dispatch → evidence → provenance → verification) without bypassing any safety gate.

## Why

The current single adapter (`HttpHeaderInspectTool`) proves the architecture works but does not prove it scales. Each new adapter is a fresh attack surface and a test of the governance model's expressiveness. If adding an adapter requires weakening safety, the architecture has a design flaw.

## How

### 1. Define the ToolCapabilityManifest

Every adapter requires a manifest declaring:

```csharp
new ToolCapabilityManifest(
    toolRef: "dns.reverse.lookup",        // unique, kebab-case
    toolVersion: "1.0",
    capabilityRef: "dns.reverse.lookup",  // matches CapabilityRegistry entry
    privilegeLevel: "unprivileged",
    readOnly: true,                        // MUST be true for network tools
    networkDestinations: ["https://target.example.com"],
    dataClasses: ["http_metadata"],        // only "http_metadata" or "synthetic"
    requiresContainedWorker: true,
    evidenceRequirements: ["raw", "redacted", "observation"],
    cleanupRequired: true,
    maxDuration: TimeSpan.FromSeconds(15),
    maxOutputBytes: 64 * 1024)
```

Rules:
- `readOnly` must be `true`. Write operations are R3/R4 and require a different execution path.
- `dataClasses` must contain only `"http_metadata"` for network tools.
- `networkDestinations` must be explicit origins, never `"*"` in production.
- `maxDuration` and `maxOutputBytes` are hard limits enforced by the broker.

### 2. Implement IContainedNetworkToolAdapter

```csharp
public sealed class DnsReverseLookupTool : IContainedNetworkToolAdapter, IAsyncDisposable
{
    public string ToolRef => "dns-reverse-lookup";
    public string ToolVersion => "1.0";
    
    public async Task<ToolAdapterResult> ExecuteAsync(
        ToolExecutionContext context, CancellationToken ct)
    {
        // 1. Verify authorization (defense-in-depth)
        NetworkToolGuard.RequireAuthorizedNetworkAction(context, CapabilityRef);
        
        // 2. Extract target from envelope
        var target = context.Envelope.Request.TargetRef;
        
        // 3. Execute bounded operation
        // ... your logic here ...
        
        // 4. Return structured result
        return new ToolAdapterResult(
            Status: ToolResultStatus.Success,
            ExitCode: 0,
            RawOutput: Encoding.UTF8.GetBytes(observation),
            ObservationRefs: ["dns.reverse"],
            ArtifactRefs: Array.Empty<string>(),
            CleanupResult: "PENDING");
    }
}
```

### 3. Register and Freeze

```csharp
var registry = new ToolRegistry();
registry.Register(toolManifest, adapter);
registry.Freeze();
// No more registrations after freeze
```

### 4. Add Capability to Policy Engine

```csharp
var capabilities = new CapabilityRegistry();
capabilities.Register(new CapabilityManifest(
    "dns.reverse.lookup", RiskClass.R1,
    allowedTargets, privilegeLevel, readOnly,
    prohibitedTargets, dataClasses,
    maxDuration, maxOutputBytes, 
    requiresApproval: false, cleanupRequired: true));
capabilities.Freeze();
```

### 5. Write Tests

Minimum test coverage per adapter:
- Happy path executes and records evidence
- Out-of-scope target is blocked before adapter invocation
- Expired permit blocks execution
- Tampered permit blocks execution
- Output exceeds limit truncates safely
- Timeout cancels cleanly with no partial state
- Cleanup succeeds after normal completion
- Cleanup failure marks result as error
- Sensitive data in output is redacted

## Threat Matrix

| Threat | Vector | Mitigation |
|---|---|---|
| DNS rebinding | Adapter resolves hostname, attacker changes DNS between resolve and connect | Use `ConnectCallback` with pinned resolved addresses |
| SSRF via redirect | Target redirects to internal/metadata endpoint | Disable auto-redirects; validate each hop through ScopeEvaluator |
| Response injection | Malicious server returns crafted headers/body that exploit parser | Bound all reads; parse defensively; redact sensitive headers |
| Resource exhaustion | Server sends infinite response body | Enforce `maxOutputBytes`; stream with early termination |
| Credential leak | Adapter accidentally includes auth headers in output | Redact known sensitive headers; scan output for secret patterns |
| Timing attack | Response time reveals internal topology | Do not expose sub-millisecond timing in observation output |
| Model manipulation | Model proposes arguments that alter adapter behavior | Validate all arguments against manifest; ignore unknown keys |

## Dependencies

- `CyberSopHarness.Core.Phase3Contracts` — IToolAdapter, IContainedNetworkToolAdapter, ToolRegistry
- `CyberSopHarness.Core.PolicyEngine` — CapabilityRegistry, PolicyDecision
- `CyberSopHarness.Core.PermitIssuer` — Permit lifecycle
- `CyberSopHarness.Core.Phase3Runtime` — ToolBroker, EvidenceLedger
- `System.Net.Http` or target-specific networking library

## Pitfalls

- Forgetting `IAsyncDisposable`: leaks HttpClient sockets under load
- Using `AllowAutoRedirect = true`: bypasses scope evaluation on redirect targets
- Not calling `NetworkToolGuard.RequireAuthorizedNetworkAction`: relies on broker alone; defense-in-depth exists because bugs happen
- Returning raw server headers without redaction: leaks cookies, auth tokens, internal infrastructure details
- Not bounding output reads: a hostile server can send gigabytes
- Hardcoding timeout instead of using `context.Capability.MaxDuration`
- Not handling `OperationCanceledException` distinctly from other exceptions
- Registering adapter but forgetting to add capability to `CapabilityRegistry`: broker finds tool but policy engine rejects every request

## Debug Guide

If dispatch fails:
1. Check `outcome.FailureReason` for the block reason
2. Verify `policy.Decision == Allow` before permit issuance
3. Verify `issuer.TryConsume()` returned `true` before broker call
4. Check `registration.Manifest.NetworkDestinations` includes the target origin
5. Check adapter implements `IContainedNetworkToolAdapter` (not just `IToolAdapter`)
6. Check `context.Capability.MaxDuration` > actual operation time
7. Enable debug logging on `ToolBroker` if available

## Acceptance Criteria

- New adapter passes all existing tests plus its own adapter-specific tests
- Zero warnings, zero errors in Release build
- Evidence chain verifies after successful execution
- Provenance stamp verifies against engagement manifest
- No secrets appear in raw output, redacted output, or observation refs
- Emergency stop terminates adapter mid-execution within one second
