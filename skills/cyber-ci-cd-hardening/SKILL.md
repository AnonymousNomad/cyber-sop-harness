---
name: cyber-ci-cd-hardening
description: Hardens the CI/CD pipeline for a security governance framework where a compromised build could ship unsafe code. Covers supply-chain integrity, reproducible builds, signed artifacts, dependency auditing, and deployment gates. Use when modifying build, test, release, or deployment infrastructure.
---

# Cyber CI/CD Hardening

## What

Ensure every artifact that reaches a user is built from verified source, tested against known attack vectors, and signed with auditable provenance.

## Why

A governance framework that cannot prove its own supply chain has no authority to govern anyone else's. If an attacker injects code into the build pipeline, they bypass every safety control the framework enforces at runtime.

## Pipeline Stages

### 1. Source Integrity
- All commits signed (GPG or SSH)
- Branch protection on `main` requiring PR review
- No force-push to protected branches
- Dependabot/Renovate for automated dependency updates

### 2. Build Reproducibility
- Deterministic builds (same source → same binary hash)
- No network access during build (all dependencies vendored or cached)
- Build environment pinned (container digest, not tag)
- Build logs archived and tamper-detectable

### 3. Test Gates
- Unit tests must pass (zero tolerance)
- Integration tests must pass
- Security-focused tests must pass:
  - Fuzzed proposal parsing
  - Scope bypass attempts
  - Permit tampering detection
  - Evidence chain integrity after crash simulation
- Coverage report generated but NOT used as sole gate (coverage without adversarial testing is theater)

### 4. Artifact Signing
- Every release binary signed with Sigstore/cosign
- SLSA provenance attestation attached
- SBOM generated and published
- Checksums published separately from binaries

### 5. Deployment Gates
- No automatic deployment to production
- Manual approval required for each release
- Release notes must document what changed, what was tested, and what remains experimental
- Rollback plan documented before release

## Threat Matrix

| Threat | Vector | Mitigation |
|---|---|---|
| Dependency confusion | Attacker publishes package with same name in public registry | Pin exact versions; use private feed; verify package hashes |
| Build injection | Malicious code in build script modifies output | Build scripts reviewed as code; no dynamic script generation |
| Test suppression | Attacker disables failing tests via config change | Tests defined in code, not config; CI checks test file integrity |
| Artifact substitution | Released binary differs from tested binary | Signed provenance links binary to source commit and build log |
| Supply-chain compromise | Upstream library compromised | SBOM enables rapid impact assessment; pin versions; audit dependencies |

## Dependencies
- GitHub Actions or equivalent CI system
- Sigstore/cosign for artifact signing
- Syft or similar for SBOM generation
- .NET SDK with deterministic build settings
- Container registry with image signing support

## Pitfalls
- Using floating version tags (`latest`, `main`) in CI: non-reproducible builds
- Not caching NuGet packages by hash: supply-chain attack vector
- Treating coverage percentage as a security metric: 100% coverage of happy paths is worse than 60% coverage of adversarial cases
- Not testing the rollback path: if you have never rolled back, your rollback plan is theoretical
- Signing artifacts with a key stored in the same CI environment: compromise the CI, compromise the key
