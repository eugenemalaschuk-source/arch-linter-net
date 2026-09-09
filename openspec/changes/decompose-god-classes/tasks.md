## 1. Partial declaration evidence and policy

- [x] 1.1 Extend source parsing and indexing with a stable, path-complete partial declaration inventory while preserving existing ambiguity semantics.
- [x] 1.2 Extend layout-convention policy/schema validation with `max_declarations_per_type` and reject non-positive values.
- [x] 1.3 Evaluate declaration-count expectations in strict/audit layout checks and emit deterministic human, JSON, and SARIF diagnostics.
- [x] 1.4 Add unit, integration, and policy-validation coverage for declaration-count evidence, selector scope, audit behaviour, and stable paths.
- [x] 1.5 Add the production audit self-policy rule and record its baseline partial aggregates.

## 2. Production responsibility extraction

- [x] 2.1 Map the dependencies and responsibility seams currently hidden in `ArchitectureAnalysisSession`; extract the first cohesive analysis collaborator with focused parity tests.
- [x] 2.2 Extract the remaining `ArchitectureAnalysisSession` family-analysis collaborators and reduce the session to orchestration without `partial` declarations.
- [x] 2.3 Replace the `ArchitectureContractGroups` partial aggregation with a single purpose-named, non-partial contract-group binding root while preserving YAML and public API compatibility.
- [x] 2.4 Replace `ArchitectureDiagnosticFormatter` and SARIF formatter partial aggregates with named renderers/projections while preserving human, JSON, and SARIF output parity.
  - [x] Extract the v0.8 applicability, waiver, imported-diagnostic, policy-inventory, and contract-surface projections behind compatibility façades, and move imported-diagnostic SARIF locations to a focused projection collaborator (#777).
  - [x] Extract the remaining diagnostic and SARIF responsibility families into top-level internal, non-partial renderers/projectors; retain one public compatibility façade per formatter, preserve output/cancellation parity, and remove the two exact declaration-count waivers (#802).
- [ ] 2.5 Remove incidental production partial aggregates created by command, validation, policy-loading, and source-index splits; every replacement must have a named responsibility.
  - [x] Replace the five-declaration `ValidateCommandHandler` and two-declaration `ReportCoordinator`
    aggregates with non-partial responsibility collaborators, preserve command/cache/cancellation/profile
    and sink-rendering semantics, and remove their exact declaration-count waivers (#803).
  - [x] Extract `BuildStatePreparationService` runtime preparation into the non-partial `BuildStateRuntimeBuildPreparation`, preserving structured child-process arguments, cancellation cleanup, and receipt-trust regressions; remove its exact declaration-count waiver (#807).
  - [x] Replace `ArchitectureSourceSetExpander`'s inclusion and layer-template fragments with `ArchitectureSourceSetInclusionResolver` and `ArchitectureLayerTemplateContainerExpansionRecorder`.
  - [x] Extract `ArchitectureTopologyEvaluator` observation into explicit validation/capture and metric ownership collaborators while preserving topology evidence and identities (#773).
  - [x] Extract `ArchitectureMetricEvaluator` metric-kind calculators while retaining one
    coordinator, session, topology projection, applicability/output authority, canonical
    contributor semantics, and focused calculator seams (#779).
  - [x] Extract `SarifEvidenceReader` repository-local artifact acquisition and SARIF
    document/context/source projection collaborators while retaining one public trust facade,
    current evidence semantics, and focused collaborator/consumer coverage (#774).
  - [x] Extract `ArchitectureAnalysisSnapshot` v0.8 applicability, measurement, input/review,
    cache-work, and evaluation-inventory projections into non-partial internal collaborators while
    retaining one public lifecycle/fact-set owner; prove strict/audit/measure/topology reuse and
    remove the reviewed declaration-count exception (#776).
  - [x] Decompose `ArchitectureContractSurfaceExposureScanner` into non-partial shared scan
    state and purpose-named traversal, member/accessor, and attribute-metadata collaborators
    without changing recursive evidence, reflection-incomplete paths, or consumers (#775).
  - [x] Freeze the post-v0.8 self-health baseline at `929983eb985dab934f886cc2f8e25824ac854984`
    with a durable raw-evidence bundle; assign every reviewed aggregate and audit finding to a focused
    remediation owner: existing #802/#803, #807 through #816, Core Model #819, History #820, and
    factual `build_state_preflight` presentation #801. The shared policy/OpenSpec hunks remain serial
    coordination work; this records ownership and does not mark the extraction task complete (#804).
  - [x] Move the 24 frozen Core Model audit-layout identities into the existing `Models` source
    directory without changing namespaces, API, schema, or policy; retain a focused negative
    regression for a record outside that directory (#819).
  - [x] Extract `ArchitectureSourceFileFactIndex` bounded source traversal and its parallel partition
    seam into `ArchitectureSourceFileFactTraversal`, retaining one lazy/cached facade, deterministic
    merge and cancellation behavior, and rejecting enumerated paths outside configured source roots (#810).
  - [x] Extract `RegularFileHandleReader` repository-local root/traversal handling into the
    non-partial `RepositoryLocalRegularFileReader`, retaining native regular-file validation,
    containment/reparse/regular-file protections, the existing evidence-file seam, and bounded-read
    ownership; remove only its exact declaration-count waiver (#813).
  - [x] Extract target-framework selection from `ArchitectureAssemblyResolutionService` into the
    non-partial `ArchitectureTargetFrameworkSelector`, retaining one artifact-resolution and
    cancellation owner, selected build-output path semantics, and removing its exact declaration-count
    waiver (#808).
  - [x] Extract `ArchitectureCoverageAnalysisService` rule-input coverage summary and finding
    analysis into the non-partial `ArchitectureRuleInputCoverageAnalysisService`, retaining the
    session's canonical cached coverage inventory, descriptor catalog, deterministic ordering, and
    unmatched-ignore collection; remove its exact declaration-count waiver (#809).
  - [x] Extract `LayoutConventionChecker` file-level selector matching into the non-partial
    `LayoutConventionFileSelectorMatcher`, retaining one convention-evaluation facade, the shared
    applicability projection, normalized paths, deterministic selector participation, and the
    cached source-fact path; remove its exact declaration-count waiver (#811).
  - [x] Extract `ArchitecturePublicApiSurfaceScanner` member scanning into the non-partial
    `ArchitecturePublicApiMemberScanner`, retaining one surface materialization, canonical member
    identity/order and visibility filtering, shared incomplete-reflection evidence, and removing its
    exact declaration-count waiver (#814).
  - [x] Extract `ArchitecturePublicApiApplicationService` contract/build/surface resolution into
    the non-partial `ArchitecturePublicApiSurfaceResolver`, retaining one public application facade,
    canonical member identity/order, selector-safety, build-state, cancellation, and disposal
    behavior; remove only its exact declaration-count waiver (#816).
- [x] 2.6 Remove the unused `CelEngine` placeholder and its smoke test; retain the actual CEL evaluator pipeline as the supported execution seam.

## 3. Test-suite cleanup

- [ ] 3.1 Split unrelated CLI test aggregates into focused fixtures without changing scenario coverage (#821).
- [ ] 3.2 Split unrelated Core test aggregates into focused fixtures; retain only dedicated partial-language source fixtures (#822).
  - [x] Extract the v0.8 full-cycle Checkpoint B scenario's orchestration, phase-trace/restore-reuse
    state, and validation/policy-weakening/health-matrix/Unity/reporting phases out of the shared
    `CheckpointBReleaseGateTests` partial aggregate into named `CheckpointBV08*` collaborator types,
    leaving `CheckpointBReleaseGateTests.V08FullCycle.cs` a thin NUnit entrypoint (#778).
- [ ] 3.3 Add regression coverage proving intentional partial-language fixtures remain discoverable and production aggregates are not reintroduced (#822).

## 4. Enforce and verify the final convention

- [ ] 4.1 Switch the production declaration-count self-policy from audit to strict with a maximum of one source declaration and add a negative regression (#805).
- [ ] 4.2 Update architecture capability documentation and OpenSpec specifications with the final convention and any reviewed exceptions (#805).
- [ ] 4.3 Run public API review, policy/lint gates, full tests, and OpenSpec validation; verify that no handwritten production partial aggregate remains (#805).
- [x] 4.4 Model direct CLI command folders as independent feature modules, retain recursive convention rules for their nested folders, and add a negative self-policy regression.
