## Context

`change snapshot` currently prepares validation with `EnsureBuilt`, then independently asks each graph and optional baseline contributor to prepare with `EnsureBuilt` again. Each request can run the structured graph build. In addition, a failed baseline diff is represented as `Succeeded = false`, but the CLI projects only its `Frozen` collection and therefore loses that failure.

## Goals / Non-Goals

**Goals:**

- Make an explicitly requested snapshot fail without writing a file if validation or baseline debt cannot produce complete facts.
- Run the graph build once, retain its receipt-backed output selection, and have graph and baseline contributors re-verify and load that prepared state in isolated runners.
- Preserve typed preflight diagnostics and maintain both Core public API approval baselines.

**Non-Goals:**

- Replacing independent isolated runner contexts with a shared mutable assembly-load context.
- Changing ordinary snapshot behavior, snapshot identity/schema, or the build-state process invocation protocol.
- Generalizing snapshot-scoped preparation to unrelated CLI commands.

## Decisions

### Represent receipt-backed reuse explicitly

Add `UsePreparedPostBuildState` to the graph and baseline-diff request models. The handler uses `EnsureBuilt` only for its first validation request, then sends ordinary, receipt-verifying requests with this flag to both graph projections and optional baseline debt. The flag directs those contributors to materialize their existing isolated post-build runner without restoring or building.

The dedicated request flag makes the handoff explicit and independently safe: a caller cannot treat an unverified ordinary state as prepared because preflight still blocks it. Re-running `EnsureBuilt` was rejected because it rebuilds the same graph three or four times. Passing ordinary mode alone was rejected because it does not identify the required isolated post-build runner for shared-framework consumers.

### Treat each requested contributor as required evidence

The handler checks the validation preflight result before creating graph facts. When a baseline is requested, it retains the full `BaselineDiffOutcome`, stops on `Succeeded = false`, and prints any typed preflight diagnostics through the existing formatter. `WriteAllText` occurs only after every requested contributor succeeds.

`BaselineDiffOutcome` carries the preflight diagnostics that caused a failed collection. This is additive public API, aligned with the existing verify outcome, and avoids converting a typed Core failure into a generic CLI error.

### Test the orchestration contract rather than timing

CLI fake-runtime tests will prove that one complete ensure-built snapshot emits exactly one `EnsureBuilt` request and that all later contributors receive the prepared-state request flag; Core service tests cover isolated post-build materialization with ordinary, non-building preflight. This establishes the process-invocation count without depending on wall-clock measurements or a real compiler.

## Risks / Trade-offs

- [A prepared state is stale or absent] → `Prepared` re-runs ordinary receipt verification and fails closed before graph or baseline execution.
- [A baseline failure has no typed diagnostics] → CLI still fails and does not write; diagnostics are emitted whenever Core supplies them.
- [An additive API change drifts] → update the architecture public-API snapshot and NUnit approval baseline deliberately.

## Migration Plan

The CLI surface is unchanged. `--ensure-built` snapshots become cheaper and stricter: one build produces receipts, then contributors load those verified outputs. Rollback is a code revert; snapshot artifacts remain compatible.

## Open Questions

None.
