## Context

`contract_surface_exposure` already resolves a bounded source surface,
recursively materializes visible-signature and metadata exposure evidence, and
projects its findings through applicability, baseline, and normalized output.
Issue #514 needs a static API-version boundary on top of that capability. It
must distinguish identically named types by their reflected assembly-qualified
identity and must not infer endpoint routing, payload negotiation, or runtime
compatibility.

## Goals / Non-Goals

**Goals:**

- Declare named version or surface groups using the established bounded
  structural/semantic type selector vocabulary.
- Express that one named source surface must not expose types selected by one
  or more other named surfaces.
- Reuse the existing exposure traversal, diagnostic payload, strict/audit
  lifecycle, canonical identity, baseline, and normalized output projection.
- Fail closed when a referenced source or forbidden surface is stale, empty,
  incomplete, or otherwise not assessable.

**Non-Goals:**

- Runtime endpoint routing, version negotiation, serializer execution, or
  payload-schema compatibility.
- Binary compatibility analysis, semantic-version policy, or a new generic
  expression/tag language.
- Replacing or changing generic `contract_surface_exposure` controls.

## Decisions

### Add a dedicated additive contract family

The policy groups will be
`strict_versioned_contract_surface_isolation` and
`audit_versioned_contract_surface_isolation`. Each rule declares an ID, name,
one local list of named `surfaces`, a `source_surface` ID, and non-empty
`forbidden_surfaces` IDs. Surface IDs are local to the rule, which keeps
policy ownership, provenance, and applicability scoped to one effective
control rather than introducing a document-wide registry with cross-rule
lifetime and collision rules.

Each surface has an ID and a `types_matching` selector using the existing
`ArchitecturePublicApiSurfaceSelector` fields. This keeps namespace, base
type, interface, attribute, layer, name, and semantic-role behavior exactly
aligned with existing policies. All selector fields remain conjunctive.

### Separate source-root selection from forbidden-target selection

The source group selects only types that are also in the existing exported
visible surface of a target assembly; their recursive visible contract is the
root of analysis. Forbidden groups match against the complete existing
exposure target universe, including referenced external types, so a source can
be prevented from leaking a first-party internal implementation type even when
that type is not itself exported. This retains #513's distinction between
visible source roots and referenced target types.

### Factor the generic exposure checker around typed evaluation inputs

The existing checker will expose an internal shared evaluation seam for
pre-resolved roots, a deterministic forbidden target map, rule metadata, and
source-surface label. The generic exposure family continues to feed that seam
unchanged. The versioned family owns only named-group resolution, configuration
validation, and applicability evidence. Both families produce the existing
path-rich exposure payload and diagnostic kind; the versioned family registers
its own strict/audit and baseline group identities.

This is a concrete reuse requirement: without it the new family would need to
reimplement recursive traversal, canonical exposure identity, ignores, and
normalized output.

### Treat referenced group evidence as required applicability input

Source and forbidden groups referenced by a rule are evaluated in stable ID
order. Unknown, duplicate, blank, unbounded, self-referential, or malformed
declarations are invalid policy configuration. A valid referenced group that
selects no types, a source group with no exported roots, incomplete type or
exposure evidence, or a missing required target universe creates the rule's
single required `unassessable` applicability record using the established
reason/provenance model. A zero-finding rule is clean only when that record is
evaluable.

## Risks / Trade-offs

- [A local group is repeated across rules] → local ownership avoids a global
  policy registry; existing imports/source sets remain the reuse mechanism for
  broader policy composition.
- [A source selector finds only non-exported types] → treat it as unexpected
  empty source input rather than silently scanning an implementation surface.
- [Refactoring the generic checker changes #513 behavior] → retain existing
  focused generic exposure fixtures and add parity tests for shared payload,
  ignore, baseline, and strict/audit paths.
- [Group IDs are confused with CLR type identity] → keep group IDs only as
  policy provenance/display facts and retain the existing assembly-qualified
  reflected target identity in findings.

## Migration Plan

The feature is additive: policies without the new groups are unchanged.
Adopters define named version/surface selectors inside one isolation rule,
select its source group, then list forbidden group IDs. Removing the rule
removes only this additional static check; no public API snapshot, semantic
role, runtime route, or existing exposure policy is migrated.

## Open Questions

None. The issue acceptance criteria and existing #512/#513 behavior define
the required static boundary.
