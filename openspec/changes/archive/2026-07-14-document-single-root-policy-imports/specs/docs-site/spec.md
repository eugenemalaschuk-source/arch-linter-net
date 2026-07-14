## ADDED Requirements

### Requirement: Policy import documentation is publicly discoverable
The MkDocs navigation and core public entry pages SHALL link to the policy-import guide, and the README capability summary SHALL identify deterministic local policy imports without linking users to internal design documents.

#### Scenario: User looks for policy imports
- **WHEN** a user starts from the README, policy-format overview, YAML schema reference, troubleshooting page, or Policy Authoring navigation
- **THEN** the public policy-import guide is reachable without consulting `docs/internal`

