## Context

The CLI validate command currently outputs a single format to stdout via `--format human|json|sarif`. The `ValidateCommandHandler.WriteOutcome` method and its `WriteCombinedOutcome` sibling call formatter methods on `ICliRuntime`, which return strings, and write the result directly to `ICliConsole.Out`. There is no file output, no multiple-sink routing, and no intermediate representation shared across format projections.

Issue #363 (`ArchitectureAnalysisSnapshot`) already ensures policy composition, project discovery, and assembly loading happen once per session. Multi-mode (`--mode strict,audit`) already evaluates against one snapshot and merges per-mode JSON/SARIF into single valid documents. What remains is output routing: building format strings once and dispatching each to its configured destination (stdout, stderr, file) without re-analysis.

The key constraint from #353 (First Ice adoption): "Producing a second output format should add serialization cost only, not another analysis pass."

## Goals / Non-Goals

**Goals:**
- One CLI invocation can emit human, JSON, and/or SARIF output to separate destinations
- Normalize diagnostics once; format per sink without re-analysis
- File writes are atomic (temp + replace) and pre-validated
- Backward compatible: `--format human` with no new flags behaves identically
- Multi-mode merged documents route to all configured sinks
- Output failures return exit code 2 with typed status

**Non-Goals:**
- Persistent analysis cache (#365)
- Network report upload
- Arbitrary output templates / plugin formatters
- Multiple independent validation sessions in one invocation
- Global transaction across multiple file writes (each file is atomic individually)
- Changing `ArchitectureDiagnosticFormatter` or `ArchitectureSarifFormatter` signatures

## Decisions

### D1: `--report <format>=<destination>` (repeatable) over `--output-json`/`--output-sarif`

Established by #355 compatibility contract. A single repeatable option is more extensible than N dedicated flags. Format values: `human`, `json`, `sarif`. Destination values: `stdout`, `stderr`, or a file path.

Rejected alternatives:
- `--output-json <path>` — N flags for N formats, doesn't scale
- `--format json:path` — changes existing `--format` semantics, breaking

### D2: ReportCoordinator as the routing abstraction

Extract the "format then write" logic from `ValidateCommandHandler` into a `ReportCoordinator` that:
1. Receives the outcome(s) and the list of configured sinks
2. Calls the existing `ICliRuntime.Format*` methods to produce format strings
3. Routes each string to its destination (stdout/stderr via `ICliConsole`, file via `IFileSystem`)

This keeps `ValidateCommandHandler` focused on orchestration and makes the routing testable.

### D3: Atomic file writes via temp + rename

For each file sink:
1. Resolve path, check no input-file collision (policy, baseline, snapshot, etc.)
2. Write to `<target>.tmp` in the same directory
3. `File.Move(tmp, target, overwrite: true)` — atomic on same filesystem on macOS/Linux
4. On any write failure, delete temp file and report `output-failed`
5. Pre-validate all destinations before any write begins (fail fast, leave no partial output)

### D4: No new numeric exit code; typed status distinguishes output failures

Exit code 2 already means "runtime error". Within that, the typed error status distinguishes:
- `output-failed`: one or more file writes failed before any wrote (no output produced beyond stdout)
- `partial-output`: some sinks succeeded, some failed (filesystem partially written)
- `invalid-arguments`: the `--report` value failed to parse

This is exposed via the already-existing typed error message shape (`kind: "architecture_execution_error"`) with an additional `output_status` field.

### D5: Format strings built once, dispatched N times

The pattern for both single-mode and combined-modes paths:

```
foreach sink in configuredSinks:
    if sink.format == stdoutFormat:
        continue  # already written to stdout
    formatString = formatter(sink.format, outcome[s])
    writeTo(sink.destination, formatString)
```

For combined modes, the merged JSON/SARIF document (already built by `WriteCombinedOutcome`) is passed through the same routing: formatted once per unique format, dispatched to all sinks requesting that format.

### D6: `--output` stays reserved

`--output` is used by `baseline generate`, `graph`, and snapshot commands for their own artifact output. It does not participate in multi-sink report routing. This avoids ambiguity between "output file for this command's primary result" and "additional report sink".

## Risks / Trade-offs

- [**Risk**] `--report sarif=results.sarif` + `--format sarif` writes SARIF to both stdout and a file — this is intentional and useful. [**Mitigation**] Document explicitly in help text.
- [**Risk**] Atomic rename is not atomic on all filesystems (e.g. Docker overlay, some network drives). [**Mitigation**] Temp+rename is best-effort atomic. Document that `File.WriteAllText` is the fallback behavior. Accept that concurrent writers to the same path may race.
- [**Risk**] Large JSON/SARIF documents are built in memory before being written. [**Mitigation**] This is the existing behavior — no regression. Streaming writes are a future concern.
