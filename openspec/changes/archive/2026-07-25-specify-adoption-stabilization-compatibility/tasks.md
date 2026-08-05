## 1. Compatibility capability

- [x] 1.1 Add the `adoption-stabilization-compatibility` normative spec.
- [x] 1.2 Define the 0.5.1 release and schema/version registry.
- [x] 1.3 Define stable identity versus display evidence.
- [x] 1.4 Define baseline/API snapshot lifecycle and normalized finding projections.
- [x] 1.5 Define report, cache, profiling, concurrency, cancellation, policy-only, support, and security boundaries.
- [x] 1.6 Reconcile the shipped baseline v2 example with violation `identity_version: 1`.
- [x] 1.7 Preserve artifact `--output` and reserve validation-only `--report` for multi-sink routing.
- [x] 1.8 Replace impossible multi-file transaction claims with per-file atomic replacement and `partial-output` evidence.
- [x] 1.9 Make shipped framework-reference and assembly-aware composition contracts explicit compatibility inputs.

## 2. Architecture documentation

- [x] 2.1 Add the internal compatibility blueprint.
- [x] 2.2 Add the child design-slice/consumer matrix.
- [x] 2.3 Link the blueprint from the internal documentation index.
- [x] 2.4 Record the current consistency audit and open child-owned reconciliation points.

## 3. Backlog integration

- [x] 3.1 Publish the approved slice map on #355.
- [x] 3.2 Define the requirement that each remaining child reference the applicable slice when implementation begins.
- [x] 3.3 Define the single final max-depth consistency pass as a blocking future gate after all slices land.

## 4. Validation

- [ ] 4.1 Run `openspec validate --all --strict`.
- [ ] 4.2 Run repository documentation/acceptance validation.
- [x] 4.3 Perform the current-state cross-slice audit and record unresolved child-owned reconciliation.
- [x] 4.4 Record that the complete Checkpoint B walkthrough and final repository-wide reconciliation remain required before closing #355.
