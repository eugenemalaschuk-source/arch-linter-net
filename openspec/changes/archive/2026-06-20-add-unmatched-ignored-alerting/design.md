## Context

The current ignore system silently accepts every `ignored_violations` entry in policy YAML. There is no feedback when an ignore entry ceases to match any actual dependency violation — stale entries accumulate, eroding trust in the baseline. The matching logic is centralized in `ArchitectureIgnoreMatcher.IsIgnored`, called from 8 sites across the runner and scanners.

The detection must be single-pass (no re-running contracts) and additive — existing policies must work without changes.

## Goals / Non-Goals

**Goals:**
- Detect `ignored_violations` entries that match no current violation
- Emit deterministic diagnostics identifying stale entries by index, pattern, and reason
- Support configurable severity (`error | warn | off`, default `error`)
- Work in both `strict` and `audit` CLI modes
- Produce separate human-readable section and JSON field (not mixed with violations)
- Preserve all existing matching behavior (no regressions in valid ignore suppression)

**Non-Goals:**
- Auto-removing stale ignores from policy files
- Redesigning the `ignored_violations` format (schema/model unchanged)
- Per-contract severity configuration (global only for this iteration)
- Per-contract override of the unmatched behavior
- Full baseline lifecycle management UI or reporting dashboard

## Decisions

### D1: Single-pass tracking (Approach B) over two-pass or post-hoc

Instrument `IsIgnored` to record which ignore entries matched, rather than running detection twice or matching after the fact.

- **Why not two-pass (A):** 2× compute cost for each contract — unacceptable for large codebases.
- **Why not post-hoc (C):** Would need to collect unfiltered violation sets, requiring more plumbing than the tracking approach.
- **Why tracking (B):** One pass, no redundant detection, ~5 line additions per call site + one new tracker object per contract.

### D2: Index-based tracking over object-reference tracking

`ArchitectureIgnoreUsageTracker` stores matched indices (`HashSet<int>`), not references to `ArchitectureIgnoredViolation` objects.

- **Why index:** `ArchitectureIgnoredViolation` is a mutable class (no value equality). Duplicate entries are possible. Index maps deterministically to YAML position — diagnostics can say "ignored_violations[2] is stale".
- **Trade-off:** Index stability requires the ignore list not to change between tracking and diffing (it doesn't — same list reference throughout).

### D3: New `ArchitectureUnmatchedIgnoredViolation` type over reusing `ArchitectureViolation`

Separate record with `ContractName`, `ContractId`, `IgnoreIndex`, `SourceType`, `ForbiddenReference`, `Reason`.

- **Why not reuse:** `ArchitectureViolation` represents real dependency violations (source type → forbidden reference). Stale baseline entries are policy-hygiene diagnostics. Mixing them would break tooling that relies on the violation schema, and the formatter expects `ForbiddenReferences` (plural) + `ForbiddenNamespace`, neither of which maps well.
- **Cost:** One more record type, one more formatter method. Clean separation.

### D4: Global config `analysis.unmatched_ignored_violations` over per-contract

Single YAML field under `analysis:` with values `error | warn | off`. Default: `error`.

- **Why not per-contract:** Adds schema complexity, new way to clutter policy files. A global switch is sufficient for the first iteration.
- **Why `error` default:** Stale baseline = broken trust in architecture policy. The whole point of issue #16 is preventing silent accumulation. Anyone migrating can opt into `warn`.

### D5: No short-circuit when tracking

When a tracker is present, `IsIgnored` scans ALL ignore entries for each (source, ref) pair, not stopping at the first match.

- **Why:** Without this, overlapping ignores (e.g., a broad `*` entry before a specific one) would cause the specific entry to appear unmatched — a false positive. Every ignore entry that matches the violation must be tracked, not just the first one that short-circuited.
- **Performance:** Ignore lists are typically small (<20 entries). The linear scan is negligible. Without a tracker, short-circuit is preserved for zero-overhead in the non-tracking path.

### D6: Both `strict` and `audit` modes

The CLI already has full audit-mode support (ternary branching in `Program.cs`). Unmatched detection runs in both modes; severity is determined by the config, not by the mode.

### D7: `IReadOnlyCollection` → `IReadOnlyList` in finder signatures

`ArchitectureNamespaceViolationFinder`, `ArchitectureExternalDependencyViolationFinder` currently accept `IReadOnlyCollection<ArchitectureIgnoredViolation>`. Index-based tracking requires `IReadOnlyList<T>`. The real sources (`List<T>` from contract models) already implement `IReadOnlyList<T>`, so this is a backward-compatible signature change at call sites.

### D8: Per-contract tracker lifecycle

Each `Check*Contract` method creates a fresh `ArchitectureIgnoreUsageTracker`, passes it to all `IsIgnored` calls within that method, then produces `ArchitectureUnmatchedIgnoredViolation` entries by diffing `contract.IgnoredViolations` indices against `tracker.MatchedIndices`.

For `CheckMethodBodyContract`, the single tracker is shared between the Roslyn scanner (`ArchitectureSourceScanner`) and the IL scanner (`ArchitectureIlMethodBodyScanner`) since their results are merged afterward.

### D9: Separate human section and JSON field

Human output appends an `Unmatched ignored violations:` section after violations and cycles. JSON output adds `"unmatched_ignored_violations": []` at the top level alongside `"violations"` and `"cycles"`.

### D10: Include `reason` in unmatched diagnostics

Every `ArchitectureIgnoredViolation` already requires a `reason` (schema enforces it). Including it in the unmatched diagnostic gives the user immediate context — typically an issue number or migration note explaining why the ignore existed.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| **Performance regression**: full scan of ignores instead of short-circuit | Ignore lists are typically small (<20 entries). Short-circuit preserved when tracker is null (production path without unmatched detection, e.g. `off` mode). |
| **Signature churn**: `IReadOnlyCollection` → `IReadOnlyList` in 2-3 finders | Backward-compatible at call sites (`List<T>` implements both). Only internal APIs affected. |
| **Overlapping ignores with same pattern appear as "matched" when only one is needed**: e.g., two identical entries — both are tracked as matched, neither appears unmatched | This is correct behavior: both entries are valid (they both match the violation). A separate lint could detect duplicates, but that's out of scope. |
| **Tracker not threaded to a call site**: a new `IsIgnored` call added later without tracker | Compile-time risk — the tracker parameter is optional (`null` by default), so the call compiles but the ignore entry won't be tracked. Mitigation: code review + integration tests asserting that known ignores appear as matched. |
| **Method-body tracking split across two scanners**: Roslyn and IL scanners each have their own `IsIgnored` calls | Shared tracker reference passed to both scanners in `CheckMethodBodyContract`. Both scanners mark into the same `HashSet<int>`. Either scanner matching is sufficient. |
