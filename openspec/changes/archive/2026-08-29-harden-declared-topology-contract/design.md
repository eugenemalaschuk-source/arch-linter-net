## Context

The initial native topology model intentionally stopped before observed-fact
evaluation. Review established that its selector language must nevertheless be
fully constrained by the declared observed-fact granularity, and that every
static identity must be structurally comparable before #509 uses it.

## Goals / Non-Goals

**Goals:**

- Bind every permitted topology selector to an unambiguous subject-fact field.
- Make duplicate detection, projection ordering, and weakening comparison
  collision-free and independent of YAML order.
- Restore topology-affected repository CI contracts and prove imported
  topology provenance.

**Non-Goals:**

- Implement observed-fact matching, graph evaluation, topology findings, or a
  diagram importer.
- Change the serialized topology YAML shape or add an evaluator convention.

## Decisions

### Subject kind determines selector vocabulary

`type` subjects accept layer, namespace, project, assembly, and context
selectors: each is a predicate over one observed type fact. `namespace`
subjects accept namespace, project, and assembly selectors: the latter two are
exact equality against the namespace fact's canonical owning project and
assembly. `project` subjects accept only project selectors; `assembly`
subjects accept only assembly selectors. Layer and context selectors are
therefore rejected for non-type subjects, rather than defining an existential
or universal aggregate over contained types.

### Structural keys replace delimiter strings

Validator duplicate checks use a typed selector-key object with explicit
equality over selector kind, scalar values, and sorted metadata entries.
Directional edges use a `(from, to)` tuple. Policy-context ordering and
weakening comparison use dedicated structural comparers over their typed
selector projections. Delimiter escaping is rejected because it remains an
encoding protocol whose correctness is easy to regress.

### Preserve the existing mutable cache convention

`ArchitectureTopologySubjectSelector.Namespace` uses a backing field and
clears its parsed glob cache on assignment, matching `ArchitectureLayer`.

## Risks / Trade-offs

- **Some formerly accepted policies become invalid.** → They were semantically
  under-specified; clear validation diagnostics name the selector kind and
  subject kind.
- **Structural comparers add small code paths.** → Focused collision and
  reordered-input tests cover equality, ordering, and weakening.
- **Context exports are public API.** → Update both reviewed public API
  mechanisms and verify the schema version in CLI tests.
