## Context

Issue #50 requests a validation timing baseline harness. The architecture validation pipeline currently runs sequentially in `Program.cs` (the CLI entry point), with no instrumentation. Three code paths duplicate the pipeline (CLI, `ArchitectureValidator`, `TestingBuilder`), but per the non-goals of #50, no refactoring of this duplication will be performed here.

The CLI entry point at `src/ArchLinterNet.Cli/Program.cs` (346 lines) is the only file that needs modification. It contains the full validation orchestration with clear phase boundaries.

## Goals / Non-Goals

**Goals:**

- Add `--timings` CLI flag that collects `Stopwatch` timings around each validation phase
- Print an aligned columnar timing report to stderr
- Report total duration, coarse phases, sub-phases, and per-contract-family timings with contract counts
- Keep stdout unchanged (both `--format human` and `--format json`)
- Preserve all exit code semantics
- Zero changes to Core library or Testing adapter
- Document usage in CLI docs

**Non-Goals:**

- No caching, parallel execution, incremental validation, or hot-path rewrites
- No machine-readable timing output format (JSON, structured logging)
- No per-contract-instance granularity (aggregate per contract family only)
- No timing in `ArchitectureValidator.Validate()` or `ArchitectureValidationBuilder`
- No refactoring of the three pipeline duplications
- No changes to YAML schema, validation semantics, diagnostics ordering, or strict/audit behavior
- No performance optimization of any kind — measurement only

## Decisions

### Decision 1: CLI flag vs separate benchmark project

**Choice:** `--timings` CLI flag.

**Rationale:** A separate benchmark project would be a new entry point, separate maintenance surface, and different invocation. Issue #50 explicitly calls for a "lightweight" harness before optimization begins, not a full performance infrastructure. The CLI flag is simpler to document, run, and compare across branches.

**Alternatives considered:**
- Separate console project (`tools/ArchLinterNet.Bench`) — rejected as over-engineering for a baseline measurement need.
- Environment variable — less discoverable and harder to document than a CLI flag.

### Decision 2: Output destination — stderr

**Choice:** Timing report to stderr.

**Rationale:** The acceptance criteria require that timing mode does not change validation results or exit code semantics. Writing to stderr guarantees stdout is untouched in both `--format human` and `--format json` modes. This is the standard unix convention for diagnostic/metadata output (cf. `curl --trace`).

**Alternatives considered:**
- Extension of `--format json` — would change the JSON output shape, violating acceptance criteria.
- Separate file via `--timings-file` — adds complexity of file path handling.

### Decision 3: Output format — aligned text table

**Choice:** Aligned columnar text with fixed phase names, indented sub-phases, right-aligned `count=N` and `N ms` columns.

**Rationale:** This is a human-readable mode for comparing before/after runs. Deterministic shape (same phase names and ordering every time) satisfies the acceptance criterion. Column alignment makes visual diffing easier.

**Format:**

```
Validation timings:
  total                                      184 ms

  load_and_setup                              37 ms
    yaml_loading                               8 ms
    condition_set_resolution                   1 ms
    root_resolution                            2 ms
    assembly_resolution                       26 ms

  configuration_check                         12 ms

  contract_checks                            133 ms
    dependency                 count=4        31 ms
    layer                      count=3        24 ms
    allow_only                 count=0         0 ms
    cycle                      count=1        18 ms
    method_body                count=2        44 ms
    asmdef                     count=0         0 ms
    independence               count=1         9 ms
    protected                  count=0         0 ms
    external                   count=1         7 ms
    acyclic_sibling            count=0         0 ms

  post_processing                              2 ms
```

**Alternatives considered:**
- `key = value ms` — simpler but harder to scan when 10+ contract families are present.
- JSON — not needed; no machine ingestion requirement in #50.

### Decision 4: Granularity — aggregate per contract family

**Choice:** Time the entire loop for each contract type, with a contract count. Do not time individual contract instances.

**Rationale:** Issue #50 says "useful phase-level timings if the current code structure allows it without invasive refactoring." The current structure is a sequential foreach per contract type with a single `Check*` method call per instance. Aggregate timing is a single `Stopwatch` outside each foreach loop — zero structural change.

Per-contract timing would require either modifying `Check*` method signatures to return timing data (invasive) or wrapping each individual call (noisy). Not justified for a baseline tool.

Contract counts are included because #45 will change the cost model of specific contract families. Without counts, a timing difference cannot be attributed to "slower execution" vs "more contracts."

### Decision 5: Pipeline duplication — not addressed

**Choice:** Instrument only the CLI path in `Program.cs`. Do not touch `ArchitectureValidator.Validate()` or `ArchitectureValidationBuilder.Validate()`.

**Rationale:** Issue #50 explicitly forbids invasive refactoring and changes to validation semantics. Fixing the triplication would change code structure and risk unintended behavioral changes. A separate cleanup issue should be created after #50 baseline is in place.

## Risks / Trade-offs

- **[Risk] Timing overhead** — `Stopwatch` overhead is negligible (sub-microsecond start/stop). No measurable impact on validation duration.
- **[Risk] stderr mixing** — If the CLI ever writes error diagnostics to stderr, the timing report will interleave with those. Acceptable for a baseline tool; users redirect stderr if needed.
- **[Risk] Phase boundaries drift** — If new contract types are added to the pipeline later, the timing instrumentation must be updated to include them. Mitigation: the timing block structure mirrors the contract-type loops, so any addition to the validation pipeline will naturally show where timing is missing.
