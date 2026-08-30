## Context

The existing public-API materialization cache identifies the effective exported type universe and records flattened signature identities for snapshot checks. The general reference scanner recursively walks all declared members but deliberately has only type-list paths and can silently omit reflection failures for ordinary consumers. Attribute-usage and relationship scanners provide defensive compiled-metadata and inheritance facts, but neither records a visible-contract path graph.

This change is the evidence foundation for #513/#514, not an exposure policy family. See `proposal.md` and the capability specification for behavior.

## Goals / Non-Goals

**Goals:**

- Build an immutable, session-owned index of visible contract exposure records and incomplete-evidence records.
- Preserve existing effective reviewed-surface selection as caller-supplied input, and reuse the established exported-visibility, safe reflection, type-name, relationship, and custom-attribute metadata conventions.
- Make every record deterministic and retain separate path evidence when different contract sites reach the same target.

**Non-Goals:**

- Add YAML, schema, diagnostics, or strict/audit policy evaluation; those belong to #513/#514.
- Change public-API snapshot capture/comparison or the semantic-role model.
- Interpret runtime serialization, strings, primitive metadata, or object graphs.
- Add a second global type/reference graph or a public Core API.

## Decisions

### Session-scoped exposure index over caller-provided roots

Add a Core-internal scanner and an `ArchitectureAnalysisSession`-owned cache whose keys are resolved `Type` object identity plus the requested visible-surface shape. Consumers provide already-selected roots (including the effective #525 surface) and receive immutable exposure and incompleteness evidence. This keeps #525 authoritative for membership and permits #513 to decide source/target policy separately.

The index will not use `ArchitectureReferenceGraph` as its output model: that graph scans private implementation members, collapses path alternatives, and lacks metadata-site detail. It will reuse its defensive reflection and cycle-termination posture where appropriate.

### Visible root and member rules mirror public-API materialization

The scanner will use the established exported type/member visibility, nested-type reachability, compiler-generated filtering, and accessor treatment from `ArchitecturePublicApiSurfaceScanner`. A root type contributes its own base/interface, type generic constraints, visible attributes, and visible declared contract members. A delegate's visible `Invoke` signature is treated as its contract signature. This avoids treating private implementation details as contract exposure while preserving established #94/#525 semantics.

### Structured paths and assembly-qualified type identity

Exposure records will carry stable path tokens for site (`type`, `member`, `parameter`, `return`, `attribute`, `attribute_argument`, etc.) and type-shape transitions (`generic_argument`, `array_element`, `nullable_underlying`, `tuple_element`, `constraint`, `base_type`, `interface`). Canonical ordering compares source identity, token sequence, target assembly, and target full name ordinally. Deduplication applies only to identical complete records; it never collapses distinct paths which happen to reach the same type.

Raw `Type` reference identity remains useful while scanning, but persisted/cross-component identity always includes `(assembly name, full type name)` so same-named types remain distinct.

### Metadata argument extraction is typed and fail-aware

Traverse `CustomAttributeData` at only visible sites. Add the attribute type itself, `System.Type` values and declared enum argument types (including supported nested argument arrays) as type targets; ignore primitive/string/null arguments. Attribute reflection failures become incomplete evidence with the site and a stable reason rather than silently disappearing. This builds on existing `CustomAttributeData` conventions without changing #86 placement evaluation.

### Bounded shape expansion with branch-local cycle protection

Expand signature type shapes and constraints recursively, but do not traverse arbitrary member bodies or runtime graphs. A branch-local identity set prevents self-referential generic/constraint shapes from recurring indefinitely; each retained branch emits its independently explainable prefix. The scanner records any reflection failure needed to assess a visible first-party site and leaves applicability classification to a later policy consumer using #505/#506.

## Risks / Trade-offs

- **Reflection can throw after an assembly was resolved** → wrap every site/shape/metadata read, record deterministic incompleteness, and preserve currently safe scan behavior for unrelated families.
- **Path counts can grow for nested generic and attribute-array shapes** → keep traversal bounded to contract signatures/metadata and use branch-local cycle protection plus canonical identical-record de-duplication.
- **A shared scanner could accidentally broaden public-API semantics** → tests will pin visibility, selector-root consumption, nested types, and compiler-generated/accessor handling against existing public-API fixtures.
- **Future policy consumers may need a different traversal bound** → keep policy-specific target matching and any rule limit outside this foundational index rather than baking a future contract into it.

## Migration Plan

The change is additive and internal to Core. Existing contract evaluation and snapshot behavior remain unchanged. Future #513/#514 will consume the index and map its incompleteness records into the already-delivered applicability evidence model.
