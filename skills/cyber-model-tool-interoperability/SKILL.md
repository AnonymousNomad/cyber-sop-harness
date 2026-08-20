---
name: cyber-model-tool-interoperability
description: Defines model-provider adapters, typed tool capabilities, normalized action requests, provider portability, tool portability, and model-output validation. Use when adding or changing any model, provider, MCP server, security tool, or agent execution path.
---

# Cyber Model and Tool Interoperability

## Model Role

The model may interpret authorized context, propose a plan, select registered capabilities, explain evidence, suggest the next permitted transition, and draft a finding.

The model may not authorize itself, expand scope, claim a tool result without an event, mark a finding verified, invoke an unregistered capability, or bypass the policy engine.

## Action Contract

Require structured fields for run, action, parent event, phase, target reference, capability, arguments, purpose, hypothesis, expected observation, risk class, scope, and approval.

Reject malformed output, unknown capabilities, and undeclared side effects.

## Provider Adapter

Record provider, model, version, configuration, context policy, data-retention policy, tool-call behavior, latency, token usage, and failure class.

A provider swap must not alter scope or policy decisions.

## Tool Adapter

Adapters validate arguments, enforce target scope, capture exact output, return structured status, declare artifacts, report partial output, expose cleanup behavior, and never silently retry dangerous actions.

## Acceptance Tests

Prove that two model providers produce the same policy decision for the same action, two tool adapters produce normalized result envelopes, unknown tool calls are blocked, fabricated tool calls cannot change state, provider failure becomes a controlled error, and tool timeout does not become a successful observation.
