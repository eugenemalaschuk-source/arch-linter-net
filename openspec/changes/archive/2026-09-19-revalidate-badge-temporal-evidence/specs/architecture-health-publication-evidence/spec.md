## MODIFIED Requirements

### Requirement: Promotion and renewal preserve the product-owned evidence horizon
Every ready promotion or metadata-only renewal SHALL carry and verify the canonical publication-evidence validity horizon produced by the product. A consumer workflow, adapter, publisher timestamp, unchanged tree, or unchanged payload digest SHALL not create or extend a semantic horizon. A trusted temporal revalidation MAY produce a replacement product-owned horizon only when it re-evaluates the bounded temporal facts for the same canonical Health evidence and exact publication identities under the rules below.

#### Scenario: Fresh metadata can renew before the horizon
- **WHEN** the current approved producer, required gate, artifact, and product-owned validity receipt are revalidated before expiry
- **THEN** a metadata-only renewal may commit a new transport lease under the current generation
- **AND** it does not rerun the architecture analysis or alter canonical disclosure bytes

#### Scenario: Renewal without a fresh temporal receipt cannot revive evidence
- **WHEN** a same-tree renewal is attempted after the stored product-owned validity horizon or after artifact retention/revocation invalidates the evidence
- **AND** no valid temporal revalidation receipt exists for the exact source evidence, producer identity, merged tree, and payload
- **THEN** the destination becomes or remains unavailable
- **AND** renewal does not manufacture a new ready payload or extend the old lease

#### Scenario: A canonical temporal receipt can renew an unchanged tree
- **WHEN** the stored horizon has elapsed but the trusted Core/CLI revalidator produces a ready receipt for the unchanged exact tree, original producer identity, source Health evidence, and unchanged payload
- **THEN** promotion may use only that receipt's finite horizon for a metadata-only publication or renewal
- **AND** it preserves the original Gate, Health, counts, findings, and disclosure bytes

#### Scenario: Missing evidence is unavailable
- **WHEN** the product-owned receipt is missing, malformed, unsupported, inconsistent, or expired at the supplied evaluation context
- **THEN** promotion and renewal return an explicit unassessable result
- **AND** they do not publish or preserve a ready result

## ADDED Requirements

### Requirement: Temporal revalidation emits a bound product-owned receipt
The product SHALL provide a deterministic temporal revalidation operation for a complete supported Health report-evidence envelope. The operation SHALL accept an explicit UTC evaluation date and publication identity binding, SHALL re-evaluate only time-sensitive waiver and evidence validity, and SHALL emit a versioned receipt containing the evaluation date, finite semantic horizon, source Health evidence SHA-256, badge payload SHA-256, exact merged-tree SHA, producer-identity SHA-256, state, and actionable reasons. It SHALL never recompute Architecture Health, Gate, findings, counts, or canonical payload bytes.

#### Scenario: Cross-midnight evidence remains ready when temporal facts remain valid
- **WHEN** a complete ready Health evidence envelope was produced before midnight and the exact merged tree, producer identity, source evidence, and payload are unchanged after midnight
- **AND** all time-sensitive waiver records remain assessable and no required external evidence lacks a finite horizon
- **THEN** temporal revalidation emits a ready receipt with a finite horizon after the new UTC evaluation date
- **AND** the receipt is bound to every supplied publication identity

#### Scenario: A waiver that expired at the new evaluation date fails closed
- **WHEN** a Health evidence envelope contains a waiver whose expiry is before the supplied UTC evaluation date
- **THEN** temporal revalidation emits an unassessable receipt with an expired-waiver reason
- **AND** it does not improve the original result or extend its horizon

#### Scenario: Stale, invalid, or incomplete lifecycle evidence fails closed
- **WHEN** any required waiver lifecycle record is stale, invalid, metadata-incomplete, malformed, or inconsistent with its canonical receipt
- **THEN** temporal revalidation emits an unassessable receipt with a stable reason
- **AND** it does not infer validity from an unchanged tree, newer publication time, or aggregate counts

#### Scenario: Required external evidence without a finite reuse rule remains unavailable
- **WHEN** a required external-evidence requirement is present but its trusted receipt does not carry a finite reusable horizon
- **THEN** temporal revalidation emits an unassessable receipt with `required_external_evidence_horizon_unknown`
- **AND** the publisher does not renew or publish a ready result

#### Scenario: Binding mismatch fails closed
- **WHEN** the source Health SHA-256, payload SHA-256, producer-identity SHA-256, or merged-tree SHA in a revalidation result does not match the exact provider facts being promoted
- **THEN** promotion rejects the receipt as unavailable
- **AND** no refreshed semantic horizon is accepted
