## ADDED Requirements

### Requirement: Explicit reviewed public API addition approval

The comparator SHALL accept an intentional public API addition only when an explicit approval is bound to the exact base and current policy-context digests, the exact `public_api_surface` contract identity, an unchanged supported API comparison mode, and the complete canonical added-entry delta. It SHALL calculate that delta with the existing public API snapshot differ.

The approval SHALL be rejected fail-closed when it is stale, malformed, for another contract, has a different added set, or the canonical delta includes a removal or signature change. It SHALL suppress only the corresponding `resolved_snapshot_entries` impact-not-proven finding; selector, inventory, comparison-mode, and unrelated weakening findings SHALL remain visible.

#### Scenario: Exact reviewed addition is approved

- **WHEN** a reviewed public API snapshot gains exactly one canonical added entry and an approval binds that entry to the exact base/current contexts and contract
- **THEN** policy weakening accepts that snapshot-fact change and exposes deterministic approval evidence in human, JSON, and SARIF output

#### Scenario: Approval cannot hide selector widening

- **WHEN** a public API selector changes with a snapshot update and an addition approval is supplied
- **THEN** the selector weakening remains reported
