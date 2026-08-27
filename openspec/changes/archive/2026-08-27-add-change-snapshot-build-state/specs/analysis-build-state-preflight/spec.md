## ADDED Requirements

### Requirement: Change-snapshot contributors honor an explicit build-state selection
When a change-snapshot request selects `--ensure-built`, `--no-restore`, or an output-context override, every Core analysis contributor used to construct that snapshot SHALL receive the same preparation mode, no-restore value, configuration, target framework, platform, and runtime identifier. A graph or baseline-debt contributor that receives `EnsureBuilt` SHALL perform metadata-first preparation, run the existing build-state preflight, and then analyze a fresh isolated post-build runner. A contributor SHALL fail closed rather than substitute ordinary-resolution facts after explicit preparation is requested.

#### Scenario: Graph projection uses the post-build isolated runner
- **WHEN** a change snapshot selects `EnsureBuilt` for a discovered project graph whose policy opts into a shared framework
- **THEN** its namespace and assembly graph projections are built from verified post-build artifacts with the configured shared-framework probing path

#### Scenario: Baseline debt uses the selected output context
- **WHEN** a change snapshot includes a baseline and selects a configuration or framework override
- **THEN** baseline-debt identities are collected from artifacts selected with those same overrides

#### Scenario: Build-state selection is not silently downgraded
- **WHEN** an explicitly prepared graph or baseline contributor has blocking preflight diagnostics
- **THEN** that contributor does not continue with ordinary-resolution facts
