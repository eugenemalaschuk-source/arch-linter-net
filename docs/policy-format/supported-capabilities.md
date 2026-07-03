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
- protected surface contracts;
- external dependency contracts;
- method-body forbidden API contracts;
- Unity `.asmdef` contracts;
- reusable layer templates;
- coverage contracts (`scope: namespace`, `scope: rule_input`, `scope: project`, `scope: assembly`);
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
- unrestricted custom contract families outside the documented YAML schema;
- unrestricted namespace pattern systems;
- arbitrary YAML fields such as `severity`, `from`, `to`, `regex`, `owner`, or custom rule groups unless the schema documents them.

Coverage support currently excludes `scope: dependency_edge`, which remains reserved
and fails validation with an actionable error.

Assembly independence contracts detect only **direct** assembly references; transitive
reference paths between two listed assemblies are not resolved.

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
