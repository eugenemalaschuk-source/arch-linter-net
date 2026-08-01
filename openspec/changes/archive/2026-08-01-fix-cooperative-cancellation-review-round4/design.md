## Context

Round 3 of PR #416 review closed 8 gaps but left 3 more, confirmed against commit `5671254`. This round
closes those 3 without changing sequential (non-cancelled) behavior.

## Goals / Non-Goals

**Goals:** make finding mapping itself (not just its callers' per-item serialization loop) genuinely
interruptible; give type discovery and Roslyn-based source scanning a token, checked at the same
per-assembly/per-file/per-syntax-tree granularity the rest of the codebase already uses; make the spec match
the implementation.

**Non-Goals:** profile-generation/artifact-cleanup cancellation (still tracked by issue #418, blocked on
#374); new CLI flags; changing sequential validation identity, ordering, or output schema when cancellation
is never requested; interrupting the smaller report sections (cycles, coverage summary counts, policy
consistency, unmatched, classification facts) that round 3 already deferred as disproportionate to their
size.

## Decisions

- `ArchitectureFindingMapper.FromViolations` gained a genuinely new required-position `cancellationToken`
  parameter with a `= default` value, checked at the top of its per-violation loop — every existing call site
  (`ArchitectureDiagnosticFormatter`, `ArchitectureSarifFormatter`) already threads its own token through, so
  no additional overload was needed here; unlike the `ICliRuntime`/formatter-level DIM overloads from earlier
  rounds, this is an internal `Core` helper with no external implementers to protect from a breaking
  signature change.
- `ArchitectureDiagnosticFormatter.FormatCoverageForHumans` follows the exact DIM pattern established for
  `FormatViolationsForHumans` in round 2: a new interface DIM overload on `ICliRuntime` that ignores the
  token and delegates to the existing overload by default, a concrete `CliRuntime` override that forwards the
  real token, and a non-token concrete overload on `ArchitectureDiagnosticFormatter` itself that calls the
  new token overload with `CancellationToken.None` — kept consistent with `FormatViolationsForHumans` even
  though `FormatCoverageForHumans` delegates most of its own per-item work to `FormatViolationsForHumans`
  internally, since round 3 already made that inner call cancellation-aware and this round's job is only to
  thread the token the one remaining hop from `ReportCoordinator`.
- `ArchitectureTypeScanner`'s three methods (`FindTypesInLayer`, `FindTypesInNamespace`, and the private
  `FindTypes` they both delegate to) all gained an optional trailing `CancellationToken cancellationToken =
  default`, checked once per target assembly in `FindTypes`'s outer loop — matching the existing
  per-type/per-assembly boundary convention (`ArchitectureIlMethodBodyScanner`, `ArchitectureTypeIndex`)
  rather than introducing a new granularity. Because `FindTypesInLayer`/`FindTypesInNamespace` did not
  already have optional trailing parameters, this could be added as a plain optional parameter (no DIM/
  overload-arity trick needed) without breaking any caller.
- `ArchitectureSourceScanner.FindMethodBodyViolations` gained the same optional trailing
  `cancellationToken = default`, checked immediately before the (expensive, single-call) Roslyn compilation
  is built and once per syntax tree while analyzing it afterward. Its own `FindMatchingSourceFiles` helper —
  which discovers the candidate file set the compilation is built from — was split into the pre-existing
  4-arg public overload (delegates to a new 5-arg private overload with `CancellationToken.None`) and the new
  5-arg private overload carrying the real token, checked once per file inside `FindSourceFilesForNamespace`;
  this mirrors the exact `FindTypesInLayer`/`FindTypesInNamespace`-over-`FindTypes` shape one file up.

## Risks / Trade-offs

- [Smaller report sections still uninstrumented] → Cycles, policy-consistency, unmatched, and classification
  sections still render as one uninterruptible call, same trade-off round 3 already accepted for the same
  reason (disproportionate to their typical size). Revisit together if profiling ever shows otherwise.
