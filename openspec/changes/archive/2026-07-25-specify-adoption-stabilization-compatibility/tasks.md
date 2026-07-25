## 1. Compatibility capability

- [x] 1.1 Add the `adoption-stabilization-compatibility` normative spec.
- [x] 1.2 Define the 0.5.1 release and schema/version registry.
- [x] 1.3 Define stable identity versus display evidence.
- [x] 1.4 Define baseline/API snapshot lifecycle and normalized finding projections.
- [x] 1.5 Define output, cache, profiling, concurrency, cancellation, policy-only, support, and security boundaries.

## 2. Architecture documentation

- [x] 2.1 Add the internal compatibility blueprint.
- [x] 2.2 Add the child design-slice/consumer matrix.
- [x] 2.3 Link the blueprint from the internal documentation index.

## 3. Backlog integration

- [x] 3.1 Publish the approved slice map on #355.
- [ ] 3.2 Ensure each remaining child references the applicable slice when implementation begins.
- [ ] 3.3 Perform the final max consistency pass after all slices land.

## 4. Validation

- [ ] 4.1 Run `rtk openspec validate --all --strict`.
- [ ] 4.2 Run repository documentation/acceptance validation.
- [ ] 4.3 Walk #366 Checkpoint A scenarios against approved applicable slices.
- [ ] 4.4 Before closing #355, walk the complete Checkpoint B corpus and reconcile schemas, manifest, CLI/API, docs, and issue wording.
