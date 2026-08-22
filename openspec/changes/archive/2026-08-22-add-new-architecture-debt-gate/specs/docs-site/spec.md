## ADDED Requirements

### Requirement: New-debt CI workflow is documented
Public MkDocs guidance SHALL document invoking the architecture-debt gate, its strict/audit boundary, exact baseline lifecycle, optional policy-weakening integration, output and exit behavior, and fail-closed comparison limits. The examples SHALL distinguish matched/new/resolved/stale debt from independent weakening failures and SHALL state that baseline updates remain explicit review operations.

#### Scenario: CI adopter can compose the gate safely
- **WHEN** an adopter follows the CI integration and baseline migration guidance
- **THEN** they can run the gate with explicit baseline and policy-context artifacts without treating warnings or policy weakening as persistent baseline debt
