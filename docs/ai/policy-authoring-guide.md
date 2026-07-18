# Policy Authoring Guide

This guide describes how AI agents should author ArchLinterNet policies safely.

## Decompose Large Policies By Concern

ArchLinterNet executes one selected root policy. For a large policy, prefer
focused imported fragments organized by architecture concern or bounded
context:

```yaml
# architecture/arch.yml — recommended root convention, not a required name
version: 1
name: Product Architecture
imports:
  - policy/shared/layers.arch.yml
  - policy/bounded-contexts/sales.arch.yml
layers: {}
analysis: {}
contracts: {}
```

The selected path makes this file the root. Import reachability makes the other
files fragments; filenames never assign runtime roles. Read
[Policy imports](../policy-format/imports.md) before changing a composed policy.

Agent rules:

- Inspect the selected root and its import graph before editing. Do not inspect
  a fragment as though it were an independently executable policy.
- Edit the smallest fragment that owns the concern. A Sales rule belongs in a
  Sales fragment; do not touch Inventory or shared-layer fragments merely to
  reduce file count or make formatting uniform.
- Add or reorder a root import only when the owning fragment is new or composed
  order must intentionally change. Avoid broad unrelated root rewrites.
- Keep small shared settings inline when that is clearer. Split by concern or
  bounded context, not arbitrary line count.
- Treat layer/package/external-dependency/condition-set keys as global across
  the graph. A duplicate is a conflict, not an override.
- Keep contract IDs unique across the whole graph within each contract family
  and strict/audit mode. Fragment boundaries do not create ID namespaces.
- Validate fragments with
  `schema/dependencies.arch.fragment.schema.json`, then run strict and relevant
  audit validation through the one selected root.
- Preserve existing list and import order unless the task explicitly changes
  diagnostic or contract evaluation order.

## Start With Layers

Define layers from real namespace prefixes:

```yaml
layers:
  application:
    namespace: MyCompany.Product.Application
  domain:
    namespace: MyCompany.Product.Domain
```

Layer namespaces are prefix matches. `namespace_suffix` is available for
conventions such as `*.Contracts`.

Layer definitions also support a constrained `*` wildcard when it occupies a
whole namespace segment:

```yaml
layers:
  feature_modules:
    namespace: MyCompany.Product.Features.*

  feature_contracts:
    namespace: MyCompany.Product.Features.*
    namespace_suffix: Contracts
```

Use this only when you need one layer to cover repeated sibling namespaces.

Rules:

- `*` matches exactly one namespace segment.
- Descendants under the resolved prefix still match.
- With `namespace_suffix`, the suffix is position-fixed immediately after the
  full resolved namespace pattern.
- `*` must be a full segment. Do not author `Feature*`, `*Feature`, or `F*eature`.
- Do not author `**`, `?`, character classes, or regex.

Examples:

- `MyCompany.Product.Features.*` matches `MyCompany.Product.Features.Audio` and
  `MyCompany.Product.Features.Audio.Player`.
- `namespace: MyCompany.Product.Features.*` with
  `namespace_suffix: Contracts` matches
  `MyCompany.Product.Features.Audio.Contracts` and
  `MyCompany.Product.Features.Audio.Contracts.Dto`.
- That same pattern does not match
  `MyCompany.Product.Features.Audio.Internal.Contracts`.

Prefer narrow layers before broad aggregate layers. If a repository has modules
such as `Sales`, `Billing`, and `Inventory`, model those modules directly before
adding a broad `application` layer that hides cross-module coupling. Use glob
layers as aggregate views, not as a replacement for the concrete layers you need
for specific contracts and diagnostics.

## CEL When Predicates

`layers.<name>.selector.when` and contextual dependency/allow-only
`source`/`forbidden`/`allowed`/`exclude` selectors accept an optional `when`
field carrying a narrow CEL boolean predicate. This is the **only** place a
CEL expression is ever accepted — every other YAML string, including every
other selector field, stays literal.

