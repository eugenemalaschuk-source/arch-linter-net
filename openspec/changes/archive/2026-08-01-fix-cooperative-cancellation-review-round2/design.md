## Context

PR #416 implemented the `cooperative-cancellation` capability and one prior review-fix round
(`fix-cooperative-cancellation-gaps`). A further review round found remaining coarse-only checks and one
canonical-spec self-contradiction.

## Goals / Non-Goals

**Goals:** close every remaining boundary flagged by review, without changing sequential (non-cancelled)
behavior; make the spec internally consistent and honest about what is and is not implemented.

**Non-Goals:** implement profile-generation/artifact-cleanup cancellation (no such capability exists yet —
tracked by issue #418, blocked on #374); introduce new CLI flags; change `ICliRuntime`'s public formatting
interface signatures (would break every test fake implementing it — `ReportCoordinator`'s own methods gained
the token instead, checked between the boundaries it already controls).

## Decisions

- `BuildStateProcessCleanupTimedOutException` derives from `OperationCanceledException` so every existing
  `catch (OperationCanceledException)` handler keeps working unchanged, while carrying the process ID/timeout
  as evidence that cleanup could not be confirmed.
- `ReportCoordinator.WaitForExitOrCancellationCore`-style delegate extraction (for the process-kill timeout)
  and `IArchitecturePolicyDocumentLoader.Load(string, CancellationToken)` as a second interface overload (not
  a default parameter on the existing single-arg method) — the latter because a dedicated test
  (`PolicyDocumentLoader_PublicContractPreservesSinglePathLoadMethod`) asserts the single-path `Load(string)`
  contract stays exactly one parameter.
- `ReportCoordinator`'s Format* methods take an optional `CancellationToken cancellationToken = default`
  rather than requiring every caller to pass one — `RenderReportContent` (used to build error/cancellation
  fallback documents) deliberately renders unconditionally regardless of the real cancellation state, since
  that method's whole job is reporting the error/cancellation itself.
- Baseline/public-API command handlers re-check the token immediately before their write/rename step (and,
  for capture/update/migrate, before a dry-run/already-current preview too) rather than only relying on
  Core's own last check, since a handler can observe cancellation in the window between Core returning and
  the handler's own I/O.

## Risks / Trade-offs

- [Deep IL/session scanners beyond `ArchitectureTypeIndex`/`ArchitectureRoleIndex`] → Left uninstrumented in
  this round; they run per-type during contract execution, which already has family-level cancellation
  boundaries (`ArchitectureContractExecutor.Execute`, fixed in the prior round). A dedicated pass over the
  full `Scanning` namespace is a larger, separate increment if profiling shows it matters.
- [Rendering iteration granularity] → `ReportCoordinator` checks between sections/modes it directly controls;
  the single delegated `_runtime.FormatViolationsForHumans`/`FormatResultForCiArtifacts`/`FormatResultAsSarif`
  call for one large findings set inside one mode is not itself interruptible mid-call without adding a
  `CancellationToken` to every `ICliRuntime` formatting method — a breaking change for the many test fakes
  implementing that interface. Deferred as a separate, larger increment.
