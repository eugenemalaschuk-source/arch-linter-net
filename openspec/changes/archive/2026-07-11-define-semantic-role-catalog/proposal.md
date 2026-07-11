## Why

The semantic-classification model defines how roles and metadata are represented, but it does not yet provide a reviewed vocabulary for common .NET architecture styles. Downstream extraction, selector, fixture, and policy-authoring work needs a bounded catalog with clear support tiers so teams can adopt shared terminology without turning ArchLinterNet into a prescriptive architecture framework.

## What Changes

- Define a first-wave catalog of stable semantic roles covering layered/clean architecture, DDD, CQRS/Event Sourcing, web/API, desktop/mobile UI, Unity/client, infrastructure, and cross-cutting concerns.
- Define the metadata vocabulary, including key semantics, intended policy use, documentation-only use, and discouraged or deferred keys.
- Classify each role by support tier: canonical vocabulary, optional annotation candidate, examples-only, custom-mapping expected, or deferred.
- Document optional type-level and assembly-level annotation shapes without requiring production projects to reference an ArchLinterNet binary.
- Add YAML-first mapping guidance and policy-authoring examples for a modular monolith, Unity/client code, custom attributes, and assembly metadata.
- Document ambiguity, conflict, precedence, static-analysis-only, and safe AI policy-authoring guidance for the catalog.

## Capabilities

### New Capabilities

- `semantic-role-catalog`: A reviewed standard .NET semantic role catalog, metadata vocabulary, support tiers, annotation guidance, YAML mappings, and examples for future semantic discovery consumers.

### Modified Capabilities

- None. The existing semantic-classification model remains the source of truth for classification facts and YAML shape; this change adds the product vocabulary that consumes that model.

## Impact

- Adds reusable product documentation and a new OpenSpec capability specification.
- Updates documentation navigation and AI/policy-authoring references as needed.
- No C# code, runtime behavior, package dependency, YAML schema, annotation assembly, selector engine, or CEL implementation changes.
