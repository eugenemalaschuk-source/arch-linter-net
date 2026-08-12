## ADDED Requirements

### Requirement: Capture, diff, update, and migrate resolve the selector-filtered effective surface

When a `public_api_surface` contract declares a `surface_selector`, the system SHALL resolve the same selector-filtered effective surface for `public-api capture`, `public-api diff`, `public-api update` (including `--dry-run`), and `public-api migrate` as strict/audit validation uses, rather than the contract's full unfiltered assembly-wide exported surface. `ArchLinterNet.Testing` SHALL resolve the same effective surface through the same underlying computation, with no separate code path.

#### Scenario: Capture only records the selected surface

- **WHEN** `public-api capture` runs against a contract declaring `surface_selector`
- **THEN** the captured snapshot SHALL contain only entries for types and members matching the selector, not the contract's full unfiltered exported surface

#### Scenario: Diff and update reflect the same selected surface as validation

- **WHEN** `public-api diff` or `public-api update --dry-run` runs against a contract declaring `surface_selector`
- **THEN** the reported delta SHALL be computed against the same selector-filtered live surface that strict/audit validation would use for that contract

#### Scenario: CLI and Testing resolve identical selected surfaces

- **WHEN** the same policy and contract are evaluated once through the CLI `validate` command and once through `ArchLinterNet.Testing`
- **THEN** both SHALL resolve the same effective selected surface and the same findings for that contract

#### Scenario: A selector-evidence change produces a reviewed membership delta

- **WHEN** a type gains or loses the evidence a `surface_selector` matches on (for example, an attribute is added or removed) between two captures
- **THEN** the next `public-api capture`/`diff`/`update` SHALL reflect the type's members entering or leaving the reviewed snapshot as an explicit, review-visible addition or removal, not a silent change
