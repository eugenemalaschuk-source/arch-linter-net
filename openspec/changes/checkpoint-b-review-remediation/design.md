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

- The external Testing consumer uses the Testing package's validation-entry
  barrier, invoked inside `ValidateStrict` immediately before the engine begins
  work. It is a caller-supplied testing composition seam, not a Core runtime
  switch.
- The Linux CLI oracle walks the CLI process tree until `/proc/<pid>/maps`
  proves that the selected target assembly has been materialized by the real
  tool host, then sends its native termination signal and inspects
  output/cache postconditions. This keeps the packaged Core runtime free of
  test-controlled behavior.
- Each oracle returns `CheckpointScenarioResult`; the test collects those
  values rather than manufacturing `Passed` entries in the coordinator.

## Risks / Trade-offs

- [Signal portability] → use a child script per selected runner shell and only
  claim its native entrypoint; platform evidence records non-applicable peers.
- [Blocked child process] → a bounded timeout fails the test rather than
  allowing a release gate to hang.
