## 1. Core identity model

- [x] 1.1 Add `ArchitectureViolationIdentity` record to `src/ArchLinterNet.Core/Model/` with `IdentityVersion`, `ContractFamily`, `Kind`, `ContractId`, `SourceAssembly`, `SourceType`, `SourceMember`, `TargetAssembly`, `TargetType`, `TargetMember`, `Occurrence`, `Configuration`.
- [x] 1.2 Add a legacy-pair projection helper (`ToLegacyPair()` or similar) for v1 comparison compatibility.
- [x] 1.3 Extend `ArchitectureBaselineCandidate` (and `ArchitectureBaselineComparisonEntry` where needed) to carry an `ArchitectureViolationIdentity`.

## 2. Versioned document model, loader, schema

- [x] 2.1 Extend the baseline entry model to support the v2 structured fields (nullable, alongside existing `source_type`/`forbidden_reference`/`reason`) — implemented as a dedicated `ArchitectureBaselineIgnoredViolation` type used only by baseline documents, keeping the widely-shared policy-side `ArchitectureIgnoredViolation` untouched.
- [x] 2.2 Update `ArchitectureBaselineLoadingService.ValidateBaseline` to dispatch on `Version` (1 or 2) instead of hard-asserting 1; preserve the existing error for unsupported versions.
- [x] 2.3 `ArchitectureBaselineLoadingService` merge/dedup (`ContractGroupMerger`) — confirmed unchanged is correct: it dedups into the policy's runtime ignore list (glob matching), which is independent of baseline-comparison identity version; no version branching needed there.
- [x] 2.4 Add `version: 2` `$defs` to `schema/baseline.schema.json` alongside the existing `version: 1` shape; verified v1 files still validate (unit + CLI integration tests covering v1 files pass unchanged).

## 3. Comparer and generator rewrite

- [x] 3.1 Update `ArchitectureBaselineComparer.Compare` to branch on baseline document version: v1 legacy key matching unchanged; v2 full-identity structural equality.
- [x] 3.2 Update `ArchitectureBaselineGenerator` to always emit v2 output for `generate`, replacing the flat-string dedup key with `ArchitectureViolationIdentity` equality; `update`/`prune` preserve the input document's existing version.
- [x] 3.3 Add an explicit `status` (`new`/`matched`/`stale`/`configuration_error`) field to the `diff`/`verify` JSON formatters, shared with the new `migrate` command's JSON output.

## 4. Threading identity through call sites

- [x] 4.1 Extend `ArchitectureContractExecutionContext.IsIgnored` with additive optional parameters for assembly/member; untouched call sites default to a v2-shaped identity with `TargetMember` falling back to the full `forbiddenReference` display text (preserving legacy discrimination exactly for unqualified families).
- [x] 4.2 Qualify the primary dependency-contract check (`ArchitectureNamespaceViolationFinder`, used by `strict`/`audit` contracts) with `SourceAssembly`/`TargetAssembly` from the checked `Type` symbols.
- [x] 4.3 Qualify method-body/call contract scanning (`ArchitectureSourceScanner`) with `TargetAssembly` and `TargetMember` (moved out of the display string); a generic, family-agnostic `ArchitectureBaselineCandidateOccurrenceAssigner` (run once after candidate collection) supplies the non-line-based `Occurrence` discriminator for every family, not just method-body — this covers "multiple forbidden calls in one type" more broadly than originally scoped. Full assembly/member qualification for every remaining baseline-capable family remains out of scope per design.md.

## 5. `baseline migrate` command

- [x] 5.1 Add `BaselineMigrateRequest`/`BaselineMigrateOutcome` records under `src/ArchLinterNet.Core/Validation/`.
- [x] 5.2 Implement `IArchitectureBaselineApplicationService.Migrate`: collect current v2 candidates, classify each legacy entry (`matched`/`stale`/`ambiguous`), enforce fail-closed behavior on ambiguity, enforce output-path safety (never equals input, required unless dry-run).
- [x] 5.3 Add `MigrateBaselineSubcommandModule` + `BaselineMigrateCommandHandler` in `src/ArchLinterNet.Cli/Commands/Baseline/`, wired via the existing reflection-based `IBaselineSubcommandModule` discovery.
- [x] 5.4 Update `BaselineHelpTexts` for the new subcommand.

## 6. Docs

- [x] 6.1 Add a "Migrating legacy baselines" section (as "Migrate" subsection) to `docs/guides/migration-baselines.md`; update "Merge semantics" to describe versioned identity instead of the flat `(source_type, forbidden_reference)` pair as the sole contract.
- [x] 6.2 Update `docs/ai/policy-authoring-guide.md` with versioned-identity guidance and a pointer to `baseline migrate`. Reviewed `docs/ai/capabilities.md`, `docs/ai/policy-review-checklist.md`, `docs/ai/semantic-role-governance.md` — their existing baseline mentions are generic ("baselines are frozen debt") and remain accurate without edits.

## 7. Tests

- [x] 7.1 Extended `ArchitectureBaselineComparerTests` with v2 structured matching, same-named-type-different-assembly, and v1-unaffected-by-richer-candidate-identity scenarios.
- [x] 7.2 Extended `ArchitectureBaselineGeneratorTests` with a distinct-occurrence scenario; updated `ArchitectureBaselineIntegrationTests`/round-trip tests for v2 output shape and determinism.
- [x] 7.3 Updated `ArchitectureBaselineMergerTests`/`ArchitectureBaselineLoaderTests` for the new `ArchitectureBaselineIgnoredViolation` type (behavior unchanged, type name only).
- [x] 7.4 Added `ArchitectureBaselineCandidateOccurrenceAssignerTests`; added `Migrate` coverage via CLI handler unit tests (`BaselineCommandHandlerTests.Migrate.cs`) covering matched/stale/ambiguous/dry-run/output-safety at the handler layer.
- [x] 7.5 Added CLI integration tests (`tests/ArchLinterNet.Cli.Tests/CliIntegrationTests.BaselineMigrate.cs`) covering the new subcommand end to end (stale-only migration, dry-run non-write, overwrite refusal, missing-output guard, already-v2 refusal, missing-baseline-file, help text).
- [x] 7.6 Full Core test suite (including `ArchitectureBaselineGroupCoverageTests`) passes unchanged — no group/schema drift surfaces were touched by this change.

## 8. Spec sync and archive

- [x] 8.1 Verified implementation matches the delta spec scenarios (adjusted the SARIF/Testing-API status-field claim during implementation once it was confirmed neither surface exists for baseline comparison today — see design.md).
- [x] 8.2 Run `openspec validate --all`.
- [x] 8.3 Run `openspec archive exact-baseline-identity`.
