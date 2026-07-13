## Context

ArchLinterNet currently deserializes one YAML document directly into `ArchitectureContractDocument`. The public entry points already take one policy path, and the modular loader/validator/family-binding work from #210 and #216 provides the boundary on which import resolution can be added. Issue #280 is the design gate for #281 (resolution and composition) and #282 (provenance).

The design must cover every current root section: `version`, `name`, `layers`, `external_dependencies`, `packages`, `legacy_runtime_layers`, `analysis`, `contracts`, and `classification`. Baselines remain separate input documents. Future CEL expression fields are ordinary scalar values within their owning composed node and receive no import-specific execution capability.

## Goals / Non-Goals

**Goals:**

- Preserve exactly one explicitly selected, normally executable root policy.
- Allow root and fragment inline content to compose deterministically without filename semantics or silent precedence.
- Define portable local-path safety, bounded nested imports, source provenance, and schema/editor behavior.
- Preserve monolithic policy behavior and the existing family-owned YAML binding and validation architecture.
- Give #281 and #282 an implementation-ready component boundary and test matrix.

**Non-Goals:**

- Implementing the resolver, composer, provenance model, or production schemas in #280.
- Remote/package/Git imports, globs, optional imports, templating, interpolation, scripting, runtime plugins, or cross-file YAML anchors.
- Multiple roots, a manifest-only root, filename-based roles, overrides, or unrelated loader refactoring.
- Importing or merging baseline files.

## Decisions

### Use `imports` with explicit relative paths

`imports` is a top-level ordered YAML sequence of non-empty relative file paths. It is allowed in the root and fragments. Absolute paths, URI-like values, globs, environment syntax, and non-scalar entries are invalid. Paths resolve relative to the file that declares them.

This is explicit, schema-friendly, and independent of YamlDotNet `!include` behavior. `include` was rejected because the plural collection is clearer; typed destinations were rejected because they would duplicate the existing top-level section model.

### Roles come only from the graph

The CLI or Testing adapter supplies the sole root. Every document reached from its `imports` graph is a fragment. `arch.yml` and `*.arch.yml` are recommendations only. The same YAML file is validated as a root when explicitly selected and as a fragment when imported, so a fragment containing root-only fields fails in the latter role.

The root requires `version` and `name` and may contain any existing section plus `imports`. A fragment may contain `imports`, `layers`, `external_dependencies`, `packages`, `legacy_runtime_layers`, `analysis`, `contracts`, or `classification`; it must contain at least one mergeable section or non-empty `imports`. `version`, `name`, and any baseline-shaped top-level field are forbidden in fragments.

### Expand nested imports in stable pre-order

Composition starts with the root's inline content, then visits each root import in declared order. For each fragment, its inline content is appended before recursively visiting its imports in declared order. This depth-first pre-order produces a stable source stream without treating the YAML location of the top-level `imports` key as an ordering signal.

The resolver fails on the first error encountered in that traversal. A path already on the active stack is a cycle; a previously completed canonical path is a duplicate import. Duplicate paths are errors rather than implicit de-duplication because de-duplication would conceal authored graph structure.

### Compose by field category and reject conflicts

No source has precedence over another. Root-inline and imported values use the same rules.

| Category | Fields | Rule |
|---|---|---|
| Root identity | `version`, `name` | Root-only; exactly one root declaration. |
| Keyed definitions | `layers`, `external_dependencies`, `packages`, `analysis.condition_sets` | Union by key using ordinal case-sensitive keys; any repeated key is a conflict, even if values are identical. |
| Ordered lists | `legacy_runtime_layers`; analysis path/project/assembly lists; classification mapping lists; every strict/audit contract list | Concatenate in composed source order; do not sort, deduplicate, or merge list entries. |
| Singletons | Analysis scalar settings; `classification.precedence` | At most one explicit declaration in the graph; defaults apply only after composition. |
| Contract nodes | Contract entries, including nested `ignored_violations` | Atomic list items; nested values never merge across files. |

