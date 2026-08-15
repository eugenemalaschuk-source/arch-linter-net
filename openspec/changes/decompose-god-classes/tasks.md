## 1. Partial declaration evidence and policy

- [x] 1.1 Extend source parsing and indexing with a stable, path-complete partial declaration inventory while preserving existing ambiguity semantics.
- [x] 1.2 Extend layout-convention policy/schema validation with `max_declarations_per_type` and reject non-positive values.
- [x] 1.3 Evaluate declaration-count expectations in strict/audit layout checks and emit deterministic human, JSON, and SARIF diagnostics.
- [x] 1.4 Add unit, integration, and policy-validation coverage for declaration-count evidence, selector scope, audit behaviour, and stable paths.
- [x] 1.5 Add the production audit self-policy rule and record its baseline partial aggregates.

## 2. Production responsibility extraction

- [x] 2.1 Map the dependencies and responsibility seams currently hidden in `ArchitectureAnalysisSession`; extract the first cohesive analysis collaborator with focused parity tests.
- [ ] 2.2 Extract the remaining `ArchitectureAnalysisSession` family-analysis collaborators and reduce the session to orchestration without `partial` declarations.
- [ ] 2.3 Replace the `ArchitectureContractGroups` partial aggregation with named contract-group binding/model collaborators while preserving YAML and public API compatibility.
- [ ] 2.4 Replace `ArchitectureDiagnosticFormatter` and SARIF formatter partial aggregates with named renderers/projections while preserving human, JSON, and SARIF output parity.
- [ ] 2.5 Remove incidental production partial aggregates created by command, validation, policy-loading, and source-index splits; every replacement must have a named responsibility.

## 3. Test-suite cleanup

- [ ] 3.1 Split unrelated CLI test aggregates into focused fixtures without changing scenario coverage.
- [ ] 3.2 Split unrelated Core test aggregates into focused fixtures; retain only dedicated partial-language source fixtures.
- [ ] 3.3 Add regression coverage proving intentional partial-language fixtures remain discoverable and production aggregates are not reintroduced.

## 4. Enforce and verify the final convention

- [ ] 4.1 Switch the production declaration-count self-policy from audit to strict with a maximum of one source declaration and add a negative regression.
- [ ] 4.2 Update architecture capability documentation and OpenSpec specifications with the final convention and any reviewed exceptions.
- [ ] 4.3 Run public API review, policy/lint gates, full tests, and OpenSpec validation; verify that no handwritten production partial aggregate remains.
