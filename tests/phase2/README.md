# Phase 2 Test Battery

The executable test project is `tests/Phase2.Tests`.

## Required Commands

```text
dotnet build tests\Phase2.Tests\Phase2.Tests.csproj --configuration Release
dotnet run --project tests\Phase2.Tests\Phase2.Tests.csproj --configuration Release
```

## Test Scope

The suite covers:

- Authorization signature validation
- Scope and redirect enforcement
- Capability and risk policy
- Approval decisions
- Permit signature, expiry, consumption, and replay
- Credential vault encryption and revocation
- Rate and concurrency limits
- Contained versus uncontained workers
- Relay-loss worker stopping
- Rollback order and idempotence
- Windows Job Object creation and termination

No test contacts an external target or uses real credentials.
