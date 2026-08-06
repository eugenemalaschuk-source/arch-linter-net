## Why

The archived Checkpoint B implementation still allows a pre-cancel race and
self-declared scenario evidence.  Release authorization must only use outcomes
that were independently executed and must reject structurally ambiguous evidence.

## What Changes

- Add a deterministic in-flight cancellation barrier for the packed Testing and
  CLI consumers, including output/cache postconditions.
- Make every reported scenario result originate from its executed oracle.
- Reject duplicate scenario IDs during evidence aggregation.
- Remove remaining OpenSpec placeholders and complete the archived validation record.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `checkpoint-b-release-evidence`: Require executable scenario result provenance and duplicate-free evidence.
- `checkpoint-b-candidate-provenance`: Define cancellation/output/cache safety and a resolved purpose.

## Impact

Checkpoint B NUnit harness, release evidence aggregator, release workflow
validation, and owning OpenSpec specifications.
