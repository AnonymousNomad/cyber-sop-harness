---
name: cyber-methodology-as-code
description: Converts versioned cybersecurity methodologies into executable, typed, auditable procedures with prerequisites, allowed actions, evidence requirements, completion oracles, and escalation behavior. Use when importing or updating OWASP, PTES, NIST, ASVS, CISA, or MITRE guidance.
---

# Cyber Methodology as Code

## Source Handling

Pin every source by name, version, URL, retrieval date, content hash, and applicable requirement or scenario IDs.

Never use unversioned `latest` content to authorize a run.

## Procedure Schema

Each procedure defines procedure ID, source references, objective, preconditions, required context, allowed capabilities, prohibited actions, risk class, expected observations, evidence requirements, safe validation method, completion oracle, failure behavior, human escalation, cleanup, and coverage state.

## Compilation Rule

A methodology document is not executable until it has been compiled into this schema and reviewed.

Do not place a raw methodology document in a system prompt and call it operational.

## Coverage

Record `TESTED`, `PASSED`, `FAILED`, `BLOCKED`, `NOT_APPLICABLE`, `UNKNOWN`, and `NOT_TESTED` separately.

Never claim full methodology coverage from tool execution alone.

## Initial Scope

Start with web applications, APIs, authentication, authorization, session behavior, input handling, workflow logic, evidence, and reporting.

Defer network, cloud, identity, binary, and physical operations until separate profiles exist.

## Acceptance Tests

Use local vulnerable fixtures and verify every procedure parses, every capability is registered, every procedure has a completion oracle, every high-impact procedure has approval behavior, every procedure produces coverage state, and invalid or incomplete procedures fail compilation.
