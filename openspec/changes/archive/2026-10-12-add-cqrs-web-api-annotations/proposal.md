## Why

The reviewed v0.10 catalog includes stable CQRS/messaging and web/API roles, but the source-only annotation set currently contains only layered/clean and DDD definitions. Adding the remaining approved roles lets consumers express those semantic facts directly while reusing the existing single-role classifier and keeping reviewed API membership independent.

## What Changes

- Add the 8 approved CQRS/messaging and 6 approved web/API role attributes to the annotation source set, matching the v1 manifest.
- Extend source-contract and semantic integration fixtures to prove exact identities, targets, bounded metadata, and existing classifier behavior.
- Cover representative command/handler, query/read-model, integration-event, request/response, controller/endpoint, and reviewed-API/`ValueObject` scenarios.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `semantic-annotation-contract`: require the approved CQRS/messaging and web/API role definitions and preserve their existing semantic and API-membership boundaries.

## Impact

- Adds source files under `src/ArchLinterNet.Annotations/contentFiles/cs/any/`.
- Updates Core test fixtures and the semantic annotation delta spec.
- No Core production API, package build mechanism, dependency, or runtime behavior changes are in scope.
