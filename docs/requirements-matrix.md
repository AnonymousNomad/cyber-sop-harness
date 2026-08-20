# Phase 1 Requirements Matrix

Status: planning baseline
Date: 2026-08-17

| ID | Requirement | Exact source references | Planned component | Verification test refs | Phase status |
|---|---|---|---|---|---|
| GOV-001 | Security sources are pinned by version, URL, retrieval date, and content-hash status. | WSTG-v42-Reference-Conventions; ASVS-v5.0.0-Reference-Conventions | `docs/standards-lock.json` | P1-001, P1-002, P1-014 | Planned |
| GOV-002 | Authorization must identify owner, operator, target, time, methods, exclusions, data rules, and stop contacts. | APTS-SE-001; CISA-VDP-Authorization-and-Guidelines; NIST-SP-800-115-Planning-and-Conducting | Engagement manifest | P1-003, P2-AUTH-001 | Planned |
| GOV-003 | Scope must be checked before every action, including redirects, shared IPs, tenants, and third parties. | APTS-SE-002, APTS-SE-003, APTS-SE-006, APTS-SE-009, APTS-SE-012; CISA-VDP-Scope-and-Third-Party-Authorization | External policy gate | P1-004 | Planned |
| GOV-004 | Model output is a proposal, not an authorization or observed fact. | APTS-SC-020; APTS-MR-001, APTS-MR-018, APTS-MR-023; APTS-AR-001, APTS-AR-002 | Model/tool adapter contract | P1-005, P1-016, P3-STATE-001 | Planned |
| GOV-005 | Tools are typed capabilities with declared side effects, privilege, network, data, and limits. | APTS-SC-019, APTS-SC-020; APTS-TP-006 | Tool broker | P1-006 | Planned |
| GOV-006 | Workers require least privilege, containment, resource limits, egress controls, and independent stop behavior. | APTS-SC-004, APTS-SC-009, APTS-SC-019; WINDOWS-SANDBOX-Overview; WINDOWS-JOB-Objects; LINUX-SECCOMP-What-It-Isnt; LINUX-LANDLOCK-ABI | Execution worker | P1-007 | Planned |
| GOV-007 | Raw tool results are captured separately from redacted model-visible results. | APTS-AR-001, APTS-AR-010, APTS-AR-011, APTS-AR-012, APTS-AR-015, APTS-AR-020 | Evidence store | P1-008 | Planned |
| GOV-008 | Finding status must distinguish hypothesis, candidate, reproducible, verified, unknown, blocked, and reportable. | APTS-RP-001, APTS-RP-002, APTS-RP-006, APTS-RP-008 | Finding state machine | P1-009, P1-015, P3-FINDING-001 | Planned |
| GOV-009 | Independent verification must not be performed solely by the discovery context. | APTS-RP-002, APTS-RP-005, APTS-RP-007; CYBENCH-2408.08926; CYBERGYM-2506.02548 | Independent verifier | P1-010, P3-VERIFY-001 | Planned |
| GOV-010 | Methodology procedures require prerequisites, allowed capabilities, oracle, evidence, cleanup, and escalation. | WSTG-v42-Procedure-References; ASVS-v5.0.0-Verification-Requirements; NIST-SP-800-115-Planning-and-Conducting | Methodology compiler | P1-011, P4-COMPILE-001 | Planned |
| GOV-011 | iPhone and Android are control clients; durable execution remains on PC/Linux. | APPLE-RUNTIME-Sandbox-and-Background; APPLE-APP-REVIEW-2.5.2; APPLE-APP-REVIEW-2.5.4; ANDROID-SANDBOX-Application-Sandbox; ANDROID-BACKGROUND-Foreground-Services; FLUTTER-PLATFORM-Windows-iOS-Android-Linux | Mobile control plane and desktop gateway | P1-012, P5-MOBILE-001 | Planned |
| GOV-012 | Releases include the complete append-only project audit. | PROJECT-REQ-AUDIT-001; APTS-AR-005, APTS-AR-011, APTS-AR-012, APTS-AR-020 | `agent_notes.Md` and release manifest | P1-013, P5-RELEASE-001 | Planned |

## Phase 1 Scope

Phase 1 establishes contracts, decisions, and fixtures. It does not implement the policy engine, worker, mobile app, tool adapters, or target interaction.
