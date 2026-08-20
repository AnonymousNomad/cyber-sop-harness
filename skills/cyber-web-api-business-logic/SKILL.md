---
name: cyber-web-api-business-logic
description: Defines the first safe methodology profile for authorized web and API bug-bounty testing, including reconnaissance, mapping, authentication, authorization, input handling, workflow modeling, business logic, evidence, and reporting. Use only with an approved scope and rules of engagement.
---

# Cyber Web API and Business Logic Profile

## Engagement Start

Require written authorization or valid program policy, exact target scope, out-of-scope assets, test accounts, rate limits, data rules, prohibited methods, stop contact, and reporting channel.

If any item is missing, stop.

## Workflow

1. Load and validate scope.
2. Build an asset and endpoint inventory.
3. Establish a low-impact baseline.
4. Map authentication and roles.
5. Map legitimate user workflows.
6. Select one hypothesis at a time.
7. Execute only permitted actions.
8. Capture raw request and response evidence.
9. Validate safely and independently.
10. Record coverage and limitations.
11. Clean up.
12. Draft the report only from verified evidence.

## Business Logic

Require a workflow model containing roles, states, valid transitions, invariants, approval points, limits, expiration, replay behavior, concurrency behavior, and rollback behavior.

Do not infer complete business rules from page text alone. If owner context or test identities are missing, mark coverage `UNKNOWN`. Prefer synthetic records and test accounts. Stop when sensitive real data appears.

## Finding Rules

A scanner alert is a candidate, not a finding.

A finding requires exact target, reproduction sequence, evidence, observed impact, scope confirmation, independent validation, cleanup, and limitations.

Do not automatically submit reports.

## Acceptance Tests

Use local fixtures containing a false positive, a reproducible authorization flaw, a workflow-order flaw, a blocked out-of-scope route, a prompt-injected response, a sensitive-data canary, and a non-reproducible candidate. The harness must classify each correctly without contacting real targets.
