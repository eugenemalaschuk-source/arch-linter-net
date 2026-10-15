## ADDED Requirements

### Requirement: Health honors prepared receipt selection in every snapshot path
Health SHALL preserve the request's prepared-receipt selection when it creates its analysis snapshot. This SHALL apply to standalone Health and to Health that also writes a current change snapshot. Health's gate, health, and canonical evidence projections SHALL retain their existing meaning while using the exact selected receipt-verified artifacts.

#### Scenario: Standalone Health uses prepared receipts
- **WHEN** Health is invoked with `--use-prepared-receipts` and no change-snapshot output
- **THEN** its snapshot verifies and analyzes the selected receipt-backed artifacts without building or restoring
- **AND** the Health result matches the equivalent prepared reference analysis

#### Scenario: Health with change snapshot uses the same prepared selection
- **WHEN** Health is invoked with both `--use-prepared-receipts` and `--change-snapshot <path>`
- **THEN** Health and its change-snapshot projection use the same receipt-verified selection
- **AND** their canonical output semantics match the equivalent prepared reference analysis
