## Context

`ArchitectureExternalDependencyContract` (`strict_external`/`audit_external`) checks a source layer against a `forbidden` list of named `external_dependencies` groups, reusing `ArchitectureExternalDependencyViolationFinder` (type-level reference matching) and `ArchitectureExternalDependencyIlScanner` (method-body IL matching). `ArchitectureAllowOnlyContract` (`strict_allow_only`/`audit_allow_only`) checks a source layer against an `allowed` list of first-party layers, flagging any project-type reference outside that allowed set (plus `allowed_types` exact-name exceptions). Issue #60 wants the allow-only shape applied to the external-dependency axis: a source layer that may only reference a finite, explicitly allowed set of external groups.

The most recent precedent for adding a directional/allow-only sibling family to an existing contract family without touching the executor is `assembly_dependency`/`assembly_allow_only` (#58), which reused the catalog + handler-registry seam introduced for `assembly_independence` and added two new models/handlers/groups with zero `ArchitectureContractExecutor` changes.

## Goals / Non-Goals

**Goals:**
- Add `strict_external_allow_only`/`audit_external_allow_only`: a source layer may only reference `external_dependencies` groups present in its `allowed` list.
- Reuse the existing group-matching primitive (`ArchitectureExternalDependencyViolationFinder` + `ArchitectureExternalDependencyResolver.MatchesGroup`) rather than writing new matching logic.
- Make BCL/system reference behavior explicit and tested: BCL types are never implicitly treated as "external" — they are only checked if a policy author declares an `external_dependencies` group matching BCL namespaces.
- Fail closed on typos: an `allowed` entry naming a group that doesn't exist in `external_dependencies` has no effect (it can't match anything to exclude), so it never accidentally widens what's permitted.
- Zero changes to `ArchitectureContractExecutor`; wire the new family entirely through the existing catalog/handler-registry seam.

**Non-Goals:**
- Method-body IL scanning (`ArchitectureExternalDependencyIlScanner`) for this family. `strict_external`/`audit_external` itself got IL scanning as a separate follow-up change after its initial release; `external_allow_only` starts at the same type-level-only baseline and IL scanning is deferred as a symmetric follow-up, not silently dropped.
- PackageReference validation, runtime DI resolution, and semantic data-flow analysis (already out of scope per the issue).
- Any change to `strict_external`/`audit_external` or `strict_allow_only`/`audit_allow_only` semantics — purely additive.

## Decisions

**Violation universe = declared external groups, not "everything non-project."**
For each source type in the contract's `source` layer, iterate every group name in `Document.ExternalDependencies` that is *not* in `contract.Allowed`, and run the existing `ArchitectureExternalDependencyViolationFinder.FindViolations` for that group (same call `CheckExternalContract` already makes per forbidden-group entry). This means only *declared* external groups are ever in scope for a violation — a reference that doesn't match any declared group (most BCL/system types, in a typical policy that never declares a "BCL" group) is never flagged. This mirrors the already-shipped precedent in `assembly_allow_only`: "References to assemblies outside `analysis.target_assemblies` ... are not violations," i.e. only the declared universe is checked, not the entire universe of possible references.
- Alternative considered: treat every referenced type that isn't a first-party project type as a candidate "external" reference and require it to match an allowed group or be flagged. Rejected — this would silently start flagging *every* BCL/framework reference for any layer with an `external_allow_only` contract unless the author enumerates the entire BCL surface as an allowed group, which is a hostile authoring experience and not what the issue's "explicit BCL/system behavior" criterion is asking for. It also has no precedent in this codebase's existing allow-only or external contracts.

**Group iteration order is deterministic.**
`Document.ExternalDependencies.Keys.Where(...)` is materialized and sorted (`OrderBy(name, StringComparer.Ordinal)`) before iterating, so diagnostic/violation order does not depend on YAML key insertion order or dictionary enumeration order.

**`allowed_types` filters the finder's output, not its input.**
`ArchitectureExternalDependencyViolationFinder.FindViolations` returns one `ArchitectureViolation` per source type per checked group, with `ForbiddenReferences` sorted and deduplicated. After calling it once per disallowed group, filter each violation's `ForbiddenReferences` to drop any entry present in `contract.AllowedTypes` (exact full-type-name match, same semantics as the existing namespace-level `allowed_types`), and drop the violation entirely if that empties `ForbiddenReferences`. Uses the record's `with` expression (`violation with { ForbiddenReferences = filtered }`) since `ArchitectureViolation` is an immutable `sealed record`.
- Alternative considered: push `AllowedTypes` into a modified `ArchitectureExternalDependencyViolationFinder` signature. Rejected — the finder is shared with `strict_external`/`audit_external`, which has no `allowed_types` concept; forking behavior into the shared finder for one caller adds a parameter that's meaningless for the other caller. Post-filtering keeps the finder untouched and the allow-only-specific concept local to the allow-only check method.

**No group-existence validation for `allowed` at load time.**
`ArchitecturePolicyDocumentLoader` today does *not* validate that `strict_external`/`audit_external`'s `forbidden` group names exist in `external_dependencies` (`CheckExternalContract` just `continue`s past an unresolvable name). For `external_allow_only`, an `allowed` entry that doesn't match a real group name simply never appears in `Document.ExternalDependencies.Keys`, so it can't be excluded from the disallowed-groups set — meaning a typo makes the contract *more* restrictive (every real group stays disallowed), never less. This fail-closed property means skipping load-time validation is safe and keeps the new family consistent with the sibling `strict_external` family's existing (lack of) validation, rather than introducing an inconsistent stricter rule for only the new family.
- Alternative considered: validate like `assembly_dependency`/`assembly_allow_only` does for assembly names. Rejected for this change — those contracts validate against a required `analysis.target_assemblies` allowlist that already exists independently; there is no equivalent required declaration to validate external group names against beyond `external_dependencies` itself, and the existing sibling family (`strict_external`) already tolerates unresolvable names silently. Adding asymmetric strictness between siblings is a bigger footprint than the issue asks for; can be revisited later as a shared follow-up across both families.

**New model fields mirror `ArchitectureAllowOnlyContract` exactly, swapping layer axis for external-group axis.**
`ArchitectureExternalAllowOnlyContract`: `Name`, `Id`, `Source` (a first-party layer name — resolved via `ArchitectureLayerResolver.ResolveLayer`, same as every other contract), `Allowed` (list of `external_dependencies` group names), `AllowedTypes` (exact full type names), `IgnoredViolations`, `Reason`. `Source` stays a *layer* (not an external group) because the contract is asking "what may this first-party layer reference," matching every other `source`-rooted contract in the codebase.

## Risks / Trade-offs

- **[Risk] Silent zero-effect `allowed` entries]** → A misspelled group name in `allowed` doesn't error; it silently fails to relax the contract. Mitigated by the fail-closed direction (over-restrictive, not under-restrictive) and by documenting this explicitly in `docs/contracts/external-allow-only.md`; a future load-time validation pass (if added) can apply symmetrically to both `strict_external`'s `forbidden` and this family's `allowed` in one follow-up.
- **[Risk] No method-body IL coverage at launch]** → A forbidden external type referenced only inside a method body (not in a field/property/base-type/signature) will not be caught by this family yet, unlike `strict_external`/`audit_external` (which has IL scanning). Mitigated by explicit documentation of the scope decision and by matching exactly how `strict_external` itself shipped before its own IL-scanning follow-up — this is a known, bounded, already-precedented gap, not a regression.

## Migration Plan

Purely additive: new YAML contract-group keys, new schema defs, new docs page. No existing contract family, schema field, or CLI behavior changes. No migration or rollback beyond normal code review/revert.
