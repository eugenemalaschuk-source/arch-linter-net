## Why

The grouped `Structure` badge currently shows dependency count and largest SCC size but omits maximum dependency depth. This leaves a requested structural metric unavailable in the compact public badge set even though the repository-metrics snapshot already calculates it.

## What Changes

- Include maximum dependency depth in the existing grouped `Structure` badge alongside dependency count and largest SCC size.
- Keep the badge absolute and informational, with no additional per-metric badge or quality interpretation.
- Treat a missing depth value as unavailable evidence rather than silently omitting it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `repository-metrics-observability`: the grouped `Structure` badge exposes maximum dependency depth.

## Impact

Only the ArchLinterNet badge projection, its tests, documentation, and repository-metrics badge specification change. No repository-metrics snapshot schema or analysis behavior changes.
