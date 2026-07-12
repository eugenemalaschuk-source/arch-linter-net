## Context

The `semantic-classification-model` spec and `dependencies.arch.schema.json` already fully define six classification sources (`type_attribute`, `assembly_attribute`, `inheritance`, `namespace`, `path`, plus `yaml_override`/`overrides`/`exclusions`), a fixed precedence order, and metadata canonicalization/extraction-failure rules. Only `type_attribute`/`assembly_attribute` are executed today, via `ArchitectureAttributeRoleExtractor` and `ArchitectureRoleIndex`. `ArchitectureAttributeRoleExtractor.Combine()` currently hardcodes a 2-source precedence (type wins, else assembly). `ArchitectureClassificationConfiguration`'s deserializer intentionally ignores `inheritance`/`namespace`/`path`/`overrides`/`exclusions` as unbound sections.

The repo's own `docs/internal/core-architecture-blueprint.md` documents a dependency-direction rule: `Scanning` (home of the extractor) must not depend on `Resolution` (home of the reusable namespace-glob matcher, `ArchitectureLayerResolver`/`NamespaceGlobPattern`). This rule is not currently enforced by the repo's self-hosted arch-linter policy (`architecture/dependencies.arch.yml` only forbids `core_scanning -> [core_execution, core_validation, cli, testing]`, not `core_resolution`), but is followed here as the deliberate target architecture regardless.

Issue #171 (source file/declared-type fact index), required for `classification.path`, is still open. Path conventions are out of scope for this change.

## Goals / Non-Goals

**Goals:**
- Execute `classification.namespace` and `classification.inheritance` mappings, producing role/metadata assignments that participate in the existing fixed precedence order and same-tier conflict resolution.
- Preserve the `Scanning` → `Resolution` dependency-direction rule while reusing existing namespace-glob logic (no duplicated glob implementation).
- Give `classification.path` visible, deterministic diagnostic feedback instead of pure silence, without implementing path matching itself.
- Leave all existing behavior (type_attribute/assembly_attribute extraction, layer selectors, policies with no `classification` section) unchanged.

**Non-Goals:**
- Implementing `classification.path` matching (blocked on #171).
- Implementing `classification.overrides`, `classification.exclusions`, or the `yaml_override` precedence source (separate follow-up issues).
- Adding a new self-hosted arch-linter rule to mechanically enforce the `Scanning`→`Resolution` boundary (noted as a gap, not closed here).
- Any new metadata-extraction syntax beyond the four already-reviewed forms.

## Decisions

**1. Generalize `Combine()` into an N-tier precedence walk, not a pairwise merge.**
Replace the current two-candidate `Combine(typeCandidate, assemblyCandidate)` with a walk over a fixed tier order `[TypeAttribute, AssemblyAttribute, Inheritance, Namespace]`, filtered to whichever sources `classification.precedence` enables (default: all four, `IsSourceEnabled` already supports this per-source check). The first tier with a non-null `Role` wins; conflicts and metadata failures from every enabled tier are unconditionally unioned into the result regardless of which tier's role wins — this already happens today for the 2-tier case and generalizes directly. Cross-tier resolution (which source wins) is precedence, not a `conflict` fact; only same-tier disagreement (two entries within one source's list) is recorded as a `conflict`, per the existing spec requirement, applied uniformly to the two new sources.
*Alternative considered*: keep pairwise `Combine` and add two more nested calls. Rejected — doesn't scale cleanly if precedence disables a middle tier, and duplicates the "first non-null wins" logic three times instead of once.

