## ADDED Requirements

### Requirement: Extended metrics remain inside the existing secure report transport

The pull-request publication workflow SHALL transport the Core/CLI-rendered report containing the optional repository metrics delta through the existing single manifest-bound sticky-comment artifact. Workflow scripts SHALL not calculate, interpret, truncate, or publish repository metrics independently, and the publisher SHALL retain the existing inert-artifact and current-head trust checks.

#### Scenario: Metrics are published as part of the canonical report
- **WHEN** a producer has compatible base/head metrics and renders the unified report
- **THEN** the uploaded and published Markdown contains the Core-rendered repository metrics delta
- **AND** the publisher performs the same one-comment publication path

#### Scenario: Producer cannot fabricate a delta
- **WHEN** base metrics are unavailable or incompatible
- **THEN** the producer publishes the Core-rendered unavailable state when the report is otherwise publishable
- **AND** workflow glue does not emit zeroes, a second comment, or a metrics-specific status check
