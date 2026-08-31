## ADDED Requirements

### Requirement: Current packaged schemas and frozen legacy resources are distinguished
The packaged schema registry and compatibility manifest SHALL identify the
current normalized-finding and analysis-cache schema resources used for writing
and package smoke validation. They SHALL also retain frozen earlier resources
as explicit legacy read contracts, without presenting an obsolete resource as
the current writer schema.

#### Scenario: Packaged tooling inspects current and legacy schemas
- **WHEN** the packaged CLI prints the normalized-finding and analysis-cache
  schemas
- **THEN** it reports the current v3/0.8 normalized-finding and current cache
  resources, while a separate legacy check verifies the frozen 0.6.1 bytes
