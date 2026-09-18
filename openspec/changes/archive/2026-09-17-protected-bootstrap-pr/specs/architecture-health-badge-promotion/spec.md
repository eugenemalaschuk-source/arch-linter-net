> **Historical / superseded.** This #945 design was implemented by #946 and later superseded by the no-App bootstrap handoff in #963/#964. It is preserved only as historical evidence. Current product authority is the canonical spec under `openspec/specs/`; do not implement the GitHub App writer model from this archived change.

## ADDED Requirements

### Requirement: Protected-branch bootstrap handoff

The trusted publisher workflow SHALL use a pull-request handoff for protected bootstrap output.

When bootstrap generates managed Badge setup output for a protected base ref,
the trusted publisher workflow SHALL create a dedicated review branch and pull
request rather than directly updating the base ref.

#### Scenario: Protected consumer base branch

- **WHEN** trusted bootstrap completes setup for a protected base ref
- **THEN** it stages only declared managed output and opens a pull request to
  that exact base ref
- **AND** it does not merge, approve, or directly update the base ref

#### Scenario: Existing bootstrap review branch

- **WHEN** the deterministic bootstrap branch already exists
- **THEN** bootstrap fails closed without force-pushing, replacing, or merging
  that branch

### Requirement: Scoped workflow-file writer authority

Bootstrap SHALL require an explicitly configured repository-scoped GitHub App
writer token before it creates managed GitHub workflow files on its dedicated
review branch.

#### Scenario: Writer credentials are unavailable

- **WHEN** trusted bootstrap needs to create managed workflow files without
  configured GitHub App writer credentials
- **THEN** it returns an actionable failure before pushing any branch
- **AND** it does not substitute a personal access token or relax branch rules
