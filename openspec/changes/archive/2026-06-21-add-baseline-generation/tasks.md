## 1. Model Layer

- [x] 1.1 Create `ArchitectureBaselineCandidate` record — `record(ContractGroup, ContractId, SourceType, ForbiddenReference)`
- [x] 1.2 Create `ArchitectureBaselineDocument` model — `Version`, `Baseline` dictionary of contract groups to entries
- [x] 1.3 Create `ArchitectureBaselineContractEntry` model — `Id`, `IgnoredViolations` list
- [x] 1.4 Wire `ArchitectureBaselineDocument` into YamlDotNet deserialization with YAML member aliases (`baseline`, `id`, `ignored_violations`)

## 2. Baseline Loader

- [x] 2.1 Create `ArchitectureBaselineLoader.LoadFromPath(path)` — deserializes baseline YAML into `ArchitectureBaselineDocument`
- [x] 2.2 Validate required fields: `version` must be 1, each entry must have non-empty `id`, each `ignored_violations` entry must have non-empty `source_type` and `forbidden_reference`
- [x] 2.3 Report descriptive error for invalid or missing baseline file

## 3. Runner: Baseline Candidate Collection

- [x] 3.1 Add `_baselineCandidates` list (`List<ArchitectureBaselineCandidate>`) to `ArchitectureContractRunner`
- [x] 3.2 Add public `BaselineCandidates` property (`IReadOnlyList<ArchitectureBaselineCandidate>`)
- [x] 3.3 In `CheckCycleContract`: after `IsIgnored` returns `false` (line 280), add candidate with `(contractGroup, contract.Id, sourceTypeName, referencedTypeName)`
- [x] 3.4 In `CheckAcyclicSiblingContract`: after `IsIgnored` returns `false` (line 340), add candidate
- [x] 3.5 In `CheckAllowOnlyContract`: after `IsIgnored` returns `false` in LINQ Where (line 231-232), add candidate
- [x] 3.6 In `CheckProtectedContract`: after `IsIgnored` returns `false` (line 542-543), add candidate
- [x] 3.7 In `CheckContract` (dependency): pass candidate collector through `FindNamespaceViolations` / `FindTransitiveNamespaceViolations`
- [x] 3.8 In `FindNamespaceViolations`: when `IsIgnored` returns `false`, record candidate via callback or out parameter
- [x] 3.9 In `FindTransitiveNamespaceViolations`: same as 3.8
- [x] 3.10 In `FindViolations` (external dependency): same as 3.8
- [x] 3.11 In `CheckLayerContract`: same as dependency path via `FindNamespaceViolations`
- [x] 3.12 In `CheckIndependenceContract`: same as dependency path
- [x] 3.13 In `CheckMethodBodyContract`: pass candidate collector through method body scanners
- [x] 3.14 In `CheckExternalContract`: same as 3.10
- [x] 3.15 Ensure candidates are only collected when `_enableUnmatchedIgnoreTracking` is true (reuse flag semantics — candidates only during validation, not during baseline generation)

## 4. Baseline Generator

- [x] 4.1 Create `ArchitectureBaselineGenerator.Generate(document, runner, reason)` — collects candidates from runner, deduplicates, sorts, returns `ArchitectureBaselineDocument`
- [x] 4.2 Implement deterministic sorting: sort candidates by `(contractGroup, contractId, sourceType, forbiddenReference)` using ordinal comparison
- [x] 4.3 Implement deduplication: identical `(contractGroup, contractId, sourceType, forbiddenReference)` appears only once
- [x] 4.4 Implement baseline file writer — serialize `ArchitectureBaselineDocument` to YAML with `version: 1` and `baseline:` root key
- [x] 4.5 Support optional `--reason` argument — overrides default `"generated baseline"` in all entries

## 5. Merge: Baseline into Policy

- [x] 5.1 Create `ArchitectureBaselineMerger.Merge(document, baselineDocument)` — iterates baseline entries, finds matching contracts by `Id` per group
- [x] 5.2 For each matched contract, append baseline `ignored_violations` to `contract.IgnoredViolations`
- [x] 5.3 Implement deduplication: skip if identical `(source_type, forbidden_reference)` already exists in contract's manual ignores
- [x] 5.4 Report error for baseline entries referencing unknown contract IDs (non-zero exit, list unknown IDs)

