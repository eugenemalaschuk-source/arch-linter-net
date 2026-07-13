## Why

Large architecture policies need deterministic local decomposition without changing ArchLinterNet's single-root execution model. Issue #280 must settle the format, composition, safety, provenance, and schema contract before resolver and diagnostic implementation begins in #281 and #282.

## What Changes

- Define `imports` as the explicit top-level field for local policy fragments while preserving one user-selected root policy.
- Define root-only, mergeable, and forbidden fields for every current top-level policy section.
- Define deterministic nested import expansion, merge order, conflict handling, duplicate contract-ID handling, repository-bound path resolution, graph limits, and source provenance.
- Define separate root-policy and fragment schema roles that do not depend on filenames or extensions.
- Add public draft format guidance, positive and negative YAML examples, an internal resolver/composer architecture note, and a complete implementation test matrix.
- Preserve existing monolithic policies and explicitly exclude remote resolution, globs, templating, interpolation, cross-file anchors, scripting, and silent overrides.

## Capabilities

### New Capabilities

- `policy-import-composition`: Defines the single-root import graph, fragment document shape, deterministic composition semantics, safety boundaries, provenance, schema support, and compatibility requirements.

### Modified Capabilities

None.

## Impact

This design change affects OpenSpec and repository documentation only. It establishes the future contract for the Core policy loader/composer, JSON Schema publication, CLI and Testing adapter behavior, and diagnostic provenance work in #281 and #282; it does not implement or advertise runtime import support in this change.
