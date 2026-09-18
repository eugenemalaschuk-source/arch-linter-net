## ADDED Requirements

### Requirement: Bootstrap emits a no-App review handoff

The trusted reusable bootstrap workflow SHALL, after its existing exact-base,
capability, required-check, and OIDC inspection succeeds, emit a private
versioned setup handoff artifact. The handoff SHALL bind the exact base ref,
commit, tree, repository identity, shipped publisher pins, and a closed
allowlist of managed UTF-8 files with byte lengths and SHA-256 digests.

The bootstrap workflow SHALL not require a GitHub App ID or private key, mint a
GitHub App token, push a consumer ref, create a pull request, or update the
consumer base ref.

#### Scenario: Trusted bootstrap succeeds without writer credentials

- **WHEN** a consumer invokes the exact pinned reusable workflow for bootstrap
  with valid authoritative prerequisites and no GitHub App writer secret
- **THEN** it emits the private content-bound setup handoff
- **AND** it performs no consumer remote write or pull-request API call

#### Scenario: Bootstrap prerequisites are not authoritative

- **WHEN** the required check, exact base/tree, repository identity, OIDC
  inspection, or provider capability proof is missing, stale, or contradictory
- **THEN** bootstrap fails closed before creating a handoff
- **AND** it does not emit generated managed output as trusted

### Requirement: Bootstrap handoff is bounded and private

The trusted workflow SHALL upload the handoff only through an immutable pinned
artifact action and SHALL keep it within the consumer repository's private
Actions access boundary. It SHALL not place configuration, repository identity,
full provenance, provider credentials, or raw OIDC material in public logs,
workflow outputs, URLs, or job summaries.

#### Scenario: Bootstrap output contains private Relay setup material

- **WHEN** a Relay bootstrap produces configuration and generated assets
- **THEN** those files are available only in the private handoff artifact
- **AND** public-facing workflow data contains no generated configuration or
  credential material
