## Why

Policy authors should be able to use the approved layered/clean and DDD role vocabulary in source without defining repetitive per-repository attributes or YAML mappings. The v0.10 contract has fixed the identities and targets, so this issue can add those source definitions while the shared classifier and package mechanics remain owned by their parallel issues.

## What Changes

- Add the approved nine layered/clean and eight DDD role attributes to the source-only annotation source set, using identities, targets, visibility, and metadata properties from the reviewed v1 manifest.
- Add focused compile-time and classification tests for the new source definitions and their compatibility with the existing role extraction/index path.
- Keep built-in classifier recognition, package construction, and the other role families within their separately owned issues.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `semantic-annotation-contract`: specify the available layered/clean and DDD source definitions and their exact target contracts.

## Impact

- Adds compile-injected annotation source files under `src/ArchLinterNet.Annotations/contentFiles/cs/any/`.
- Adds Core test fixtures and contract tests; no production Core classifier or runtime dependency changes are required for this issue.
- Uses `architecture/semantic-annotations.v1.json` as the authority for the 17 identities and target declarations.
