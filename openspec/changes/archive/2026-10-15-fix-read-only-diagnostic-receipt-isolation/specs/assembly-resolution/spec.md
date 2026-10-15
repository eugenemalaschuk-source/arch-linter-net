## ADDED Requirements

### Requirement: Receipt-verified consumer assemblies are isolated from CLI assemblies
When a request selects prepared receipts, the system SHALL load target assemblies from the exact receipt-verified output paths in an isolated load scope. If a dependency with the same simple name is already loaded by the CLI, resolution for the consumer target SHALL use the consumer's selected probing paths and SHALL NOT bind the target's type signatures to the CLI's incompatible dependency version.

#### Scenario: Consumer dependency version coexists with the CLI version
- **WHEN** the CLI has loaded `Microsoft.Extensions.DependencyInjection.Abstractions` version 9 and a receipt-verified consumer output references version 10 from its output directory
- **THEN** the consumer assembly and dependency load in the isolated scope and its public property or interface signatures can be reflected successfully

#### Scenario: Incomplete type loading cannot produce topology success
- **WHEN** reflection over a target assembly reports `ReflectionTypeLoadException` and cannot provide a complete type universe
- **THEN** topology observation fails through the analysis error path instead of returning a successful partial subject or relationship set
