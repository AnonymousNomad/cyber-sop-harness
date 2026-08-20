---
name: cyber-mobile-control-plane
description: Defines the iPhone and Android control-plane architecture for approvals, monitoring, evidence viewing, device identity, secure pairing, revocation, and emergency intervention. Use when implementing or changing mobile clients or mobile-to-desktop communication.
---

# Cyber Mobile Control Plane

## Role

The mobile client is not the executor.

It may display an action preview, target, scope, risk, duration, approval, evidence summary, and run state. It may request pause or stop.

It must not store provider API keys, send arbitrary commands, expand scope, approve expired actions, become the only emergency stop, or receive raw secrets by default.

## Approval Binding

Every approval binds run, action, target, scope hash, policy version, risk, device identity, operator identity, expiry, and nonce. Replay or modification must fail.

## Authentication

Use platform secure storage and device authentication. Use OAuth authorization through an external browser with PKCE. Use device enrollment and revocation. Treat rooted, jailbroken, lost, or stale devices as untrusted.

## Connectivity

Support explicit LAN pairing, authenticated relay, and offline display of already-authorized state. Do not queue high-impact actions for later execution. PC-side stop behavior must work without mobile connectivity.

## Acceptance Tests

Test revoked device, lost device, expired approval, duplicate approval, replay attack, clock skew, offline mode, delayed notification, network transition, stale scope, gateway failure, and worker failure.

No test may use real credentials or real target data.
