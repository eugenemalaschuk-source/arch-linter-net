## Context

`ValidateCommandHandler.ExecuteCombinedModes` already creates one
`ArchitectureAnalysisSnapshot` for a comma-separated `strict,audit` request.
The snapshot's metadata preparation owns `--ensure-built` and later serves both
mode evaluations; `ReportCoordinator` renders its supplied outcomes without
re-entering validation. Existing benchmark and profile evidence contains this
scenario, but the primary CI/adoption pages still lead readers to two full
processes and focused regression coverage does not connect the ensure-built
boundary to the two-mode path directly.

## Goals / Non-Goals

**Goals:**

- Make the one-process, one-snapshot command the documented choice when strict
  and audit must inspect the same build state.
- Add deterministic, low-cost regression evidence for shared ensure-built
  preparation, per-mode equivalence, and rendering-only multi-sink work.
- Preserve the existing output envelopes, exit categories, and single-mode
  workflows.

**Non-Goals:**

- Change snapshot, cache, build-receipt, report-routing, or CLI-public API
  behavior.
- Make audit blocking in workflows that intentionally use it only as
  non-blocking visibility.
- Reuse preparation across independent processes, including baseline or
  public-API commands.

## Decisions

### Treat the existing combined command as the integration seam

Tests will exercise the existing snapshot and profile counters rather than add
a new execution abstraction or bespoke CI-only switch. This directly proves
the contract the CLI already uses and avoids creating another path with subtly
different preparation or exit semantics.

### Keep the documentation recommendation conditional on workflow semantics

CI/adoption documentation will recommend `--mode strict,audit --ensure-built`
when both results are required from one build state, including combined JSON
and SARIF artifacts. It will retain separate strict and non-blocking audit
steps for teams that intentionally do not gate on audit, because a combined
command exits with validation failure when either requested mode fails.

### Use profile counters to separate analysis from output work

Regression evidence will assert shared snapshot/preparation counters for a
combined run and compare report-sink configurations through existing profile
counter semantics. Additional sinks must change render/output counters only;
they must not create another snapshot or re-evaluate contracts.

## Risks / Trade-offs

- [Users may mistake combined mode for a non-blocking audit replacement] →
  explicitly document its aggregate exit behavior and retain the separate
  workflow example.
- [Tests could become hardware-sensitive] → assert deterministic counters and
  canonical outcomes only; do not add time thresholds or run the benchmark
  harness in the normal suite.
- [Post-build verification can be confused with a second mode preparation] →
  describe receipt verification as part of the same snapshot-owned
  ensure-built preparation and assert no per-mode preparation is invoked.
