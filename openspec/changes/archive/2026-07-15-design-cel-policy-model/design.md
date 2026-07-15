## Context

Issue #324 established the bounded CEL language profile and the public
`ArchLinterNet.CEL` API. Issue #162 needs the product-side contract that sits
above that engine: where YAML can declare predicates, which architecture facts
Core exposes, and how failures behave without weakening existing policies.

Current policy authoring has two relevant shapes:

- selector-backed layers use exact role and metadata matching only;
- contextual contracts use a closed metadata operator vocabulary
  (`exact`/`in`/`any`/`not-equal-to-source`) with no general expression field.

The repository already documents a strong safety boundary:

- expressions must be explicit, never inferred from ordinary strings;
- Core may depend only on the public `ArchLinterNet.CEL` surface;
- policy loading and diagnostics must remain deterministic and explainable;
- the live product must remain fail-closed until #163 ships actual compilation
  and evaluation.

This issue is design-only. It should leave a durable source of truth that #163
can implement without reopening YAML shape or semantics debates.

## Goals / Non-Goals

**Goals:**

- Define the first-wave expression-bearing YAML locations.
- Define the typed evaluation contexts for selector-backed layers and
  contextual contracts.
- Bound the architecture fact catalog to data Core can expose deterministically.
- Define compile-time and evaluation-time failure semantics that never weaken
  a policy.
- Define explainability and reporting expectations for JSON, explain, coverage,
  baselines, and future SARIF.
- Preserve backward compatibility for literal-only policies.

**Non-Goals:**

- Implement parser, binder, type checker, evaluator, or public CEL APIs.
- Expand the CEL language beyond profile v1.
- Introduce implicit parsing of existing scalar YAML fields.
- Add expression support to coverage contracts, imports, analysis settings,
  baselines, or classification extraction mappings.
- Depend on private `ArchLinterNet.CEL` internals, reflection-based host
  adapters, or user-defined functions.

## Decisions

### D1. The product surface uses explicit additive `when` fields only

Expression support is additive and localized. The model uses optional `when`
fields rather than overloading existing literals such as `role`, `metadata`,
`namespace`, `reason`, or `imports`.

Why:

- It keeps literal and expression authoring visually distinct.
- It preserves backward compatibility for existing policies.
- It prevents hidden language parsing in places where users expect plain data.

Alternatives considered:

- Implicitly parse every string-valued selector field as a CEL expression.
  Rejected because it is ambiguous, unsafe, and would make literal policies
  non-obvious.
- Introduce a parallel `expression:` object wrapper. Rejected for the first wave
  because `when` is simpler, reads naturally, and still keeps expressions
  explicit.

### D2. First-wave locations are limited to selector predicates

The first wave allows predicates only on selector-bearing nodes:

- `layers.<name>.selector.when`
- contextual contract `source.when`
- contextual contract target/exclusion selector `when`

No other YAML location gains expression support in this design.

Why:

- These are already the narrowest policy locations that choose candidate types.
- They solve the motivating problem without turning every policy field into a
  mini language.
- They interact naturally with existing role/metadata selectors instead of
  replacing them.

Alternatives considered:

- Add expressions to all contract families immediately. Rejected because it
  explodes context surface area and makes the first Core integration much
  riskier.
- Restrict expressions to contextual contracts only. Rejected because selector
  layers are the main reusable consumption boundary and need the same precision.

### D3. Expressions refine, not replace, literal selectors

Literal selector fields remain the primary narrowing mechanism. `role`,
`metadata`, `namespace`, `namespace_suffix`, and existing contract lists still
apply first. `when` is an additional boolean predicate over the candidate facts
already selected by the surrounding node.

Why:

- It preserves explainability and keeps the first-wave fact catalog small.
- It prevents authors from bypassing explicit structural policy shape.
- It keeps AI-generated policies pointed toward narrow predicates rather than
  broad free-form logic.

Alternatives considered:

- Allow expression-only selectors everywhere. Rejected because it weakens
  predictability and makes coverage/explain output harder to reason about.

### D4. Contexts are fixed, typed, and public-API-compatible

The model uses closed CEL context schemas built only from public
`ArchLinterNet.CEL` types. The root variables are:

- selector layer predicate: `subject`
- contextual source predicate: `source`
- contextual target/exclude predicate: `source`, `target`, `dependency`

The shared subject object is a closed catalog — exactly these members:

- identity: `fullName`, `simpleName`, `namespace`, `assemblyName`,
  `projectName`
- classification: `role`, `metadataText`, `metadataBool`
- type facts: `kind`, `isAbstract`, `isSealed`, `baseTypeNames`,
  `interfaceTypeNames`, `attributeTypeNames`
- path facts: `sourcePaths`, `sourceDirectoryPrefixes`

`sourceDirectoryPrefixes` is the precomputed list of every repository-relative
ancestor directory of every source path (no trailing slash). It exists because
CEL profile v1 `in` over a list is exact element membership and the profile has
no list comprehensions, so directory-prefix predicates need a precomputed
representation rather than substring tricks over `sourcePaths`.

