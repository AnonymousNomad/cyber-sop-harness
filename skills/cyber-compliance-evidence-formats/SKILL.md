---
name: cyber-compliance-evidence-formats
description: Maps Cyber SOP Harness evidence output to SOC 2, FedRAMP, ISO 27001, and bug bounty disclosure formats so operators can generate compliance-ready artifacts without manual reformatting. Use when preparing evidence for auditors, compliance reviews, or responsible disclosure.
---

# Cyber Compliance Evidence Formats

## What

Transform the harness's native evidence format (hash-chained JSON events) into formats accepted by specific compliance frameworks and disclosure platforms.

## Why

The evidence chain is the differentiator, but it is useless if an auditor cannot read it. Different frameworks require different fields, different ordering, and different attestation models. The harness should export to these formats natively rather than requiring manual transformation.

## Export Formats

### SOC 2 Evidence Package
- Control ID mapping (CC6.1 → authorization enforcement)
- Evidence event timestamps in ISO 8601
- Hash chain integrity proof
- Policy decision trail
- Access control implementation description
- System description generated from architecture docs

### FedRAMP OSCAL
- NIST SP 800-53 control mapping
- Component definition in OSCAL JSON
- Implementation evidence linked to control statements
- Continuous monitoring events from harness journal

### Bug Bounty Disclosure
- Finding title and severity
- Reproduction steps derived from evidence chain
- Raw request/response pairs (redacted)
- Impact analysis with supporting observations
- Timeline of discovery through verification
- Proof-of-concept that does not include destructive payloads

### Internal Audit Trail
- Chronological event log with hash chain verification status
- All policy decisions with reasons
- All permit lifecycle events
- All tool adapter invocations with outcomes
- Any emergency stop or scope change events

## Threat Matrix

| Risk | Mitigation |
|---|---|
| Sensitive data leaks into compliance export | Redaction pipeline runs before export; secret scan on exported files |
| Evidence tampered before export | Verify hash chain immediately before generating export; reject if broken |
| Wrong framework mapping | Each mapping reviewed by someone who knows the framework |
| Incomplete evidence for auditor | Coverage report shows which controls have evidence and which have gaps |
| Format version drift | Version every export format; maintain backward compatibility |

## Dependencies
- `CyberSopHarness.Core.Phase3Evidence` — EvidenceEvent, DurableEvidenceJournal
- `CyberSopHarness.Core.Provenance` — ProvenanceAuthority for signature verification
- JSON Schema for each export format

## Pitfalls
- Including raw credentials or API keys in exports: redact first, always
- Assuming hash-chain integrity without verifying: verify at export time
- Generating evidence for controls the system does not actually implement: dishonest and dangerous
- Not versioning the export schema: auditors need to know which format version they are reading
- Forgetting to include negative evidence (blocked actions): proves the control is active, not absent

## Acceptance Criteria
- Each export format has a schema definition
- Round-trip test: export → import → verify hash chain still valid
- Redaction verified: no known secrets appear in any exported file
- Framework mappings reviewed by domain expert
