# Versioned contract-surface isolation contracts

Versioned contract-surface isolation contracts keep one statically selected
contract surface from exposing types belonging to another version or to an
implementation surface. They build on the existing contract-surface exposure
analysis, but let each rule name its source and forbidden surfaces locally.
This is an additive policy family; existing `contract_surface_exposure`
contracts are unchanged.

Groups:

- `strict_versioned_contract_surface_isolation`
- `audit_versioned_contract_surface_isolation`

## Example

```yaml
contracts:
  strict_versioned_contract_surface_isolation:
    - id: orders-v1-isolation
      name: orders-v1-isolation
      surfaces:
        - id: v1-contracts
          types_matching:
            namespace: Acme.Orders.V1.Contracts
        - id: v2-contracts
          types_matching:
            namespace: Acme.Orders.V2.Contracts
        - id: transport-implementation
          types_matching:
            role: TransportImplementation
      source_surface: v1-contracts
      forbidden_surfaces:
        - v2-contracts
        - transport-implementation
      reason: V1 contracts must not disclose newer-version or transport types.
```

Each rule has a non-blank `id` and `name`, one local non-empty `surfaces`
list, one `source_surface`, and one or more `forbidden_surfaces`. Every
surface has a unique non-blank `id` and a non-empty `types_matching` selector.
Surface references resolve only within the same rule. Blank, duplicate,
unknown, empty, unbounded, or self-referential declarations are invalid policy
configuration.

## Selecting surfaces

`types_matching` reuses the bounded selector vocabulary used by other contract
families:

| Field | Matches |
| --- | --- |
| `name_suffix` / `name_prefix` | Simple type name |
| `namespace` | A namespace or one of its child namespaces |
| `layer` | A namespace resolved to a declared layer |
| `base_type` | A type whose base-type chain includes the named type |
| `implements_interface` | A type implementing the named interface |
| `has_attribute` | A type carrying the named full-name custom attribute |
| `role` | The type's existing winning semantic role |

All populated fields combine with **AND** semantics. The selector observes
existing classification and metadata; it does not add a second role, invent
tags, or introduce regex or free-form expressions. Surface IDs are policy
labels, not CLR type identities.

The source surface selects exported visible roots. A forbidden surface selects
target types from the complete exposure universe, including first-party
non-exported implementation types when they occur in an exposure path. Thus a
v1 contract can be protected from both v2 contracts and internal transport
types.

## What is inspected

For each source root, the checker follows recursively visible CLR signatures
and compiled contract metadata. It traverses nested generic arguments, tuple
elements, arrays, wrappers, and other metadata-supported signature shapes. For
example, exposing `Task<Envelope<V2.Customer>>` produces a finding for the
v2 `Customer` target when it matches a forbidden surface.

The checker emits one deterministic finding per distinct source/path/target
occurrence. Findings retain the source surface, declaring type/member or
metadata site, deterministic exposure path, and assembly-qualified target
identity. Same-named types from different namespaces or assemblies remain
distinct, as do distinct paths to the same target. Strict and audit findings
reuse the existing contract-surface exposure diagnostic payload, canonical
identity, suppression/ignore, baseline, Human, JSON, SARIF, and Testing
projections.

## Applicability and failures

Each effective rule contributes one required applicability record. A rule is
unassessable, rather than clean, when required evidence is unavailable or
unexpectedly empty, including:

- a referenced source or forbidden surface matches zero current types;
- the source surface has zero exported roots;
- required type or recursive exposure evidence is incomplete; or
- the target universe is unavailable.

These records carry deterministic reason and provenance through the normal
governance lifecycle. Zero leak findings are a clean result only when the
source and every referenced target surface are evaluable. Strict mode blocks
on findings; audit mode reports the same normalized findings non-blockingly.

## Scope

This family is static contract-surface isolation only. It does not:

- route runtime endpoints or negotiate API versions;
- execute serializers, payloads, or runtime schemas;
- decide semantic-version increments or compatibility policy; or
- perform binary/package compatibility analysis.

It also does not change public-API snapshots, semantic roles, runtime
configuration, or the behavior of generic contract-surface exposure rules.