Numeric metadata is excluded from the first wave. The classification pipeline
canonicalizes numbers into `decimal`, and CEL profile v1 has only signed 64-bit
`Int` and IEEE 754 `Float` with no implicit widening — no lossless mapping
exists, and a lossy conversion could change comparison outcomes relative to
literal metadata selectors. Numeric metadata stays matchable through literal
`metadata` selectors; a future reviewed spec change may add a numeric bag
together with explicit conversion rules and non-representability diagnostics.

The `dependency` object is available only where a source-target edge exists and
is likewise a closed catalog: `kind`, `viaMethodBody`, `sourceMemberName`,
`targetMemberName`.

Adding, removing, or retyping any member of either schema requires a reviewed
change to the `cel-policy-model` spec, because the member set defines the
policy attack surface, structural schema identity, compilation cache identity,
and policy expression compatibility.

Why:

- CEL profile v1 does not support open union-typed maps, so metadata must be
  split into typed bags instead of a heterogeneous single map.
- Fixed schemas give compile-time diagnostics for unknown members and avoid
  reflection or dynamic dispatch.

Alternatives considered:

- Expose raw CLR objects to the CEL engine. Rejected by the CEL project
  boundary and safety constraints.
- Flatten every fact into top-level variables. Rejected because it scales badly
  and makes source/target contexts harder to explain.
- Expose numeric metadata as `Map[Float]` or split `Int`/`Float` bags now.
  Rejected for the first wave because either choice forces conversion rules
  that #162 cannot validate against real usage; exclusion is lossless and
  reversible.

### D5. Expression failures are configuration errors, not selector misses

Compilation happens at policy load. Any compile-time error blocks the policy for
both strict and audit contracts. At evaluation time:

- `false` means "does not match";
- `true` means "matches";
- evaluation failure means "policy/configuration error" and fails validation
  regardless of contract strictness.

Why:

- Treating failures as "no match" would silently weaken policies.
- Audit mode is about whether violations block the run, not whether policy
  infrastructure errors are tolerated.

Alternatives considered:

- Fail open on evaluation errors. Rejected as policy weakening.
- Fail closed by treating evaluation errors as `true`. Rejected because it would
  produce confusing over-matching and misleading diagnostics.

### D6. Reporting must preserve provenance and expression visibility

Every expression location keeps its existing node provenance and adds the exact
expression source text to explainable output. Future JSON/explain reporting is
expected to surface:

- policy path and YAML location;
- selector/contract identity;
- expression source text;
- bound context kind (`subject` or `source/target/dependency`);
- result status (`true`, `false`, `evaluation_error`);
- diagnostic payload when compilation or evaluation fails.

Baselines do not suppress compilation/evaluation errors. Coverage and explain
must treat expression-bearing consumers as first-class selector consumers rather
than opaque strings.

Alternatives considered:

- Collapse expression failures into existing generic violation messages.
  Rejected because it hides the source of the policy defect.

### D7. The design is documented internally before runtime adoption

Because the active change will be archived, a durable internal blueprint is
added under `docs/internal/` and linked from the internal README. Public docs
only clarify that expressions are not implemented yet.

Why:

- #163 needs a stable design reference after the change archive is moved.
- Public docs should not imply runtime availability before the implementation
  issue lands.

## Risks / Trade-offs

- [Risk] The fact catalog is too broad for the first Core integration.
  → Mitigation: keep the allowed locations narrow and treat the internal
  blueprint as the source for trimming facts during #163 if any prove too
  expensive or unavailable.

- [Risk] Typed metadata bags add authoring complexity.
  → Mitigation: keep literal `metadata` selectors as the primary authoring
  surface and use `when` only for cases those selectors cannot express.

- [Risk] Public docs could be read as feature availability.
  → Mitigation: update public capability pages only to state that policy
  expressions are not supported yet.

- [Risk] Reporting requirements sprawl into implementation-heavy territory.
  → Mitigation: keep this issue design-only and leave concrete output shapes to
  #163, while still fixing the required semantics and provenance expectations.

## Migration Plan

1. Land the new `cel-policy-model` spec and internal blueprint.
2. Keep runtime behavior unchanged; `when` remains unsupported until #163.
3. Implement #163 against the approved YAML locations, contexts, and
   fail-closed semantics.
4. After #163, update public policy-format docs with implemented examples and
   schema references.

No rollback-specific data migration is needed because this issue introduces only
design and documentation artifacts.

## Resolved Questions

- Numeric metadata mapping: **excluded from the first wave** (see D4). No
  `decimal → Int/Float` conversion is defined here; adding numeric facts later
  requires a reviewed spec change that fixes conversion rules and diagnostics.
- Dependency edge facts: the first-wave `dependency` schema is **closed** to
  `kind`, `viaMethodBody`, `sourceMemberName`, and `targetMemberName` (see D4).
  Further edge facts require a reviewed spec change rather than staying loosely
  reserved.

## Open Questions

- Whether future semantic coverage should report a separate "expression error"
  bucket or reuse existing stale/unknown evidence shapes. (Non-blocking for
  #163: it concerns coverage reporting shape, not policy semantics.)
