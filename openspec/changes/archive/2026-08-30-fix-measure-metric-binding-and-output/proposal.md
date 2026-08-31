## Why

Review of the first measure-first metrics implementation found that namespace
and assembly owner ambiguity can produce a trusted value from the wrong native
subject. Its unassessable JSON shape also presents unknown contributor evidence
as a measured zero, and the committed public API approval snapshot is stale.

## What Changes

- Bind namespace external-dependency facts only to their exact canonical
  project/assembly-owned topology subject.
- Preserve every assembly subject candidate for an assembly simple name and
  mark metrics unassessable when a dependency endpoint cannot bind uniquely.
- Represent unknown contributor evidence as `null` rather than a verified zero
  in unassessable JSON measurements.
- Regenerate the reviewed Core public API approval snapshot and add regression
  coverage for each corrected contract.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `architecture-metric-semantics`: Require exact owner-aware subject binding
  and fail closed on ambiguous native assembly endpoints.
- `architecture-metric-measurement`: Require unassessable JSON to distinguish
  unknown contributor evidence from an evaluated empty contributor set.

## Impact

- Core topology projection and metric evaluator; CLI JSON formatter and tests.
- Reviewed Core public API snapshot.
- No new policy syntax, CLI options, dependencies, or write behavior.
