# Supported Capabilities and Non-Goals

This page is the public product boundary for what ArchLinterNet can and cannot validate.

Use it before authoring policies, reviewing AI-generated YAML, or linking documentation from NuGet.org.

## Supported today

ArchLinterNet supports static architecture validation through documented YAML policy fields:

- namespace/layer dependency contracts;
- ordered layer contracts;
- allow-only whitelist contracts;
- dependency cycle contracts;
- acyclic sibling namespace contracts;
- independence contracts;
- assembly independence contracts (direct .NET assembly reference detection);
- project metadata governance contracts (exact required/forbidden MSBuild property values, friend-assembly allowlists, forbidden project-reference path matches, driven from discovered `.csproj` metadata);
- protected surface contracts;
- external dependency contracts;
- external allow-only whitelist contracts;
- method-body forbidden API contracts;
- Unity `.asmdef` contracts;
- reusable layer templates;
- type placement and naming governance contracts (name suffix/prefix, namespace, layer, base type, interface, and attribute selectors; layer/namespace/project/assembly residency; required/forbidden naming);
- public API surface governance contracts (per-assembly declared exported public/protected/protected-internal type and member allowlist, undeclared-surface detection, optional forbid-public-constants-unless-declared lever);
- attribute usage governance contracts (declared attribute/marker types restricted to, or forbidden from, declared layers/namespaces/projects/assemblies; exact and prefix attribute-name matching; type and member scanning regardless of visibility);
- inheritance boundary contracts (types in declared source layers/namespaces forbidden from inheriting, directly or transitively, from declared base types; exact and prefix base-type-name matching; generic base types matched by generic type definition);
- interface implementation boundary contracts (implementations of declared interfaces restricted to, or forbidden from, declared layers/namespaces/projects/assemblies; exact and prefix interface-name matching, including interfaces inherited through base classes);
- composition contracts (composition-root/service-locator API calls restricted to a declared allowed layers/namespaces/projects/assemblies boundary; same call-pattern vocabulary as method-body contracts — member names, `Type.Member`, fully qualified members, namespace/type prefixes; static reflection/IL-based call-site detection only, does not validate runtime dependency-injection resolution or prove every service is registered correctly);
- coverage contracts (`scope: namespace`, `scope: rule_input`, `scope: project`, `scope: assembly`);
- contextual dependency contracts (`strict_context_dependencies`/`audit_context_dependencies`: a source `(role, metadata)` selector's type must not reference a target matching a `forbidden` selector, compared directly against discovered role/metadata without an intermediate declared layer; `exact`/`in`/`any`/`not-equal-to-source` metadata operators; `exclude` selector pre-filtering);
- contextual allow-only contracts (`strict_context_allow_only`/`audit_context_allow_only`: a source `(role, metadata)` selector's type may reference only targets matching an `allowed` selector, using the same selector shape and operator vocabulary as contextual dependency contracts);
- strict and audit contract groups;
- constrained namespace glob patterns;
- external dependency groups;
- condition sets for source-level analysis;
- ignored violations and generated baselines;
- human and JSON diagnostics;
- CLI and test-adapter execution.

## Not supported

ArchLinterNet does not currently validate:

- runtime behavior or dynamic dependency injection resolution;
- security policy, authorization, or data access permissions;
- code ownership or review ownership;
- semantic data-flow analysis;
- third-party package internals;
- full MSBuild evaluation, target execution, or arbitrary imported-property semantics;
- unrestricted custom contract families outside the documented YAML schema;
- unrestricted namespace pattern systems;
- arbitrary YAML fields such as `severity`, `from`, `to`, `regex`, `owner`, or custom rule groups unless the schema documents them.

Coverage support currently excludes `scope: dependency_edge`, which remains reserved
and fails validation with an actionable error.

`classification.attributes`/`classification.assembly_attributes` are **implemented**:
type-level and assembly-level attributes mapped by full type name are extracted into
role/metadata facts, cached per validation run in a role index, and `validate` surfaces
resulting role/conflict/evidence-extraction-failure facts as informational output.
Selector-backed layers (`layers.<name>.selector`) consume the role index to resolve types
by exact role/metadata match; those layers produce violations through existing contract
families exactly as namespace-based layers do. Empty non-external selector-only layers are
surfaced as configuration diagnostics. Every other part of `classification` and
`layers.<name>.selector` remains unimplemented — `precedence` beyond
`type_attribute`/`assembly_attribute`, `inheritance`, `namespace`, `path`, `overrides`,
and `exclusions` are schema-accepted but have no effect on validation. See
[Semantic classification](semantic-classification.md).

Assembly independence contracts detect only **direct** assembly references; transitive
reference paths between two listed assemblies are not resolved.

Assembly dependency and assembly allow-only contracts also detect only **direct**
assembly references, and their optional `dependency_depth` field only accepts `direct`
(the default) — declaring `dependency_depth: transitive` fails policy loading with an
actionable error rather than being silently ignored, since transitive assembly-reference-path
resolution is not implemented yet. Ordered assembly/project layer contracts and
assembly/project cycle detection are not yet supported either; these are deferred to a
follow-up contract family.

External allow-only contracts detect forbidden references only through **type-level**
reference metadata (base types, interfaces, fields, properties, method signatures, generic
arguments) — unlike `strict_external`/`audit_external`, they do not yet scan method bodies
via IL. This is a deliberate scope decision, not an oversight, and may be added as a
follow-up.

Type placement contracts' `must_reside_in_projects` resolves a configured project name to
its assembly name via project discovery and checks it identically to
`must_reside_in_assemblies` — there is no true type-to-`.csproj` mapping in this tool, so
"must reside in project X" means "must reside in the assembly project X produces," not
physical file placement within a project. Type placement contracts also do not scan method
bodies; types are matched structurally (name, namespace, base type, interfaces, attributes).

Public API surface contracts only detect **undeclared** exported surface (a new `public`/
`protected`/`protected internal` type or member not present in `declared_api`) — they do not
detect *removed* or *changed* declared signatures and are not a substitute for full .NET
binary/package compatibility validation. They are reflection-based, like protected surface
and type placement contracts, not project-aware Roslyn compilation.

Attribute usage contracts are static marker **placement** validation only, not runtime
authorization/security correctness validation — they cannot evaluate an attribute's
constructor arguments or named properties (e.g. whether `[Authorize(Roles = "Admin")]`
grants the right roles). They also do not detect the *absence* of a required marker (a
"required attribute" rule such as "every controller action must carry `[Authorize]` or
`[AllowAnonymous]`") — required-marker enforcement is deferred to a documented follow-up
and is not implemented by this contract family.

Inheritance contracts only walk the compiled base-class chain; they do not match interface
implementations, do not support required-inheritance rules ("every X must derive from Y"),
and stop the chain walk at the last resolvable base type when an assembly cannot be loaded.
Interface implementation contracts are static metadata checks over each type's implemented
interface set — they do not resolve runtime dependency-injection registrations and do not
support required-implementation rules ("interface X must have an implementation in layer Y").

## Important distinctions

### Static references, not runtime behavior

ArchLinterNet checks static dependency surfaces visible from assemblies, source analysis, and supported project files. It does not prove what happens at runtime through dependency injection, reflection, configuration, or plugin loading.

### Architecture guardrails, not security validation

A policy can prevent a domain layer from referencing an infrastructure SDK. It cannot prove that authorization, data access permissions, or tenant isolation are correct.

### Baselines freeze debt, they do not hide new debt

A baseline should capture known current violations so teams can block new ones. Broad baseline patterns should be treated as temporary migration debt and reviewed carefully.

### AI agents must not invent fields

AI-authored policies must use only fields supported by the YAML schema and documented contract families. Plausible-looking YAML that the runtime loader ignores creates false confidence.

## Where to look next

- [Policy format](index.md)
- [Contract overview](../contracts/index.md)
- [AI capabilities](../ai/capabilities.md)
- [Policy review checklist](../ai/policy-review-checklist.md)
