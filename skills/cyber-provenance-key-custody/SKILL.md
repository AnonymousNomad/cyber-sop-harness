---
name: cyber-provenance-key-custody
description: Governs custody, generation, rotation, and use of the signing keys that produce provenance stamps and release manifests, following SLSA build-provenance levels. Use whenever a signing key is created, stored, rotated, or used to sign a release or provenance stamp.
---

# Cyber Provenance Key Custody

## Directive

Manage the `ProvenanceAuthority` signing key with these controls, aligned to SLSA build-provenance levels:

- Key generation: generate the release/provenance key with a strong asymmetric algorithm (RSA >= 2048 or Ed25519) using a CSPRNG. Never hard-code or derive the key from a password in source.
- Key custody: the private key must not be committed to the repository, embedded in binaries, written to logs, or exposed to the model. Store it in platform secure storage or an offline/air-gapped location for release signing.
- Separation: the key that signs evidence/provenance at runtime must be distinct from the key that signs release artifacts. A runtime compromise must not let an attacker sign a release.
- Signed provenance: every `ProvenanceStamp` and `SignedReleaseManifest` is signed over a canonical payload that includes product/build identity, run/action, authorization/scope hashes, provider/model/tool identity, and evidence/artifact hashes. Verification must re-derive the canonical payload and check the signature.
- Offline verification: verification must work without network access using the embedded/distributed public key. Report `VERIFIED`, `UNVERIFIED`, or `REJECTED`.
- Rotation: support key rotation by publishing the new public key and honoring a validity window; old stamps remain verifiable under the key that signed them. Never reuse a retired key to sign new artifacts.
- Level target: aim for SLSA Build L2 at minimum (signed provenance tied to a dedicated build/signing step, not an individual workstation), and document why L3 (hardened build platform) is or is not in scope.

## Rationale and Architectural Reason

A provenance stamp is only as trustworthy as the key that signs it. If the private key is accessible to the model, to build steps, or to anyone who can edit the repo, then a forged stamp is indistinguishable from a real one and the entire provenance chain collapses. SLSA's core insight is that provenance must be signed by a key the user-defined build steps cannot reach; that is what separates "provenance exists" (L1, trivially forgeable) from "signed provenance that deters tampering" (L2+).

Separating the runtime evidence-signing key from the release-signing key bounds the blast radius: a compromised runtime can forge its own evidence stamps but cannot mint a valid release manifest, and vice versa. Canonical-payload signing means any change to the bound fields invalidates the signature, so provenance cannot be re-pointed at different evidence or a different model without detection.

Offline verification is required because the product must remain auditable without a cloud service; the public key ships with the release and verification is a pure local computation.

## Threat Matrix

| Threat/trap | Likely complication/error | Required prevention/detection | Test |
|---|---|---|---|
| Private key committed to repo | Key leaked in source control | Pre-commit secret scan; key in secure storage only | Repo secret-scan test |
| Key embedded in binary | Key extractable from shipped artifact | Load key at runtime from secure storage; never embed | Binary string/entropy scan |
| Runtime key signs release | Compromised runtime mints a release | Separate runtime vs release keys | Cross-key signature rejection test |
| Forged/re-pointed stamp | Stamp bound to different evidence | Canonical-payload signature verification | Tampered-stamp rejection test |
| No offline verification | Verification requires network | Ship public key; verify locally | Offline verification test |
| Key rotation breaks old stamps | Old stamps fail after rotation | Honor validity window; keep old public keys | Rotation validity-window test |
| Weak key/algorithm | Small RSA or non-CSPRNG key | Enforce RSA>=2048/Ed25519 + CSPRNG | Key-strength assertion test |
| Unsigned release ships | Release manifest missing signature | Block release on missing/invalid signature | Unsigned-release rejection test |