Prefer a narrow, explainable `when` over one that is broad enough to weaken
the contract:

```yaml
# Good: narrow, explains intent, refines an already-scoped literal match.
layers:
  sales_domain:
    selector:
      role: Domain
      when: subject.metadataText["domain"] == "Sales"

# Bad: trivially true for almost every candidate — defeats the point of a
# selector and will surface as a broad-match signal in coverage/explain
# output rather than silently passing review.
layers:
  everything:
    selector:
      role: Domain
      when: "true"
```

Cross-context comparisons (source vs. target) are the primary reason to reach
for `when` over literal `metadata` matching — for example, forbidding any
cross-domain reference without hand-listing every domain pair:

```yaml
contracts:
  strict_context_dependencies:
    - name: sales-must-not-depend-on-other-domain
      source:
        role: Domain
      forbidden:
        - role: Domain
          when: target.metadataText["domain"] != source.metadataText["domain"]
      reason: Bounded contexts must not depend on each other's domain types.
```

Rules for agents:

- Never author `when` anywhere outside the closed set of locations above —
  policy loading rejects it, but do not rely on that as your only check.
- Guard a map lookup before comparing it (`subject.metadataText.containsKey("domain") && subject.metadataText["domain"] == "Sales"`)
  when the key may legitimately be absent for some classified type. An
  unguarded lookup against a missing key is an **evaluation failure**, not a
  non-match — it fails the run as a policy/configuration error, and a
  baseline does not suppress it.
- `when` is additive to `role`/`metadata`, never a replacement for them —
  keep `role` as the fast, explainable pre-filter and use `when` only for
  the comparison `role`/`metadata` cannot express.
- Numeric metadata is not exposed to `when` (`metadataText`/`metadataBool`
  only) — match numeric metadata with a literal `metadata` constraint
  instead.
- After adding or narrowing a `when`, check the selector isn't reported stale
  in coverage output — a `when` that never evaluates `true` for any
  classified type makes the whole selector stale even though its literal
  `role`/`metadata` still matches real types.

## Choose Strict Or Audit

Use strict rules for current gates. Add an `id` for stable CLI and CI references:

```yaml
contracts:
  strict:
    - id: domain-not-infrastructure
      name: domain-must-not-depend-on-infrastructure
      source: domain
      forbidden: [infrastructure]
      reason: Domain code must remain independent of infrastructure.
```

Use audit rules for migration discovery and future-state boundaries:

```yaml
contracts:
  audit:
    - id: audit-ui-to-domain
      name: audit-ui-bypassing-application
      source: ui
      forbidden: [domain]
      reason: Discover UI code that bypasses application use cases before making this strict.
```

When `id` is omitted it is derived automatically from `name` (lowercased with
hyphens). Explicit `id` values are recommended for stable references in CI and
AI-agent workflows.

Do not put known-failing future-state rules in strict unless the team explicitly
wants a blocking gate.

## Use External Dependencies For Vendor Or Framework Leakage

When the target is not a first-party layer but a vendor/framework surface such
as Unity, EF Core, or a cloud SDK, model it with `external_dependencies` and
`strict_external` / `audit_external` instead of inventing pseudo-layers:

```yaml
external_dependencies:
  unity_runtime:
    namespace_prefixes:
      - UnityEngine
    type_prefixes: []

contracts:
  strict_external:
    - id: core-no-unity
      name: core-must-not-reference-unity
      source: core
      forbidden: [unity_runtime]
      reason: Pure core must not expose Unity runtime types.
```

Use `external: true` on a layer only when you intentionally want layer-style
semantics with missing-type suppression. For new vendor/framework controls,
prefer `external_dependencies`.

External dependency contracts detect forbidden references through type-level
metadata (base types, interfaces, fields, properties, method signatures,
generic arguments) and method-body IL scanning (method calls, constructor
calls, field/property access, type references inside method bodies). They do
not analyze third-party package internals. This is static reference analysis,
not semantic data-flow or runtime validation.

