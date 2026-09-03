## ADDED Requirements

### Requirement: Architecture Health publisher verifies effective main protection
Before promoting a ready Architecture Health payload, trusted automation SHALL
verify that `Architecture Coverage` is a strict required status check in the
effective protection rules applied to `main`, including organization-level
rules. A required check present only in an unrelated active ruleset SHALL not
satisfy this proof. Missing or non-applicable protection proof SHALL reject
ready evidence and publish the explicit unavailable state.

#### Scenario: Unrelated ruleset requirement cannot authorize promotion
- **WHEN** an active ruleset for another branch requires `Architecture Coverage`
  but no effective `main` protection rule requires it
- **THEN** the publisher rejects the ready artifact
- **AND** it publishes only the explicit unassessable payload

#### Scenario: Effective main requirement authorizes evidence evaluation
- **WHEN** effective protection for `main` requires `Architecture Coverage`
  and the remaining immutable evidence is valid
- **THEN** the publisher may evaluate the ready artifact for promotion

### Requirement: Unavailable badge receipt is verified by unprivileged CI
The unprivileged pull-request producer SHALL verify that the committed
unavailable payload is byte-for-byte the output of the Architecture Health CLI
for unavailable input. The trusted publisher SHALL use only that verified,
committed receipt when it must publish unavailable state; it SHALL not restore,
build, or run the CLI in the privileged fallback path.

#### Scenario: Producer rejects a drifted unavailable receipt
- **WHEN** the committed unavailable receipt differs from the CLI-generated
  unavailable output
- **THEN** pull-request artifact production fails
- **AND** no manifest claims that the receipt is trusted

#### Scenario: Trusted fallback does not execute build tooling
- **WHEN** trusted publication rejects ready evidence
- **THEN** it publishes the verified unavailable receipt without a .NET restore,
  build, or CLI command
- **AND** it records bounded unavailable metadata

