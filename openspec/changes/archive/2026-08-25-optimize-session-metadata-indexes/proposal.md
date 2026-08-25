## Why

The immutable analysis session already owns reusable type, reference, role, and source facts, but
package, framework-reference, assembly-dependency, and project-metadata checks still reconstruct
repository-wide project or assembly lookups for every contract. As policy templates fan out across
many projects, this turns stable session facts into repeated O(contract × project/assembly) work.

## What Changes

- Add lazy, immutable session projections for assembly names and discovered-project metadata.
- Route package dependency, framework-reference source ownership, assembly dependency, and project
  metadata checks through those projections.
- Add deterministic internal materialization counters and a synthetic many-project/many-contract
  regression fixture proving the projections are built once per analysis session.
- Preserve findings, identities, ordering, strict/audit behavior, baseline behavior, and output
  semantics. No persistent or cross-process state is introduced.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-session-indexes`: Extend session-owned immutable indexes to cover project and assembly
  metadata projections consumed by additional contract families.

## Impact

- Affects Core session fact access, the package, framework-reference, assembly-dependency, and
  project-metadata checker paths, plus focused Core tests.
- No policy-schema, CLI, Testing API, persistent-cache, or public-output contract changes.
