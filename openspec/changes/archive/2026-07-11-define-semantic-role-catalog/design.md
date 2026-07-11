## Context

The existing `semantic-classification-model` specification defines classification facts, source precedence, metadata extraction, selectors, overrides, and exclusions. Issue #172 adds the product-design layer needed by future extraction and policy work: a bounded vocabulary of common .NET roles, metadata keys, support tiers, and examples. The artifact is documentation and specification, not an executable taxonomy.

## Goals / Non-Goals

**Goals:**

- Make the first supported role wave understandable and reusable across layered/clean, DDD, CQRS/Event Sourcing, web/API, UI, Unity/client, infrastructure, and cross-cutting styles.
- Give every role a definition, static detection sources, typical metadata, use cases, and an explicit support tier.
- Define metadata semantics and distinguish policy-relevant keys from documentation-only or deferred keys.
- Preserve custom YAML mappings and user-owned attributes as the primary adoption path.
- Provide safe examples for modular monolith and Unity/client policies, including type and assembly metadata.

**Non-Goals:**

- No extraction engine, selector evaluation, runtime framework inspection, CEL implementation, annotation package, or automatic policy generation.
- No claim that every role is canonical or that any architecture style is mandatory.
- No change to the existing YAML schema or runtime behavior.

## Decisions

### Use a bounded first wave with explicit tiers

The catalog will include stable roles that can be recognized from static facts, but each role will be labeled as canonical vocabulary, optional annotation candidate, examples-only, custom-mapping expected, or deferred. Framework-specific roles remain deferred or examples-only unless their meaning is stable without runtime behavior.

### Keep role names style-neutral and metadata separate

Role names describe a semantic responsibility (`Entity`, `CommandHandler`, `Controller`, `ViewModel`, `ExternalClient`). Context such as `domain`, `boundedContext`, `module`, `feature`, `layer`, `platform`, and `runtime` is represented as metadata, not encoded into role names. This keeps selectors composable and allows one role to occur in multiple bounded contexts.

### Treat annotations as an optional convenience surface

The documentation will show type-level and assembly-level annotation shapes, but all examples will also show full-type-name YAML mappings. No production project is expected to reference a binary ArchLinterNet annotation assembly; a future convenience package remains a separate decision and implementation.

### Prefer deterministic static evidence

Detection guidance will prioritize explicit attributes and assembly metadata, then inheritance/interface facts, namespaces, and paths according to the existing classification model. Runtime DI graphs, framework registration, reflection execution, and semantic data flow are explicitly excluded. Ambiguous roles are marked risky or deferred and must produce reviewable diagnostics when consumed by future engines.

### Make examples downstream-compatible

Examples will use the existing `classification` and `layers.<name>.selector` shape from `semantic-classification-model`, without implying that the current runtime evaluates them. The modular-monolith example will cover Sales, Inventory, and SharedKernel; the Unity example will use namespace conventions and client roles. AI guidance will recommend narrow selectors and explicit mappings rather than broad exclusions or always-true rules.

## Risks / Trade-offs

- [Risk] A broad catalog can be mistaken for a mandatory architecture. → Mitigation: tier every role, state YAML-first customization, and label framework-specific entries as deferred/examples-only.
- [Risk] Similar terms such as `Service`, `Component`, `System`, and `Model` are ambiguous. → Mitigation: exclude vague names from canonical defaults and document safer qualified alternatives.
- [Risk] Annotation examples could imply a runtime dependency. → Mitigation: pair every annotation example with a user-defined full-name YAML mapping and state that annotations are optional.
- [Risk] Documentation can drift from future extraction behavior. → Mitigation: make the catalog the reviewed product artifact and require downstream implementation issues to consume its tiers and examples.

## Migration Plan

No migration or rollback is required. Add the catalog documentation and spec, link it from the policy-format and AI guidance navigation, and leave existing policies and runtime behavior unchanged.

## Open Questions

- Whether a future optional annotation package should be source-only, analyzer-only, or a separate binary package remains owned by issue #108.
- Which examples graduate from examples-only to canonical requires real extraction fixtures and review by the implementation issues.