## Use External Allow-Only For A Small, Known Vendor Surface

When a layer should only ever use one or a few approved vendor/framework
dependency groups instead of an ever-growing forbidden list, use
`strict_external_allow_only` / `audit_external_allow_only`:

```yaml
external_dependencies:
  approved_sdk:
    namespace_prefixes:
      - MyApp.Vendor.ApprovedSdk
  legacy_sdk:
    namespace_prefixes:
      - MyApp.Vendor.LegacySdk

contracts:
  strict_external_allow_only:
    - id: adapter-approved-sdk-only
      name: adapter-may-only-reference-approved-sdk
      source: infrastructure_adapter
      allowed: [approved_sdk]
      reason: This adapter may only use the approved SDK, not any other declared vendor dependency group.
```

Only groups declared in `external_dependencies` are ever evaluated — a group not present
in `allowed` is disallowed, but a reference that matches no declared group at all
(including BCL/system references, unless a policy author explicitly declares a matching
group) is never a violation. A misspelled `allowed` entry has no relaxing effect: it can
only make the contract more restrictive than intended, never less. Detection is
type-level only (no method-body IL scanning yet, unlike `strict_external`). See
[External allow-only contracts](../contracts/external-allow-only.md) for full semantics.

## Use Type Placement For Where A Role Lives And How It's Named

For the reviewed vocabulary of roles such as controllers, handlers, domain
events, Unity systems, and their contextual metadata, see the [semantic role
catalog](../policy-format/semantic-role-catalog.md). Treat its reserved
classification examples as design guidance until extraction and selector
evaluation are implemented; keep policy selectors narrow and explicit.

When an architectural role (a controller, a handler, a domain event, a Unity
`MonoBehaviour`) must live in a specific layer/namespace/project/assembly
and/or carry a specific naming convention, use `strict_type_placement` /
`audit_type_placement` instead of trying to express this with dependency or
allow-only contracts:

```yaml
layers:
  api:
    namespace: MyApp.Api

contracts:
  strict_type_placement:
    - id: controllers-in-api
      name: controllers-must-live-in-api-layer
      types_matching:
        name_suffix: Controller
      must_reside_in_layers: [api]
      required_name_suffix: Controller
      reason: Controller types are API boundary types and must be named and placed consistently.
```

`types_matching` selects candidate types using only `name_suffix`,
`name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, and
`has_attribute` — every populated field combines with AND, and there is no
regex or expression-language selector. `must_reside_in_layers`,
`must_reside_in_namespaces`, `must_reside_in_projects`, and
`must_reside_in_assemblies` together form one set of allowed locations (a
match against any one of them satisfies placement). `required_name_suffix`,
`required_name_prefix`, `forbidden_name_suffix`, and `forbidden_name_prefix`
check the type's simple name.

A contract must declare at least one placement or naming expectation —
declaring only `types_matching` with no expectation fails policy loading with
an actionable error, since such a rule could never produce a violation.

`must_reside_in_projects` resolves to assembly-name matching via project
discovery; it is not physical `.csproj`-membership tracking, since there is no
type-to-project mapping in this tool beyond a project's own assembly name. See
[Type placement contracts](../contracts/type-placement.md) for full semantics.

## Use Public API Surface For A Library's Exported Boundary

When a library assembly's exported (`public`/`protected`/`protected internal`)
API should be intentional and reviewed before every release, use
`strict_public_api_surface` / `audit_public_api_surface` instead of trusting
default visibility everywhere:

```yaml
contracts:
  strict_public_api_surface:
    - id: core-public-api
      name: core-public-api-declared
      assemblies: [MyApp.Core]
      declared_api:
        - "class MyApp.Core.Foo"
        - "ctor MyApp.Core.Foo()"
        - "method MyApp.Core.Foo.Bar(System.Int32): System.Void"
      forbid_public_constants_unless_declared: true
      allowed_public_constants: []
      reason: Track Core's exact exported surface before every NuGet release.
