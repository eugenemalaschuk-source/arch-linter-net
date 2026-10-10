## 1. Build the manifest-backed canonical catalog

- [x] 1.1 Add the internal Core role catalog and marker/version contract from `architecture/semantic-annotations.v1.json`; verify a parity test covers all 49 role FQNs, names, scopes, reserved names, generation, and package range.
- [x] 1.2 Validate assembly catalog markers on canonical-role use and fail closed for missing, duplicate, malformed, unsupported-generation, or unsupported-package identities; verify focused tests cover valid assembly-only analysis and every failure code.
- [x] 1.3 Diagnose unknown attribute FQNs under the reserved namespace without treating the marker or known metadata-only identities as role evidence; verify exact-FQN and same-simple-name tests.

## 2. Integrate with classification and policy validation

- [x] 2.1 Resolve canonical role attributes in the existing type/assembly candidate path and role index; verify canonical-only, canonical/custom coalescing, deterministic fail-closed conflicts, and existing source precedence in Core tests.
- [x] 2.2 Preserve all contributing exact FQNs on the existing role fact and expose them through report and Testing projections; verify one role/index result is produced and evidence order is deterministic.
- [x] 2.3 Reject type and assembly YAML mappings in the reserved namespace, including an exact redundant mapping; verify policy diagnostics identify the authored YAML path and user-owned attributes outside the namespace retain current behavior.

## 3. Carry diagnostics through report and cache surfaces

- [x] 3.1 Add the additive canonical-annotation diagnostic model and thread its sorted collection through the analysis outcome, cache-v1 serialization/mapping, CLI human/JSON/coverage output, and Testing adapter; verify cache and Testing parity.
- [x] 3.2 Add stable SARIF projection for canonical identity/compatibility diagnostics without changing native pass state; verify SARIF codes, subjects, FQNs, and details match JSON.
- [x] 3.3 Preserve existing public constructor and deconstruction shapes, update reviewed API snapshots for additive members, and verify `make public-api-check` passes.

## 4. Publish the capability boundary

- [x] 4.1 Add manifest-backed role/generation/package support metadata to `archlinternet.capabilities.json` and update the live schema description and AI authoring help; verify exact capability-to-manifest parity.
- [x] 4.2 Keep public semantic-classification guidance truthful for released v0.9.1 and the developing v0.10.0 line; verify docs lint and inspect generated schema/help output.

## 5. Synchronize and validate

- [x] 5.1 Compare the final behavior and metadata projections against the delta specs, format changed files, and run focused Core/CLI/Testing tests plus strict OpenSpec validation.
- [x] 5.2 Run the issue-required repository acceptance gate, inspect the full diff, synchronize the main specs, and archive `recognize-canonical-annotations`; verify `openspec validate --all --strict` after archive.
