## Why

Single-root policy imports are implemented and provenance-aware, but the public documentation and executable examples still present policies primarily as monolithic files. Users and AI agents need one discoverable, schema-backed workflow that explains graph-derived root/fragment roles, safe composition, migration, and failure modes without turning the recommended filenames into runtime requirements.

## What Changes

- Publish public policy-import guidance covering syntax, document roles, composition order, conflicts, path boundaries, nested imports, limits, unsupported behavior, migration, troubleshooting, and editor schema selection.
- Add realistic modular-monolith and Unity/client split-policy examples using the recommended root/fragment conventions, while also proving equivalent arbitrary filenames.
- Add fixture-backed NUnit acceptance coverage for monolithic/imported equivalence, recommended/arbitrary filename equivalence, root-versus-fragment conflicts, and fragment-versus-fragment conflicts.
- Update AI authoring guidance to prefer concern-focused fragments and minimize unrelated policy edits while preserving globally unique contract IDs.
- Add README, policy-format, schema-reference, troubleshooting, and MkDocs navigation links that make the import workflow discoverable.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-import-composition`: Require complete public authoring, migration, troubleshooting, schema/editor, and unsupported-behavior guidance backed by executable acceptance fixtures.
- `sample-policy`: Require realistic modular-monolith and Unity/client examples that demonstrate one root with inline content and focused imported fragments.
- `ai-policy-authoring`: Require AI agents to decompose policies by architecture concern or bounded context and avoid broad unrelated fragment edits.
- `docs-site`: Require policy-import documentation to be reachable from the public navigation and core entry pages.

## Impact

Affected areas are public Markdown documentation, MkDocs navigation, README capability links, sample YAML fixture trees, NUnit import acceptance tests, and OpenSpec requirements. Runtime APIs, policy loading semantics, JSON schemas, and dependencies are unchanged.
