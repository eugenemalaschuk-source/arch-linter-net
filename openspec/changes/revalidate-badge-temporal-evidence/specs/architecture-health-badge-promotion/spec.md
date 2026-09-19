## MODIFIED Requirements

### Requirement: Promotion validates untrusted artifact bytes safely
The privileged promotion path SHALL treat downloaded artifacts and manifests as untrusted data. It SHALL enforce bounded archive and file sizes, safe relative paths, regular-file and UTF-8 constraints, duplicate/path-traversal/symlink rejection, exact manifest binding, and closed canonical disclosure validation before publishing bytes. It SHALL not repair, regenerate, reserialize, or calculate Architecture Health facts from rejected input. When temporal revalidation is required, promotion SHALL consume only a trusted Core/CLI receipt and SHALL not calculate or reinterpret its temporal facts.

#### Scenario: Hostile artifact is rejected without extraction side effects
- **WHEN** an artifact contains a traversal path, symlink, duplicate entry, oversized member, malformed UTF-8, duplicate JSON key, or unexpected file
- **THEN** validation rejects it with a fixed diagnostic
- **AND** no untrusted hook, extracted executable, or partial publication is created

#### Scenario: Canonical bytes remain unchanged
- **WHEN** a producer supplies a valid canonical disclosure payload and matching digest
- **THEN** the published bytes are byte-identical to the validated input
- **AND** the promotion layer does not recalculate Gate, Health, counts, color, or the semantic validity horizon
- **AND** any refreshed horizon comes only from the separately validated trusted temporal receipt

## ADDED Requirements

### Requirement: Promotion consumes exact-bound temporal receipts for every adapter
When the stored semantic horizon has elapsed, the trusted promotion path SHALL obtain and validate a canonical temporal receipt before deciding readiness for `github-raw`, `relay`, or metadata-only renewal. The receipt SHALL match the exact merged-tree SHA, source Health evidence bytes, badge payload bytes, producer identity, and approved configuration facts already resolved by the provider. The promotion path SHALL use the receipt's horizon only as a bounded input to the existing decision and adapter contracts.

#### Scenario: First-party raw promotion crosses a UTC-day boundary
- **WHEN** exact merged-tree evidence remains unchanged across midnight and the trusted temporal receipt is ready and identity-matched
- **THEN** `github-raw` promotion publishes the unchanged canonical badge payload as ready
- **AND** the publication receipt records the refreshed temporal receipt identity and horizon

#### Scenario: Relay renewal uses the same canonical receipt
- **WHEN** a Relay renewal crosses midnight with unchanged exact-tree evidence and a ready identity-matched temporal receipt
- **THEN** Relay receives the refreshed finite horizon through its existing prepare/publish or renew contract
- **AND** no second waiver, Health, or architecture evaluator runs in provider glue

#### Scenario: Receipt mismatch or unassessable evidence remains unavailable
- **WHEN** the temporal receipt is missing, malformed, stale, expired, unassessable, or mismatched with any bound identity
- **THEN** raw and Relay promotion return the existing unavailable outcome, including `semantic_horizon_expired` where that is the applicable reason
- **AND** they do not publish a ready payload or extend a transport lease
