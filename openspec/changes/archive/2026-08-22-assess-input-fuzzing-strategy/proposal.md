## Why

ArchLinterNet now directly parses Git repository objects and history metadata to
produce release-forensics evidence.  That custom byte-processing boundary has a
materially different robustness profile from the repository's YAML, JSON, and
framework-owned project/metadata parsers, so a Scorecard signal alone is not a
useful basis for selecting test techniques.

## What Changes

- Record an evidence-based A/B/C decision for each current untrusted-input
  surface, ranked by custom-code exposure and realistic failure impact.
- Define the required safety and operational contract for any future selected
  fuzzing harness, including bounded inputs, replay, corpus ownership, and
  CI cadence.
- Select the custom Git object/pack parser seams for a focused, separately
  tracked coverage-guided fuzzing implementation; defer the other assessed
  surfaces where current deterministic tests or framework validation are the
  better investment.

## Capabilities

### New Capabilities

- `input-robustness-testing`: Evidence-based selection and safe operation of
  fuzzing and property-based testing for accepted untrusted inputs.

### Modified Capabilities

- None.

## Impact

This change adds an internal security-testing decision record and its OpenSpec
contract. It does not change shipped commands, public APIs, parser semantics,
or CI execution. A subsequent issue may add a bounded Git-parser fuzz harness.
