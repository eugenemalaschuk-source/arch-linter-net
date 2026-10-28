## Why

The v0.10.0 release amplified coupling between read-only badge operations and publication failure handling, while six release jobs independently copy the same approved publisher source pins. Both are post-release architecture cleanup: existing publication, provenance, and command behavior must remain intact.

## What Changes

- Give preflight and runtime integration their own execution and failure paths, with no publication capability, while keeping explicit CLI dispatch and the existing evidence and decision authorities.
- Acquire the independently approved publisher workflow and action commits through one repository-owned implementation in all six release jobs, leaving every frozen-transport verification in place.
- Add boundary and compatibility regression coverage without changing shipped pins, publication authority, or external contracts.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-health-badge-promotion`: Specify isolated, read-only preflight and runtime integration operation boundaries.
- `release-artifact-provenance`: Specify shared acquisition of independently approved publisher sources before per-job verification.

## Impact

The Python badge promotion CLI and operation modules, release-distribution tooling, release workflow, focused regression tests, and the two existing specifications are affected. No .NET API, package, schema, release format, shipped publisher pin, or authorization changes are intended.
