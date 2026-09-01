## Why

Issues #520-#523 and #507 delivered a trusted Core protocol that reads a repository-local SARIF
artifact, validates its trust/context binding, selects policy-authorized diagnostics, and projects
them into normalized findings and applicability evidence. Today that entire chain is reachable only
by writing custom .NET host code that calls `SarifEvidenceReader`, `SarifExternalDiagnosticSelector`,
`ArchitectureImportedDiagnosticProjector`, and `ArchitectureExternalEvidenceApplicabilityProjector`
directly — the packed `arch-linter-net` CLI has no option that binds a declared `external_evidence`
requirement to a local artifact. `docs/policy-format/external-evidence.md` says so explicitly: "This
page does not define a command-line integration." That gap blocks the v0.8 product promise (#90/#524)
that one packed CLI executes the complete architecture-governance cycle in local and generic CI use,
and is called out as release-blocking P0 product-loop closure in #741.

## What Changes

- Add a new Core orchestration seam, `ArchitectureExternalEvidenceBinder`, that composes the already
  delivered #520/#521/#522/#507 building blocks (read → select → project → applicability) into one
  reusable call, and a merge helper that attaches the result to a `ValidationOutcome` without
  reimplementing trust, selection, normalization, or applicability semantics.
- Add an echo property `ValidationOutcome.ExternalEvidenceRequirements` so a caller can discover which
  `external_evidence` requirements the loaded policy declared, including on a cache-hit reconstruction.
- Add CLI options to the root `arch-linter-net` validate command (not the separate `gate` subcommand,
  which has an unrelated debt-baseline exit-code contract):
  - repeatable `--external-evidence id=<id>,path=<path>[,repository=<v>][,revision=<v>][,scope=<v>]`
    to bind one logical `external_evidence` requirement to a repository-local SARIF artifact plus
    optional producer/CI context;
  - `--evidence-repository`, `--evidence-revision`, `--evidence-scope` to supply the single current
    assessment context shared by every binding in the invocation.
- Wire the CLI to call the new binder once per invocation and merge its result into every requested
  mode's outcome for output/exit-code purposes, while keeping the persistent analysis cache
  (`--cache`) populated only from the un-enriched native outcome, so external evidence is always
  freshly re-read from disk and never baked into a stale cache entry.
- This automatically closes the existing PASS/FAIL/UNASSESSABLE → 0/1/2 exit-code contract (#506) for
  external evidence: once merged applicability records are present, the CLI's existing
  `ResolveValidationExitCode`/`ResolveCombinedValidationExitCode` logic already maps `Unassessable` to
  exit 2 — no new exit-code logic is required.
- Update `docs/policy-format/external-evidence.md` with the real CLI binding flag syntax and a
  copy-paste local/CI example, replacing the "does not define a command-line integration" caveat.

## Capabilities

### New Capabilities
- `cli-external-evidence-binding`: the CLI-level option/manifest surface that binds repository-local
  SARIF artifacts and assessment/producer context to policy-declared `external_evidence` requirements,
  and the deterministic, order-independent merge of the resulting imported diagnostics and
  applicability evidence into the packed CLI's validate output and exit-code contract.

### Modified Capabilities
(none — `external-sarif-evidence`, `external-diagnostic-filtering`, `imported-diagnostic-normalization`,
and `governance-applicability-evidence` keep their existing Core-boundary requirements unchanged; this
change only adds a CLI caller on top of them)

## Impact

- `src/ArchLinterNet.Core/Validation/ValidationOutcome.cs` — new echo property.
- `src/ArchLinterNet.Core/Validation/ArchitectureAnalysisSnapshot.cs` — populate the echo property.
- `src/ArchLinterNet.Core/Caching/AnalysisCacheOutcomeMapper.cs` — thread the echo property through
  cache-hit reconstruction (new optional trailing parameter, source-compatible).
- New file `src/ArchLinterNet.Core/Validation/ArchitectureExternalEvidenceBinder.cs`.
- `src/ArchLinterNet.Cli/Commands/Validate/Application/ValidateCommandOptions.cs`,
  `ValidateCommandDefinition.cs`, `ValidateCommandHandler.cs`, `ValidateCommandHandler.Execution.cs`.
- `docs/policy-format/external-evidence.md`.
- `architecture/api/ArchLinterNet.Core.public-api.txt` (reviewed public API snapshot update via
  `make public-api-update` for the new public Core members).
- New/updated tests in `tests/ArchLinterNet.Core.Tests/` and `tests/ArchLinterNet.Cli.Tests/`.
