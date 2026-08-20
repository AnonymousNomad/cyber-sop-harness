# Phase 1 Evidence Index

Status: planning and artifact verification
Date: 2026-08-17

| Evidence ID | Artifact | Purpose | Integrity status | Verification reference |
|---|---|---|---|---|
| EVD-P1-001 | `README.md` | Product scope and current status | File re-read; release hash pending | P1-001 |
| EVD-P1-002 | `RESEARCH.md` | Source-backed market and standards findings | File re-read; source snapshot hashes pending | P1-009 |
| EVD-P1-003 | `ARCHITECTURE.md` | Topology, trust boundaries, platform constraints | File re-read; runtime tests pending | P1-007, P1-011 |
| EVD-P1-004 | `ROADMAP.md` | Phases and gates | File re-read; phase implementation pending | P1-011 |
| EVD-P1-005 | `docs/standards-lock.json` | Versioned research inputs | JSON parsed; exact refs and content-hash status checked; content hashes pending | P1-001, P1-009, P1-014 |
| EVD-P1-006 | `docs/requirements-matrix.md` | Requirement traceability | File re-read; 12 rows checked | P1-009 |
| EVD-P1-007 | `docs/threat-model.md` | Assets, threats, controls, residual risk | File re-read; runtime controls pending | P1-010 |
| EVD-P1-008 | `docs/architecture-decision-record.md` | Architecture decision and rejected alternatives | File re-read; runtime tests pending | P1-011 |
| EVD-P1-009 | `docs/state-model.md` | Initial state and finding invariants | File re-read; runtime transition tests pending | P1-005, P1-011, P1-015 |
| EVD-P1-010 | `docs/local-fixture-plan.md` | Owned fixture design and data rules | File re-read; fixtures not implemented | P1-012 |
| EVD-P1-011 | `schemas/*.schema.json` | Engagement, action, permit, and result contracts | Four schema JSON files parsed; five JSON contracts parsed including `docs/standards-lock.json` | P1-003, P1-004, P1-008 |
| EVD-P1-012 | `skills/` | Ten governing project skills | Ten frontmatter checks passed | P1-002 |
| EVD-P1-013 | `agent_notes.Md` | Complete project audit | File re-read; release hash pending | P1-013 |
| EVD-P1-014 | `docs/data-contracts.md` | Cross-record contract and runtime invariant definition | File re-read; runtime tests pending | P1-004, P1-008, P1-016 |
| EVD-P1-015 | Final acceptance and independent review output | Phase 1 closure evidence | Acceptance passed; independent review clean for blockers/high/medium; release hashes pending | VERIFY-0005, REVIEW-0003 |
| EVD-P2-001 | `docs/phase2-research.md` | Phase 2 source-backed scope and implementation contract | File re-read; source snapshots and runtime behavior remain pending | PHASE2-RESEARCH-0001 |
| EVD-P2-002 | `src/CyberSopHarness.Core` | Authorization, scope, policy, permit, credential, rate, worker, relay, rollback, and Windows containment implementation | Build passed; runtime evidence pending review | P2-TEST-0003 |
| EVD-P2-003 | `tests/Phase2.Tests` and `docs/phase2-acceptance.md` | Phase 2 behavioral acceptance suite | 10/10 tests passed; independent review pending | P2-TEST-0003 |

## Hash Policy

Artifact hashes will be generated in a release manifest after the repository has a release process. No hash is invented or represented as a content hash in this planning index.
