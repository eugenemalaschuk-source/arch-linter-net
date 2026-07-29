## Context

Core already maps checker output to sealed `ArchitectureDiagnostic` subtypes, but formatters independently select fields into JSON and SARIF. Baseline comparison and build/policy failures are represented outside this path. Consequently, no public object gives every adapter the same versioned evidence.

## Goals / Non-Goals

**Goals:**

- Provide one immutable normalized finding envelope for all public diagnostic producers.
- Preserve existing common JSON fields through an explicit v1 compatibility projection.
- Make typed family evidence, canonical identity, provenance, baseline status, ordering, and location available without parsing prose.
- Version and validate the machine contract alongside the packaged schema registry.

**Non-Goals:**

- Change architecture-check or baseline identity semantics.
- Replace SARIF 2.1.0, create an untyped property bag, or add runtime/data-flow analysis.
- Remove legacy JSON fields during the 0.5.1 compatibility window.

## Decisions

### 1. A normalized finding is the sole adapter input

Introduce `ArchitectureFinding` with `SchemaVersion`, stable string `Kind`, envelope metadata, `CanonicalIdentity`, optional baseline state, source/policy locations, and a typed `Details` record. A mapper converts every existing `ArchitectureDiagnostic`, baseline comparison entry, build/preflight error, and policy failure to it before any output adapter runs. Human, JSON, SARIF, and Testing API use this object only.

Keeping the existing diagnostic hierarchy as the checker-facing representation avoids a broad semantic rewrite. Serializing each hierarchy subtype directly was rejected because it couples the wire contract to CLR type names and does not cover non-violation findings.

### 2. Details are a closed, typed discriminated hierarchy

`Details` is an abstract record with one concrete record per diagnostic family; each record contains only the evidence meaningful to its kind. JSON emits `{ schema_version, kind, ..., details: { ... } }`. The mapper writes the stable lower-snake-case kind and uses ordinal ordering. Unknown versions are rejected; an unknown future kind with a supported schema version is surfaced as an opaque finding retaining envelope and raw details for non-strict consumers, and rejected by strict contract validation.

An `object` or string-keyed property bag was rejected because it cannot express required payload shape or forward-compatibility rules.

### 3. Compatibility is additive and derived

The JSON formatter retains documented legacy `source`, `forbidden_namespace`, `forbidden_references`, contract, and family fields. They are derived from the normalized finding; no adapter reads an `ArchitectureDiagnostic` after normalization. A `schema_version` and `kind` are added. The compatibility fields are documented as deprecated for machine consumers in favour of `details`.

### 4. SARIF carries the exact normalized semantic payload

SARIF keeps its standard message/rule/location fields and adds the normalized envelope and typed `details` under `result.properties.arch_linter_net`. Physical locations remain first-class SARIF locations. This preserves SARIF interoperability while preventing evidence loss.

### 5. Identity and ordering are format-independent

The mapper receives canonical identity from the existing identity resolver and baseline lifecycle state from comparison results. It defines one ordinal comparison key `(contract id, canonical identity, kind, source location)`, used before every projection. No serializer hashes, parses, or recomputes identity.

## Risks / Trade-offs

- [Risk] A family is omitted from the mapper → Mitigation: exhaustive kind-to-details matrix tests and a guarded default that fails test/strict paths.
- [Risk] Additive JSON changes surprise byte-level consumers → Mitigation: document schema version, retain old fields, and validate compatibility examples.
- [Risk] SARIF properties duplicate data → Mitigation: retain only the normalized projection under one namespaced property and use standard locations for physical locations.

## Migration Plan

1. Add normalized model, mapper, schema, and exhaustive tests.
2. Route JSON, SARIF, human, Testing, and baseline producers through the mapper.
3. Document v1 compatibility fields and unknown-kind/version behavior.
4. Package and validate the schema, run acceptance, then archive the OpenSpec change. Rollback consists of reverting the additive v1 projection; existing internal diagnostic and baseline formats remain intact.

## Open Questions

None.
