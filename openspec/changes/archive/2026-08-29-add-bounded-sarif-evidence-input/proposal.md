## Why

ArchLinterNet needs to consume pre-produced static-analysis evidence without confusing a
missing, malformed, stale, or ambiguously scoped artifact with a successful zero-result run.
Issue #520 establishes the fail-closed trust boundary before later work filters or projects
external diagnostics.

## What Changes

- Add a bounded, repository-local SARIF 2.1.0 evidence reader with deterministic artifact hashing.
- Add explicit, vendor-neutral evidence requirement and producer-context models, including logical
  evidence identity plus optional repository, revision, and scope binding.
- Validate supported SARIF shape, matching producer/run identity, successful execution, required
  context, and bounded run/result counts without invoking an analyzer or remote service.
- Surface typed, deterministic evidence outcomes that distinguish absent, malformed, failed,
  mismatched, and valid zero-result input so downstream applicability work can preserve the trust
  decision.
- Add policy schema support and validation for declared external-evidence requirements. No
  diagnostic filtering or normalized-finding projection is introduced in this change.

## Capabilities

### New Capabilities

- `external-sarif-evidence`: bounded, vendor-neutral reading and trust validation of local SARIF
  evidence artifacts.

### Modified Capabilities

- `governance-applicability-evidence`: define how external-evidence reader outcomes supply the
  external-diagnostics family's typed assessability evidence.

## Impact

- Core policy models and policy-document validation.
- New Core external-evidence reader API and focused NUnit coverage.
- New OpenSpec requirements and later reviewed Core public-API snapshot update.
