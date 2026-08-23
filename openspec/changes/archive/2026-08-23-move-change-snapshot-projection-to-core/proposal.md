## Why

The versioned Core snapshot contract is currently populated by canonical identity and fact-projection logic in the CLI command handler. That makes the CLI the semantic authority for a Core artifact and risks divergent snapshot producers.

## What Changes

- Move canonical architecture-change snapshot projection from the CLI handler into an internal Core seam.
- Keep the CLI handler responsible for options, runtime orchestration, collision protection, filesystem/console I/O, and exit-code presentation.
- Add focused Core characterization tests for project, graph, semantic, coverage, finding, and baseline-debt projection while retaining CLI orchestration coverage.
- Preserve the v2 schema, serialized JSON, comparison behavior, and supported public API.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-change-report`: complete snapshot construction is Core-owned and the CLI delegates its canonical projection without changing artifact behavior.

## Impact

- `ArchLinterNet.Core.Change` gains an internal snapshot projector using existing Core analysis models.
- `ChangeCommandHandler` no longer defines architecture-fact identities or normalization helpers.
- Core and CLI test ownership is adjusted; no dependencies, schema, CLI surface, or reviewed public API are expanded.