## 6. CLI: `baseline generate` Subcommand

- [x] 6.1 Add `baseline` command group with `generate` subcommand to CLI `switch` parser
- [x] 6.2 Parse `--config` (default: `architecture/dependencies.arch.yml`) and `--output` (required) flags
- [x] 6.3 Parse optional `--reason` flag with default `"generated baseline"`
- [x] 6.4 Implement `baseline generate` execution flow:
  - Load policy document (existing `ArchitectureContractLoader`)
  - Resolve assemblies (existing resolver)
  - Create runner and run all checks (existing flow)
  - Collect baseline candidates from runner
  - Generate `ArchitectureBaselineDocument` via generator
  - Write to output path
- [x] 6.5 Handle errors: policy load failure, assembly resolution failure, output path write failure → exit code 2

## 7. CLI: `validate --baseline` Flag

- [x] 7.1 Add `--baseline` flag to validate command parsing
- [x] 7.2 When present: load baseline file, merge into policy document before runner creation
- [x] 7.3 Report error for baseline load failure or unknown contract IDs
- [x] 7.4 Update help text for both `validate` and `baseline generate`

## 8. JSON Schema

- [x] 8.1 Create `schema/baseline.schema.json` with `version` (int), `baseline` root object, contract group entries
- [x] 8.2 Each group entry: `id` (required string), `ignored_violations` (array, reusing same shape as policy schema's `ignoredViolation` definition)
- [x] 8.3 Ensure schema validates minimal entry (only `id` + `ignored_violations` required, no contract definition fields)

## 9. Tests

- [x] 9.1 `ArchitectureBaselineDocument` serialization — round-trip YAML produces identical structure
- [x] 9.2 `ArchitectureBaselineMerger` — merge with matching ID appends ignores
- [x] 9.3 `ArchitectureBaselineMerger` — unknown ID produces error
- [x] 9.4 `ArchitectureBaselineMerger` — deduplication of identical `(source_type, forbidden_reference)`
- [x] 9.5 `ArchitectureBaselineMerger` — merged ignores participate in existing ignore matching
- [x] 9.6 Baseline generator — clean project produces empty baseline (no entries)
- [x] 9.7 Baseline generator — single violation produces one exact `(source_type, forbidden_reference)` entry
- [x] 9.8 Baseline generator — deterministic output: repeated runs produce identical file
- [x] 9.9 Baseline generator — manual ignores are NOT duplicated in baseline
- [x] 9.10 Baseline generator — cycle violations produce type-level edge entries
- [x] 9.11 Baseline generator — acyclic sibling violations produce type-level edge entries
- [x] 9.12 Baseline generator — multiple contract types each produce correct entries
- [x] 9.13 Baseline generator — optional `--reason` overrides default
- [x] 9.14 Validation with `--baseline` — baseline suppresses violations, new violations still fail
- [x] 9.15 Validation with `--baseline` — stale baseline entries trigger unmatched alerting
- [x] 9.16 CLI integration — `baseline generate --config --output` produces valid file
- [x] 9.17 CLI integration — `validate --baseline` consumes baseline and filters violations
- [x] 9.18 CLI integration — unknown contract ID in baseline produces error exit code 2
- [x] 9.19 Run full acceptance gate (`rtk make acceptance`) — no regressions

## 10. Documentation

- [x] 10.1 Add CLI reference for `baseline generate` in `docs/cli/index.md`
- [x] 10.2 Add `--baseline` flag documentation to validate CLI reference
- [x] 10.3 Create or update `docs/guides/migration-baselines.md` with generation and lifecycle workflow
- [x] 10.4 Add baseline file format reference (inline YAML example) to docs
- [x] 10.5 Update AI-facing policy authoring guide in `docs/ai/policy-authoring-guide.md`
- [x] 10.6 Not applicable — baseline workflow does not affect PR review guidance

## 11. Integration and Validation

- [x] 11.1 Verify baseline generation against the project's own `architecture/dependencies.arch.yml` (test that self-validation passes with generated baseline)
- [x] 11.2 Run `rtk make acceptance` — all lint and test targets pass
- [x] 11.3 Run `rtk make lint-architecture` — self-architecture validation passes with generated baseline
