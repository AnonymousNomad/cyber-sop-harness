# Edge Coverage Ledger

## What To Do
Track which SOP steps have been completed, skipped, or failed for each engagement. Calculate completion percentage. Provide evidence references for each step. Export as human-readable report.

## Why
Professional bug bounty reports require demonstrating thoroughness. "We tested 85% of our methodology" is stronger than "we tested some things." The ledger provides auditable proof of coverage.

## Code Guidance
```javascript
// Each step record:
{ status: 'completed'|'skipped'|'failed'|'blocked',
  evidenceRef: 'hash-chain-entry-id',
  timestamp: ISO8601 }

// Completion rate = (completed + skipped) / totalSteps * 100
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Ledger modified after engagement | False coverage claims | Entries include evidence chain hashes |
| Skipped steps counted as complete | Inflated coverage | Only 'completed' and justified 'skipped' count |
| Evidence reference points to deleted entry | Broken audit trail | Verify evidence exists before recording |

## Dependencies
- Cipher state bus (for persistence)
- Evidence chain (for integrity verification)

## Pitfalls
- Distinguish "not yet attempted" from "attempted but pending"
- Lock SOPs during active engagements to prevent ledger incompatibility
- Export should be both human-readable (markdown) and machine-readable (JSON)
