## MODIFIED Requirements

### Requirement: Explicit reviewed public API addition approval

The comparator SHALL accept an intentional public API addition only when an explicit approval is bound to the exact base and current policy-context digests, the exact `public_api_surface` contract identity, an unchanged supported API comparison mode, and the complete canonical added-entry delta. The comparator SHALL obtain a fresh read-only capture of the current CLR public API surface through the existing public API capture capability. It SHALL require the current policy-context `resolved_snapshot_entries` to exactly match that captured surface, then calculate the approved delta from the base snapshot to that captured surface with the existing public API snapshot differ.

The approval SHALL be rejected fail-closed when it is stale, malformed, for another contract, has duplicate or missing live evidence, differs from the captured CLR surface, has a different added set, or the canonical delta includes a removal or signature change. It SHALL suppress only the corresponding `resolved_snapshot_entries` impact-not-proven finding; selector, inventory, comparison-mode, and unrelated weakening findings SHALL remain visible.

#### Scenario: Exact reviewed addition is approved

- **WHEN** a reviewed public API snapshot gains exactly one canonical added entry, an approval binds that entry to the exact base/current contexts and contract, and a current CLR capture exactly matches the snapshot
- **THEN** policy weakening accepts that snapshot-fact change and exposes deterministic approval evidence in human, JSON, and SARIF output

#### Scenario: Hand-edited snapshot is rejected

- **WHEN** a context snapshot claims an added API entry that the current CLR capture does not export
- **THEN** policy weakening retains the `resolved_snapshot_entries` impact-not-proven finding despite a matching approval artifact

#### Scenario: Approval cannot hide selector widening

- **WHEN** a public API selector changes with a snapshot update and an addition approval is supplied
- **THEN** the selector weakening remains reported
