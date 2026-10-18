## Why

The reviewed v0.10 catalog includes stable ports, infrastructure, UI, and Unity roles, but the source-only annotation set does not yet define those attributes. Adding the approved source definitions completes this family without requiring framework references or changing semantic classification behavior.

## What Changes

- Add the 18 first-class role attributes already approved in `architecture/semantic-annotations.v1.json`, with exact FQNs, targets, visibility, and bounded metadata inheritance.
- Extend the annotation source contract checks and C# 9 consumer fixture to cover the ports/infrastructure, UI, and Unity/client families.
- Keep Unity runtime/editor roles as assembly-only static facts and avoid claims about framework runtime behavior.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `semantic-annotation-contract`: require source definitions for the approved ports/infrastructure, UI, and Unity/client role families.

## Impact

Adds source files under `src/ArchLinterNet.Annotations/contentFiles/cs/any/` and extends annotation contract tests and the C# 9 consumer fixture. The package distribution mechanism and shared Core recognition remain owned by #567 and #568; this change consumes the approved manifest and does not alter role resolution, metadata composition, or policy authority.
