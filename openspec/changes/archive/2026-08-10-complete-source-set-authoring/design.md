## Context

Source-set expansion runs when the policy document is loaded, before the validator pipeline.
That ordering makes ordinary expanded contracts transparent to execution, baselines, coverage and
reporters. It cannot, however, see the final filtered solution inventory: solution parsing and
`project_include`/`project_exclude` filtering are applied later by the runner setup service.

Directional assembly dependency and allow-only contracts also still use scalar models, even though
the established expansion interface already carries source, list selectors, exclusions, clone
semantics, derived identity, and provenance for package/framework/external families.

## Goals / Non-Goals

**Goals:**

- Reuse the one existing source-set expansion seam for both directional assembly families.
- Resolve project sets against either explicit project paths or the final filtered solution
  inventory, using a constrained path-glob grammar appropriate for repository-relative paths.
- Retain deterministic identities and fully structured source/provenance inventory everywhere.
- Keep existing policies, direct-only assembly evaluation, and analysis boundaries compatible.

**Non-Goals:**

- Discovering projects or assemblies outside the solution/explicit analysis boundary.
- Adding arbitrary regexes, templates, or a second assembly macro mechanism.
- Changing project coverage, solution discovery, or direct assembly-reference semantics.
- Performing the packaged-artifact release gate from issue #466.

## Decisions

### 1. Implement the assembly models through `IArchitectureSourceExpandableContract`

Add the established selector fields, expansion origin and explicit `CloneForSource` methods to
assembly dependency and allow-only contracts. Register their strict/audit groups with the existing
source-set expander. This reuses its derived-ID, de-duplication, bounds, subtraction, selector
provenance and selection behavior. A bespoke assembly macro would duplicate all those guarantees.

### 2. Resolve project sets only after project discovery provides the final universe

Keep eager expansion for assembly and layer sets at policy load. Defer project-kind set resolution
and `project_sets` list union until runner setup has called project discovery. The discovery service
returns repository-relative paths after solution filtering; these paths become the only project-set
universe when a solution owns discovery. Explicit `analysis.projects` remains its existing
backward-compatible universe.

The deferred resolver must reconstruct the source-expansion inventory deterministically and run
the project-metadata validation after its union, before the contract runner is created. This avoids
silently accepting a metadata contract with no resolved projects.

### 3. Use `ProjectPathGlob` for project selectors, not dotted-name glob parsing

Assembly/layer glob semantics use dot-segment namespace patterns. Project source sets instead use
the existing constrained repository-relative `*`/`**` path glob grammar, normalized to `/`, because
project paths are neither namespaces nor assembly names. The schema and docs name this distinction
explicitly.

### 4. Preserve provenance at authored, reference, and selector levels

The deferred project resolver receives the loaded document and its provenance index, and records
the project-set root, `project_sets[i]`, and matching `members[i]`/`globs[i]` locations using the
same expansion inventory model. Imported fragments therefore retain root/set/selector provenance.

### 5. Treat empty/stale project selectors as configuration failures

Unknown sets, kind mismatch, out-of-universe members, zero-match selectors, and all-empty
non-optional project sets fail before execution. Optional sets require a reason and are recorded as
optional-empty. This is consistent with the original source-set model and prevents a matching
repository change from being silently ungoverned.

## Risks / Trade-offs

- [Deferred expansion changes the established load-time-only model] → Keep the deferred portion
  limited to project-kind sets and invoke it at one runner-setup boundary after discovery.
- [Repeated runner setup could mutate a document twice] → Make project union/idempotent inventory
  replacement explicit and test repeated setup.
- [Project path patterns could be confused with assembly patterns] → Use the existing path-glob
  implementation and document the grammar separately in schema/reference guidance.
- [Metadata-only runs may lack assembly outputs] → Resolve the project universe independently of
  build outputs, as existing project-metadata discovery already supports.

## Migration Plan

Existing scalar assembly contracts and explicit project sets remain valid unchanged. Consumers can
incrementally replace repeated assembly blocks with one `source_sets` reference, and can remove a
duplicated `analysis.projects` list once their solution-discovered set is exercised. Removing the
new fields restores the prior policy shape without data migration.

## Open Questions

None. The issue acceptance criteria select the constrained path-glob and final filtered-universe
approach.
