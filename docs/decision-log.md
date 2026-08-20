# Decision Log Index

The authoritative append-only event record is `agent_notes.Md`. This document indexes the current decisions for review without replacing the audit trail.

| Decision | Date | Record | Status |
|---|---|---|---|
| Audit file is release-bound and append-only | 2026-08-17 | `agent_notes.Md` AUDIT-0001 | Accepted |
| Product is a portable governance/conformance layer | 2026-08-17 | `agent_notes.Md` RESEARCH-0001 | Accepted for planning |
| Mobile is control plane; PC/Linux is execution plane | 2026-08-17 | `agent_notes.Md` ARCH-0001 | Accepted for Phase 1 planning |
| Five phases and ten skills | 2026-08-17 | `ROADMAP.md` and `agent_notes.Md` ROADMAP-0001 | Accepted for planning |
| Phase 1 artifact contract | 2026-08-17 | `docs/requirements-matrix.md`, `docs/state-model.md` | Accepted for Phase 1 |
| Phase 1 completion | 2026-08-17 | `agent_notes.Md` VERIFY-0005, REVIEW-0003, PHASE-0001 | Complete for artifact/contract scope; Phase 2 not started |

## Reconciliation Rule

Every future event must include date, event type, actor, intent, observed result, evidence, risks, and next action. Historical conversation-derived entries are supplemented by this index and subsequent normalized audit entries; they are not silently rewritten.
