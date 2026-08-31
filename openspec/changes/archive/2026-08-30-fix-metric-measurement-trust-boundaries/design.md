## Context

The measure-first evaluator already has a single session-owned authority, but
the project projection has retained a legacy simple-name metadata lookup. Its
public results also retain empty arrays on an unassessable outcome, and the
snapshot's separate measurement path misses `Evaluate()`'s terminal
cancellation transition.

## Goals / Non-Goals

**Goals:**

- Preserve the current one-session metric evaluation while making project
  ownership artifact-specific and fail closed.
- Make an unassessable Core result impossible to read as a proven empty set.
- Keep snapshot cancellation terminal across all public evaluation paths.

**Non-Goals:**

- Change existing validation topology display semantics or project metadata
  contract behavior outside metrics.
- Add a new project graph or re-scan assemblies.

## Decisions

### Bind metric project owners from the loaded artifact identity

The session metadata index will build a one-to-one association between each
loaded target assembly's normalized artifact location and discovered project
output artifact candidates. Metric-specific topology projection will use the
normalized project path as its canonical owner contributor. No candidate, or
more than one candidate, produces no owner so existing applicability handling
returns `missing_required_input`.

This avoids simple assembly-name lookup and avoids changing ordinary validation
topology selector/display semantics. A simple-name dictionary with a duplicate
marker was considered, but cannot distinguish two resolved artifacts with the
same output name.

### Model unavailable evidence with nullable Core properties

`Contributors` and `ContributorCount` become nullable when `Value` is null.
The evaluator passes no contributor collection for unassessable results, while
formatters retain their existing null JSON behavior and access collections only
for evaluable results. This is an intentional public API change and the
approved Core surface will be regenerated through its explicit lifecycle.

### Centralize Measure's terminal cancellation transition

Wrap the existing measurement body in `try`/`catch (OperationCanceledException)`
under the snapshot lock and set `_cancelled` before rethrowing. This mirrors
`Evaluate()` without changing ordinary preflight or exception behavior.

## Risks / Trade-offs

- [Some fixture assemblies have no physical location] → owner resolution fails
  closed only for project metrics; assembly metrics remain usable.
- [Public API nullable-property change] → regenerate the reviewed public API
  snapshot and run its read-only check.
- [Resolved artifact paths may not be present in an older construction path] →
  keep zero-candidate behavior unassessable rather than falling back to a name.

## Migration Plan

1. Implement the index, model, and lifecycle changes with focused tests.
2. Regenerate the reviewed Core public API snapshot using the explicit update
   command and verify it with the read-only check.
3. Archive the OpenSpec change after validation succeeds.
