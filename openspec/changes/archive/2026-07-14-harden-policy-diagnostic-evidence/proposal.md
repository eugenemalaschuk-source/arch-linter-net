## Why

Policy exceptions in SARIF currently omit part of the authored evidence, and
policy-consistency findings can attach a location for a same-named contract
that was not part of the finding. Both failures produce credible but incomplete
or incorrect machine-readable diagnostics.

## What Changes

- Make policy-exception SARIF reuse the established policy-location mapping:
  every primary and related declaration appears in `relatedLocations` with a
  portable URI, source region, and YAML-path message.
- Select policy-consistency provenance by participating contract IDs whenever
  identifiers are available, rather than broad name-only matching.
- Add exact regression coverage for root-versus-fragment SARIF evidence and
  same-name contracts in different contract families.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `violation-reporting`: SARIF policy exceptions must contain complete,
  machine-readable declaration evidence.
- `policy-import-composition`: consistency provenance must identify only the
  contracts that participate in a diagnostic.

## Impact

Affected code is limited to the CLI policy-exception writer, the shared SARIF
policy-location mapper, the provenance side index, and their NUnit tests. No
contract DTO, import graph, or public policy syntax changes are required.
