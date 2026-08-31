## Why

Selecting one metric must not make a read-only measurement depend on build
inputs that are required solely by an unselected metric. The current setup can
inspect every declared metric before applying the selection, so an unavailable
project output can prevent an unrelated requested metric from being measured.

## What Changes

- Apply metric-ID selection before measurement-specific analysis setup decides
  whether exact project-artifact ownership evidence is required.
- Preserve ordinary policy validation for the complete document while limiting
  measurement setup and reported measurements to the selected definitions.
- Index topology classifications by canonical identity while grouping external
  dependencies, retaining existing result semantics without repeated linear
  classification scans.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-metric-measurement`: Selected metric IDs isolate measurement
  setup from requirements belonging exclusively to unselected metrics.

## Impact

- Core measurement snapshot construction and project-artifact discovery.
- External-dependency metric projection performance.
- Core regression tests and the architecture-metric measurement specification.
