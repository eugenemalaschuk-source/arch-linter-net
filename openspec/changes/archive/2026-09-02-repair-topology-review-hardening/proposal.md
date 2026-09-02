## Why

The topology review workflow can currently present an unassessable ordinary-validation result as
a clean diff and protects output aliases by scanning every repository file after analysis. Its
acceptance proof, command help, and cancellation reporting also fall short of the workflow's
review and no-mutation guarantees.

## What Changes

- Preserve declared-topology applicability records in diff documents and return a typed
  unassessable outcome when ordinary validation cannot produce reviewable evidence.
- Replace repository-wide output-collision discovery with consumed-input provenance and a fast
  path for new output files.
- Strengthen .NET and Unity lifecycle acceptance evidence to cover file publication and all
  consumed inputs.
- Align capture, diff, and verify help with registered options and preserve cancellation through
  atomic publication.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `topology-review-workflow`: clarify reviewable applicability, provenance-based output safety,
  lifecycle immutability evidence, CLI option parity, and cancellation semantics.

## Impact

Affected code includes the topology CLI command handler, output guard, diff renderer, validation
provenance projection, command help, and topology lifecycle/CLI tests. The reviewed Core surface
adds provenance properties to capture and validation outcomes; no new package is introduced.