**2. Namespace matching via delegate injection, not a direct `Resolution` reference.**
`ArchitectureRoleIndex` (in `Execution`, which may depend on both `Resolution` and `Scanning`) builds a delegate — `Func<ArchitectureNamespaceClassificationMapping, string, (bool Matched, string? MatchedPrefix)>` — that wraps each namespace mapping entry in a throwaway `ArchitectureLayer` and calls the existing `ArchitectureLayerResolver.MatchNamespace`, then projects the `Resolution`-typed result down into a plain tuple before returning. This delegate is passed into `ArchitectureAttributeRoleExtractor`'s constructor, mirroring the existing `Func<string, Type?> resolveType` pattern already used for `const:` resolution. The tuple return type (not the `Resolution`-typed `ArchitectureNamespaceMatch`) ensures `Scanning` never needs `using ArchLinterNet.Core.Resolution;`, even in a method signature.
*Alternative considered*: reference `ArchitectureLayerResolver` directly from `Scanning`. Rejected — violates the documented dependency-direction rule.
*Alternative considered*: reimplement glob matching inside `Scanning`. Rejected — duplicates logic the schema explicitly says is shared ("Same namespace glob syntax already accepted by layers.<name>.namespace").

**3. Inheritance matching needs no delegate — pure reflection inside `Scanning`.**
**Amended post-review (PR #307):** the original plan below — resolving `base_type` to a `Type` via `_typesByFullName`/`ResolveTypeByFullName`, then calling `IsAssignableFrom` — shipped initially but was found to be a functional defect: `_typesByFullName` is built only from `typeUniverse` (the scanned target assemblies), so a `base_type` declared in a framework/package assembly that is not itself a scanned target assembly (e.g. `Microsoft.AspNetCore.Mvc.ControllerBase`, `Microsoft.EntityFrameworkCore.DbContext`, `UnityEngine.MonoBehaviour` — exactly the documented use cases) never resolves, and the mapping silently matches nothing. The shipped fix instead walks each candidate type's own reflected base-class chain (`type.BaseType` repeatedly) and transitive interface set (`type.GetInterfaces()`, which already includes inherited interfaces) and compares each ancestor's/interface's full name against `base_type` by ordinal string equality — this works regardless of which assembly declares the base type or interface, as long as it is loadable. Reflection exceptions are guarded the same defensive way as `SafeGetCustomAttributesData`.

~~Original plan (superseded, kept for history): `base_type` strings resolve to `Type` via the extractor's existing `_typesByFullName`/`ResolveTypeByFullName` lookup (already built from `typeUniverse` for `const:` resolution — full reuse). Matching uses `baseType.IsAssignableFrom(type) && baseType != type`, a single expression covering transitive class derivation and transitive interface implementation (`Type.IsAssignableFrom` already implements exactly this semantics; `!= type` excludes self-match).~~
*Alternative considered (at the time, now moot)*: manually walk `type.BaseType` plus separately check `GetInterfaces()`. Originally rejected as more complex than `IsAssignableFrom`; this is in fact the shipped approach, since `IsAssignableFrom` requires a resolved `Type` for `base_type` that the target-assembly-only lookup cannot always provide.

**Second post-review amendment (PR #307):** the base-chain/interface-name walk above initially compared `Type.FullName` directly, which does not match a closed generic instantiation (e.g. `IRepository<Order>`, whose `FullName` embeds the assembly-qualified closed type argument) against an open generic `base_type` mapping (e.g. `MyApp.IRepository\`1`). Fixed by normalizing each candidate ancestor/interface to its own `GetGenericTypeDefinition()` before comparing full names, mirroring `ArchitectureRoleIndex.TryGetRole`'s existing open-generic-definition fallback for the classified type itself.

**Third post-review amendment (PR #307):** the public two-argument constructor (preserved for binary compatibility, see the Contracts/wiring notes) has no namespace-glob matcher, so a direct Core API consumer who populates `classification.namespace` through it would previously see every namespace mapping silently never match, with no signal. The constructor now throws `InvalidOperationException` at construction time when `classification.namespace` is non-empty and the `namespace` source is enabled, directing the consumer to `ArchitectureAnalysisSession`/`ArchitectureRoleIndex` instead. This is non-breaking for pre-existing binary consumers, since `classification.namespace` did not exist before this change — no prior consumer could have populated it.

**4. Metadata extraction for inheritance/namespace: new no-attribute-instance entry point, not a dummy `CustomAttributeData`.**
The schema restricts inheritance/namespace metadata to literal scalar or `const:Full.Type.NAME` only (no `constructor[]`/`property:`, since there's no attribute instance). `CustomAttributeData` has no public constructor, so a dummy-instance approach isn't viable. Add `ArchitectureAttributeMetadataExtraction.ExtractWithoutAttributeInstance(object rawYamlValue, Func<string, Type?> resolveType)`, factoring the existing `Extract`'s `const:`/literal-fallback branches into a shared private helper both entry points call. If a `constructor[`/`property:`-prefixed string is ever seen here (schema should prevent this at author time, but defense in depth matters for hand-constructed configs, e.g. in tests), it resolves as an extraction failure with a clear reason, not a crash.

**5. `classification.path`: informational, non-fatal diagnostic at policy-load time.**
Detected via the same raw-YAML pre-parse pattern already used elsewhere in `ArchitecturePolicyDocumentLoader.cs` (checking for a non-empty `classification.path` sequence in the raw node tree, not the still-unbound C# model). Fires once per policy load, independent of scanned types, so it appears even for a policy with zero scanned types. Reuses the existing `ArchitectureDiagnosticKind.Configuration` kind rather than adding a new kind. Surfaced through the same "classification findings" channel as conflicts/metadata failures (human output, JSON, CI artifact). Non-blocking: does not fail `validate`'s exit code, consistent with how conflicts/metadata failures are already informational rather than violations.
*Alternative considered*: hard exception at load time. Rejected — the live spec.md already guarantees reserved constructs "SHALL proceed without exception," and narrowing that guarantee for `path` alone is a bigger behavior change than the issue asks for.
*Alternative considered*: leave `path` fully silent (no new diagnostic). Rejected in favor of visibility — the issue explicitly asks that "missing or ambiguous source path facts" produce explicit diagnostics rather than silent skipping.

**6. Unresolved `base_type`: silent no-match, no new diagnostic.**
Consistent with how `const:` resolution already treats an unresolved reference — the entry matches nothing for this run, with no per-type or per-run diagnostic. Keeps this change's diagnostic surface additive and narrow (only the `path` deferral gets new diagnostic surface).

## Risks / Trade-offs

- **[Risk]** A future reviewer might expect `Scanning`→`Resolution` to be mechanically blocked by the self-hosted policy; it currently isn't. → Mitigation: documented explicitly in this design and the proposal's impact section; the delegate-injection design is followed regardless, so no actual violation occurs even without the guardrail.
- **[Risk]** A mistyped `base_type` silently classifies nothing, with no signal to the author. → Mitigation: accepted trade-off for consistency with existing `const:` behavior; can be revisited in a follow-up if it proves to be a common authoring mistake in practice.
- **[Risk]** Widening `Combine()` to 4 tiers touches a well-tested, central extraction path. → Mitigation: existing `ArchitectureRoleIndexTests`/`ArchitectureAnalysisSessionClassificationTests` already cover type/assembly precedence; extend rather than replace those tests, and add explicit precedence/conflict fixtures for the new 4-tier ordering before removing the old 2-tier logic.

## Migration Plan

No data migration. Purely additive at the schema/runtime boundary — existing policies without `classification.namespace`/`classification.inheritance` are unaffected (empty-list guard in `ArchitectureRoleIndex.BuildData()` extends but preserves current early-return behavior). Roll out as a normal PR; no feature flag needed since behavior only activates when a policy author declares the new sections.

## Open Questions

None outstanding — the three open questions raised during design (path diagnostic severity, unresolved `base_type` handling, and the Scanning/Resolution enforcement gap) were resolved with the user before this change was proposed; see Decisions 5, 6, and the Context section above.
