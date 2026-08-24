# Contributing

Thanks for considering a contribution. This project is built verification-first: nothing is claimed done until it is built, run, and evidenced.

## Ground rules

- **Authorized defensive testing only.** Contributions that weaken the fail-closed boundary, bypass authorization, or add live-target behavior without gates will not be accepted.
- **No credentials or target data in the repository.** Local runtime state (`data/`, `models/`, `runtime/`) is git-ignored; keep it that way.
- Public-facing copy stays technical. No contest, grant, or funding references in README or docs.

## Build and test

Prerequisites: Windows or Linux and the .NET 10 SDK. Persisted secrets and governed runs require Windows today; Linux is validated for build, command-desk development, and deterministic tests.

```powershell
dotnet build CyberSopHarness.slnx --configuration Release
dotnet run --project tests/Phase2.Tests --configuration Release
dotnet run --project tests/Phase3.Tests --configuration Release
dotnet run --project tests/CommandDesk.Tests --configuration Release
```

`TreatWarningsAsErrors` is enabled — the build must be clean. The real-model runtime test in Phase 3 is opt-in via `PHASE3B_REAL_MODEL=1` and never runs in CI.

## Conventions

- C# 13 / .NET 10, nullable enabled, no comments unless they carry meaning the code cannot.
- Every new behavior gets a deterministic offline test in the matching console suite (self-runner, prints `PASS <name>` and a final count).
- Update `docs/phase3b-acceptance.md` and `agent_notes.Md` for acceptance-level changes; document decisions in `docs/decisions/`.

## Pull request process

1. Open an issue describing the change and the verification you will run.
2. Implement + add tests; run the full build and all three suites locally.
3. Keep README claims synchronized with implementation: distinguish working preview behavior from roadmap work.
4. PR with a description that lists the exact commands run and their output counts.
5. CI must pass on Windows and Linux (build + all suites, no real-model opt-in).
6. A maintainer reviews; squash-merge with a conventional message.

## Update and release cadence

- Push every validated logical unit rather than holding a large working tree. Do not push failing, undocumented, or unverified states to `main`.
- Use focused Conventional Commit subjects such as `feat(desk): add status rail`, `fix(runtime): defer DPAPI construction`, or `docs: clarify preview limits`.
- Keep `main` suitable for the next development-preview build; a tag is reserved for a passing Windows/Linux matrix, updated acceptance evidence, reviewed security impact, and synchronized documentation.
- Before every push, rerun the four verification commands above and state the exact counts in the pull request or handoff note.
