## Context

ArchLinterNet's architecture model identifies "what code is in this layer" exclusively through `layers.<name>.namespace`/`namespace_suffix` glob matching (`ArchitectureLayer`, `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`), resolved by `NamespaceGlobPattern`/`ArchitectureLayerResolver`. Every dependency/layer/independence/protected/allow-only contract reads that same namespace-to-layer mapping. Story #106 wants code itself — attributes, assembly-level metadata, inheritance/interface facts, and namespace/path conventions — to be able to *imply* an architectural role and metadata (e.g. `domain: Sales`), so future YAML selectors and contextual contracts (#111/#112, and port/adapter contracts in #173) can validate boundaries between business contexts without every module being hand-listed as a namespace layer.

Existing contract families that read attributes, base types, or interfaces (`AttributeUsageContractFamily`, `InheritanceContractFamily`, `InterfaceImplementationContractFamily`, `TypePlacementContractFamily` — `src/ArchLinterNet.Core/Contracts/Families/*.cs`) are a different kind of thing and are **not** superseded or duplicated by this design: they are point-in-time constraint contracts ("types with attribute X must/must not reside in layer Y"), each contract independently re-declaring its own attribute/base-type/namespace criteria inline. None of them produce a reusable, named, cross-contract *fact* ("this type's role is `DomainLayer`, and its `domain` metadata is `Sales`") that a different contract or a future selector could reference. Semantic classification is that missing, reusable fact layer — a single discovery pass whose output (role + metadata per type) other YAML constructs consume, rather than N contracts each re-deriving overlapping criteria.

Story #57's coverage model (#96, archived at `openspec/changes/archive/2026-06-27-2026-06-27-design-architecture-coverage-model`) is the direct structural precedent for this design: a schema-and-vocabulary-only change, reviewed before any engine exists, that #97–#103 later implemented against. This design follows the same discipline for #106's child tasks (#108–#114, per #106's "Recommended sequence"), and reuses the coverage vocabulary term-for-term wherever the concepts genuinely overlap (see Decision 8) instead of inventing parallel terminology.

## Goals / Non-Goals

**Goals:**
- Define a classification vocabulary precise enough that #108–#114 can implement against it without re-deciding terminology.
- Design a `classification` YAML section and a `layers.<name>.selector` field that follow existing conventions: glob-pattern reuse, `reason` fields on broad/exclusion entries, and additive schema changes.
- Fix a deterministic, six-source precedence order and a deterministic same-tier conflict rule.
- Fix a small, deterministic metadata-extraction syntax with no reflection DSL and no regex.
- Keep classification fully backward compatible and opt-in: a policy with no `classification` section and no `layers.<name>.selector` is provably unaffected.
- Explain how classification's `uncovered`/`stale` concepts will extend the existing #96 coverage vocabulary, without implementing that extension.
- Produce a reviewed JSON-schema acceptance shape that #108–#114 can implement against without re-litigating field names.

