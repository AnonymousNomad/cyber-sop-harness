---
name: cyber-project-governance
description: Governs the cybersecurity harness project using verified sources, explicit requirements, threat modeling, traceability, and fail-closed engineering. Use at the start of every project phase and whenever a design or implementation decision affects security, scope, evidence, or architecture.
---

# Cyber Project Governance

## Mission

Build a portable cybersecurity harness for authorized defensive testing.

The model is not the authority. The model proposes. Host-side policy, typed adapters, sandboxing, evidence, and independent verification determine what is allowed.

Use only explicit written authorization, owned labs, CTFs, or bug-bounty programs whose rules permit the exact action.

## Mandatory Rules

1. Research security decisions from primary sources.
2. Record the source, version, URL, retrieval date, and applicable requirement.
3. Separate verified facts, design decisions, assumptions, and unresolved questions.
4. Never convert a methodology document directly into an unbounded prompt.
5. Treat OWASP WSTG, ASVS, APTS, PTES, NIST, CISA, and MITRE references as versioned inputs.
6. Require a threat model before implementing a security boundary.
7. Require an acceptance test before calling a phase complete.
8. Fail closed when authorization, scope, identity, evidence, or containment is missing or ambiguous.
9. Never claim a capability was verified unless it was executed and observed.
10. Do not start live target testing during foundation work.

## Required Records

Maintain:

- `standards-lock`
- `requirements-matrix`
- `architecture-decision-record`
- `threat-model`
- `risk-register`
- `phase-plan`
- `acceptance-matrix`
- `evidence-index`
- `decision-log`

Each requirement must link to an implementation component and a test.

## Source Baseline

Use OWASP APTS v0.1.0 for autonomous testing governance, OWASP WSTG v4.2 for stable web-testing references, OWASP ASVS v5.0.0 for application verification, NIST SP 800-115 for assessment workflow, CISA VDP guidance for scope and prohibited research, NIST AI RMF for AI risk management, NIST SP 800-207 for zero-trust architecture, and MITRE ATLAS/ATT&CK for threat-informed mappings.

## Completion Rule

A phase is incomplete if a source is unpinned, a security decision is unsupported, a required artifact is missing, a test is only theoretical, a failure path is untested, the implementation depends on model obedience alone, or a live-target capability exists without authorization and containment.

Because this project is a security control plane, an unverified safety claim is a defect, not a documentation gap.
