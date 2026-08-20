# Initial State Model

Status: Phase 1 contract

## States

| State | Meaning |
|---|---|
| `READY` | Valid engagement and policy context loaded; no action proposed |
| `PLANNED` | A methodology node and objective are selected |
| `PROPOSED` | Model emitted a typed action proposal |
| `ALLOWED` | External policy issued a valid permit |
| `RUNNING` | Worker is executing the permitted action |
| `STOPPING` | Local watchdog or operator is terminating active work and preserving evidence |
| `OBSERVED` | Worker result exists and is hash-linked |
| `VERIFIED` | Independent verifier confirmed the relevant observation |
| `REPORTABLE` | Scope, evidence, impact, limitations, and reporting requirements are complete |
| `BLOCKED` | Policy or authorization denied the transition |
| `UNKNOWN` | Result or interpretation is incomplete or ambiguous |
| `STOPPED` | Run is halted and no new permits may be issued |

## Allowed Transitions

```text
READY -> PLANNED
PLANNED -> PROPOSED
PROPOSED -> ALLOWED
PROPOSED -> BLOCKED
ALLOWED -> RUNNING
ALLOWED -> STOPPED
RUNNING -> OBSERVED
RUNNING -> UNKNOWN
RUNNING -> STOPPED
RUNNING -> STOPPING
STOPPING -> STOPPED
OBSERVED -> VERIFIED
OBSERVED -> UNKNOWN
VERIFIED -> REPORTABLE
VERIFIED -> PLANNED
UNKNOWN -> PLANNED
UNKNOWN -> STOPPED
BLOCKED -> PLANNED
BLOCKED -> STOPPED
```

## Invariants

1. Every action transition has a policy decision event; only `ALLOW` may produce a permit.
2. `RUNNING` requires a valid, unexpired permit.
3. `OBSERVED` requires a captured result event.
4. `VERIFIED` requires an independent verifier event and a verification event linked to the result.
5. `REPORTABLE` requires `VERIFIED` evidence, complete scope metadata, and a report-policy decision event.
6. Bookkeeping transitions such as `OBSERVED -> VERIFIED` and `VERIFIED -> REPORTABLE` require their own immutable event; they do not bypass policy or verification.
7. `UNKNOWN`, `BLOCKED`, and `STOPPED` states cannot be silently treated as success.
8. Any hash mismatch transitions the run to `STOPPED`.
9. A model cannot directly set a host state.

## Finding States

Finding lifecycle states are separate from execution states:

- `HYPOTHESIS`
- `CANDIDATE`
- `REPRODUCIBLE`
- `VERIFIED`
- `REPORTABLE`
- `UNVERIFIED`
- `REJECTED`
- `UNKNOWN`
- `BLOCKED`

A finding state change must reference the execution event, evidence event, verifier event, and policy/report event that justify it.

Finding transitions:

```text
HYPOTHESIS -> CANDIDATE
CANDIDATE -> REPRODUCIBLE
CANDIDATE -> UNKNOWN
CANDIDATE -> REJECTED
REPRODUCIBLE -> VERIFIED
REPRODUCIBLE -> UNKNOWN
REPRODUCIBLE -> BLOCKED
VERIFIED -> REPORTABLE
VERIFIED -> REJECTED
UNVERIFIED -> UNKNOWN
UNVERIFIED -> BLOCKED
UNVERIFIED -> REJECTED
```

`UNKNOWN` means evidence or reproduction is incomplete. `BLOCKED` means policy, authorization, safety, or approval prevented verification. Neither state is a negative finding and neither may be reported as verified.