Contract fallback IDs are assigned after composition. Duplicate IDs retain the existing compatibility rule: compare case-insensitively and reject duplicates within the same contract family and mode across all source files; the same ID remains allowed across different families or strict/audit groups. This extends the current validator's scope across fragments without changing valid monolithic behavior.

`classification` lists preserve their existing authored-order semantics. Future CEL fields are carried with their node, parsed and validated only after composition, and cannot read import metadata or the filesystem.

### Conflicts report both sources

Every parsed node carries a source descriptor containing the explicit root path, canonical repository-relative source path, and YAML path. A composed node retains its descriptor. Conflict diagnostics name both the first declaration and conflicting declaration; other validation diagnostics name the originating source and YAML path. Output ordering uses composed order, not filesystem enumeration.

Provenance is composition metadata, not new properties on every contract POCO. #282 may use a side table keyed by composed object identity or a focused wrapper, but it must not couple all family DTOs to filesystem services.

### Confine and bound the graph

The allowed boundary is the repository root resolved once for the run from the explicit root policy; imports never change it. Before reading, each import is combined with its declaring directory, made absolute, physically canonicalized through links/junctions where supported, and checked to remain inside that boundary.

Authored path segments must match on-disk casing exactly. Canonical repository-relative paths use `/`; portability identity is also compared with `OrdinalIgnoreCase`, so repositories containing case-only aliases fail consistently rather than working on one operating system and failing on another. Physical aliases to one file are duplicate imports.

The root is depth 0. An import chain may contain at most 16 edges, and the composed graph may contain at most 256 files including the root. Limits are fixed product constants in v1, checked before reading the over-limit file. Missing files, directories, non-regular files, boundary escapes, casing mismatches, duplicate paths, cycles, depth overflow, and file-count overflow are configuration errors.

### Publish two role schemas without filename coupling

The root schema extends the current policy schema with `imports`. The fragment schema shares definitions with the root schema but permits only fragment fields and does not require root identity. Both reject unknown top-level fields. Runtime chooses the schema from graph role. Editors can select either published schema explicitly, including with a YAML language-server `$schema` comment; repository filename associations may be examples but cannot be required for correctness.

### Keep resolver, composer, and validation responsibilities separate

#281 should add focused resolver/composer collaborators behind the existing `IArchitecturePolicyDocumentLoader` orchestration seam. Resolution owns graph traversal and safe file identity; per-file parsing owns role-shape validation; composition owns field rules and provenance attachment; the existing validator pipeline runs once on the final document. Family bindings remain the source of contract-list enumeration, so adding a future family does not require a new composer branch.

## Risks / Trade-offs

- [Exact-case paths are stricter on Windows/macOS] → This prevents cross-platform drift and produces an actionable casing diagnostic.
- [Nested imports increase resolver complexity] → Fixed traversal, active-stack cycle detection, and hard graph limits keep behavior bounded.
- [Rejecting identical keyed definitions prevents convenient repetition] → Explicit failure avoids hidden ownership and makes the composed policy equivalent to intentional monolithic YAML.
- [Singleton ownership can force authors to reorganize fragments] → Diagnostics identify both declarations, and analysis list fields remain freely decomposable.
- [Two schemas require explicit editor selection for arbitrary names] → Publish stable schema URLs and document inline schema directives; runtime role never depends on editor configuration.
- [Physical canonicalization varies by filesystem] → Test through the file-system seam and reject unresolved/reparse escapes before composition.

## Migration Plan

1. Archive this design so `policy-import-composition` becomes the normative source of truth.
2. #281 implements graph resolution, role validation, composition, root/fragment schemas, and regression fixtures behind the existing loader seam.
3. #282 attaches provenance to composed nodes and all relevant diagnostics/machine-readable output.
4. #283 publishes end-user modular-authoring guidance and exercised samples once runtime support ships.

Rollback is additive: policies without `imports` continue down the single-document path. Removing the new resolver/composer registration restores the previous runtime behavior without migrating monolithic policies.

## Open Questions

None for v1. Configurable graph limits, optional imports, globs, and package-distributed fragments require separate future proposals.
