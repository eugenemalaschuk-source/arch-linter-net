## ADDED Requirements

### Requirement: Confinement survives SonarCloud Python security analysis

A release-workspace confinement helper SHALL be structured so that SonarCloud's Python
path-injection analysis recognizes the confinement and does not re-report a filesystem sink that is
already guarded by it. Where recognition cannot be achieved without weakening the helper, each
residual finding SHALL carry an individually reviewed false-positive disposition that names the
guarding helper, the rule key, the component and the line, recorded in the debt inventory. A
confinement decision SHALL NOT be expressed as a rule-wide, file-wide or directory-wide suppression,
and SHALL NOT weaken the `_safe_path`/`_github_command_file_path`/`_github_runner_temp_path` trust
boundaries defined above.

#### Scenario: A guarded sink is not reported as an unguarded vulnerability

- **WHEN** a script under `tools/release/` validates a `Path`-typed CLI argument through the shared
  confinement helper before a filesystem sink
- **THEN** the corresponding SonarCloud path-injection finding is either absent or carries a reviewed
  false-positive disposition that names the guarding helper

#### Scenario: A residual finding is individually justified

- **WHEN** a confinement-guarded sink is still reported after the helper is applied
- **THEN** the disposition records the rule key, file and line and the guarding helper, and no
  rule-wide suppression is introduced
