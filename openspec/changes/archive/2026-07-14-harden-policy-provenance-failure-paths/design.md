## Context

`ArchitecturePolicyDocumentLoader` currently asks `ContainsImports` to parse
the selected root before a root source descriptor has been created. The same
loader then invokes raw-YAML validators after composition but before the
provenance index has an active validation subject. Separately, the provenance
map keys use rendered dot/index paths as identity, so legal YAML mapping keys
can overwrite another node's index entry.

The existing side index, parser, composer, and format-aware CLI exception
boundary are the correct seams. This change must preserve monolithic-policy
compatibility, import traversal order, and public human-readable YAML paths.

## Goals / Non-Goals

**Goals:**

- Produce typed root source-shape diagnostics before import detection can leak
  a raw parser exception.
- Enrich imported raw-YAML validation failures with the source that introduced
  the invalid layer or contract.
- Make provenance lookup identity unambiguous for every legal YAML mapping key.
- Preserve JSON and SARIF policy-error formatting through existing typed
  exception handling.

**Non-Goals:**

- Changing import grammar, graph limits, composition order, policy DTOs, or
  rendered dot/index YAML paths.
- Replacing the side-index architecture, adding a new validator framework, or
  redesigning CLI output.

## Decisions

### 1. Create the root descriptor before root parsing

The loader will derive a root descriptor through the same root-path resolution
logic used by the import resolver, then pass it to the root parsing/detection
step. The parser will convert malformed, multi-document, and non-mapping roots
to its existing source-shape exception enriched with that descriptor.

This keeps graph resolution responsible for filesystem identity while making
the parser responsible for YAML shape. Parsing all roots through full graph
resolution was rejected because monolithic policies must retain their current
loading path and effective-schema behavior.

### 2. Keep raw checks before deserialization, but give them a provenance subject

Raw validators will continue to inspect the composed YAML because they must
detect fields `IgnoreUnmatchedProperties()` would erase. Before validating each
layer or contract entry, they will set the corresponding collision-safe
effective path as the active provenance subject. The loader will wrap the raw
validation phase in the existing enrichment boundary.

Moving raw checks into the post-deserialization validator pipeline was rejected
because unknown raw keys and explicit nulls are no longer observable there.

### 3. Use escaped JSON Pointer only for provenance-map identity

A focused internal path helper will build and traverse JSON Pointer keys,
escaping `~` and `/` and representing sequence indices as pointer segments.
The map builder, index binding, schema-error lookup, and runtime path lookups
will use those keys. `ArchitecturePolicySourceLocation.YamlPath` will continue
to receive the existing dot/index display form.

Escaping only dots in the current string form was rejected because brackets,
numeric segments, and future YAML key characters still leave ambiguous parsing.
A typed node-tree key was also unnecessary: escaped JSON Pointer already
matches Json Schema instance locations and solves the concrete collision.

## Risks / Trade-offs

- [Risk] A missed raw lookup could leave one failure untyped. → Route every
  raw validator group through the same active-subject helper and add imported
  layer and contract regressions.
- [Risk] Pointer migration can miss a runtime lookup. → Centralize append,
  parent, and direct-sequence-item operations in one helper; run full
  acceptance plus collision regressions.
- [Risk] Exception messages may retain duplicated human suffixes. → Preserve
  this optional cleanup outside the critical fix scope unless it is directly
  exposed while validating the new tests.

## Migration Plan

The change has no persisted data or deployment migration. If a regression is
found, revert the feature-branch commit; no policy file migration is needed.

## Open Questions

None.
