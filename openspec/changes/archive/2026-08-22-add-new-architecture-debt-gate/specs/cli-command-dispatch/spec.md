## ADDED Requirements

### Requirement: Architecture debt gate is an instance-based command module
The CLI SHALL expose `arch-linter-net gate` through a dedicated composed command module and instance handler. It SHALL require explicit policy and baseline paths, accept an optional complete pair of base/current policy-context artifacts, and support the existing full-analysis build-state selectors plus Human, JSON, and SARIF output.

#### Scenario: Gate is reachable without changing validation commands
- **WHEN** a caller invokes `arch-linter-net gate --policy policy.yml --baseline baseline.yml`
- **THEN** the composed CLI invokes the gate handler and existing `validate`, `baseline`, and `policy weakening` command behavior remains unchanged

#### Scenario: Incomplete weakening inputs are rejected
- **WHEN** a caller supplies only `--base-context` or only `--current-context`
- **THEN** the command returns exit code 2 with an actionable invalid-input diagnostic