**Non-Goals:**
- Implementing attribute/assembly-attribute/inheritance/namespace/path extraction (#109, #113).
- Implementing the role index or explainable-role diagnostics (#110).
- Implementing selector matching or contextual dependency contracts (#111, #112).
- Implementing port/adapter/anti-corruption boundary contracts (#173).
- Implementing or requiring a binary ArchLinterNet annotation package (#108) — the only supported path in this design is mapping user-defined or source-only attributes by full type name in YAML.
- Any runtime behavior, DI resolution, security proof, semantic data-flow analysis, or unrestricted plugin execution (explicit #107 acceptance criterion) — this design therefore does **not** add a C# binding or a load-time reject guard for `classification`, unlike the coverage-model precedent (see Decision 9, which explains and justifies this deviation).
- Replacing or changing the meaning of `layers.<name>.namespace`.
- Deciding final default behavior for how aggressively conventions (namespace/path) should be enabled by default in a fresh policy — deferred to #113's rollout design.

## Vocabulary

| Term | Meaning |
|---|---|
| **role** | A named architectural classification assigned to a type (e.g. `DomainLayer`, `Repository`, `Adapter`). Exactly one role is assigned per type by the classification model (see Decision 3 for tie-breaking); a type with no matching source has no role. |
| **metadata** | Named key/value facts attached alongside a role (e.g. `domain: Sales`, `boundedContext: Inventory`). Metadata is scoped to the source that assigned the current role — metadata from a lower-precedence source is not merged in once a higher-precedence source assigns a role (see Decision 4). |
| **source** | One of the six origins a role/metadata assignment can come from: `yaml_override`, `type_attribute`, `assembly_attribute`, `inheritance`, `namespace`, `path`. |
| **evidence** | The concrete fact that justifies an assignment: the attribute type name and constructor/property values used, the base type/interface name, the matched namespace/path glob, or the override entry itself. Every role assignment is explainable in terms of its evidence (feeds #110's diagnostics). |
| **confidence / precedence** | The fixed, ordered ranking of sources (`yaml_override > type_attribute > assembly_attribute > inheritance > namespace > path`) used to pick a winner when more than one source would assign a role to the same type. Higher precedence always wins regardless of how many lower-precedence sources agree (see Decision 2). |
| **conflict** | Two or more entries *at the same precedence tier* assign contradictory roles or metadata to the same type (e.g. two attributes present on one type, mapped to different roles). Resolved deterministically by first-declared-wins in YAML declaration order (see Decision 3); the underlying contradiction is still surfaced as a `conflict` fact for #110's diagnostics, not silently hidden by the tie-break. |
| **override** | An explicit `classification.overrides` entry that assigns a role/metadata directly in YAML, outranking every automatic source. |
| **exclusion** | An explicit `classification.exclusions` entry that removes a type or namespace from classification entirely — it never receives a role, regardless of what any source would otherwise assign. |
| **stale selector** | A `layers.<name>.selector` whose `role`/`metadata` criteria match zero types classified by the model — the selector equivalent of coverage's `stale` (a rule that governs nothing). |
| **uncovered semantic fact** | A type that received a role/metadata assignment from some source but is not matched by any `layers.<name>.selector` and is not the subject of an `override`/`exclusion` — the classification equivalent of coverage's `uncovered` (a fact the policy never consumes). |

These ten terms are pairwise distinct: `role`/`metadata` are the *what* was assigned; `source`/`evidence`/`precedence` are the *how* it was assigned; `conflict`/`override`/`exclusion` are the *authoring controls* over assignment; `stale selector`/`uncovered semantic fact` are the two *blind-spot* categories, mirrored directly from coverage's `stale`/`uncovered` (Decision 8).

## Decisions

1. **Classification is a new top-level `classification` section, not a contract family.** Unlike coverage (a new *contract family* alongside Dependency/Layer/etc.), classification does not itself validate anything — it produces facts (role + metadata per type) that `layers.<name>.selector` and future contextual contracts (#111/#112) consume. Modeling it as a contract would force every policy to author a pass/fail rule just to describe "how do I discover roles," which is a config concern, not a validation concern. This mirrors how `analysis.condition_sets` is a top-level configuration section, not a contract family, because it also describes an input to other contracts rather than a validation rule itself.

2. **Precedence is a fixed six-source tier list; `classification.precedence` only lets an author narrow it, not reorder or invent new tiers.** The relative order `yaml_override > type_attribute > assembly_attribute > inheritance > namespace > path` is fixed by this design (matching the issue's proposed direction exactly) because a reorderable precedence would make "which source wins" a per-policy variable that #110's diagnostics would have to re-derive per policy instead of stating once. `classification.precedence`, when present, is validated as a **non-empty subsequence** of the fixed order — schema-enforced structurally, not just documented, via an explicit `oneOf` enumerating all 63 valid ordered, duplicate-free subsequences of the six names (`schema/dependencies.arch.schema.json`'s `classification.properties.precedence`); a reordered list (e.g. `[namespace, type_attribute]`) or a list with a repeated name matches none of the 63 `const` alternatives and is rejected. Any of the six names the declared subsequence omits are disabled sources for that policy (e.g. `precedence: [yaml_override, namespace]` never consults `type_attribute`/`assembly_attribute`/`inheritance`/`path`). Omitting `classification.precedence` entirely defaults to all six tiers in the fixed order; declaring it as an empty list is rejected (schema-enforced, since the `oneOf` only contains non-empty subsequences) — an author who wants to disable every source should omit `precedence` together with every source's own entries, not declare a degenerate empty list. *Alternative considered*: a freely reorderable precedence list. Rejected because it would let `namespace` outrank `type_attribute`, silently inverting the entire "explicit annotations should win over conventions" premise #106 is built on, for no demonstrated use case. *Alternative considered*: enforcing only per-item `enum` membership and documenting (not structurally enforcing) the ordering/uniqueness rule. Rejected after review — a schema that only prose-documents an invariant it does not enforce lets a reordered or duplicated `precedence` list pass as schema-valid, silently contradicting the reviewed design; the enumerated-`oneOf` approach costs 63 lines of generated schema but closes that gap completely.

3. **Same-tier conflicts resolve by first-declared-wins in YAML order, not by an error.** If two `classification.attributes` entries both match attributes present on the same type (e.g. a type carries two different marker attributes each mapped to a different role), the model deterministically picks the first-declared entry's role/metadata — the same "declaration order is the tie-break" convention already used for override entries (Decision 6) and for `ArchitectureContractFamilyBindings`'s stable enumeration order. The contradiction is still recorded as a `conflict` fact (Vocabulary) so #110 can surface it as an explainable diagnostic ("type X matched attribute A (role Foo) and attribute B (role Bar); A won because it was declared first") rather than the type silently getting one role with no visibility into the discarded alternative. *Alternative considered*: treating same-tier conflicts as a hard validation error. Rejected here because detecting and erroring on it requires the extraction engine (#109) to actually run — this design fixes the *rule* the engine must implement, not the engine itself.

4. **Metadata does not merge across sources once a role is assigned.** Once the highest-precedence matching source assigns a role, that source's own `metadata` entries are used in full; metadata from a lower-precedence source that would have matched the same type is not merged in as a fallback for missing keys. This keeps "which source produced this metadata key" always answerable from a single source (needed for #110's evidence trail) rather than requiring a per-key precedence resolution on top of the per-type precedence resolution. *Alternative considered*: per-key metadata merging across sources (e.g. take `domain` from the type attribute but fall back to `namespace` convention for a `module` key the attribute didn't set). Rejected as unnecessary complexity for this design stage; if a real gap surfaces during #109/#110 implementation, it is an additive follow-up (a type can always be given an explicit `override` today to set an additional key).

5. **`classification.attributes`/`assembly_attributes` map by full type name in YAML — no binary annotation package dependency.** Per #106's explicit product boundary ("must not require a mandatory binary ArchLinterNet annotation dependency in production projects"), `attribute: <FullTypeName>` is a plain string matched against the full name of any attribute type the scanned code declares or references — user-defined, third-party, or a future optional ArchLinterNet annotation package (#108) are all the same shape to this design. This is exactly the "type attribute" and "assembly attribute" tiers from the issue's proposed YAML direction.

6. **Overrides require `reason` only when broad; exclusions always require `reason`; scopes are mutually exclusive, not combinable.** An `overrides` entry scoped to a single named type (`type: MyApp.Domain.Order`) is a narrow, self-documenting statement — the type name itself is the scope, so a mandatory `reason` would be boilerplate for the common "I know better than the attribute here" case. An `overrides` entry scoped to a `namespace`/`namespace_suffix` (affecting every type currently or later matching that pattern) is broad exactly the same way a coverage or dependency contract's `reason` is required for its forbidden/allow lists — an author overriding an entire namespace's classification is making a policy decision that outlives the current type set, so it must be justified in text for the next reader. This mirrors the issue's own instruction ("Require reasons for broad overrides and exclusions") precisely: narrow == type-scoped == optional reason; broad == namespace/assembly-scoped == required reason. `exclusions` entries always require `reason` regardless of scope, matching coverage's exclusion-reason rule exactly (Decision 8) — an exclusion is inherently a "stop classifying this" policy decision, not a correction, so the narrow/broad distinction that applies to overrides does not apply here. Every `classificationOverride`/`classificationExclusion` entry declares **exactly one** of `type`/`namespace`/`namespace_suffix` — never more than one — schema-enforced via `oneOf` branches that each require their own scope field and explicitly forbid the other two (`not: { anyOf: [...] }`), not `anyOf`, which would let an entry satisfy the narrow (`type`) branch while a broad `namespace` field sat alongside it unexamined and its `reason` requirement went unchecked. Combining scopes on one entry has no defined meaning in this design (would it narrow the match to the intersection, or widen it to the union?), so the schema rejects it outright rather than picking an unreviewed interpretation; an author who needs both a type-scoped and a namespace-scoped override authors two separate entries.

7. **Selector matching reuses layer glob syntax and adds one new exact-match metadata constraint — no new pattern language.** `layers.<name>.selector.role` is an exact string match against the role vocabulary produced by classification (role names are opaque strings to the schema, defined by whatever `classification.attributes`/`inheritance`/`namespace`/`path`/`overrides` entries declare — there is no fixed enum of roles in this design, matching #106's explicit deferral of a standard role catalog to #172). `selector.metadata` is an object of exact key/value AND-matched constraints (all listed keys must match; no wildcard/regex value matching). This satisfies the same "prefer constrained matchers over unrestricted regex" instruction from #106 that motivated coverage's identical decision (#96 Decision 3).

8. **`stale selector` and `uncovered semantic fact` are named to align directly with #96's `stale`/`uncovered` coverage vocabulary, and #114 is the integration point — not this change.** #107's acceptance criteria require this design to "explain how semantic role discovery will integrate with #57 coverage diagnostics" without implementing it. The natural integration point is the coverage contract's existing `scope` discriminant (`namespace | project | assembly | dependency_edge | rule_input`, #96 Decision 2): #114 is expected to add a `scope: semantic_role` variant whose `covered`/`excluded`/`uncovered`/`unknown`/`stale`/`empty-input` classification of *discovered roles* reuses the identical six-term vocabulary #96 already defined for namespaces/projects/assemblies/edges, rather than #107 inventing a seventh, parallel diagnostic vocabulary. Concretely: a role discovered by classification but matched by no `layers.<name>.selector` and not the subject of an `override`/`exclusion` is the `semantic_role`-scope equivalent of coverage's `uncovered`; a `layers.<name>.selector` matching zero classified types is that scope's `stale`. This design fixes only the vocabulary alignment and the future `scope` value name; #114 owns the actual coverage-contract binding, resolution logic, and diagnostic.

9. **This design deliberately omits the reject-not-silently-dropped guard the coverage-model precedent (#96 Decision 10) added, because #107's acceptance criteria explicitly forbid introducing runtime behavior.** #96's coverage design added a minimal C# binding plus a load-time `InvalidOperationException` guard specifically because the YAML loader's `IgnoreUnmatchedProperties()` would otherwise silently drop a schema-valid-but-unenforced coverage contract — a trust-boundary gap for a validation tool. The identical gap exists here: a schema-valid `classification` section or `layers.<name>.selector` field will, after this change, be silently ignored by the loader until #108/#109 add a real binding. #107's issue text is explicit — "No runtime behavior... is introduced" is a stated acceptance criterion, not an implementation detail this design is free to reinterpret, and adding even a minimal load-time throw is a runtime behavior change. This design therefore accepts the gap as a **documented, time-boxed risk** (see Risks) rather than closing it the way #96 did, and explicitly recommends #108/#109 close it as their first task, before this schema shape reaches any policy an engine doesn't yet enforce.

10. **Backward compatibility is structural.** A policy with no `classification` key has an empty/absent classification input; no role is ever assigned to any type; `layers.<name>.selector` is simply never present on any layer such a policy declares. This is the same backward-compatibility shape #96 Decision 9 already established for coverage — a mechanism with nothing configured produces nothing, independent of any default.

11. **`const:` resolves only compile-time `const` fields — never `static readonly` — to preserve the static-analysis-only, deterministic product boundary.** #106's product boundary explicitly excludes "runtime behavior validation... semantic data-flow analysis" and everything this design produces must be deterministic and explainable from static facts alone. A C# `const` field's value is baked into metadata at compile time and is always a literal — resolving it is a pure static lookup with exactly one possible answer. A `static readonly` field's value, by contrast, is whatever its static constructor computes, which can call arbitrary methods, read environment variables or configuration, perform I/O, or depend on process state — resolving it deterministically would require either running that code (violating the static-analysis-only boundary outright) or restricting resolution to the narrow case where the field's declared initializer is itself a literal expression (no method calls, no member access beyond literals) and explicitly treating every other `static readonly` field as unresolved. This design does not attempt that narrower carve-out in its first revision — it excludes `static readonly` entirely from `const:` and defers a possible literal-initializer subset to #109 as a reviewed follow-up, so the extraction rule shipped by this design has exactly one deterministic case (`const`) and one explicit failure case (anything else named after `const:`, which is an unresolved-constant diagnostic, not a silent guess). *Alternative considered*: accepting `static readonly` alongside `const`, deferring the "is the initializer a literal" check to #109's implementation. Rejected because it would make this reviewed design schema-compatible with a class of YAML (`const: SomeType.SomeStaticReadonlyField`) whose resolvability is not actually decidable from the shape alone, reopening exactly the kind of ambiguity this design exists to close before #109 starts building against it.

## YAML Shape

```yaml
classification:
  precedence:            # optional; subsequence of the fixed 6-tier order; default: all 6
    - yaml_override
    - type_attribute
    - assembly_attribute
    - inheritance
    - namespace
    - path

  attributes:
    - attribute: Acme.Architecture.DomainLayerAttribute   # full type name, no package required
      role: DomainLayer
      metadata:
        domain: constructor[0]        # positional constructor argument
        module: property:Module       # named property/field on the attribute instance
        tier: const:Acme.Architecture.Tiers.CORE   # compile-time const field only, not static readonly
        owner: platform-team          # literal scalar (matches none of the reserved prefixes)

  assembly_attributes:
    - attribute: Acme.Architecture.BoundedContextAttribute
      role: ApplicationLayer
      metadata:
        boundedContext: constructor[0]

  inheritance:
    - base_type: Acme.Domain.AggregateRootBase
      role: DomainLayer
      metadata:
        domain: Sales                 # literal scalar — inheritance evidence carries no ctor/property args

  namespace:
    - namespace: MyApp.Sales.Domain
      role: DomainLayer
      metadata:
        domain: Sales
    - namespace_suffix: .Repositories
      role: Repository

  path:
    - path_prefix: src/Sales/Domain
      role: DomainLayer
      metadata:
        domain: Sales

  overrides:
    - type: MyApp.Legacy.OrderProcessor    # narrow, type-scoped: reason optional
      role: ApplicationLayer
    - namespace: MyApp.Legacy               # broad, namespace-scoped: reason required
      role: Unclassified
      reason: Legacy area predates attribute adoption; reviewed quarterly.

  exclusions:
    - namespace_suffix: .Generated
      reason: Source-generated code is not hand-authored and is exempt from classification.

layers:
  domain:
    selector:                 # new: additive alternative/complement to namespace
      role: DomainLayer
      metadata:
        domain: Sales
  # existing namespace-only layers are entirely unaffected:
  infrastructure:
    namespace: MyApp.Infrastructure
```

### Metadata extraction syntax

A `metadata.<key>` value is interpreted by a fixed, ordered set of reserved prefixes, checked in this order; the first match wins, and no other extraction form is inferred from content:

1. `constructor[<index>]` — the attribute's `<index>`-th positional constructor argument (0-based). Not applicable to `inheritance`/`namespace`/`path` sources, which carry no constructor evidence.
2. `property:<Name>` — a named property or field on the attribute instance (applicable to `attributes`/`assembly_attributes` only).
3. `const:<Full.Type.NAME>` — the value of a **compile-time `const` field**, resolved statically by full type-qualified name from source/metadata only (never by loading or executing the referenced assembly). This form is deliberately restricted to `const` and does **not** extend to `static readonly` fields (see Decision 11): a `static readonly` field's initializer can be arbitrary code (a method call, an environment-variable lookup, I/O, or any other runtime-computed expression), so resolving it would require either executing that code — a static-analysis-only tool must not do this — or a source/IL literal-initializer check that still needs an explicit "cannot resolve" outcome for every non-literal case. This design draws the line at `const` alone so the extraction rule has zero ambiguous cases in its first revision; extending `const:` to a narrowly-scoped literal-initializer subset of `static readonly` is a possible additive follow-up for #109 (see Open Questions), not part of this reviewed baseline.
4. Anything else is a **literal YAML scalar**, used verbatim as the metadata value (applicable to every source, including `inheritance`/`namespace`/`path`/`overrides`, which have no extractable code evidence to reference).

A `const:<Full.Type.NAME>` reference that does not resolve to an actual compile-time `const` field (the name does not exist, or resolves to a `static readonly`, instance, or computed member instead) is a deterministic **unresolved-constant** condition: extraction MUST NOT execute, reflect over, or guess at the referenced member, and MUST report it as an explainable extraction failure (feeds #110's diagnostics) rather than silently omitting the metadata key or substituting a default value.

### Worked example: Sales/Inventory/SharedKernel modular monolith

```yaml
classification:
  attributes:
    - attribute: Acme.Architecture.DomainLayerAttribute
      role: DomainLayer
      metadata:
        domain: constructor[0]
    - attribute: Acme.Architecture.ApplicationLayerAttribute
      role: ApplicationLayer
      metadata:
        domain: constructor[0]
  namespace:
    - namespace: Acme.SharedKernel
      role: SharedKernel
  exclusions:
    - namespace_suffix: .Generated
      reason: Source-generated code is exempt from classification.

layers:
  sales-domain:
    selector:
      role: DomainLayer
      metadata:
        domain: Sales
  inventory-domain:
    selector:
      role: DomainLayer
      metadata:
        domain: Inventory
  shared-kernel:
    selector:
      role: SharedKernel
```

`[DomainLayer("Sales")] class Order { }` in any namespace is classified `role: DomainLayer, domain: Sales` and matched by the `sales-domain` selector regardless of which namespace the type physically lives in — the scenario #106 calls out explicitly ("A repository can start with namespace conventions and gradually add attributes for stronger intent").

### Worked example: Unity/client-style namespace-convention policy

```yaml
classification:
  precedence:            # attributes disabled entirely for this policy — conventions only
    - namespace
  namespace:
    - namespace: Game.Gameplay.Systems
      role: System
    - namespace_suffix: .ViewModels
      role: ViewModel
    - namespace_suffix: .Views
      role: View

layers:
  gameplay-systems:
    selector:
      role: System
  view-models:
    selector:
      role: ViewModel
```

No attributes are declared at all; `classification.precedence: [namespace]` disables every other tier, so every type's role falls through directly to the namespace-convention tier — the "optional annotations, namespace conventions provide initial classification" path #106 requires for Unity/client-style repositories.

## Risks / Trade-offs

- **[Risk] Omitting the reject-not-silently-dropped guard (Decision 9) means a schema-valid `classification`/`selector` declaration is unenforced and silently ignored until #108/#109 land.** Mitigation: this is a deliberate, documented deviation from the #96 precedent driven directly by #107's acceptance criteria; it is called out explicitly here (not left implicit) and #108/#109's tasks.md should list "add the minimal binding + reject guard, mirroring #96 Decision 10" as their first task before any other extraction work, so the gap is time-boxed to one implementation cycle rather than indefinite.
- **[Risk] A fixed six-tier precedence could prove wrong once #109/#113 hit real extraction cases (e.g. an inheritance fact an author wants to outrank a namespace convention only sometimes).** Mitigation: `classification.precedence`'s subsequence-of-the-fixed-order shape already lets a policy disable tiers it doesn't want (Decision 2); if per-policy reordering proves necessary during #109/#113, it is an additive schema change (a new enum value or a boolean opt-in), not a breaking one.
- **[Risk] Same-tier conflict resolution by declaration order (Decision 3) is easy to implement wrong** (e.g. #109 silently picking last-match instead of first-match, or not surfacing the discarded alternative as a `conflict` fact for #110). Mitigation: this design states the rule and the diagnostic expectation explicitly so #109/#110 have a documented contract to implement and test against.
- **[Risk] No metadata merge-across-sources (Decision 4) could force authors into verbose per-source metadata duplication for keys that are genuinely stable across a whole namespace.** Mitigation: `namespace`/`path` convention entries already let an author declare that shared metadata once at the convention tier; if a real gap surfaces, per-key merging is an additive follow-up, not a breaking change to this shape.
- **[Risk] `layers.<name>.selector` and `layers.<name>.namespace` being simultaneously legal on one layer (Decision 7's `anyOf`) could produce a layer whose namespace match and selector match disagree about which types belong to it once #111 implements selector-based layer membership.** Mitigation: this design fixes the schema shape only; #111's resolution-logic design is the right place to define whether a layer's members are the union or intersection of its `namespace` and `selector` matches, and is explicitly out of scope here (Non-Goals).
- **[Risk] Restricting `const:` to compile-time `const` only (Decision 11) is less convenient than accepting `static readonly` too, since many real C# codebases use `static readonly` for constant-like values (e.g. `static readonly string[] Tags = {...}` is common, plain `readonly string Tag = "Sales"` less so but still seen).** Mitigation: an author blocked by this restriction always has the literal-scalar fallback (form 4) or an explicit `override`/attribute-constructor-argument as an immediate workaround; the restriction is deliberately conservative for this reviewed baseline (see Decision 11) and #109 can extend it once a real literal-initializer detection exists, without a breaking schema change.
- **[Risk] The 63-entry `oneOf` enumeration for `classification.precedence` (Decision 2) is verbose and must be regenerated by hand if the six-source list ever changes.** Mitigation: the fixed source list only grows via a reviewed design change (this design, or a future revision of it), not silently; a follow-up change that adds a seventh source already has to touch this same section to update the vocabulary, precedence order, and worked examples, so regenerating the `oneOf` at the same time is not an additional maintenance burden beyond what that change already requires.

## Open Questions

- Should `classification.attributes`/`assembly_attributes` support matching an attribute by prefix (`attribute_prefix`) in addition to full type name, the way `AttributeUsageContractFamily.AttributePrefixes` already does? Deferred to #109 — this design fixes full-type-name matching as the reviewed baseline; prefix matching is an additive follow-up if #109 finds a demonstrated need.
- Should `conflict` (Decision 3) escalate to a validation error under `strict` policies instead of always resolving silently by declaration order? Deferred to #110, which owns diagnostic severity design for classification findings.
- Should project/assembly-level `assembly_attributes` support the same `unknown`-vs-`uncovered` distinction #96 defined for project/assembly coverage, given both depend on #56 discovery maturity? Deferred to #114's coverage-integration design.
- Should `const:` extend to a narrowly-scoped literal-initializer subset of `static readonly` fields (Decision 11) once #109 has a concrete IL/source-based way to detect "this initializer is a literal, not computed"? Deferred to #109 — this design fixes `const`-only as the reviewed baseline specifically because that subset check does not exist yet; extending it is an additive follow-up, not a breaking change to this shape.
