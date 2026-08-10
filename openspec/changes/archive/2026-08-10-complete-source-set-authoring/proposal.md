## Why

The existing reusable source-set model still leaves two high-noise governance cases with no
safe, reusable authoring shape: directional assembly rules must be duplicated per source, and
project metadata sets cannot use the filtered project universe discovered from a solution. This
blocks the v0.6.1 consumer-adoption exit by preserving avoidable policy inventories.

## What Changes

- Extend directional assembly dependency and allow-only contracts with the established
  `sources`/`source_sets` and compatible subtraction declarations, producing one deterministic
  instance per resolved assembly source.
- Bind project-kind source-set members and constrained repository-relative path globs to the
  final solution-discovered project inventory after `project_include` and `project_exclude`, while
  preserving the explicit `analysis.projects` universe.
- Keep `project_sets` as the one reusable list-union input for project-metadata contracts so
  solution-driven policies do not duplicate project paths.
- Preserve exact-source behavior, direct-only assembly semantics, deterministic expansion,
  bounded fail-closed selector handling, baseline identity, provenance, coverage, explain, JSON,
  and SARIF projections.
- Update the schema, capability metadata, reference documentation, and AI authoring guidance.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `source-set-expansion`: Extend reusable source-set universes and fan-out support to directional
  assembly contracts.
- `assembly-dependency-contracts`: Permit native multi-source authoring without changing
  direct-reference enforcement.
- `assembly-allow-only-contracts`: Permit native multi-source authoring without changing
  direct-reference enforcement.
- `project-metadata-contracts`: Reuse project sets from the final solution-discovered inventory.
- `project-discovery`: Expose the filtered discovered-project universe to project source-set
  resolution.
- `architecture-coverage-inventory`: Preserve solution-derived project expansion provenance in
  coverage inventory and projections.
- `explain-command`: Explain authored and resolved source provenance for the new expansion cases.
- `adoption-stabilization-compatibility`: Record the v0.6.1 authoring compatibility guarantee.

## Impact

Affected areas include the YAML contract model and schema, source-set expansion and validation,
project discovery and runner setup ordering, policy provenance, coverage/reporting projections,
documentation, capability metadata, and NUnit regression coverage. The change is additive and
keeps existing explicit-source and explicit-project policies compatible.
