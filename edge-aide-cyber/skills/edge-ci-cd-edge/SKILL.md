# Edge CI/CD

## What To Do
Set up GitHub Actions for linting, unit testing, integration testing (offline), and build artifact generation. Include an on-device test script for Android-specific behavior.

## Why
CI catches regressions before they ship. Edge-specific behavior (Android file paths, CPU affinity, Termux quirks) can't be tested on ubuntu-latest, so a separate on-device script fills the gap.

## GitHub Actions Pattern
```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        node-version: [18, 20]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
      - run: npm ci
      - run: npm test
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Tests pass in CI but fail on device | Hidden platform dependency | On-device test script |
| Supply-chain via npm | Malicious dependency | Pin versions; enable Dependabot |
| Secrets in CI logs | Credential leak | Use GitHub encrypted secrets |

## Dependencies
- GitHub Actions runner (ubuntu-latest)
- Node.js 18+ (CI and local)
- npm with committed package-lock.json

## Pitfalls
- Some Node.js APIs differ between versions; test against 18 and 20
- `npm audit` reports false positives for dev-only deps
- Integration tests must not require llama.cpp or nmap
