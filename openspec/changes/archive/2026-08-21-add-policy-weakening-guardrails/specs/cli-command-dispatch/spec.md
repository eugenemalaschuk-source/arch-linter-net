## ADDED Requirements

### Requirement: Policy weakening comparison is an instance-based policy subcommand

The CLI SHALL expose `arch-linter-net policy weakening` through the existing
policy command module and an instance handler.  It SHALL accept explicit base
and current policy-context artifact paths and `human`, `json`, or `sarif`
format selection; it SHALL not read or simulate a historical policy from the
current working tree.

#### Scenario: Explicit contexts produce a guardrail result
- **WHEN** a caller invokes `policy weakening` with valid base and current
  context artifacts
- **THEN** the instance handler renders the normalized Core comparison result
  and returns a failing exit code only for configured error-severity findings

#### Scenario: Invalid artifact fails closed
- **WHEN** either supplied context artifact is missing, malformed, incomplete,
  or incompatible
- **THEN** the command returns exit code 2 with existing human/JSON error
  conventions and does not report a clean result
