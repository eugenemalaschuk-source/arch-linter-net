## Why

Repository size metrics can report zero source lines when configured C# files are readable but their project ownership is ambiguous. Unity-generated projects commonly share the repository root, so physical source-size reporting should not depend on assigning every file's declared types to one assembly.

## What Changes

- Count readable, non-generated C# files within configured source roots for `sourceFiles` and `sourceLines`, independently of assembly ownership.
- Keep type-to-source enrichment ownership-aware and deduplicate overlapping roots by normalized repository-relative file identity.
- Preserve unreadable-source evidence and document the distinction between physical source inventory and assembly-owned type facts.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `repository-metrics-observability`: physical source-size metrics cover configured source roots even when project ownership is ambiguous.

## Impact

The Core source-file traversal and repository-metrics tests change; no public API or dependency changes are expected. Existing generated-file exclusions, source-root boundaries, and architecture validation outcomes remain unchanged.
