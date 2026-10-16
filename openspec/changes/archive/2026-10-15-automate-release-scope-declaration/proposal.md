## Why

Release preparation currently requires manually translating the already reviewed release story into a versioned JSON declaration and a matching hard-coded regression-test entry. This change removes that duplicate authoring step while preserving the existing declaration as the tracked publication authority and leaving the dry-run, review, and separate publication procedure unchanged.

## What Changes

- Define a structured release-scope section in the existing release story as the authoring input for the required, excluded, and delivered issue inventories.
- Add a deterministic generator that validates the structured story input and emits the existing versioned declaration.
- Keep the generated declaration in the existing preparation PR so reviewers continue to approve the exact publication authority before any release workflow runs.
- Preserve exact candidate-version selection, declaration digest binding, Checkpoint B, provenance, the manual dry-run review, and the separate `publish=true` run.
- Fail without modifying outputs when scope data is incomplete, contradictory, or ambiguous; never infer publication scope from milestone membership alone.

## Capabilities

### New Capabilities

- `release-scope-preparation`: generate the tracked release-scope declaration from the reviewed release story and validate it through shared regression coverage.

### Modified Capabilities

None. Existing publication authorization and release workflow behavior remain unchanged.

## Impact

Affected systems include `tools/release/` tooling and tests, `docs/ai/release-preparation-workflow.md`, and the release-scope authoring instructions. The generated JSON retains the current schema, path, exact-target semantics, and hash binding. `.github/workflows/release-nuget.yml`, `tools/release/create_release_scope_evidence.py`, the manual two-stage publication procedure, package contents, and publication credentials are outside the change scope.
