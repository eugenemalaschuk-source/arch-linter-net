## Context

Round 2 of PR #416 review closed 8 gaps but left 8 more, confirmed against commit `5828521`. This round
closes those 8 without changing sequential (non-cancelled) behavior.

## Goals / Non-Goals

**Goals:** make single-format rendering genuinely interruptible per finding; stop discarding
cleanup-timeout evidence; stop the cancellation notice from overwriting an existing report file; close the
sibling-import and IL-scanner gaps; close the temp-write-to-rename race in baseline/public-API publication;
make the spec match the implementation.

**Non-Goals:** profile-generation/artifact-cleanup cancellation (still tracked by issue #418, blocked on
#374); new CLI flags; changing sequential validation identity, ordering, or output schema when cancellation
is never requested.

## Decisions

- `ICliRuntime`'s three cancellation-aware overloads (`FormatViolationsForHumans`/
  `FormatResultForCiArtifacts`/`FormatResultAsSarif`) are declared as default interface methods that ignore
  the token and delegate to the pre-existing overload, mirroring the established pattern already used for
  `FormatClassificationFactsForHumans`/`FormatResultAsSarif`'s additive overloads in this same interface —
  every existing test fake implementing `ICliRuntime` keeps compiling unaffected; only the concrete
  `CliRuntime` overrides them with real per-finding cancellation checks.
- The same DIM pattern is used one layer down, in `IArchitectureDiagnosticFormatter` and directly on the
  concrete `ArchitectureSarifFormatter`/`ArchitectureDiagnosticFormatter` classes (which `CliRuntime` holds
  by concrete type, not interface, so no interface change was even required there for the SARIF/CI-artifacts
  JSON builders) — each new overload requires the previously-optional trailing parameter
  (`subtractiveMatcherParticipation`) to become required, which is what keeps the new, wider overload
  unambiguous by arity against the existing one; this exact trade-off is already documented at the existing
  `sourceExpansion`-carrying overloads these new ones sit beside.
- `CiArtifactsRequest` (the private request struct `BuildCiArtifactsJson` consumes) gained a
  `CancellationToken` init-only property defaulting to `CancellationToken.None` — fully private, so it has
  zero impact on any public signature; only the widest public overload sets it.
- `BuildStateProcessCleanupTimedOutException` is caught explicitly, ahead of the general
  `OperationCanceledException` catch (a subtype-before-supertype `catch` ordering C# requires), so its
  `ProcessId`/`TimeoutMs` evidence reaches every CLI output format instead of being silently generalized.
- `WriteCancellation` moved from `allowFileSinks: true` to `allowFileSinks: false` — this also incidentally
  removes any need for the `routingCancellationToken` workaround the previous round introduced, since the
  stream-fallback path never touches `RouteErrorToAllSinks` and therefore never depends on the (already
  cancelled) handler token at all.
- `ArchitecturePolicyImportGraphResolver`'s per-sibling check sits in the `for` loop inside `Visit()`, before
  each `VisitImport` call — not only at the top of `Visit()` itself, which only covers a document's own
  entry, not the loop that walks its sibling imports.
- `BaselineWriteGate`/`PublicApiTwoPhaseWriter` both check the token immediately before `RenameTempToTarget`
  and delete the staged temp file (best-effort) on cancellation, closing the specific window between
  `WriteAllTextToTemp` succeeding and the rename that publishes it.

## Risks / Trade-offs

- [Rendering interruption granularity for cycles/coverage/policy-consistency/unmatched/classification
  sections' own delegate calls] → Only violations and coverage findings (the two largest, identically-shaped
  lists) got per-item cancellation-aware JSON/SARIF serialization in this round; the smaller sections still
  render as one call. Deferred as disproportionate to their typical size — revisit if profiling shows
  otherwise.
