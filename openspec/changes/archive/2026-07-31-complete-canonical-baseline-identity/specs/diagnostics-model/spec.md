## ADDED Requirements

### Requirement: Every normalized baseline-capable finding carries its execution identity
The normalized finding model SHALL expose the exact canonical identity created by the finding
execution path for every baseline-capable family. Human, JSON, SARIF, Testing, and baseline
comparison projections SHALL consume that identity without reconstructing it from display fields.

#### Scenario: Rendering does not affect a finding identity
- **WHEN** the same validation result is rendered sequentially to human, JSON, SARIF, or Testing output
- **THEN** every projection SHALL expose the same canonical identity for the finding.

