## 1. Core: binder and outcome plumbing

- [x] 1.1 Add `ValidationOutcome.ExternalEvidenceRequirements` echo property (default empty).
- [x] 1.2 Populate it from `_document.ExternalEvidence` in `ArchitectureAnalysisSnapshot.EvaluateCore`
      and `BuildBlockedOutcome`.
- [x] 1.3 Add an optional trailing `externalEvidenceRequirements` parameter to
      `AnalysisCacheOutcomeMapper.FromCacheOutcome` (the `mode`-taking overload) and pass
      `_document.ExternalEvidence` at its one production call site in `TryEvaluateFromCache`.
- [x] 1.4 Add `src/ArchLinterNet.Core/Validation/ArchitectureExternalEvidenceBinder.cs` with
      `ArchitectureExternalEvidenceBindingResult` (Imported, ApplicabilityExpectedEntries,
      ApplicabilityRecords, `.Empty`), `Evaluate(...)` (validates supplied artifact ids against
      declared requirements, reads one `SarifEvidenceReadResult` per requirement, selects/projects
      only from valid+authorized reads, calls the existing #521/#522/#507 projectors), and
      `Attach(outcome, binding, mode)` (merges expected/records, recomputes completion/projection/
      pass-state, calls `WithImportedDiagnostics`).

## 2. Core tests

- [x] 2.1 Unit tests for `ArchitectureExternalEvidenceBinder.Evaluate`: no requirements/no artifacts
      no-op; one required with findings; one required zero findings; two independent required
      evidences (order-independent); optional absent; required missing; wrong revision/scope/logical
      key; required binding metadata missing; unknown supplied id rejected; duplicate supplied id
      rejected.
- [x] 2.2 Unit tests for `Attach`: merges into existing (empty) applicability collections without
      duplication; sets `Passed`/`NativePassed` correctly for Pass/Fail/Unassessable completion
      states; attaches blocking strict imported findings and non-blocking audit ones; no-op fast path
      when binding is empty.
- [x] 2.3 Test that `AnalysisCacheOutcomeMapper.FromCacheOutcome` round-trips
      `ExternalEvidenceRequirements` when supplied and defaults to empty when omitted.

## 3. CLI: options and parsing

- [x] 3.1 Add `ExternalEvidenceArtifacts`, `ExternalEvidenceAssessmentContext`, and
      `ExternalEvidenceParseError` to `ValidateCommandOptions`.
- [x] 3.2 Add `--external-evidence` (repeatable, `AllowMultipleArgumentsPerToken`), `--evidence-repository`,
      `--evidence-revision`, `--evidence-scope` options to `ValidateCommandDefinition`, with a
      `id=,path=,repository=,revision=,scope=` key=value parser mirroring `ParseReportSinks`
      (duplicate id, unknown key, missing required `id`/`path` all become a parse error). Update
      `HelpText`.
- [x] 3.3 Surface `ExternalEvidenceParseError` in `ValidateCommandHandler.TryWriteImmediateResponse`
      alongside the existing `ReportParseError` check.

## 4. CLI: wiring into execution

- [x] 4.1 In `ValidateCommandHandler.Execution.cs` `ExecuteSingleMode`: after obtaining the native
      outcome, call `ArchitectureExternalEvidenceBinder.Evaluate` once using
      `outcome.ExternalEvidenceRequirements`/`outcome.RepositoryRoot`/CLI options (skip entirely when
      `outcome.PreflightBlocked`), then `Attach` to get the enriched outcome used for routing/
      `--profile`/exit code. Pass the native outcome (not enriched) to `TryPopulateCache`.
- [x] 4.2 Same for `ExecuteCombinedModes`: evaluate the binder once per invocation (not per mode),
      build a parallel enriched `outcomesByMode` list, recompute `allPassed` from the enriched list,
      keep `TryPopulateCache` on the native list.
- [x] 4.3 Catch `ArgumentException` from the binder through the handler's existing generic
      execution-error path (verify no new catch needed — confirm it flows through the existing
      `catch (Exception ex)` in `Execute`).

## 5. CLI tests

- [x] 5.1 `ValidateCommandDefinition`/options parsing tests: valid single/multiple bindings, malformed
      value, duplicate id, missing required key, `--evidence-repository`/`--evidence-revision`/
      `--evidence-scope` parsing.
- [x] 5.2 End-to-end `ValidateCommandHandler` tests against a temp repository with a policy declaring
      `external_evidence` and real SARIF fixture files, covering: required with findings, required
      zero findings, two independent required evidences, optional absent, required missing, wrong
      revision, copied previous-commit artifact, malformed/unsafe path, unknown/duplicate binding id,
      exit code 0/1/2 scenarios (pass, blocking strict imported finding, unassessable).
- [x] 5.3 Cache-interaction test — descoped from a full disk-level `--cache` round trip (population
      requires a real discovered project; `AnalysisCacheStore.Put` rejects zero project manifests
      outright, so a synthetic `ForcedOutcome` cannot exercise real persistence). Covered instead by
      construction (`nativeOutcome` vs the `Attach`-enriched `outcome` are kept as separate variables
      in `ValidateCommandHandler.Execution.cs`, and only `nativeOutcome` reaches `TryPopulateCache`)
      plus the Core-level `Attach_DoesNotDuplicateApplicabilityWhenCalledOnceEach_AcrossTwoOutcomes`
      test proving two independent `Attach` calls never accumulate duplicate applicability records.
- [x] 5.4 Packed-artifact acceptance — covered by the CLI end-to-end tests in
      `ValidateCommandHandlerExternalEvidenceTests.cs` (real repository-local SARIF fixtures read from
      disk through the same `SarifEvidenceReader`/`SarifExternalDiagnosticSelector`/
      `ArchitectureImportedDiagnosticProjector` chain the direct Core reference scenario test
      exercises) rather than a separate published-tool acceptance run, which is out of scope for this
      change's local validation.

## 6. Documentation

- [x] 6.1 Update `docs/policy-format/external-evidence.md`: add a CLI binding section with exact flag
      syntax and a copy-paste local + CI example; remove/replace the "does not define a command-line
      integration" caveat.

## 7. Validation and spec sync

- [x] 7.1 Run focused new/changed tests, `ArchLinterNet.Core.Tests`, `ArchLinterNet.Cli.Tests`.
- [x] 7.2 Run `make fmt` and inspect the diff.
- [x] 7.3 Run `make public-api-check`; run `make public-api-update` if the new public Core members are
      the only diff, then re-run `make public-api-check`.
- [x] 7.4 Run `make lint-architecture`.
- [x] 7.5 Run `openspec validate --all`.
- [x] 7.6 Run `make lint-docs`.
- [x] 7.7 Decide on `make acceptance` given P0/release-blocking risk tier; run it if it materially
      increases confidence.
- [x] 7.8 Synchronize specs against actual implemented behavior, then `openspec archive
      cli-external-evidence-binding` and re-run `openspec validate --all`.
