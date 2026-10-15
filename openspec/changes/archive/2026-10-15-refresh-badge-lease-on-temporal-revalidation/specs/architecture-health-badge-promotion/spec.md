## MODIFIED Requirements

### Requirement: Promotion consumes exact-bound temporal receipts for every adapter
When either the stored semantic horizon or the bounded producer freshness lease has elapsed, the trusted promotion path SHALL obtain and validate a canonical temporal receipt before deciding readiness for `github-raw`, `relay`, or metadata-only renewal. The receipt SHALL match the exact merged-tree SHA, source Health evidence bytes, badge payload bytes, producer identity, and approved configuration facts already resolved by the provider. The promotion path SHALL use the receipt's horizon only as a bounded input to the existing decision and adapter contracts.

#### Scenario: First-party raw promotion crosses a UTC-day boundary
- **WHEN** exact merged-tree evidence remains unchanged across midnight and the trusted temporal receipt is ready and identity-matched
- **THEN** `github-raw` promotion publishes the unchanged canonical badge payload as ready
- **AND** the publication receipt records the refreshed temporal receipt identity and horizon

#### Scenario: Delayed promotion refreshes an elapsed producer lease
- **WHEN** the producer freshness lease has elapsed but the product-owned semantic horizon remains in the future
- **AND** the trusted temporal receipt is ready and bound to the exact source Health bytes, payload bytes, merged tree, and producer identity
- **THEN** promotion may publish the unchanged canonical badge payload as ready
- **AND** it anchors the new transport lease at the trusted temporal verification time

#### Scenario: Relay renewal uses the same canonical receipt
- **WHEN** a Relay renewal crosses midnight with unchanged exact-tree evidence and a ready identity-matched temporal receipt
- **THEN** Relay receives the refreshed finite horizon through its existing prepare/publish or renew contract
- **AND** no second waiver, Health, or architecture evaluator runs in provider glue

#### Scenario: Stale waiver evidence remains unavailable
- **WHEN** temporal revalidation receives an otherwise bound Health envelope containing a stale waiver lifecycle record
- **THEN** the Core receipt is unassessable with a bounded stale-waiver reason
- **AND** raw and Relay promotion do not publish a ready payload or extend a transport lease

#### Scenario: Temporal revalidation reanchors the transport lease
- **WHEN** a trusted temporal receipt is accepted after the original producer lease has elapsed
- **THEN** the transport lease is anchored at the trusted publisher time immediately after successful receipt validation
- **AND** its deadline is the earlier of that anchor plus the configured lease duration and the refreshed semantic horizon
- **AND** the original producer verification time remains unchanged provenance

#### Scenario: Receipt mismatch or unassessable evidence remains unavailable
- **WHEN** the temporal receipt is missing, malformed, stale, expired, unassessable, or mismatched with any bound identity
- **THEN** raw and Relay promotion return the existing unavailable outcome, including `semantic_horizon_expired` where that is the applicable reason
- **AND** they do not publish a ready payload or extend a transport lease
