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
