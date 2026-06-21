## Why

Issue #50: Add a lightweight, deterministic validation timing baseline harness before performance optimization work begins (stories #19, #45). The tool needs a way to capture repeatable phase-level timings for architecture validation runs so that future changes to scanning, resolution, or contract execution can be compared against known baselines.

## What Changes

- Add `--timings` CLI flag to `arch-linter-net`
- When enabled, collect `Stopwatch` timings around each major validation phase and write an aligned columnar timing report to stderr
- Keep stdout unchanged, including `--format human` and `--format json`
- Preserve all existing exit code semantics
- No changes to Core library, Testing adapter, or validation logic itself
- Document the timing baseline usage in CLI docs

## Capabilities

### New Capabilities
- `cli-timing`: Deterministic phase-level timing harness exposed via the `--timings` CLI flag. Reports load/setup, configuration check, per-contract-family execution (with contract counts), and post-processing durations.

### Modified Capabilities

_(None — this is purely additive instrumentation with no spec-level behavior changes to existing capabilities.)_

## Impact

- `src/ArchLinterNet.Cli/Program.cs` — the only file modified. Timing instrumentation wrapped around the existing validation pipeline blocks.
- `docs/cli/index.md` — new "Timing baseline" usage section.
- No changes to any Core, Testing, or Unity packages.
- No changes to YAML schema, validation semantics, diagnostics ordering, strict/audit behavior, or JSON output format.
