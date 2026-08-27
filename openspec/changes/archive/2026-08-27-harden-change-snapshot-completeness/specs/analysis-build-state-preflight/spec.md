## MODIFIED Requirements

### Requirement: Change-snapshot contributors honor an explicit build-state selection
When a change-snapshot request selects `--ensure-built`, `--no-restore`, or an output-context override, every Core analysis contributor used to construct that snapshot SHALL receive the same no-restore value, configuration, target framework, platform, and runtime identifier. The snapshot's first ensure-built contributor SHALL perform metadata-first preparation and invoke the graph build once. Graph and baseline-debt contributors that follow SHALL use an explicit prepared-post-build-state request, verify the same receipt-backed output context without restoring or building, and then analyze fresh isolated post-build runners. A contributor SHALL fail closed rather than substitute ordinary-resolution facts after explicit preparation is requested.

#### Scenario: Graph projection uses the post-build isolated runner
- **WHEN** a change snapshot selects `EnsureBuilt` for a discovered project graph whose policy opts into a shared framework
- **THEN** its namespace and assembly graph projections are built from receipt-verified post-build artifacts with the configured shared-framework probing path
- **AND THEN** those projections do not invoke another graph build

#### Scenario: Baseline debt uses the selected output context
- **WHEN** a change snapshot includes a baseline and selects a configuration or framework override
- **THEN** baseline-debt identities are collected from artifacts selected with those same overrides
- **AND THEN** baseline debt does not invoke another graph build after snapshot preparation

#### Scenario: Build-state selection is not silently downgraded
- **WHEN** an explicitly prepared graph or baseline contributor has blocking preflight diagnostics
- **THEN** that contributor does not continue with ordinary-resolution facts
- **AND THEN** the snapshot command does not write a partial result
