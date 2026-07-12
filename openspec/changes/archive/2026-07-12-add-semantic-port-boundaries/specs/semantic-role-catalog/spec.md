## ADDED Requirements

### Requirement: The catalog defines port and anti-corruption vocabulary
The semantic role catalog SHALL define `Port`, `Adapter`, `PrimaryPort`,
`SecondaryPort`, and `AntiCorruptionLayer`, including their support tiers,
static evidence guidance, and reviewed metadata keys. `ExternalSystem`,
`IntegrationAdapter`, `PersistenceAdapter`, and direct-database examples SHALL
be marked examples-only or custom-mapping expected unless independently
promoted by the catalog.

#### Scenario: Policy author maps a project-owned port attribute
- **WHEN** a project uses a user-owned attribute for a named secondary port
- **THEN** the catalog SHALL show a YAML mapping and selector metadata without
  implying that ArchLinterNet supplies that attribute
