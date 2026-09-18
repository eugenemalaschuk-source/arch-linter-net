> **Historical / superseded.** This #945 design was implemented by #946 and later superseded by the no-App bootstrap handoff in #963/#964. It is preserved only as historical evidence. Current product authority is the canonical spec under `openspec/specs/`; do not implement the GitHub App writer model from this archived change.

## ADDED Requirements

### Requirement: Protected-branch setup review guidance

Turnkey Relay setup guidance SHALL describe that first-write output for a
protected branch is delivered as a normal reviewable pull request and requires
an explicitly configured least-privilege GitHub App writer.

#### Scenario: Owner follows protected-branch setup

- **WHEN** the consumer base branch requires pull requests or workflow-file
  write authority beyond `GITHUB_TOKEN`
- **THEN** the guide identifies the minimum GitHub App authority and normal
  review/merge handoff
- **AND** it does not instruct the owner to disable rules, use a PAT fallback,
  or run untrusted code with the writer authority
