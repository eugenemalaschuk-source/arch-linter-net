## Context

See [proposal.md](proposal.md). Public API materialization is session-cached and is shared by
legacy validation and public-surface capture. Its current `GetLoadableTypes` traversal intentionally
retains loadable types after `ReflectionTypeLoadException`, but it does not expose whether that
surviving list is complete.

## Goals / Non-Goals

**Goals:**

- Retain one completeness bit beside the cached exported entries and exported types.
- Let metric capture use that bit as required evidence while keeping validation's exported entries
  and diagnostics unchanged.

**Non-Goals:**

- Change the public API snapshot format or public `CapturePublicApiSurface` API.
- Convert partial exported type loading into a validation violation.
- Rescan public surfaces solely for metrics.

## Decisions

### Carry completeness through existing materialization

The scanner uses the existing completeness-aware type scan when it materializes the cache and
returns the same loadable best-effort types plus an `IsComplete` flag. The cache stores all three
values so selectors, validation, capture, and metrics still share one reflection traversal.

### Add an internal capture integrity output

An internal capture overload returns the aggregate completeness of every governed resolved
assembly. The public overload and validation paths retain their existing signatures and behavior.
The metric evaluator turns false completeness into `missing_required_input`; `Finish` removes any
partial contributors and numeric value.

## Risks / Trade-offs

- [Partial materialization suppresses a metric even if omitted types were internal] → omitted
  metadata does not prove that the selected export set was complete, so failing closed is required.
- [An added cache field changes internal construction sites] → keep construction centralized and
  verify materialization/capture tests plus focused metric regression.

## Migration Plan

1. Add completeness to scanner materialization, cache, and metric capture.
2. Verify partial public export evidence produces no trusted value.
3. Run focused Core and repository gates, then archive; rollback is a normal code revert.
