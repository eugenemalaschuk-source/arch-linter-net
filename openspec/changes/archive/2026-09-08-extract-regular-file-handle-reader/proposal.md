## Why

`RegularFileHandleReader` currently spreads regular-file verification and repository-local traversal across two handwritten declarations. This refactor removes that structural aggregate while retaining the existing secure evidence-file capability and all caller-visible behavior.

## What Changes

- Extract repository-root opening, repository-relative path validation, and no-follow descendant traversal into a purpose-named internal non-partial collaborator.
- Keep `RegularFileHandleReader` as the single native regular-file validation owner and retain `ArchitectureFileSystem` as the public evidence-file seam.
- Add direct regression coverage for repository containment, symlink/reparse escape rejection, regular-file rejection, and bounded artifact acquisition.
- Remove only the exact `RegularFileHandleReader` declaration-count waiver and record the completed #813 slice in the active `decompose-god-classes` change.

## Capabilities

### New Capabilities

None. This is a behavior-preserving internal refactor.

### Modified Capabilities

None. No spec-level behavior changes.

## Impact

The change affects the internal Core IO implementation, its focused Core tests, the exact strict-policy waiver, and the active partial-type-governance implementation evidence. No public API, schema, output, cache/build-state, or release authority changes.
