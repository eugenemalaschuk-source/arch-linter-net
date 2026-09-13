## ADDED Requirements

### Requirement: Packaged Relay assets are integrity-checked before setup

The packed CLI SHALL resolve the Relay deployment assets and configuration
schema from its installed package or the repository's reviewed development
assets, validate them against a shipped bundle manifest containing the closed
bundle and compatibility identities, and reject missing, altered, unexpected,
or incompatible files before generating adopter configuration or deployment
output. The validation SHALL not trust an arbitrary remote checksum URL and
SHALL preserve the existing generated bundle digest and approved publisher pin
contract.

#### Scenario: A valid packed Relay bundle is resolved

- **WHEN** setup selects Relay from an installed CLI containing the reviewed
  bundle manifest, source files, lockfile, schema, and license notice
- **THEN** setup verifies every declared file and compatibility identity before
  writing adopter output
- **AND** the generated configuration retains the verified bundle digest and
  approved immutable publisher/action pins

#### Scenario: A packaged Relay asset is tampered with

- **WHEN** a Relay source file, lockfile, template, schema, notice, or bundle
  manifest differs from its declared digest or declares an unexpected path
- **THEN** setup fails closed with a local integrity error
- **AND** it writes no Relay, workflow, registry, README, or configuration
  output

