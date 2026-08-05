## Context

Checkpoint B is release authorization, so a timing race or a declarative
evidence value can turn an untested property into a release assertion.

## Goals / Non-Goals

**Goals:**

- Bind cancellation to a barrier reached from inside validation and verify no
  successful output/cache state survives interruption.
- Return scenario evidence only from the method that performed its oracle.
- Make evidence parsing reject duplicate IDs.

**Non-Goals:**

- Add another release package or make a publishing run.

## Decisions

- Both the external Testing consumer and the CLI use a narrowly opt-in
  validation test barrier. It creates a marker from inside snapshot evaluation,
  before cache lookup or contract execution, then waits on the real
  cancellation token; without its test-only environment variable it is inert.
  The CLI child is interrupted with its native signal and its output/cache
  postconditions are inspected from the caller.
- Each oracle returns `CheckpointScenarioResult`; the test collects those
  values rather than manufacturing `Passed` entries in the coordinator.

## Risks / Trade-offs

- [Signal portability] → use a child script per selected runner shell and only
  claim its native entrypoint; platform evidence records non-applicable peers.
- [Blocked child process] → a bounded timeout fails the test rather than
  allowing a release gate to hang.
