## Why

Project-topology metrics still build their observed topology subjects from a
legacy assembly-simple-name lookup before applying the exact resolved-artifact
owner binding. Two different project artifacts with the same output assembly
name can therefore collapse into one mapped node and yield a trusted partial
measurement. The reviewed public API baseline must also be regenerated from
the actual current public surface after the nullable metric evidence contract
change.

## What Changes

- Build project-topology metric subjects and external-source lookup identities
  from the exact resolved artifact-to-project binding, while preserving the
  legacy selector spelling used by existing topology configuration.
- Treat a duplicate project selector display identity in a metric project
  projection as ambiguous required input rather than merging its contributors.
- Add regression coverage for two distinct project artifacts that share an
  output assembly name.
- Regenerate the reviewed Core public API approval fixture from its canonical
  reflection-based surface description.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-metric-semantics`: project metric topology projection must
  retain artifact-derived owner identity and fail closed when a policy's legacy
  project selector cannot identify one artifact-derived project subject.

## Impact

Affected areas are the internal Core topology/metric projection and its NUnit
regressions, the OpenSpec metric contract, and the reviewed Core public API
snapshot. No CLI option, policy syntax, or external dependency changes are
introduced.
