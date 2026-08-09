## 1. Model and schema

- [x] 1.1 Add `OverlapsWith` (`List<string>`, YAML alias `overlaps_with`, default empty) to `ArchitectureLayer` in `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`.
- [x] 1.2 Add `overlaps_with` to `$defs/layer` in `schema/dependencies.arch.schema.json`.
- [x] 1.3 Add `overlaps_with` to the equivalent layer definition in `schema/dependencies.arch.fragment.schema.json`. (Fragment schema references `$defs/layer` from the main schema via `$ref`, so 1.2 covers both.)

## 2. Validation

- [x] 2.1 In `src/ArchLinterNet.Core/Contracts/Validators/LayerNamespacesValidator.cs`, validate each `overlaps_with` entry: non-empty, references a declared layer in `document.Layers`, and is not the layer's own name. Throw `InvalidOperationException` naming the offending layer and entry, matching the existing exclude-validation error style.

## 3. Runtime overlap detection

- [x] 3.1 In `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PolicyConsistency.cs`, add `IsAcknowledgedOverlap(layerNameA, layerNameB, internalLayers)` checking either layer's `OverlapsWith` for the other's name.
- [x] 3.2 Call it in `TryCreateLayerOverlapFinding` alongside `IsContainmentRelationship`; return null (no finding) when acknowledged.
- [x] 3.3 Update the finding message to name `overlaps_with` and the namespace-narrowing alternative instead of claiming an unspecified "explicit documented allowance".

## 4. Tests

- [x] 4.1 `tests/ArchLinterNet.Core.Tests/PolicyConsistencyCheckTests.cs`: two internal layers where one declares `overlaps_with` the other are not flagged.
- [x] 4.2 Same file: acknowledgment declared on the non-alphabetically-first layer (or the "other side") still reconciles the pair (declaring on either side is sufficient).
- [x] 4.3 Same file: an unrelated layer pair without `overlaps_with` is still flagged even when a third layer declares unrelated `overlaps_with` entries (no cross-pair leakage).
- [x] 4.4 Validator test (`tests/ArchLinterNet.Core.Tests/LayerResolverTests.cs`) for `overlaps_with` referencing an undeclared layer name — load fails with actionable error.
- [x] 4.5 Validator test for `overlaps_with` self-reference — load fails with actionable error.

## 5. Docs

- [x] 5.1 `docs/reference/yaml-schema.md`: add `overlaps_with` to the `## layers` field block and prose; update the `policy_consistency` section's overlap bullet to describe the real mechanism.
- [x] 5.2 `docs/policy-format/layers-and-namespaces.md`: add an `## Overlapping layers` section documenting `overlaps_with` with a short example, following the existing `## Excluding namespaces from a layer` pattern.

## 6. Spec sync and validation

- [x] 6.1 Run `make fmt` and inspect formatting changes. (No changes needed.)
- [x] 6.2 Run `make acceptance`; fix any related failures. (Fixed a doc-size lint regression introduced by yaml-schema.md additions; lint-code-size, Core/Cli/CEL test suites all green. Full run also kicked off in CI via the PR.)
- [ ] 6.3 Run `openspec validate --all` after archiving.
