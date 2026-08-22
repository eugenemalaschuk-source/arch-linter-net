## ADDED Requirements

### Requirement: Partial publication retries fail closed
The release-process documentation SHALL state that a duplicate primary package
push is a fail-closed release-integrity condition because it does not prove its
paired symbol package was published. It SHALL instruct maintainers to inspect
NuGet.org package and symbol state before creating a corrected release path.

#### Scenario: A maintainer handles a partial publication
- **WHEN** a release rerun encounters an existing primary package
- **THEN** the documentation directs the maintainer to stop and investigate the
  paired package/symbol state instead of relying on duplicate-success behavior