```

`assemblies` must be non-empty — a contract with nothing to scan fails policy
loading with an actionable error. `declared_api` entries are normalized
signature strings (`<kind> <FullyQualifiedName>[(<param types>)][: <member type>]`, e.g. `class`/`ctor`/`method`/`property`/`field`/`const`/`event`); CLR
full type names are used throughout (e.g. `System.Int32`, not `int`), and
generic type/method parameters are rendered positionally (`!N`/`!!N`) so
renaming a generic parameter alone never changes a declared signature.
`protected`/`protected internal` members are treated as exported by default,
same as `public`.

`forbid_public_constants_unless_declared` is an independent, stricter check —
an exported `const` field can still be a violation even when its full
signature is already in `declared_api`, unless its fully-qualified member name
is also present in `allowed_public_constants`. This only detects **undeclared**
exported surface; it does not detect removed or changed declared signatures
and is not a substitute for binary/package compatibility validation. See
[Public API surface contracts](../contracts/public-api-surface.md) for full
semantics.

## Use Attribute Usage For Where A Marker Is Allowed To Appear

When a specific attribute type (an ASP.NET routing attribute, a Unity
serialization attribute, a custom marker) should only appear in — or must
never appear in — a specific layer/namespace/project/assembly, use
`strict_attribute_usage` / `audit_attribute_usage`:

```yaml
contracts:
  strict_attribute_usage:
    - id: aspnet-attributes-api-only
      name: aspnet-attributes-must-stay-in-api-layer
      attributes:
        - Microsoft.AspNetCore.Mvc.ApiControllerAttribute
        - Microsoft.AspNetCore.Mvc.RouteAttribute
      allowed_only_in_layers: [api]
      reason: ASP.NET attributes define API boundary concerns.
```

`attributes` matches an attribute type's fully-qualified name exactly;
`attribute_prefixes` matches by prefix. A contract must declare at least one
of these — a contract with neither fails policy loading with an actionable
error. Every declared member (constructor, method, property, field, event) and
the type itself are scanned **regardless of visibility** — unlike public API
surface, a `private` field carrying `[SerializeField]` is still in scope.

`allowed_only_in_layers`, `allowed_only_in_namespaces`,
`allowed_only_in_projects`, and `allowed_only_in_assemblies` together form an
allow-list (a `misplaced` violation if none match). `forbidden_in_layers`,
`forbidden_in_namespaces`, `forbidden_in_projects`, and
`forbidden_in_assemblies` together form a deny-list (a `forbidden` violation if
any match). A contract must declare at least one allow-list or deny-list entry
— declaring only an attribute selector with no location expectation fails
policy loading with an actionable error. If a single matched attribute usage
fails both checks, only one violation is reported, described as `forbidden`.

This is static marker placement validation only — it does **not** validate
attribute constructor arguments/named properties (no authorization/security
correctness checking), and it does **not** detect the absence of a required
marker (e.g. "every controller action must carry `[Authorize]` or
`[AllowAnonymous]`"); required-marker enforcement is deferred to a documented
follow-up. See [Attribute usage contracts](../contracts/attribute-usage.md)
for full semantics.

## Use Project Metadata Contracts For `.csproj` And Friend-Assembly Governance

When architecture policy must govern package-facing `.csproj` metadata,
friend assembly declarations, or production-to-test project references, use
`strict_project_metadata` / `audit_project_metadata`:

```yaml
analysis:
  solution: ArchLinterNet.slnx

contracts:
  strict_project_metadata:
    - id: core-project-governance
      name: core-project-governance
      projects:
        - src/ArchLinterNet.Core/ArchLinterNet.Core.csproj
      required_properties:
        Nullable: enable
        TreatWarningsAsErrors: true
      allowed_friend_assemblies:
        - ArchLinterNet.Core.Tests
      forbidden_project_references:
        - tests/**/*.csproj
      reason: Core must keep package defaults, expose internals only to approved assemblies, and avoid test-project references.
