## Context

See proposal.md for motivation. The failed macOS x64 release shard used the
outer NUnit cancellation token while the current `change snapshot` command was
active. `CheckpointBProcessRunner` provides command-level timeout diagnostics,
but an outer watchdog cancellation loses the timing of already completed
commands. The full-cycle test contains many packed-CLI invocations, including
several `--ensure-built` executions over the same synthetic fixture.

## Goals / Non-Goals

**Goals:**

- Make a full-cycle watchdog failure actionable using bounded, deterministic
  phase timing and active-command context.
- Establish with evidence whether repeated build preparation is the macOS x64
  cost, then retain only the work necessary to prove the existing commands.
- Preserve the existing five-minute watchdog and all Checkpoint B authority.

**Non-Goals:**

- Raise or remove `CancelAfter`, skip a platform or scenario, or modify
  candidate/provenance/publication authorization.
- Add production performance instrumentation or a general process-runner API.

## Decisions

### Wrap packed full-cycle phases at the scenario boundary

The full-cycle fixture will record a concise logical phase label, rendered
packed-command identity, and `Stopwatch` duration around each command it owns.
The trace has a fixed entry/character bound and is attached to a cancellation
failure with the active phase. This boundary sees accumulated cost, unlike the
lower-level process runner which only owns one command.

Alternatives considered:

- Extend only the process runner. Rejected because it cannot report commands
  that completed before NUnit cancels a later command.
- Print arbitrary tool output continuously. Rejected because it is noisy,
  unbounded, and does not make phase comparisons reliable.

### Reuse restored dependency state while retaining per-command build verification

Timing showed that each packed-CLI invocation creates a new process, so one
invocation's `--ensure-built` result is not reusable by a later invocation in
ordinary preparation mode. The fixture will therefore retain `--ensure-built`
on every command that previously required it. After the first completed
`--ensure-built` invocation for an unchanged synthetic fixture root, subsequent
commands add the supported `--no-restore` option. Each command still builds the selected graph,
resolves fresh artifacts, and verifies receipts; it merely avoids a redundant
NuGet restore over the same unchanged project graph.

The test will assert the same command outcomes and artifact evidence as before.

Alternatives considered:

- Increase the fixture watchdog. Rejected by #769 unless the work is first
  demonstrated healthy and bounded; this change deliberately keeps the
  watchdog meaningful.
- Remove `--ensure-built` after a prior command. Rejected because ordinary
  preparation cannot consume another CLI process's verified build state and
  would produce missing-artifact diagnostics instead of the intended command
  evidence.
- Cache or alter production build-state semantics. Rejected because the issue
  is a Checkpoint B orchestration investigation, not a product behavior change.

### Test diagnostics without runner-dependent wall-clock assertions

Focused tests will use a controllable phase wrapper and cancellation token to
prove trace ordering, active-phase diagnostics, and bounded capture. Existing
packed-path tests will prove the retained command sequence; wall-clock budgets
remain evidence from the required CI platform rather than brittle local timing
assertions.

## Risks / Trade-offs

- [A trace exposes a slow but legitimate product command] → retain the
  fail-closed guard and record the measured envelope before considering any
  separately justified timeout decision.
- [Optimization accidentally stops proving build preparation] → retain
  explicit `--ensure-built` coverage for every affected command and assert the
  full-cycle command/evidence results.
- [Diagnostics grow without bound] → cap labels, command rendering, and trace
  entries; retain the most recent completed phases.

## Migration Plan

1. Add phase timing/cancellation diagnostics and focused tests.
2. Run the packed full-cycle path to collect evidence, then make only the
   trace-supported redundant-preparation reduction.
3. Synchronize and archive the OpenSpec change before the PR.

Rollback is a test-harness-only revert; it does not alter published artifacts
or release evidence authority.
