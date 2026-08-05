## Context

The prior registry change correctly embedded cache and profile schemas but froze two stale descriptions and marked profile read support despite no public profile reader. The active `analysis-profile` capability and the archived change record must match the corrected release contract.

## Goals / Non-Goals

**Goals:**

- Publish self-consistent immutable schema bytes and matching digests.
- Describe profile as a write-only generated-output format until a reader is introduced.
- Synchronize the owning capability through the normal OpenSpec archive path.

**Non-Goals:**

- Implement a profile reader or alter profile JSON.
- Change cache or normalized-finding support.

## Decisions

1. Set `analysis-profile.supportsRead` to `false`; existing `AnalysisProfileJsonWriter` is a writer, and JSON Schema validation is not a profile reader contract.
2. Assert support flags per logical id in registry tests, so future metadata cannot be hidden behind a blanket invariant.
3. Correct the archived history directly while a new change carries the active-spec deltas; the history must accurately identify cache/profile as the newly enrolled formats.

## Risks / Trade-offs

- **A consumer assumes profile input is supported** → the manifest now explicitly reports no reader support.
- **Immutable bytes drift after metadata correction** → digest and exact-resource tests fail.
