---
name: cyber-contributor-onboarding
description: Onboards new contributors to the Cyber SOP Harness safely, covering build setup, test execution, code review expectations, security boundaries, and what changes require additional scrutiny. Use when a contributor wants to add an adapter, modify policy, or extend the framework.
---

# Cyber Contributor Onboarding

## What

A structured path from "I want to contribute" to "my PR is merged" that maintains security discipline without creating unnecessary friction.

## Why

The .NET choice already filters for a specific audience. Adding unnecessary onboarding complexity would shrink it further. But this is a governance framework — a careless contribution can create a bypass that affects every downstream user.

## Onboarding Path

### Level 1: Read and Build (30 minutes)
1. Clone repo
2. `dotnet restore && dotnet build`
3. Run all three test suites
4. Read README.md status table
5. Read ARCHITECTURE.md trust boundaries section

**You now know:** what the project does, how to verify your environment, and where the trust boundaries are.

### Level 2: Understand the Governance Flow (1 hour)
1. Trace one action through: proposal → PolicyEngine.Evaluate → PermitIssuer.Issue → ToolBroker.ExecuteAsync → EvidenceLedger.Append → ProvenanceAuthority.Issue
2. Read `skills/cyber-authorization-scope-policy/SKILL.md`
3. Read `skills/cyber-safe-execution-containment/SKILL.md`

**You now know:** why the model cannot execute anything directly and why every action requires a permit.

### Level 3: Make a Safe Change
Good first contributions:
- Add test cases to existing suites (especially edge cases)
- Improve documentation clarity
- Fix typos or broken links
- Add benchmark measurements

These do not touch the policy engine, permit system, evidence store, or tool adapters.

### Level 4: Add a Tool Adapter
Follow `skills/cyber-tool-adapter-expansion/SKILL.md` completely.
Requires reviewer with adapter experience.

### Level 5: Modify Policy Engine or Permit System
This is the highest-risk area.
- Must include property-based tests
- Must not weaken existing safety guarantees
- Requires review from project maintainer
- Must pass full regression battery plus new adversarial tests

## Review Expectations

| Change Type | Reviewer Required | Extra Checks |
|---|---|---|
| Documentation | Any maintainer | None |
| Test additions | Any maintainer | Tests must actually fail without the fix |
| Tool adapter | Maintainer + adapter domain expert | Full adapter checklist from expansion skill |
| Policy engine | Project lead | Property-based tests; adversarial review; no breaking changes |
| Permit/evidence | Project lead | Cryptographic correctness; replay verification |
| Build/CI | Project lead | Supply-chain impact assessment |

## Threat Matrix

| Risk | Mitigation |
|---|---|
| Well-meaning contributor weakens safety | Code review gates by change type; CI enforces existing tests |
| Social engineering via large complex PR | Break into smaller reviewable pieces; each piece must independently pass tests |
| Malicious dependency added | Dependabot alerts; manual review of new dependencies; lock file required |
| Contributor accidentally commits secrets | Pre-commit hooks; CI secret scan; git history rewrite procedure documented |

## Pitfalls

- Making the onboarding so heavy that good contributors leave: keep Levels 1-3 under 90 minutes total
- Not explaining WHY rules exist: contributors who understand the threat model make better decisions
- Accepting PRs that only add happy-path tests: every test should also prove a negative case fails correctly
- Not having a CODEOWNERS file: random people can approve their own security-critical changes

## Definition of Done for Onboarding
- Contributor can build, test, and explain the governance flow
- First PR merged successfully through the correct review gate
- Contributor understands which types of changes require additional scrutiny
