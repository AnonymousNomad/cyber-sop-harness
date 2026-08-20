---
name: cyber-model-packaging-supply-chain
description: Governs licensing, provenance, quantization, hashing, release manifests, optional model downloads, llama.cpp binary packaging, and offline verification for model-enabled releases.
---

# Cyber Model Packaging and Supply Chain

## Directive

Implement a signed `ModelRuntimeManifest` and release pack that includes:

- model publisher, repository, exact revision, quantization repository/revision, filename, byte size, SHA-256, architecture, context limit, and license/notice references;
- runtime publisher, exact llama.cpp release/commit, binary SHA-256, build backend, and third-party license inventory;
- source model card, base-model license, derivative/quantizer attribution, WhiteRabbitNeo extended restrictions, and redistribution decision;
- a signed file manifest for every executable, model, tokenizer/metadata file, notice, and configuration file;
- an opt-in download flow that downloads one selected artifact, verifies size/hash/signature, stages atomically, and deletes incomplete files;
- offline verification that does not contact the network and clearly reports `VERIFIED`, `UNVERIFIED`, or `REJECTED`;
- a release SBOM and the project audit file `agent_notes.Md`.

Do not bundle WhiteRabbitNeo weights until the license/redistribution review is explicit. Until then, ship the provider contract and a user-selected download path with the original notices intact. Never clone the entire Hugging Face repository when one GGUF file is sufficient.

## Rationale and Architectural Reason

Model weights are executable supply-chain inputs even when they are data files: changing them changes behavior. A model card license tag does not prove that a third-party quantization is authorized for redistribution. Exact hashes and a signed manifest make the installed runtime reproducible and let the user detect substitution. Atomic staging prevents partial model files from being mistaken for valid weights. Optional download avoids distributing a questionable derivative before legal and provenance review, while still allowing the user to select the model.

The release manifest must cover both the model and its runtime. A trustworthy GGUF served by an altered binary is not trustworthy, and a trusted binary loading altered weights is not trustworthy. The offline verifier is required because the product must remain useful and auditable without a cloud service.

## Threat Matrix

| Threat/trap | Likely complication/error | Required prevention/detection | Test |
|---|---|---|---|
| License mismatch | HF tag says `llama2` but derivative terms are omitted | Preserve notices, record publisher/quantizer, legal review gate | Missing-notice rejection |
| Unapproved quantization | Third-party GGUF differs from base or has altered metadata | Pin source revision, file hash, architecture, and conversion provenance | Metadata/hash mismatch |
| Partial download | Large GGUF interrupted or disk fills | Temporary file, size/hash check, atomic rename | Interrupted-download recovery |
| Disk exhaustion | 7-13 GB model plus runtime exceeds free disk/RAM | Preflight free-space and memory budgets | Insufficient-resource test |
| Binary substitution | llama.cpp executable replaced | Binary hash/signature and version check | Tampered binary rejection |
| Runtime/model mismatch | Old server cannot interpret GGUF metadata/template | Compatibility matrix and startup identity check | Version mismatch test |
| Package bloat | Bundling all quantizations wastes storage | Ship one approved candidate; offer explicit alternatives | Release inventory test |
| Automatic network use | Startup silently fetches weights/API | Opt-in network consent and offline default | Network-denied startup |
| Model-card omission | Users do not see use restrictions | Bundle notices and show license before activation | First-run notice test |
| Malicious model file | Crafted file exploits converter/runtime | Do not execute custom code; pinned runtime; sandbox load; test-tensor/metadata checks | Malformed GGUF test |