```

`projects` matches discovered `.csproj` paths, so this family requires
`analysis.solution` or `analysis.projects` to run project discovery first.
`required_properties` and `forbidden_properties` compare exact scalar MSBuild
property values, case-insensitively, using project-local values plus the
nearest readable `Directory.Build.props` chain when discovery can resolve it.
`allowed_friend_assemblies` matches exact `InternalsVisibleTo` names from project-file items and source-level assembly attributes.
`forbidden_project_references` uses the same project-path glob semantics as
`analysis.project_include` / `analysis.project_exclude`.

This is static project metadata analysis only — it does not run MSBuild,
evaluate arbitrary targets/import graphs, or replace package validation.
See [Project metadata contracts](../contracts/project-metadata.md).

## Use Inheritance Contracts For Framework Base Type Boundaries

When types in a protected surface must not derive from a framework or boundary
base class (a Unity `MonoBehaviour`, an EF Core `DbContext`, an ASP.NET
controller base), use `strict_inheritance` / `audit_inheritance`:

```yaml
contracts:
  strict_inheritance:
    - id: domain-no-framework-base-types
      name: domain-must-not-inherit-framework-types
      source_layers: [domain]
      forbidden_base_types:
        - UnityEngine.MonoBehaviour
        - Microsoft.EntityFrameworkCore.DbContext
      reason: Domain types must stay framework-independent.
```

`source_layers` names declared layers; `source_namespaces` adds namespace
prefixes — at least one of the two is required. `forbidden_base_types` matches
a base type's fully-qualified name exactly; `forbidden_base_type_prefixes`
matches by prefix (e.g. `UnityEngine.`) — at least one of the two is required.
Missing either selector fails policy loading with an actionable error.

The full base-class chain is walked, so inheriting through an intermediate
class is still a violation. Constructed generic base types match by their
generic type definition's CLR name (arity suffix, e.g. `` App.Repository`1 ``).
Interface implementation is **not** inheritance — use interface implementation
contracts for that. See [Inheritance contracts](../contracts/inheritance.md).

## Use Interface Implementation Contracts For Port Boundaries

When implementations of an interface family must be confined to (or kept out
of) a layer — application ports implemented only by adapters, infrastructure
abstractions never implemented by domain types — use
`strict_interface_implementation` / `audit_interface_implementation`:

```yaml
contracts:
  strict_interface_implementation:
    - id: ports-implemented-only-by-adapters
      name: application-ports-implemented-only-by-adapters
      interface_prefixes: [MyApp.Application.Ports.]
      allowed_only_in_layers: [infrastructure]
      reason: Port implementations belong to the infrastructure boundary.
```

`interfaces` matches an interface's fully-qualified name exactly;
`interface_prefixes` matches by prefix — at least one of the two is required.
The location fields follow the same allow-list/deny-list semantics as
attribute usage (`allowed_only_in_*` → `misplaced`, `forbidden_in_*` →
`forbidden`, at least one required). A type matches through its full interface
set, including interfaces implemented by its base classes; an interface
*extending* a selected interface is never a violation. This is static
metadata validation — it does not resolve runtime dependency-injection
registrations. See
[Interface implementation contracts](../contracts/interface-implementation.md).

## Use Composition Contracts For Composition-Root/Service-Locator Boundaries

When composition-root or service-locator APIs (DI registration, service
resolution, container `Resolve`/`Register`) must be confined to a bootstrap
boundary, use `strict_composition` / `audit_composition`. A server ASP.NET
example:

