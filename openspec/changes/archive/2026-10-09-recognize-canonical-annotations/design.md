## Context

See `proposal.md` for motivation and `specs/` for the observable contract. The current Core extractor resolves exact-FQN YAML attribute mappings into `ArchitectureTypeClassificationResult`, and `ArchitectureRoleIndex` caches one result per type. Classification conflicts and metadata failures already flow through validation and cache outcomes, but catalog identity/version failures have no accurate diagnostic model. The v0.10 manifest is the source of truth; v0.9.1 is the latest release, and v0.10.0 remains in its capability-development phase.

## Goals / Non-Goals

**Goals:**

- Project the manifest's canonical role identities into the existing classifier without runtime discovery or a second index.
- Preserve every contributing exact attribute FQN on the existing role fact and make identity failures available in the existing report lifecycle.
- Keep unsupported or incompatible canonical evidence from resolving, and reject YAML mappings that try to claim the reserved namespace.
- Make the capability, schema, help, and report projections testable against the versioned manifest.

**Non-Goals:**

- Implementing the source-only package or its role attribute definitions (#567, #569–#571).
- Implementing the six-key context metadata extraction/composition (#572), adoption documentation (#573), or packed consumer exit gate (#574).
- Adding another semantic role fact, source tier, scanner, index, automatic policy behavior, or reviewed API membership behavior.

## Decisions

### Use one manifest-backed catalog projection

Add an internal Core catalog containing the 49 manifest role identities, roles, and allowed scopes plus the reserved namespace and marker contract. Match only exact metadata FQNs. A focused parity test compares the projection with `architecture/semantic-annotations.v1.json`; Core will not load the manifest from an adopter's filesystem and will not reflect over an annotation assembly.

Alternative considered: read the manifest at runtime. Rejected because analysis must not depend on a repository file being present beside the tool, and that would turn the catalog input into an undeclared runtime dependency.

### Resolve canonical and user-mapped evidence through the current extractor

Convert built-in and YAML-mapped matches into candidates at the existing type-attribute and assembly-attribute tiers. Existing custom-only YAML conflicts retain first-declared behavior. If a canonical candidate participates at a tier, equivalent role/metadata evidence coalesces and conflicting evidence fails closed with a deterministic conflict independent of reflection or YAML order. Higher-tier precedence remains unchanged; a conflicting canonical tier blocks fallback to a lower role tier.

Keep the existing `Evidence` field and add an init-only `EvidenceSources` list containing distinct exact FQNs in ordinal order. The `ArchitectureClassificationRoleFact` remains the sole semantic fact type; JSON, explain, and Testing expose the same list. Custom-only results retain their existing `Evidence` value.

Alternative considered: pick one evidence string and discard the rest. Rejected because equivalent canonical/custom evidence must retain both provenances while still yielding one role fact.

### Validate package identity only when canonical role evidence is encountered

For each assembly containing a known canonical role attribute, inspect `CustomAttributeData` for exactly one marker with the manifest FQN, integer generation, and string SemVer. Accept only supported generation/package pairs. Parse SemVer in Core without a new package dependency, applying the manifest's `[0.10.0,0.11.0)` range. Cache the identity result by assembly. Missing, duplicate, malformed, unsupported-generation, and unsupported-version cases produce stable diagnostic codes and suppress canonical evidence. A marker alone contributes no role. Unknown attribute FQNs under the reserved namespace are diagnosed and ignored; exact reserved names for the marker and manifest-owned context attributes are recognized identities, while context extraction remains #572-owned.

Alternative considered: reuse `ArchitectureClassificationConflict` or `ArchitectureClassificationMetadataFailure`. Rejected because those types encode role-pair winner/discarded semantics or failed metadata-key extraction and cannot accurately describe identity/version errors.

### Carry compatibility diagnostics as an additive report collection

Add `ArchitectureCanonicalAnnotationDiagnostic` with stable code, subject, optional exact attribute FQN, and detail. Thread a sorted, deduplicated collection through the role index, analysis session, `ValidationOutcome`, cache-v1 payload, CLI projections, SARIF, and `ArchitectureValidationResult`. Add collections as init-only properties outside positional constructors/deconstructors so existing call shapes remain intact. The collection is informational and does not change pass/fail status by itself; canonical evidence is nevertheless suppressed on compatibility failure. Preserve legacy cache reads by defaulting missing diagnostic collections to empty.

Alternative considered: encode these conditions as metadata failures or generic architecture violations. Rejected because it would mislabel identity failures and mix capability diagnostics with authored contract enforcement.

### Reserve canonical FQNs from YAML mapping

Reject mappings in both `classification.attributes` and `classification.assembly_attributes` when the FQN uses the exact reserved namespace prefix, including a mapping that repeats a known canonical role's role and metadata. Report a configuration diagnostic with the YAML path. User-owned attributes outside the reserved namespace keep the existing exact-FQN mapping behavior.

This follows the #566 contract, which explicitly forbids policy-dependent reinterpretation of reserved identities, including redundant entries.

### Project supported identities as capability metadata

Add a `semanticAnnotations` section to `archlinternet.capabilities.json` with the reserved namespace, exact role FQNs and scopes, catalog generation, supported package ranges, minimum tool version, and failure modes. Update the live packaged policy-schema description and AI authoring help with the reserved-identity/version boundary and no-policy-authority guarantee. A parity test compares the capability projection to the manifest.

Alternative considered: add policy fields to make canonical support configurable. Rejected because annotations are fixed evidence and policy configuration remains orthogonal.

### Keep diagnostic data additive and deterministic

Human and JSON reports expose the diagnostic collection; SARIF emits stable results with code, subject, FQN, and detail; the Testing adapter carries the same typed records. Output ordering is ordinal by subject, code, and FQN. No diagnostic changes the assessment's native pass state on its own.

## Risks / Trade-offs

- [Risk] Public validation and Testing models gain additive properties and a diagnostic record. → Mitigation: leave constructors and deconstructors unchanged, update reviewed API snapshots explicitly, and verify both Core and Testing surfaces.
- [Risk] Reflection metadata can be malformed or partially loadable. → Mitigation: use the extractor's guarded attribute reads and emit deterministic diagnostics without aborting unrelated types.
- [Risk] Sibling source-package issues remain separate. → Mitigation: assert the Core projection against the shared manifest and keep package/source-definition parity as an explicit integration check before the v0.10 release gate.
- [Risk] SemVer parsing can diverge from the advertised range. → Mitigation: keep the parser bounded to standard SemVer 2.0 syntax, test boundary/prerelease/build cases, and parity-check the exact range against the manifest.
