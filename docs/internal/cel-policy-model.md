# CEL Policy Model Blueprint

This is internal project documentation for maintaining the `arch-linter-net`
repository. It is intentionally excluded from the public MkDocs/GitHub Pages
product site.

This document records the approved product-side CEL policy model for issue
[#162](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/162).
It depends on the language/API boundary defined by issue
[#324](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/324)
and is the design source of truth for the Core integration tracked by
[#163](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/163).

## Status

- This document is a design blueprint, not a shipped feature description.
- No live ArchLinterNet runtime may accept `when` fields until #163 implements
  compilation and evaluation.
- Literal strings remain literal; no existing field gains implicit expression
  parsing.

______________________________________________________________________

## Goals

- Make expression use explicit and schema-backed.
- Keep the first-wave YAML surface narrow and explainable.
- Expose only deterministic architecture facts that Core can provide through
  public `ArchLinterNet.CEL` APIs.
- Fail closed on compile-time and evaluation-time errors.
- Preserve existing policies unchanged.

## Non-goals

- A general expression field on every contract family.
- Implicit parsing of ordinary YAML strings.
- Regex, user-defined functions, host access, file/network/process access,
  runtime plugins, or reflection-backed object exposure.
- Expressions inside imports, baseline files, analysis settings, contract IDs,
  names, reasons, coverage exclusions, or classification extraction mappings.

______________________________________________________________________

## YAML surface

### First-wave allowed locations

Only the following nodes may eventually accept `when`:

| Location | Meaning |
|---|---|
| `layers.<name>.selector.when` | Refines a selector-backed layer match over the candidate type bound as `subject`. |
| `contracts.strict_context_dependencies[*].source.when` and audit equivalent | Refines the contextual source selector over `source`. |
| `contracts.strict_context_dependencies[*].forbidden[*].when` and audit equivalent | Refines a contextual target selector over `source`, `target`, and `dependency`. |
| `contracts.strict_context_dependencies[*].exclude[*].when` and audit equivalent | Refines contextual target pre-filtering over `source`, `target`, and `dependency`. |
| `contracts.strict_context_allow_only[*].source.when` and audit equivalent | Refines the contextual source selector over `source`. |
| `contracts.strict_context_allow_only[*].allowed[*].when` and audit equivalent | Refines a contextual target allow-list selector over `source`, `target`, and `dependency`. |
| `contracts.strict_context_allow_only[*].exclude[*].when` and audit equivalent | Refines contextual target exclusions over `source`, `target`, and `dependency`. |

### Explicitly forbidden locations

Expressions remain forbidden in:

- `imports` and every import path;
- every `analysis.*` field;
- every `external_dependencies.*` and `packages.*` field;
- `layers.<name>.namespace`, `namespace_suffix`, and `external`;
- `classification.*` extraction mappings and precedence;
- all non-contextual contract families;
- coverage contracts and coverage exclusions;
- `ignored_violations`;
- baseline files and baseline entries;
- contract `id`, `name`, `reason`, and similar explanatory text fields.

### Shape rules

- `when` is always optional.
- `when` always compiles as `Bool`.
- Literal selector fields still apply. `when` refines them; it does not replace
  them.
- Existing literal policies remain valid and unchanged.

______________________________________________________________________

## Context schemas

The policy model uses fixed CEL root variables. These are public-API-owned
Core schemas, not reflection-backed CLR objects.

Both schemas below are **closed catalogs**: the listed members are the complete
first-wave surface. Adding, removing, or retyping a member requires a reviewed
spec change to `cel-policy-model`, because the member set defines the policy
attack surface, the structural schema identity, and the compilation cache
identity that #163 builds on.

### Selector-backed layer predicate

Root variable:

- `subject`

### Contextual source predicate

Root variable:

- `source`

### Contextual target or exclusion predicate

Root variables:

- `source`
- `target`
- `dependency`

### Shared subject object

Every `subject`, `source`, and `target` object shares the same bounded shape.
This member list is exhaustive for the first wave.

| Member | Type | Notes |
|---|---|---|
| `fullName` | `String` | Fully-qualified type name. |
| `simpleName` | `String` | Short type name without namespace. |
| `namespace` | `String` | Declared namespace or empty string. |
| `assemblyName` | `String` | Resolved assembly simple name. |
| `projectName` | `String` | Discovered project name when available, else empty string. |
| `role` | `String` | Resolved semantic role; empty string only when the surrounding consumer permits unclassified candidates. |
| `metadataText` | `Map[String]` | String-valued metadata by key. |
| `metadataBool` | `Map[Bool]` | Boolean-valued metadata by key. |
| `kind` | `String` | Stable kind label such as `class`, `interface`, `enum`, `struct`, or `delegate`. |
| `isAbstract` | `Bool` | Structural type fact. |
| `isSealed` | `Bool` | Structural type fact. |
| `baseTypeNames` | `List[String]` | Full names of direct/transitive base types. |
| `interfaceTypeNames` | `List[String]` | Full names of implemented interfaces. |
| `attributeTypeNames` | `List[String]` | Full names of discovered type-level attributes/markers. |
| `sourcePaths` | `List[String]` | Repository-relative source file paths known for the subject. |
| `sourceDirectoryPrefixes` | `List[String]` | Every ancestor directory of every entry in `sourcePaths`, repository-relative, `/`-separated, without a trailing slash (e.g. `Assets/Game/Client/Presentation/FishingPresenter.cs` contributes `Assets`, `Assets/Game`, `Assets/Game/Client`, and `Assets/Game/Client/Presentation`). Enables exact-membership path-prefix predicates because CEL profile v1 has no list comprehensions and string `contains` does not apply to lists. |

Notes:

- Metadata is split into typed bags because CEL profile v1 has no heterogeneous
  map value type.
- Numeric metadata is **excluded from the first wave**. The existing semantic
  classification canonicalizes numeric metadata into `decimal`, and CEL profile
  v1 offers only signed 64-bit `Int` and IEEE 754 `Float` with no implicit
  widening — no lossless mapping exists, and a lossy `decimal → double`
  conversion could silently change comparison results relative to literal
  `metadata` selectors. Numeric metadata remains matchable through literal
  `metadata` selectors with exact decimal semantics. Adding a numeric bag later
  requires a reviewed spec change that fixes the conversion rules and
  diagnostics for non-representable values.
- Missing metadata keys must be guarded with `containsKey` to avoid
  fail-closed evaluation errors.
- No CLR `Type`, reflection metadata object, Roslyn syntax node, or project
  system object is ever exposed directly.

### Dependency object

`dependency` exists only for contextual target/exclusion predicates. This
member list is exhaustive for the first wave.

| Member | Type | Notes |
|---|---|---|
| `kind` | `String` | Stable edge kind label such as `type_reference`, `method_body_call`, or another Core-owned dependency classification. |
| `viaMethodBody` | `Bool` | `true` when the edge originates from method-body evidence rather than signature/reference metadata alone. |
| `sourceMemberName` | `String` | Declaring source member name when known, else empty string. |
| `targetMemberName` | `String` | Referenced target member name when known, else empty string. |

The dependency object is intentionally narrow. It carries edge facts, not
execution capability. Additional edge facts stay out of the model until a
reviewed spec change adds them explicitly.

______________________________________________________________________

## Evaluation semantics

### Compile-time

Policy loading compiles every `when` field up front.

Compilation failures include:

- unknown variables or members;
- unknown functions;
- unsupported operators or deferred CEL features;
- invalid map/list/member access;
- non-boolean result type;
- type mismatches;
- any attempt to use facts outside the fixed schema.

Any compile-time failure blocks policy loading for both strict and audit
contracts.

### Evaluation-time

At validation time:

- `true` means the candidate matches;
- `false` means the candidate does not match;
- evaluation failure means the policy is invalid for that run.

Evaluation failures are never downgraded into "no match" and never ignored.
They remain configuration/evaluation errors even when the owning contract is an
audit contract.

### Missing values

CEL profile v1 has no `null`. Missing map keys or out-of-range list access are
evaluation failures. Authors must guard such access explicitly:

```yaml
when: >
  subject.metadataText.containsKey("domain")
  && subject.metadataText["domain"] == "Sales"
```

This blueprint intentionally prefers explicit guards over implicit null-ish
coalescing.

______________________________________________________________________

## Reporting expectations

Future implementation work must preserve the owning node's provenance and make
the predicate itself explainable.

Required reporting properties:

- exact YAML path of the `when` field;
- root vs fragment provenance identical to other composed policy nodes;
- selector/contract identity;
- expression source text;
- bound context kind (`subject`, `source`, `target`, `dependency`);
- result status: `true`, `false`, or `evaluation_error`;
- structured CEL diagnostics for compile/evaluation failures.

Additional rules:

- baselines do not suppress expression infrastructure errors;
- imported `when` fields keep fragment provenance after composition;
- explain/JSON/coverage/SARIF integrations must attach the predicate to the
  owning selector or contract node rather than treat it as an unrelated string.

______________________________________________________________________

## Compatibility and rollout

- The root policy version remains `1`.
- Expression support is additive through optional `when` fields.
- Literal-only policies behave exactly as they do today.
- Until #163 lands, live runtime/schema acceptance of `when` must remain
  fail-closed.

This keeps the design documented without creating a half-shipped feature.

______________________________________________________________________

## Worked examples

### Modular monolith selector refinement

```yaml
layers:
  sales_domain:
    selector:
      role: DomainLayer
      metadata:
        domain: Sales
      when: >
        subject.metadataText.containsKey("domain")
        && subject.metadataText["domain"] == "Sales"
        && (
          subject.namespace == "Acme.Sales.Domain"
          || subject.namespace.startsWith("Acme.Sales.Domain.")
        )
```

Intent:

- keep the literal role/metadata boundary explicit;
- use `when` only for the extra namespace refinement.

### Contextual modular-monolith dependency boundary

```yaml
contracts:
  strict_context_dependencies:
    - id: domain-cross-context
      name: domain-cross-context
      source:
        role: DomainLayer
        when: >
          source.metadataText.containsKey("domain")
      forbidden:
        - role: DomainLayer
          when: >
            source.metadataText.containsKey("domain")
            && target.metadataText.containsKey("domain")
            && target.metadataText["domain"] != source.metadataText["domain"]
      reason: Domain types must not depend directly on another bounded context.
```

Intent:

- compare source and target context explicitly;
- remain within CEL profile v1 (no regex, no ternary, no custom helpers).

### Unity/client selector refinement

```yaml
layers:
  unity_presenters:
    selector:
      role: PresentationLayer
      when: >
        "UnityEngine.MonoBehaviour" in subject.baseTypeNames
        && "Assets/Game/Client" in subject.sourceDirectoryPrefixes
```

Intent:

- combine semantic role discovery with Unity-specific structural facts;
- keep the dependency on source-file facts explicit and bounded;
- use `sourceDirectoryPrefixes` for the directory constraint, because `in` over
  `sourcePaths` is exact element membership — a directory string is never equal
  to a full file path, so `"Assets/Game/Client/" in subject.sourcePaths` would
  always be `false` for real files.

______________________________________________________________________

## Negative examples

### Unsupported regex/function usage

```yaml
when: subject.namespace.matches("^Acme\\.Sales\\.")
```

Why it is invalid:

- `matches` and regex are outside CEL profile v1 and must fail policy loading.

### Stale unguarded metadata access

```yaml
when: subject.metadataText["domain"] == "Sales"
```

Why it is dangerous:

- absent `domain` triggers a fail-closed evaluation error, not a silent false.

### Broad policy weakening

```yaml
when: true
```

Why it is a bad policy:

- it adds no narrowing and invites cargo-cult expression usage;
- authoring guidance and AI review should treat it as a smell even though it is
  syntactically valid CEL.

### Policy-weakening exclusion

```yaml
exclude:
  - role: DomainLayer
    when: true
```

Why it is dangerous:

- it turns an exclusion selector into a blanket escape hatch;
- the syntax is valid, but reviews and examples should present it as a rejected
  authoring pattern.

______________________________________________________________________

## References

- CEL engine/profile design: [cel-engine-architecture.md](cel-engine-architecture.md)
- Parent implementation story: [#322](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/322)
- CEL profile/API boundary: [#324](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/324)
- Policy model design: [#162](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/162)
- Core implementation target: [#163](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/163)