```yaml
contracts:
  strict_composition:
    - id: service-locator-confined-to-composition-root
      name: service-locator-confined-to-composition-root
      allowed_only_in_layers: [composition]
      forbidden_apis:
        - System.IServiceProvider.GetService
        - Microsoft.Extensions.DependencyInjection.IServiceCollection.
      reason: Service resolution and DI registration must happen only in the composition root.
```

A Unity/VContainer-style bootstrap example, using container-specific member
names instead of BCL/ASP.NET members:

```yaml
contracts:
  strict_composition:
    - id: container-confined-to-bootstrap
      name: container-confined-to-bootstrap
      allowed_only_in_namespaces: [MyGame.Bootstrap]
      forbidden_apis:
        - Resolve
        - Register
      reason: Container resolution/registration must happen only during bootstrap.
```

`forbidden_apis` uses the same call-pattern vocabulary as method-body
contracts (member names, `Type.Member`, fully qualified members, namespace/type
prefixes) — at least one entry is required. `allowed_only_in_layers`/
`allowed_only_in_namespaces`/`allowed_only_in_projects`/
`allowed_only_in_assemblies` together form the composition boundary — at least
one entry across all four is required, since there is no separate
`forbidden_in_*` deny-list (everything outside the allow-list is forbidden by
definition). Every loaded type outside the boundary is scanned
reflection/IL-only for forbidden calls in its methods and constructors; a type
inside the boundary is never scanned. This is static call-site detection —
it does not validate runtime dependency-injection resolution or prove every
service is registered correctly. See
[Composition contracts](../contracts/composition.md).

## Use Transitive Depth For Indirect Coupling

When a dependency should be blocked at any depth (direct or indirect), use
`dependency_depth: transitive`. This follows the type dependency graph via BFS
and reports violations with full path diagnostics:

```yaml
contracts:
  strict:
    - id: cli-not-transitively-testing
      name: cli-must-not-transitively-depend-on-testing
      source: cli
      forbidden: [testing]
      dependency_depth: transitive
      reason: CLI must not have any transitive dependency path into Testing.
```

Transitive mode is more expensive than direct mode. Use it when auditing
indirect coupling across module boundaries. The default is `direct`, which
checks only immediate type references.

## Prefer Allow-Only For Pure Layers

Use `strict_allow_only` for pure layers where every first-party dependency
should be known:

```yaml
contracts:
  strict_allow_only:
    - id: domain-pure
      name: domain-allowed-dependencies
      source: domain
      allowed: []
      reason: Domain must not depend on other first-party layers.
```

Allow-only contracts permit the source layer itself and the listed allowed
layers. `allowed_types` is an exact full type-name exception list, not a glob or
namespace rule.

## Use Ordered Layers Carefully

Layer order contracts list layers from outermost to innermost:

```yaml
contracts:
  strict_layers:
    - id: clean-layering
      name: clean-architecture-layering
      layers:
        - ui
        - infrastructure
        - application
        - domain
      reason: Dependencies must point inward toward domain.
```

Do not mix parent aggregate layers and child layers in one ordered contract
unless each entry maps to a distinct namespace slice. Overlapping layers can make
diagnostics confusing.

## Use Layer Templates For Repeated Shapes

When multiple modules or features share the same internal architecture, use
`strict_layer_templates` instead of duplicating ordered-layer contracts:

```yaml
contracts:
  strict_layer_templates:
    - name: feature-clean-architecture
      containers:
        - MyApp.Features.Fishing
        - MyApp.Features.Inventory
        - MyApp.Features.Map
      layers:
        - name: Presentation
        - name: Application
          optional: true
        - name: Domain
      reason: Every feature follows the same internal dependency direction.
```

Each `containers` entry is a raw namespace prefix — layer names are resolved by
prepending the container. For container `MyApp.Features.Fishing`, the template
above produces layers `[MyApp.Features.Fishing.Presentation, ...]`.

Optional layers (`optional: true`) produce no diagnostic when absent. If present,
they must still obey the dependency direction.

