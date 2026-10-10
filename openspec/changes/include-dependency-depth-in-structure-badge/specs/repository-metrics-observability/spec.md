## MODIFIED Requirements

### Requirement: Absolute reporting and badge projection are neutral
The human and machine-readable reporting surfaces SHALL expose the absolute current snapshot in a bounded grouped Size, Coupling, and Structure section. A Core/CLI-owned badge projection SHALL expose at least `Source lines` as a compact absolute Shields endpoint payload and MAY expose bounded grouped `Repository` and `Structure` payloads. The `Structure` payload SHALL include maximum dependency depth, dependency count, and largest strongly connected component size when those values are available. Grouped badges SHALL not become one badge per metric. Badge output SHALL use current verified main evidence only, SHALL contain no PR delta, threshold, quality color, or pass/fail interpretation, and SHALL preserve the existing trusted badge publication/privacy boundary. When a custom ArchLinterNet logo is present, it SHALL be embedded as a reviewed local SVG in the Shields payload rather than loaded from an external URL.

#### Scenario: Absolute report is grouped and bounded
- **WHEN** a current repository metrics snapshot is complete or partial
- **THEN** human output groups the values under Size, Coupling, and Structure
- **AND** machine output contains typed stable fields without requiring formatted-text parsing

#### Scenario: Source lines badge is an absolute snapshot
- **WHEN** verified default-branch automation publishes a current metrics snapshot
- **THEN** the badge payload presents an absolute Source lines value
- **AND** it does not present a base-to-head delta or imply that magnitude is a governance result

#### Scenario: Optional grouped badges remain compact and recognizable
- **WHEN** verified default-branch automation publishes grouped repository metrics
- **THEN** it may publish one `Repository` payload for projects/types and one `Structure` payload for dependency depth, dependency count and largest SCC
- **AND** each payload contains the reviewed ArchLinterNet SVG logo inline
- **AND** no payload contains a PR delta or a per-metric quality interpretation

#### Scenario: Structure badge includes dependency depth
- **WHEN** a complete repository-metrics snapshot contains maximum dependency depth, dependency count, and largest SCC size
- **THEN** the grouped `Structure` payload presents all three absolute values
- **AND** it does not imply a threshold or quality verdict

#### Scenario: Missing depth does not produce a misleading structure badge
- **WHEN** a complete snapshot lacks maximum dependency depth
- **THEN** the `Structure` badge is reported as unavailable
