## Context

`ArchitecturePolicyDocumentLoader` currently reads one YAML string, performs raw-node checks, deserializes it, assigns fallback IDs, and runs the fixed validator pipeline. The approved import design requires graph and composition work before those existing semantic stages, while the CLI and Testing adapters must continue to call `Load(string)` once. YamlDotNet is already the policy parser, and the contract-family DTO/binding registry must remain independently extensible.

## Goals / Non-Goals

**Goals:**

- Resolve a bounded, deterministic local import graph from one explicit root.
- Validate graph-assigned root/fragment roles and portable path rules before composition.
- Compose YAML nodes generically enough that registered/current contract groups do not require loader switches.
- Preserve the current deserialization, fallback-ID, and validator behavior for the effective policy.
- Make failure classes stable for callers and tests while keeping messages actionable.

**Non-Goals:**

- Attaching provenance to every downstream semantic diagnostic or machine-readable result (#282).
- Multiple roots, remote resolution, globs, interpolation, optional imports, templating, or cross-file anchors.
- CLI/Test adapter signature changes or family-specific loader logic.

## Decisions

### Resolve and compose through focused instance collaborators

The loader will delegate source reading/graph traversal and AST composition to focused instance-based classes. The current loader remains the public orchestration seam. This keeps filesystem/path behavior testable and keeps graph concerns out of contract-family validation.

The existing filesystem interface cannot verify authored casing or resolve links/junctions to physical identities. A narrowly scoped policy-path identity interface will provide repository-boundary resolution, exact-case checking, and physical canonicalization. This abstraction solves those concrete security/portability requirements only; it is not an import-provider extension point.

Alternative considered: place `Path`, `DirectoryInfo`, and link logic directly in the loader. Rejected because the resulting behavior could not be tested deterministically across platforms and would recreate a large static orchestration method.

### Compose YamlDotNet representation nodes before POCO deserialization

Each source is parsed into a `YamlMappingNode`, role-validated, and retained in depth-first pre-order. The composer copies nodes into one effective mapping: keyed maps are unioned with duplicate rejection; lists are appended in source order; singleton analysis/classification settings reject repeated explicit declarations; contract group lists append without enumerating family names. `imports` is consumed rather than emitted.

This preserves explicit-field presence and raw classification sections and automatically composes new contract groups that follow the schema. The existing loader then serializes the effective node and performs its established raw checks, deserialization, fallback IDs, and validator pipeline once.

Alternative considered: deserialize every fragment and merge POCOs. Rejected because CLR defaults erase explicit singleton presence, raw classification fields disappear, and every contract-family list would require typed copy logic.

### Use explicit role/effective shape validation and published schemas

Runtime role validation rejects unknown top-level keys, root-only keys in fragments, empty fragments, malformed imports, and missing root identity. After composition, the effective mapping must contain the existing required root sections and is validated with JsonSchema.Net against the embedded published schema before semantic loading. The published effective schema gains `imports` on roots, and a sibling fragment schema reuses its `$defs` while closing the fragment top level.

Schema/editor filenames are never consulted at runtime; role comes exclusively from the entry path and graph reachability.

### Categorize policy import failures

A dedicated `ArchitecturePolicyImportException` carries an enum category for portable path, missing file, out-of-bound target, case mismatch, cycle, duplicate import, graph limit, source shape, and composition conflict failures. Messages include the relevant authored/canonical paths and declaration locations. Existing non-import load exception behavior remains unchanged where practical.

## Risks / Trade-offs

- [Physical identity differs by platform] -> Centralize path identity logic, compare portable identities case-insensitively, verify path casing by directory entries, and test with a fake plus real-file integration cases.
- [Generic AST merging could admit a future section with different semantics] -> Close root/fragment top-level shapes and explicitly classify every current mergeable section; unknown top-level fields fail rather than silently merge.
- [Schema and runtime shape rules can drift] -> Add schema instance tests for both root and fragment roles alongside loader tests using the same fixtures.
- [Large graphs could consume memory] -> Enforce depth 16 and 256 total files before reading the over-limit target; policies without `imports` take the direct single-source path.