Use `audit_layer_templates` for audit-mode templates. Templates coexist with
direct `strict_layers` / `audit_layers` contracts.

### Semantic coverage diagnostics

When a policy uses semantic classification, add an opt-in `semantic_role`
coverage contract to make role discovery auditable. Keep selector-backed layers
narrow, remove stale selectors, and give every semantic exclusion a concrete
reason. Coverage output distinguishes unclassified facts, uncovered roles, stale
selectors, and classification conflicts from dependency violations, which makes
the JSON/CI artifact suitable for policy review and AI-assisted authoring.

For the complete inspect-classify-validate-review workflow, including bounded
contexts, SharedKernel exceptions, legacy migration, and Unity/client layouts,
see [AI-first semantic-role governance](semantic-role-governance.md). Treat
diagnostic suggestions as review input: they must not automatically broaden a
selector, add a blanket ignore, or claim runtime behavior proof.

### Exhaustive container coverage

When a template declares `exhaustive: true`, the runner verifies that every
immediate child namespace under each container that contains loaded types is
mapped to a declared layer. Any unmapped sibling namespace produces a violation.

This catches new modules added under an existing container root without
corresponding layer declarations — a common governance gap in growing codebases.

```yaml
contracts:
  strict_layer_templates:
    - name: feature-clean-architecture
      containers:
        - MyApp.Features.Fishing
        - MyApp.Features.Inventory
        - MyApp.Features.Map
      layers:
        - name: Presentation
        - name: Application
        - name: Domain
      exhaustive: true
      reason: Every feature must declare all internal layers; new modules must not silently bypass the architecture.
```

When `exhaustive: true`, template layer names must be single namespace segments
(e.g. `Domain`, not `Domain.Models`). The layer name is prepended to the
container to form the full namespace, so dotted names would produce a namespace
deeper than an immediate child and cannot be validated correctly.

Only namespaces that contain at least one loadable type are checked. Empty
child namespaces are silently ignored.

Exhaustive works in both strict and audit modes. Use strict for blocking gates
and audit for discovery. The check only runs on expanded template contracts
(with a `ContainerNamespace`), not on direct layer contracts.

## Model Modules With Independence Or Cycles

Use `strict_independence` when modules must not reference each other at all. Use
`strict_cycles` when cross-references may exist but directed cycles are not
allowed.

```yaml
contracts:
  strict_independence:
    - id: modules-independent
      name: modules-must-be-independent
      layers: [sales, billing, inventory]
      reason: Bounded contexts communicate through explicit public contracts.
```

Use `strict_assembly_independence` when the boundary you need to enforce is a
compiled .NET assembly rather than a namespace/layer — for example, feature
assemblies or plugin packages whose ownership doesn't map cleanly onto
namespace prefixes. Every assembly listed must also appear in
`analysis.target_assemblies`. Detection is direct-reference-only.

```yaml
contracts:
  strict_assembly_independence:
    - id: feature-assemblies-independent
      name: feature-assemblies-must-remain-independent
      assemblies: [MyApp.Features.Billing, MyApp.Features.Shipping]
      reason: Feature assemblies must not directly reference each other.
```

This is a different mechanism from `strict_independence` (namespace-based) and
from Unity `strict_asmdef`/`audit_asmdef` (Unity `.asmdef` manifest checks) —
see [Assembly independence contracts](../contracts/assembly-independence.md)
for the distinction.

Use `strict_assembly_dependency` when the boundary you need is directional and
assembly-scoped — for example, `MyApp.Domain` must never reference
`MyApp.Infrastructure`. Use `strict_assembly_allow_only` when a source assembly
should only reference an explicit allow-list of other declared assemblies —
for example, an application assembly that may depend on abstractions but not
concrete adapters. Both are direct-reference-only, and every assembly name
referenced (`source`, `forbidden`, `allowed`) must appear in
`analysis.target_assemblies`. Both accept an optional `dependency_depth` field
that only supports `direct` (the default) in this release — do not author
`dependency_depth: transitive` for these two families; it fails policy loading
with an actionable error rather than being silently ignored.

