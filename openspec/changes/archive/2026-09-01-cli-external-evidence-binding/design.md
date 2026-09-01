## Context

`ArchLinterNet.Core` already delivers the complete external-evidence trust/selection/normalization/
applicability chain (#520-#523, #507): `SarifEvidenceReader.Read` (one requirement + one optional
artifact + assessment context → a closed `SarifEvidenceReadResult`), `SarifExternalDiagnosticSelector.
Select` (trusted reads with a diagnostic filter → deduplicated, fingerprinted selections),
`ArchitectureImportedDiagnosticProjector.Project` (selections → normalized `ArchitectureFinding`s),
and `ArchitectureExternalEvidenceApplicabilityProjector.Project` (requirements + reads + selection →
shared applicability expected/records). `ValidationOutcome` already has a `WithImportedDiagnostics`
wither and public-but-unused `ApplicabilityExpectedEntries`/`ApplicabilityRecords`/
`AssessmentCompletionEvidence`/`ApplicabilityProjection` properties — nothing in the CLI or the
production `ArchitectureAnalysisSnapshot` pipeline populates them from external evidence today; only
`ArchLinterNet.Core.Tests` exercises the chain directly. The CLI's `ValidateCommandHandler` already
implements the PASS/FAIL/UNASSESSABLE → 0/1/2 exit-code contract (#506) purely by reading
`outcome.AssessmentCompletionEvidence?.State` — so populating that field correctly is sufficient to
get exit-code behavior for free.

The persistent analysis cache (`--cache`) stores `ApplicabilityExpectedEntries`/`ApplicabilityRecords`
as part of `AnalysisCacheOutcomeV1` and reconstructs `AssessmentCompletionEvidence` from them on a
cache hit (`AnalysisCacheOutcomeMapper`). External evidence must never be folded into that persisted
payload, or a later cache hit would silently reuse stale SARIF-derived facts, or double-concatenate
them into a growing list across repeated cache-population cycles.

## Goals / Non-Goals

**Goals:**
- Let the packed `arch-linter-net` CLI bind repository-local SARIF artifacts to declared
  `external_evidence` requirements and get imported diagnostics, applicability evidence, and the
  existing 0/1/2 exit-code contract for free.
- Reuse #520/#521/#522/#507 verbatim; the CLI only supplies inputs (paths, ids, context) and merges
  already-produced outputs.
- Keep the change local to the validate command's own files plus one small, well-precedented Core
  addition (an echo property + one new orchestration class).
- Deterministic, order-independent multi-binding identity (bindings keyed by logical id, not position).

**Non-Goals:**
- Changing the separate `gate` subcommand or its unrelated debt-baseline exit-code contract.
- Changing analysis-cache key hashing/eligibility, or persisting external evidence into the cache.
- A fourth CLI exit code — reuses the existing 0/1/2 contract via `AssessmentCompletionEvidence`.
- Any new trust, selection, fingerprinting, or applicability algorithm — the CLI is a caller, not a
  second implementation.
- Analyzer execution, remote SARIF fetch, or producer service integration (already Core non-goals).

## Decisions

**One new Core orchestration class, not CLI-local glue.** `ArchitectureExternalEvidenceBinder` lives
in `ArchLinterNet.Core.Validation` and composes the existing public APIs (`Evaluate`) plus a merge
helper (`Attach`) that folds the result into a `ValidationOutcome`. Putting this in Core (not CLI-only
code) keeps it reusable and independently testable in `ArchLinterNet.Core.Tests`, matches "CLI and
Testing depend only on Core" (`AGENTS.md`), and lets any future non-CLI host reuse the same seam.
Alternative considered: keep the glue entirely inside `ValidateCommandHandler`. Rejected — it would be
untestable without the full CLI harness and would not satisfy "usable by ... packed external consumers
without calling Core APIs directly" as cleanly, since the binder itself becomes the reusable Core API.

**`Attach` recomputes `Passed`/`AssessmentCompletionEvidence` by re-deriving the same formula
`ArchitectureAnalysisSnapshot.Applicability.cs` already uses privately** (`ordinaryPassed &&
completion?.State is not (Fail or Unassessable)`), rather than trying to reuse those private methods.
Necessary because `DeriveAssessmentCompletion`/`HasPassedAssessment`/`ProjectApplicability` are
`private static` members of a different partial class family; duplicating a three-line boolean formula
is far cheaper and safer than widening that class's visibility for one external caller. `outcome.
NativePassed` (the pre-applicability, pre-imported-diagnostics conformance flag `ValidationOutcome`
already exposes publicly) stands in for the snapshot's local `ordinaryPassed` — they are equal today
because no other applicability producer exists yet, so `outcome.ApplicabilityExpectedEntries`/
`Records` are always empty before `Attach` runs.

**External evidence is bound after cache population, never before, and cache population always uses
the pre-`Attach` (native) outcome.** `ValidateCommandHandler.Execution.cs` keeps two references: the
outcome(s) as returned by `_runtime`/`snapshot.Evaluate` (native, used for `TryPopulateCache`) and the
`Attach`-enriched outcome(s) (used for routing, `--profile`, and the exit code). This guarantees SARIF
bytes are read fresh on every invocation regardless of `--cache`, and that a cache hit's reconstructed
`ApplicabilityExpectedEntries`/`Records` never already contain a previous run's external-evidence
entries (which `Attach` would otherwise concatenate onto again, corrupting the applicability join with
duplicate control identities). Alternative considered: fold external evidence into the cache key so a
hit can skip re-reading SARIF too. Rejected as disproportionate scope for this issue — it would touch
`AnalysisCacheKey` hashing and cache-eligibility correctness, which the issue's non-goals reserve for
future work; freshly reading a repository-local SARIF file is cheap relative to the rest of the
pipeline this cache already exists to avoid re-running.

**`ValidationOutcome.ExternalEvidenceRequirements` is a new echo property**, populated from
`_document.ExternalEvidence` in both `EvaluateCore` and `BuildBlockedOutcome`, and threaded through
`AnalysisCacheOutcomeMapper.FromCacheOutcome` as a new optional trailing parameter (default `null` →
empty, source-compatible). This mirrors the existing `SourceExpansion` echo exactly: "portable run
metadata, not an analysis result" that the CLI needs to know what the loaded policy declared without
re-parsing the policy file a second time. Without this, a `--cache` hit would report zero declared
requirements even when the policy has some, causing every CLI-supplied `--external-evidence` binding
on a cache-hit run to be rejected as "unknown id."

**CLI option shape: repeatable structured `--external-evidence` values, mirroring the existing
`--report format=destination` pattern.** One occurrence = one binding: `id=<id>,path=<path>
[,repository=<v>][,revision=<v>][,scope=<v>]`. Chosen over a growing set of positional tokens (ruled
out explicitly by the issue) and over a separate manifest *file* format (would add a second
policy/evidence-adjacent file format for no behavioral gain, and the issue explicitly warns against
letting "any manifest ... become a second policy/evidence semantic model" — reusing the option-value
manifest shape the CLI already has for `--report` avoids inventing one). Bindings are keyed by their
`id=` field and matched against declared requirements by dictionary lookup, not position, so ordering
two `--external-evidence` occurrences never changes identity. Malformed syntax or a duplicate `id=`
across occurrences is an immediate parse error (mirrors `ReportParseError`), surfaced before any
policy/build work starts. An `id=` that does not match any declared requirement is only knowable after
policy load, so it surfaces as an `ArgumentException` from `ArchitectureExternalEvidenceBinder.
Evaluate`, caught by the handler's existing generic execution-error path (exit 2) — the same category
every other "invalid runtime configuration" failure in this command already uses.

**No per-mode re-evaluation.** SARIF reading/selection/projection is mode-independent (severity→mode
mapping lives on each finding, mirroring how `diagnostic_filter.severity` is documented today); the
binder runs once per CLI invocation and `Attach`es the identical result to every requested mode's
outcome, exactly like the existing reference scenario test
(`ExternalDiagnosticsFederationReferenceScenarioTests`) attaches one projection covering both strict
and audit findings to one result.

**Scope: the root `arch-linter-net` validate command only, not `gate`.** `gate`'s exit codes (0/1/2)
mean something unrelated (new/resolved/stale debt vs. invalid comparison) and it does not currently
read `AssessmentCompletionEvidence` at all; extending it is a materially different, unscoped change.

## Risks / Trade-offs

- [Cache/enrichment ordering bug: enriching the outcome before `TryPopulateCache` would let stale
  external-evidence records leak into the cache and double-concatenate on the next hit] → Mitigated by
  keeping explicit native vs. enriched outcome references in `ValidateCommandHandler.Execution.cs` and
  covering it with a focused CLI test that populates the cache, then re-runs with `--cache` against
  changed SARIF bytes and asserts the fresh result (not a stale cached one) is reported.
- [`Attach`'s reimplementation of the pass-state formula could silently drift from
  `ArchitectureAnalysisSnapshot.Applicability.cs`'s private original if that logic ever changes] →
  Mitigated with an explicit code comment cross-referencing both locations, and a Core test asserting
  `Attach`'s pass-state matches `ArchitectureApplicabilityEvaluator`/`ArchitectureApplicabilityProjector`
  behavior directly (same evaluator/projector calls, so only the three-line boolean formula itself is
  duplicated).
- [Every `--external-evidence` binding re-reads and re-hashes its SARIF file on every invocation, even
  under `--cache`] → Accepted: SARIF artifacts are bounded (few MB, existing #520 limits), and this
  matches the "always freshly read" trust posture the Core protocol already requires (no filename/mtime
  freshness inference).

## Migration Plan

Additive only: new CLI options with no default binding, a new optional public Core method/property, a
source-compatible optional trailing cache-mapper parameter. Existing invocations without
`external_evidence` in policy or `--external-evidence` on the command line are unaffected (the binder
returns its empty fast path). No data migration; the reviewed public API snapshot is updated via
`make public-api-update` as an explicit, reviewed step per the repository's own release lifecycle.

## Open Questions

None — all required scenarios from issue #741 map directly onto the existing #520-#523/#507 Core
behavior; the CLI layer only needs to supply/merge, not decide, evidence trust outcomes.
