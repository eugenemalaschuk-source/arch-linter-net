## Why

The v0.10 contract fixes a source-only annotation catalog, but Core still requires each role annotation to be repeated in policy YAML. Recognizing the reviewed identities through the existing classifier removes that duplication while preserving the role index and makes incompatible or unsupported catalog use visible.

## What Changes

- Recognize the manifest's canonical role FQNs through the existing type and assembly attribute classification path.
- Preserve canonical-versus-custom evidence provenance in the existing role fact, and retain the reviewed source precedence and deterministic same-specificity conflict behavior.
- Reject YAML remapping of reserved canonical FQNs and fail closed with actionable diagnostics for unknown reserved-namespace annotations or incompatible assembly catalog identities.
- Publish the supported canonical identities, catalog generation, and package range through capability, schema, and help metadata, checked against the reviewed manifest.
- Keep annotation evidence separate from policy authority, automatic layer creation, and reviewed public API membership; leave metadata-only annotation composition to #572.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `semantic-classification-model`: Define canonical evidence provenance and how canonical evidence enters the existing classifier and role index.
- `semantic-annotation-contract`: Define the observable capability/help metadata and fail-closed diagnostics for reserved identities and catalog compatibility.

## Impact

Affected areas are Core attribute extraction and policy validation, classification diagnostics and report projections, the machine capability inventory and packaged schema/help descriptions, and Core/CLI/Testing tests. No new runtime dependency or annotation assembly is introduced. The implementation consumes the v0.10 manifest and does not implement annotation source definitions or the metadata-only composition owned by #567 and #569–#572.