```yaml
contracts:
  strict_assembly_dependency:
    - id: domain-no-infrastructure
      name: domain-must-not-reference-infrastructure
      source: MyApp.Domain
      forbidden: [MyApp.Infrastructure]
      reason: Domain must stay free of infrastructure concerns.
  strict_assembly_allow_only:
    - id: application-allowed-refs
      name: application-may-only-reference-abstractions
      source: MyApp.Application
      allowed: [MyApp.Domain, MyApp.Domain.Abstractions]
      reason: Application may depend on abstractions, not concrete adapters.
```

These are different from `strict_assembly_independence` (mutual, not
directional) — see [Assembly dependency contracts](../contracts/assembly-dependency.md)
for the distinction.

Use `strict_acyclic_siblings` when you want to automatically discover sibling
namespaces under one or more ancestor namespaces and ensure they don't form
dependency cycles. This is useful for feature-group architectures where siblings
are added over time without updating policy definitions.

```yaml
contracts:
  strict_acyclic_siblings:
    - id: features-acyclic
      name: feature-siblings-must-be-acyclic
      ancestors:
        - MyApp.Features
        - MyApp.Modules
      reason: New feature siblings should not introduce cycles.
```

## Keep Ignores Narrow

`ignored_violations` is a frozen-debt baseline. Each entry should identify a
specific source type and forbidden reference, with a reason or issue link.

```yaml
ignored_violations:
  - source_type: MyCompany.Product.Application.Legacy.LegacyUseCase
    forbidden_reference: MyCompany.Product.Infrastructure.LegacyGateway
    reason: Existing migration debt tracked in #1234.
```

Avoid broad patterns such as `source_type: "*"` or
`forbidden_reference: "MyCompany.Product.Infrastructure.*"` unless a human has
explicitly accepted the debt baseline.

When `analysis.unmatched_ignored_violations` is enabled (default `error`), the
linter warns about `ignored_violations` entries that match no current violation.
Remove stale entries proactively to keep the baseline trustworthy and avoid CI
failures. Use `warn` during migration cleanup, then switch to `error`.

## Policy Consistency Checks

Separately from scanning code, the linter always runs a policy-consistency
pass over the policy document itself, looking for internal contradictions:
duplicate contract IDs (including those produced by layer-template
expansion), allow-only contracts that conflict with a forbidding contract for
the same layer pair, independence contracts contradicted by an explicit
allowed dependency, protected-surface `allowed_importers` that conflict with
a strict forbidding rule, overlapping internal layer definitions, and
contracts that reference a structurally unreachable layer. `analysis.policy_consistency`
(default `error`) controls whether these findings fail validation (`error`),
are reported without failing (`warn`), or are suppressed entirely (`off`).

## Use Automated Baselines For Existing Codebases

When adding architecture rules to an existing codebase with existing violations,
use the automated baseline generation workflow instead of hand-writing
`ignored_violations` entries:

```bash
arch-linter-net baseline generate \
  --config architecture/dependencies.arch.yml \
  --output baseline.yml \
  --reason "Initial baseline"
```

Then validate with the baseline:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml \
  --baseline baseline.yml --mode strict
```

The baseline file is a separate YAML file that is merged into the policy's
ignores at runtime. This keeps the policy file clean and makes the baseline
lifecycle explicit — entries are added by the generator and removed as
violations are fixed. See [Migration Baselines](../guides/migration-baselines.md)
for the full lifecycle.

## Validate Before PR

Run strict validation for current gates and audit validation for migration
visibility:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --mode strict
arch-linter-net --policy architecture/dependencies.arch.yml --mode audit
```

When authoring with AI, also validate the YAML shape against
`schema/dependencies.arch.schema.json` because the current runtime loader ignores
unsupported fields.
