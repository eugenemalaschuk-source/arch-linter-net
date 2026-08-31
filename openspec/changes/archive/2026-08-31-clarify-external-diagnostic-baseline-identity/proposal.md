## Why

The external-diagnostics federation implementation intentionally treats a producer tool's name
and version, plus repository, revision, and scope, as canonical identity dimensions. The live
OpenSpec and user guidance currently overstate stability by implying that all producer/run and
artifact provenance is excluded from baseline identity, which could cause users to expect an old
baseline to suppress diagnostics after a producer upgrade.

## What Changes

- Narrow the external-diagnostics federation baseline requirement to name the transient fields
  excluded from identity: run ID, artifact path, and artifact content hash.
- State that producer tool name/version and repository/revision/scope remain intentional selected
  identity dimensions and may create a new baseline occurrence when they change.
- Correct the user-facing external-evidence guide with the same boundary and point readers to the
  ordinary baseline lifecycle for review of changed identities.
- Preserve all implementation behavior, schemas, and existing archived OpenSpec changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `external-diagnostics-federation`: Clarify the canonical identity dimensions used by imported
  diagnostic baseline candidates and distinguish transient evidence provenance from producer and
  context identity.

## Impact

- Affected documents: the live external-diagnostics federation specification and the public
  external-evidence guide.
- Validation uses the existing producer-version/run/artifact baseline-identity regression test,
  OpenSpec validation, and documentation linting.
- No runtime implementation, public API, schema, or dependency changes.
