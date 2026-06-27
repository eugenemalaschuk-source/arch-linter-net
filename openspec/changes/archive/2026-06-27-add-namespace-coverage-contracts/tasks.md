## 1. OpenSpec and contract binding

- [ ] 1.1 Add the `namespace-coverage-contracts` spec delta for issue #98.
- [ ] 1.2 Bind coverage contract `roots` / `exclude` namespace fields in `ArchitectureContractModels` and validate their namespace patterns during load.

## 2. Runtime execution

- [ ] 2.1 Remove the blanket coverage-family rejection and replace it with a scope-aware guard that allows only `scope: namespace`.
- [ ] 2.2 Add namespace coverage contract execution that classifies namespaces under configured roots as covered, excluded, or uncovered using declared layers, glob layers, and expanded layer templates.
- [ ] 2.3 Report uncovered namespaces in deterministic order with representative type evidence and contract ID/name context.

## 3. Severity and output

- [ ] 3.1 Honor `analysis.coverage` as `error`, `warn`, or `off` without regressing existing non-coverage validation behavior.
- [ ] 3.2 Surface coverage findings in human and JSON output even when severity is `warn`.

## 4. Tests

- [ ] 4.1 Add tests for mapped, unmapped, excluded, generated/test-area, namespace-suffix, and glob/template interaction cases.
- [ ] 4.2 Add tests proving policies without coverage contracts remain unchanged.
- [ ] 4.3 Add tests proving unsupported non-namespace coverage scopes are still rejected.

## 5. Validation and sync

- [ ] 5.1 Run `rtk make fmt`.
- [ ] 5.2 Run `rtk task acceptance:fresh`.
- [ ] 5.3 Sync the final spec text with the implementation, then run `rtk openspec archive add-namespace-coverage-contracts`.
- [ ] 5.4 Run `rtk openspec validate --all`.
