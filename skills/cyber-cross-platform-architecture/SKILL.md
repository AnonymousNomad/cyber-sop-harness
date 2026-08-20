---
name: cyber-cross-platform-architecture
description: Defines the secure cross-platform architecture for Windows/Linux execution and iPhone/Android control clients. Use when making system-boundary, transport, model-provider, mobile, sandbox, credential, or deployment decisions.
---

# Cyber Cross-Platform Architecture

## Required Topology

Implement separate planes:

- Mobile control plane
- Authenticated gateway
- External policy engine
- Model/provider adapter
- Typed tool broker
- Sandboxed execution worker
- Evidence and verification store

The mobile application must not be the execution authority.

The model must not directly execute shell commands or unrestricted tools.

## Mobile Responsibilities

Mobile may display proposed actions, target scope, risk, duration, approvals, evidence summaries, and run state. It may request pause or stop.

Mobile must not store cloud provider API keys, execute arbitrary downloaded code, directly invoke privileged tools, expand scope, approve expired actions, or become the only emergency stop.

## Desktop Responsibilities

The desktop gateway owns durable execution, provider access, policy checks, tool adapters, credential handles, worker lifecycle, evidence capture, replay, and local model access.

Windows is the first supported executor. Linux is a later executor using unprivileged workers, namespaces, cgroups, seccomp, Landlock, and VMs where required.

## Transport

Support explicit LAN pairing and an optional outbound relay/private VPN. LAN presence is not authorization. Device identity, operator identity, approval binding, expiry, and nonce must be validated.

Native OAuth uses external-browser authorization with PKCE according to RFC 8252.

## Credentials

Use Apple Keychain/Secure Enclave where available, Android Keystore, Windows Credential Manager or DPAPI, and an encrypted Linux store. Never place secrets in mobile bundles, model prompts, command-line arguments, logs, screenshots, ordinary evidence, or Git.

## Sandbox Rules

Use Windows Job Objects to contain process trees. Use Windows Sandbox or Hyper-V for untrusted tools and high-risk work.

Use Linux namespaces, read-only filesystems, `NoNewPrivileges`, dropped capabilities, seccomp, Landlock, cgroups, and explicit network allowlists. Seccomp alone is not a complete sandbox.

## Architecture Gate

Do not proceed until trust boundaries, provider data paths, credential locations, worker isolation, offline behavior, relay failure behavior, and a threat model covering the model, target content, tools, mobile client, provider, and dependencies are documented and tested.
