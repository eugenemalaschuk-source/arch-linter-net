## ADDED Requirements

### Requirement: Setup handoff applies only to its exact reviewed base

The packed CLI SHALL provide a normal-PR setup-handoff apply operation. Before
writing, it SHALL validate the versioned handoff schema, repository identity,
shipped publisher pins, exact expected base commit and tree, closed managed
path allowlist, regular-file/UTF-8 constraints, byte lengths, and SHA-256
digests. It SHALL apply verified output through the existing atomic managed-file
boundary.

#### Scenario: Owner applies a valid handoff on its exact base branch

- **WHEN** the owner supplies a complete valid handoff and the expected base
  commit and tree of its local review branch match the handoff
- **THEN** the command writes the declared managed output atomically
- **AND** the owner can review it through the repository's ordinary PR flow

#### Scenario: Handoff is stale, malformed, or tampered

- **WHEN** the handoff has an unknown schema, wrong identity/pin/base tree,
  unsafe or extra path, symlink/reparse point, invalid UTF-8, missing file, or
  digest/length mismatch
- **THEN** the command rejects it before writing any managed file
- **AND** it does not downgrade to local setup, a PAT, or a GitHub App writer

### Requirement: Turnkey guidance separates bootstrap and publication authority

Turnkey Relay guidance SHALL describe the private trusted handoff and the
owner-operated normal PR required for first setup. It SHALL state that ongoing
publication uses the exact pinned GitHub OIDC publisher and requires neither a
GitHub App key nor any direct write to the base branch.

#### Scenario: Owner follows the no-App first setup guide

- **WHEN** an owner follows the Relay setup guide
- **THEN** the guide directs it to verify/apply the private handoff on a normal
  review branch and merge through existing branch protection
- **AND** it does not instruct the owner to create a GitHub App, use a PAT, or
  allow automated direct base-branch writes
