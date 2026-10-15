## ADDED Requirements

### Requirement: Prepared receipt selection covers Health and topology diagnostics
The CLI SHALL support `--use-prepared-receipts` for standalone Health and for topology capture, diff, and verify. Each operation SHALL use the existing metadata-only prepared-artifact preflight to verify the selected receipts before loading target assemblies. Reader invocations SHALL NOT build or restore, and SHALL reject missing, stale, wrong-context, or otherwise unverifiable selected artifacts before analysis.

#### Scenario: Topology readers consume a current prepared selection
- **WHEN** topology capture, diff, or verify runs with `--use-prepared-receipts` after an authoritative preparation published current receipts
- **THEN** the operation reports current preflight and analyzes the exact receipt-verified output selection
- **AND** it does not build, restore, or rewrite the selected outputs

#### Scenario: Topology reader rejects a stale receipt
- **WHEN** a selected project input changes after its receipt was published and a topology reader runs with `--use-prepared-receipts`
- **THEN** preflight reports the existing stale-artifact diagnostic before target assembly loading or topology evaluation

#### Scenario: Standalone Health preserves prepared receipt selection
- **WHEN** Health runs with `--use-prepared-receipts`, with or without `--change-snapshot`
- **THEN** Health verifies and analyzes the selected receipt-backed artifacts without building or restoring
- **AND** the presence of `--change-snapshot` does not change Health's receipt-selection semantics
