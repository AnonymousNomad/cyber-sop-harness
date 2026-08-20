---
name: cyber-evaluation-reporting-release
description: Defines independent evaluation, APTS assessment, security regression, reporting, disclosure, portability, supply-chain, and release gates for the cybersecurity harness. Use before declaring a phase complete or releasing a capability.
---

# Cyber Evaluation Reporting and Release

## Verification Layers

Every phase requires unit tests, integration tests, end-to-end tests, failure-path tests, adversarial tests, independent review, regression tests, and artifact inspection.

A passing happy path is insufficient.

## Security Tests

Test out-of-scope targets, expired authorization, prompt injection, fabricated tool results, hash mismatch, tool timeout, provider failure, sandbox failure, kill requests, credential revocation, dependency substitution, cross-engagement leakage, mobile approval replay, and reports containing unsupported claims.

## Evaluation

Use safe benchmarks and local fixtures such as Cybench for cyber capability, CyberGym for reproducible vulnerability tasks, BountyBench for detection/exploitation/patching, AgentDojo for tool-use prompt injection, and APTS customer-acceptance tests for autonomous-testing controls.

Do not equate benchmark success with authorization or production safety.

## Reporting

Every report identifies scope, authorization, methodology versions, tested coverage, untested coverage, evidence references, verification state, observed impact, inferences, limitations, cleanup, reviewer, and disclosure status.

Never report model confidence as proof.

## Release Gate

Do not release or advance a phase until all required tests pass, failed tests are fixed rather than weakened, evidence is recorded, security review is complete, dependency and model versions are pinned, no secrets are present, a rollback path exists, and the phase acceptance matrix is complete.

Do not claim APTS certification. APTS has no certification body. Use scoped, evidence-backed conformance claims only.
